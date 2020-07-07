using System;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Net.Http;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using WorkBridge.Modules.AMS.AMSIntegrationWebAPI.Srv;

//Version 4.0.1

namespace AMSTowingAlertWidget
{
    class TowMonitor
    {

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly TowEventManager towManager = new TowEventManager();  //The class the takes care of set and executing the alerts
        private readonly StandManager standManager = new StandManager();


        private static MessageQueue recvQueue;  // Queue to recieve update notifications on
        private bool startListenLoop = true;    // Flag controlling the execution of the update notificaiton listener
        private System.Timers.Timer resetTimer; // Timer to control the big reset of the cache


        private readonly static Random random = new Random();
        public bool stopProcessing = false;
        private Thread startThread;
        private Thread receiveThread;           // Thread the notification listener runs in 
        private BasicHttpBinding binding;
        private EndpointAddress address;

        public TowMonitor() { }

        public bool Start()
        {
            Logger.Info($"TowNotStarted Alert Service Starting ({Parameters.VERSION})");

            // Set the binding and address for use by the web services client
            binding = new BasicHttpBinding
            {
                MaxReceivedMessageSize = 20000000,
                MaxBufferSize = 20000000,
                MaxBufferPoolSize = 20000000
            };
            address = new EndpointAddress(Parameters.AMS_WEB_SERVICE_URI);



            stopProcessing = false;
            startThread = new Thread(new ThreadStart(StartThread));
            startThread.Start();


            Logger.Info($"TowNotStarted Alert Service Started ({Parameters.VERSION})");

            return true;
        }
        public void StartThread()
        {

            // Clear the tow event cache of any existing events
            towManager.Clear();
            towManager.StandManager = standManager;


            // Initially Clear the Stand status as required
            if (Parameters.ALERT_STAND)
            {
                var tt = Task.Run(() => GetStandsAsync());
                tt.Wait();
            }

            Logger.Info($"-----> Starting Initial Population of TowEvent Cache ({Parameters.VERSION})");
            Task.Run(() => GetCurrentTowingsAsync()).Wait();
            Logger.Info($"<----- Completeted Initial Population of TowEvent Cache ({Parameters.VERSION})");


            //Start Listener for incoming towing notifications
            recvQueue = new MessageQueue(Parameters.RECVQ);
            StartMQListener();
            Logger.Info($"Started Notification Queue Listener on queue: {Parameters.RECVQ}");


            // Optionally process flights 
            // The status may have changed from "Alerted" to "Non Alaerted" while the
            // widget was not running, so get all the flights for the last 24 hours
            // and clear the alert if needed
            // Flights that do require an alerted will have been taken care of during the 
            // retrieval of the tows
            if (Parameters.STARTUP_FLIGHT_PROCESSING)
            {
                Logger.Info(">>>>>>> Starting Existing Flight Processing");
                UpdateFlights();
                Logger.Info("<<<<<<< Finished Existing Flight Processing");
            }


            //// Set the timer to regularly do a complete refresh of the towing cache
            resetTimer = new System.Timers.Timer()
            {
                AutoReset = true,
                Interval = Parameters.REFRESH_INTERVAL
            };
            resetTimer.Elapsed += (source, eventArgs) =>
            {
                resetTimer.Stop();
                Task.Run(() => GetCurrentTowingsAsync()).Wait();
                resetTimer.Start();
            };
            resetTimer.Start();
            Logger.Info($"Cache Reset timer set up for {Parameters.REFRESH_INTERVAL}ms");

        }
        public void Stop()
        {
            Logger.Info("TowNotStarted Alert Service Stopping");
            stopProcessing = true;
            startListenLoop = false;
            Logger.Info("TowNotStarted Alert Service Stopped");
        }
        public async Task GetCurrentTowingsAsync()
        {

            Logger.Trace($"Resetting Tow Cache ({Parameters.VERSION})");

            // Empty the notification queue first, so no old message overwrite the current status
            try
            {
                using (MessageQueue queue = new MessageQueue(Parameters.RECVQ))
                {
                    queue.Purge();
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Fatal Error: MSMQ Notification Queue not accessible or readable");
                Logger.Error(e.Message);
                Logger.Error($"1. Check for existance and permission on the queue {Parameters.RECVQ}\n");
                //Give the operator a chance to read the message and then exit
                Thread.Sleep(5000);
                System.Environment.Exit(1);
            }

            bool bRunningOK = false;
            do
            {
                try
                {

                    using (var client = new HttpClient())
                    {

                        client.DefaultRequestHeaders.Add("Authorization", Parameters.TOKEN);

                        string from = DateTime.UtcNow.AddHours(Parameters.FROM_HOURS).ToString("yyyy-MM-ddTHH:mm:ssZ");
                        string to = DateTime.UtcNow.AddHours(Parameters.TO_HOURS).ToString("yyyy-MM-ddTHH:mm:ssZ");
                        string uri = Parameters.AMS_REST_SERVICE_URI + $"{Parameters.APT_CODE}/Towings/{from}/{to}";

                        Logger.Trace($"URI for TowEvent cache source set to {uri}");

                        var result = await client.GetAsync(uri);
                        XElement xmlRoot = XDocument.Parse(await result.Content.ReadAsStringAsync()).Root;

                        Logger.Trace("Populating TowEvent Cache");
                        foreach (XElement e in from n in xmlRoot.Descendants() where (n.Name == "Towing") select n)
                        {
                            try
                            {
                                TowEntity tow = towManager.SetTowEvent(e);

                                // Update the relevant STAND if required
                                if (Parameters.ALERT_STAND)
                                {
                                    TowEntity towStand = new TowEntity(e);
                                    standManager.UpdateStandAsync(towStand);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Error adding tow event to cache: {ex.Message}");
                                Logger.Error(e.ToString());
                            }
                        }
                        Logger.Trace("Populating TowEvent Cache Complete");
                    }
                    bRunningOK = true;

                }
                catch (Exception e)
                {
                    Logger.Error("Failed to retrieve tow events from the AMS RestAPI Server");
                    Logger.Error(e.Message);
                    Logger.Error(e);
                    Logger.Error($"1. Check AMS RestAPI Server at: {Parameters.AMS_REST_SERVICE_URI} ");
                    Logger.Error($"2. Check AMS Access Token is correct\n");
                    bRunningOK = false;  // Flag to indicate that there was an error
                    Thread.Sleep(Parameters.RESTSERVER_RETRY_INTERVAL);  //Wait a bit  and then try again
                }
            } while (!bRunningOK);  // Continue the loop until the server can be contacted
        }
        public async Task GetStandsAsync()
        {

            Logger.Trace($"Getting the set of stands ({Parameters.VERSION})");


            // Clear the tow event cache of any existing events
            this.towManager.Clear();

            bool bRunningOK = false;
            do
            {
                try
                {

                    using (var client = new HttpClient())
                    {

                        client.DefaultRequestHeaders.Add("Authorization", Parameters.TOKEN);
                        string uri = Parameters.AMS_REST_SERVICE_URI + $"{Parameters.APT_CODE}/Stands";
                        Logger.Trace($"URI for Stands source set to {uri}");

                        var result = await client.GetAsync(uri);
                        XElement xmlRoot = XDocument.Parse(await result.Content.ReadAsStringAsync()).Root;

                        Logger.Trace("===> Start of clearing stands <=== \n ");
                        foreach (XElement e in from n in xmlRoot.Descendants() where (n.Name == "FixedResource") select n)
                        {
                            var t = Task.Run(() => standManager.ClearStandAsync(new StandEntity(e)));
                            t.Wait();
                        }
                        Logger.Trace("===> End of clearing stands <=== \n ");
                    }
                    bRunningOK = true;

                }
                catch (Exception e)
                {
                    Logger.Error("Failed to retrieve stands from the AMS RestAPI Server");
                    Logger.Error(e.Message);
                    Logger.Error(e);
                    Logger.Error($"1. Check AMS RestAPI Server at: {Parameters.AMS_REST_SERVICE_URI} ");
                    Logger.Error($"2. Check AMS Access Token is correct\n");
                    bRunningOK = false;  // Flag to indicate that there was an error
                    Thread.Sleep(Parameters.RESTSERVER_RETRY_INTERVAL);  //Wait a bit  and then try again
                }
            } while (!bRunningOK);  // Continue the loop until the server can be contacted
        }

        // Start the thread to listen to incoming update notifications
        public void StartMQListener()
        {
            try
            {
                this.startListenLoop = true;
                receiveThread = new Thread(this.ListenToQueue)
                {
                    IsBackground = false
                };
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                Logger.Error("Error starting notification queue listener");
                Logger.Error(ex.Message);
            }
        }

        //Stop the loop listening to incoming update notifications
        public void StopMQListener()
        {
            try
            {
                this.startListenLoop = false;
            }
            catch (Exception ex)
            {
                Logger.Error("Error stopping notification queue listener");
                Logger.Error(ex.Message);
            }
        }

        // Listen for incoming update notifications
        private void ListenToQueue()
        {


            while (startListenLoop)
            {

                //Put it in a Try/Catch so on bad message or reading problem dont stop the system
                try
                {
                    //                  Logger.Trace("Waiting for notification message");
                    using (Message msg = recvQueue.Receive(new TimeSpan(0, 0, 5)))
                    {
                        string xml;
                        using (StreamReader reader = new StreamReader(msg.BodyStream))
                        {
                            xml = reader.ReadToEnd();
                        }
                        ProcessMessage(xml);
                    }
                }
                catch (MessageQueueException)
                {
                    // Handle no message arriving in the queue.
                    //if (e.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                    //{
                    //    if (Parameters.DEEPTRACE)
                    //    {
                    //        Logger.Trace("DEEP TRACE ===>> No Message Recieved in Rest Serer Notification Queue <<==== DEEP TRACE");

                    //    }
                    //}

                    // Handle other sources of a MessageQueueException.
                }
                catch (Exception e)
                {
                    Logger.Error("Error in Reciveving and Processing Notification Message");
                    Logger.Error(e.Message);
                    Thread.Sleep(Parameters.RESTSERVER_RETRY_INTERVAL);
                }
            }
            Logger.Info("Queue Listener Stopped");
            receiveThread.Abort();
        }
        public void ProcessMessage(string xml)
        {
            //          string id = null;

            try
            {


                if (xml.Contains("TowingUpdatedNotification") || xml.Contains("TowingCreatedNotification"))
                {
                    //                   id = RandomString(10);
                    Logger.Info($"Processing TowUpdate or Create Message");

                    if (Parameters.DEEPTRACE)
                    {
                        Logger.Trace("DEEP TRACE  MESSAGE RECIEVED FROM QUEUE ===>>");
                        Logger.Trace($"\n{xml}");
                        Logger.Trace("\n<< ==== DEEP TRACE  MESSAGE RECIEVED FROM QUEUE");
                    }


                    XElement xmlRoot = XDocument.Parse(xml).Root;
                    XElement towNode = xmlRoot.Element("Notification").Element("Towing");
                    towManager.SetTowEvent(towNode);
                    Logger.Trace($"Update or Created Message Processed");
                    return;
                }

                if (xml.Contains("TowingDeletedNotification"))
                {
                    //                   id = RandomString(10);
                    Logger.Info($"Processing TowDelete Message");

                    if (Parameters.DEEPTRACE)
                    {
                        Logger.Trace("DEEP TRACE  MESSAGE RECIEVED FROM QUEUE ===>>");
                        Logger.Trace($"\n{xml}");
                        Logger.Trace("\n<< ==== DEEP TRACE  MESSAGE RECIEVED FROM QUEUE");
                    }

                    XElement xmlRoot = XDocument.Parse(xml).Root;
                    XElement towNode = xmlRoot.Element("Notification").Element("Towing");
                    TowEntity tow = new TowEntity(towNode);

                    towManager.RemoveTowAndClear(tow);

                    if (Parameters.ALERT_STAND)
                    {
                        try
                        {
                            Task.Run(() => standManager.UpdateStandAsync(tow, true)).Wait();
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"Failed to update stand for {tow}. {e.Message}");
                        }
                    }
                    Logger.Info($"Delete Message Processed");
                    return;
                }

                Logger.Trace($"Notification Message Does Not Contain Towing Update. Ignored Message");

            }
            catch (Exception e)
            {
                Logger.Trace($"Message Processing Error. See Contents Below");
                Logger.Trace(e.Message);

                if (Parameters.DEEPTRACE)
                {
                    Logger.Trace("DEEP TRACE ===>>");
                    Logger.Trace($"\n{xml}");
                    Logger.Trace("<< ==== DEEP TRACE");
                }
            }
        }
        private void UpdateFlights()
        {
            // The status may have changed from "Alerted" to "Non Alaerted" while the
            // widget was not running, so get all the flights for the last 24 hours
            // and clear the alert if needed
            // Flights that do require an alerted will have been taken care of during the 
            // retrieval of the tows

            try
            {
                using (AMSIntegrationServiceClient client = new AMSIntegrationServiceClient(binding, address))
                {

                    try
                    {
                        XmlElement flightsElement = client.GetFlights(Parameters.TOKEN, DateTime.Now.AddDays(-1.0), DateTime.Now, Parameters.APT_CODE, AirportIdentifierType.IATACode);

                        XmlNamespaceManager nsmgr = new XmlNamespaceManager(flightsElement.OwnerDocument.NameTable);
                        nsmgr.AddNamespace("ams", "http://www.sita.aero/ams6-xml-api-datatypes");

                        XmlNodeList fls = flightsElement.SelectNodes("//ams:Flight/ams:FlightId", nsmgr);
                        foreach (XmlNode fl in fls)
                        {
                            if (stopProcessing)
                            {
                                Logger.Trace("Stop requested while still processing flights");
                                break;
                            }
                            FlightNode fn = new FlightNode(fl, nsmgr);
                            if (!towManager.SetForFlight(fn))
                            {
                                Logger.Info($"Clearing for {fn}");
                                try
                                {
                                    towManager.SendAlertStatus(fn, "false");
                                }
                                catch (Exception e)
                                {
                                    Logger.Error($"Error while processing {fn}");
                                    Logger.Error(e.Message);
                                    Logger.Error(e);
                                }
                            }
                            else
                            {
                                Logger.Trace($"Processed flight {fn.ToString()}");
                            }
                        }

                    }
                    catch (Exception e)
                    {
                        Logger.Error(e.Message);
                        Logger.Error(e);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
                Logger.Error(e);
            }

        }
        //public static string RandomString(int length)
        //{
        //    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        //    return new string(Enumerable.Repeat(chars, length)
        //      .Select(s => s[random.Next(s.Length)]).ToArray());
        //}
    }
}

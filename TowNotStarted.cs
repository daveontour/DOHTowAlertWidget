using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Linq;
using System.Messaging;
using System.Threading;
using System.IO;

//Version RC 2.0

namespace DOH_AMSTowingWidget {
    class TowNotStarted {

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly TowEventManager towManager = new TowEventManager();  //The class the takes care of set and executing the alerts
        private static MessageQueue recvQueue;  // Queue to recieve update notifications on
        private bool startListenLoop = true;    // Flag controlling the execution of the update notificaiton listener
        private System.Timers.Timer resetTimer; // Timer to control the big reset of the cache
        private Thread receiveThread;           // Thread the notification listener runs in 
        private readonly static Random random = new Random();  
        public TowNotStarted() { }

        public void Start() {

            Logger.Info("TowNotStarted Alert Service Started");

            // Initially Populate the Towing Cache at startup 
            var t = Task.Run(() => GetCurrentTowingsAsync());
            t.Wait();

            Logger.Trace("Initial Population of TowEvent Cache Completed");


            //// Set the timer to regularly do a complete refresh of the towing cache
            resetTimer = new System.Timers.Timer() {
                AutoReset = true,
                Interval = Parameters.REFRESH_INTERVAL
            };
            resetTimer.Elapsed += (source, eventArgs) => {
                resetTimer.Stop();
                var r = Task.Run(() => GetCurrentTowingsAsync());
                r.Wait();
                resetTimer.Start();
            };
            resetTimer.Start();

            Logger.Trace($"Cache Reset timer set up for {Parameters.REFRESH_INTERVAL}ms");

            //Start Listener for incoming towing notifications
            recvQueue = new MessageQueue(Parameters.RECVQ);
            StartMQListener();

            Logger.Trace($"Started Notification Queue Listener on queue: {Parameters.RECVQ}");

        }

        public void Stop() {
            Logger.Info("TowNotStarted Alert Service Stopping");
            StopMQListener();
            resetTimer.Stop();
        }

        public async Task GetCurrentTowingsAsync() {

            Logger.Trace("Resetting Tow Cache");

            // Empty the notification queue first, so no old message overwrite the current status
            try {
                using (MessageQueue queue = new MessageQueue(Parameters.RECVQ)) {
                    queue.Purge();
                }
            } catch (Exception e) {
                Logger.Error($"Fatal Error: MQMQ Notification Queue not accessible or readable");
                Logger.Error(e.Message);
                Logger.Error($"1. Check for existance and permission on the queue {Parameters.RECVQ}\n");
                //Give the operator a chance to read the message and then exit
                Thread.Sleep(5000);
                System.Environment.Exit(1);
            }

            // Clear the tow event cache of any existing events
            this.towManager.Clear();

            bool bRunningOK = false;
            do {
                try {

                    using (var client = new HttpClient()) {

                        client.DefaultRequestHeaders.Add("Authorization", Parameters.TOKEN);

                        string from = DateTime.UtcNow.AddHours(Parameters.FROM_HOURS).ToString("yyyy-MM-ddTHH:mm:ssZ");
                        string to = DateTime.UtcNow.AddHours(Parameters.TO_HOURS).ToString("yyyy-MM-ddTHH:mm:ssZ");
                        string uri = Parameters.BASE_URI + $"{Parameters.APT_CODE}/Towings/{from}/{to}";

                        Logger.Trace($"URI for TowEvent cache source set to {uri}");

                        var result = await client.GetAsync(uri);
                        XElement xmlRoot = XDocument.Parse(await result.Content.ReadAsStringAsync()).Root;

                        Logger.Trace("Populating TowEvent Cache");
                        foreach (XElement e in from n in xmlRoot.Descendants() where (n.Name == "Towing") select n) {
                            towManager.SetTowEvent(e);
                        }
                        Logger.Trace("Populating TowEvent Cache Complete");
                    }
                    bRunningOK = true;

                } catch (Exception ) {
                    Logger.Error("Failed to retrieve tow events from the AMS RestAPI Server");
                    Logger.Error($"1. Check AMS RestAPI Server at: {Parameters.BASE_URI} ");
                    Logger.Error($"2. Check AMS Access Token is correct\n");
                    bRunningOK = false;  // Flag to indicate that there was an error
                    Thread.Sleep(Parameters.RESTSERVER_RETRY_INTERVAL);  //Wait a bit  and then try again
                }
            } while (!bRunningOK);  // Continue the loop until the server can be contacted
        }


        // Start the thread to listen to incoming update notifications
        public void StartMQListener() {
            try {
                this.startListenLoop = true;
                receiveThread = new Thread(this.ListenToQueue) {
                    IsBackground = true
                };
                receiveThread.Start();
            } catch (Exception ex) {
                Logger.Error("Error starting notification queue listener");
                Logger.Error(ex.Message);
            }
        }

        //Stop the loop listening to incoming update notifications
        public void StopMQListener() {
            try {
                this.startListenLoop = false;
                receiveThread.Abort();
            } catch (Exception ex) {
                Logger.Error("Error stopping notification queue listener");
                Logger.Error(ex.Message);
            }
        }


        // Listen for incoming update notifications
        private void ListenToQueue() {


            while (startListenLoop) {

                //Put it in a Try/Catch so on bad message or reading problem dont stop the system
                try {
                    Logger.Trace("Waiting for notification message");
                    using (Message msg = recvQueue.Receive()) {
                        
                        Logger.Trace("Message Received");
                        string xml;
                        using (StreamReader reader = new StreamReader(msg.BodyStream)) {
                            xml = reader.ReadToEnd();
                        }
                        ProcessMessage(xml, RandomString(10));
                    }
                } catch (Exception e) {
                    Logger.Error("Error in Reciveving and Processing Notification Message");
                    Logger.Error(e.Message);
                    Thread.Sleep(Parameters.RESTSERVER_RETRY_INTERVAL);
                }
            }
        }

        public void ProcessMessage(string xml, string id) {

            Logger.Trace($"Processing Message  {id}");

            try {

                XElement xmlRoot = XDocument.Parse(xml).Root;

                if (xml.Contains("TowingUpdatedNotification") || xml.Contains("TowingCreatedNotification")) {
                    XElement towNode = xmlRoot.Element("Notification").Element("Towing");
                    towManager.SetTowEvent(towNode);
                    Logger.Trace($"Update or Created Message Processed {id}");
                    return;
                }

                if (xml.Contains("TowingDeletedNotification")) {
                    XElement towNode = xmlRoot.Element("Notification").Element("Towing");
                    towManager.RemoveTowAndClear(towNode.Element("TowingId").Value);
                    Logger.Trace($"Delete Message Processed {id}");
                    return;
                }
            } catch (Exception e) {
                Logger.Trace($"Message Processing Error {id}");
                Logger.Trace(e.Message);
            }
        }

        public static string RandomString(int length) {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}

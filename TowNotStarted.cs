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


namespace DOH_AMSTowingWidget {
    class TowNotStarted {

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly TowEventManager towManager = new TowEventManager();
        private static MessageQueue recvQueue;
        private bool startListenLoop = true;
        private System.Timers.Timer resetTimer;
        private Thread receiveThread;
        private static Random random = new Random();
        public TowNotStarted() {

        }

        public void Start() {

            // Initially Populate the Towing Cache at startup 
            var t = Task.Run(() => GetCurrentTowings());
            t.Wait();

            //// Set the timer to regularly do a complete refresh of the towing cache
            resetTimer = new System.Timers.Timer() {
                AutoReset = true,
                Interval = Parameters.REFRESH_INTERVAL
            };
            resetTimer.Elapsed += async (source, eventArgs) =>
            {
                resetTimer.Stop();
                var r = Task.Run(() => GetCurrentTowings());
                r.Wait();
                resetTimer.Start();
            };
            resetTimer.Start();

            //Start Listener for incoming towing notifications
            recvQueue = new MessageQueue(Parameters.RECVQ);
            StartMQListener();

        }

        public void Stop() {
            resetTimer.Stop();
        }

        public async Task GetCurrentTowings() {

            Logger.Trace("Resetting Tow Cache");
            // Empty the notification queue first, so no old message overwrite the current status
            try {
                recvQueue = new MessageQueue(Parameters.RECVQ);
                recvQueue.Purge();
            } catch (Exception e) {
                Logger.Error($"Fatal Error: MQMQ Notification Queue not accessible or readable");
                Logger.Error(e.Message);
                Logger.Error($"1. Check for existance and permission on the queue {Parameters.RECVQ}\n");
                Thread.Sleep(5000);
                System.Environment.Exit(1);
            }
            this.towManager.Clear();

            bool bRunningOK = false;
            do {
                try {

                    using (var client = new HttpClient()) {

                        client.DefaultRequestHeaders.Add("Authorization", Parameters.TOKEN);

                        string from = DateTime.UtcNow.AddHours(Parameters.FROM_HOURS).ToString("yyyy-MM-ddTHH:mm:ssZ");
                        string to = DateTime.UtcNow.AddHours(Parameters.TO_HOURS).ToString("yyyy-MM-ddTHH:mm:ssZ");
                        string uri = Parameters.BASE_URI + $"{Parameters.APT_CODE}/Towings/{from}/{to}";

                        Logger.Trace(uri);

                        var result = await client.GetAsync(uri);
                        XElement xmlRoot = XDocument.Parse(await result.Content.ReadAsStringAsync()).Root;

                        foreach (XElement e in from n in xmlRoot.Descendants() where (n.Name == "Towing") select n) {
                            towManager.SetTowEvent(e);
                        }
                    }

                    Logger.Trace("Tow Cache Reset");
                    bRunningOK = true;

                } catch (Exception e) {
                    Logger.Error("Failed to retrieve tow events from the AMS RestAPI Server");
                    Logger.Error($"1. Check AMS RestAPI Server at: {Parameters.BASE_URI} ");
                    Logger.Error($"2. Check AMS Access Token is correct\n");
                    bRunningOK = false;
                    Thread.Sleep(Parameters.RESTSERVER_RETRY_INTERVAL);
                }
            } while (!bRunningOK);
        }



        public void StartMQListener() {
            try {
                this.startListenLoop = true;
                receiveThread = new Thread(this.ListenToQueue) {
                    IsBackground = true
                };
                receiveThread.Start();
            } catch (Exception ex) {
                Logger.Error(ex.Message);
            }
        }

        public void StopMQListener() {
            try {
                this.startListenLoop = false;
                receiveThread.Abort();
            } catch (Exception ex) {
                Logger.Error(ex.Message);
            }
        }

        private void ListenToQueue() {


            while (startListenLoop) {

                //Put it in a Try/Catch so on bad message doesn't or reading problem stop the system
                try {
                    Logger.Trace("Waiting for notification message");
                    using (Message msg = recvQueue.Receive()) {
                        
                        Logger.Trace("Message Received");
                        StreamReader reader = new StreamReader(msg.BodyStream);
                        string xml = reader.ReadToEnd();               
                        Task.Run(() => ProcessMessage(xml, RandomString(10)));

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
                    Logger.Trace($"Message Processed {id}");
                    return;
                }

                if (xml.Contains("TowingDeletedNotification")) {
                    XElement towNode = xmlRoot.Element("Notification").Element("Towing");
                    towManager.RemoveTow(towNode.Element("TowingId").Value);
                    Logger.Trace($"Message Processed {id}");
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

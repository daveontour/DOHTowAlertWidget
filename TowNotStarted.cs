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
        
        public TowEventManager towManager = new TowEventManager();
        private static MessageQueue recvQueue;
        private bool startListenLoop = true;
        private System.Timers.Timer resetTimer;
        private Thread receiveThread;

        public TowNotStarted() {

        }

        public void Start() {

            // Initiall Populate the Towing Cache at startup
            var t = Task.Run(() => GetCurrentTowings());
            t.Wait();

            //// Set the timer to regularly do a complete refresh of the towing cache
            resetTimer = new System.Timers.Timer() {
                AutoReset = true,
                Interval = 100000
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

            Console.WriteLine("Started");
        }

        public void Stop() {
            resetTimer.Stop();
        }

        public async Task GetCurrentTowings() {

            this.towManager.Clear();

              using (var client = new HttpClient()) {

                //client.BaseAddress = new Uri(Parameters.BASE_URI);
                client.DefaultRequestHeaders.Add("Authorization", "b406564f-44aa-4e51-a80a-aa9ed9a04ec6");

                var result = await client.GetAsync(@"http://localhost:80/api/v1/DOH/Towings/2019-11-05T09:36:54.210Z/2019-11-06T13:36:54.210Z");

                XElement xmlRoot = XDocument.Parse(await result.Content.ReadAsStringAsync()).Root;

                foreach (XElement e in from n in xmlRoot.Descendants() where (n.Name == "Towing") select n) {
                     var te = towManager.SetTowEvent(e);
                    if (te != null) {
                        Console.WriteLine(te.ToString());
                    }
                }
            }
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

            try {
                while (startListenLoop) {
                    using (Message msg = recvQueue.Receive()) {

                        StreamReader reader = new StreamReader(msg.BodyStream);
                        string xml = reader.ReadToEnd();
                        Console.WriteLine(xml);
                        XElement xmlRoot = XDocument.Parse(xml).Root;

                        if (xml.Contains("TowingUpdatedNotification") || xml.Contains("TowingCreatedNotification")) {
                            XElement tow = xmlRoot.Element("Notification").Element("Towing");
                            towManager.SetTowEvent(tow);
                            continue;
                        }

                        if (xml.Contains("TowingDeletedNotification")) {
                            XElement tow = xmlRoot.Element("Notification").Element("Towing");
                            towManager.RemoveTow(tow);
                       //     towManager.UpdateFlightTowUnset(tow);
                            continue;
                        }
                    }
                }
            } catch (Exception ex) {
                Logger.Error(ex.Message);
            }
        }
    }
}

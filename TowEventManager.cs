using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Linq;
using WorkBridge.Modules.AMS.AMSIntegrationAPI.Mod.Intf.DataTypes;

namespace DOH_AMSTowingWidget {

    class TowEntity {
        public string towID;
        public Timer alertTimer;
        public XElement xmlNode;
        public DateTime schedTime;
        public bool isActualSet = false;
        public DateTime actualTime;

        public TowEntity(string key, XElement xmlNode) {
            this.towID = key;
            this.xmlNode = xmlNode;
            this.schedTime = Convert.ToDateTime(xmlNode.Element("ScheduledStart").Value);
            this.isActualSet = xmlNode.Element("ActualStart").Value != "" || xmlNode.Element("ActualEnd").Value != "";

            if (this.isActualSet) {
                this.actualTime = Convert.ToDateTime(xmlNode.Element("ActualStart").Value);
            }
        }

        public TowEntity(XElement xmlNode) : this(xmlNode.Element("TowingId").Value, xmlNode) { }

        public new string ToString() {
            return $"TowID: {towID},  ScheduleTime: {schedTime}, ActualTime: {actualTime}, isActualSet: {isActualSet}";
        }

    }
    class TowEventManager {

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, TowEntity> towMap = new Dictionary<string, TowEntity>();
        private static readonly object padlock = new object();

        public TowEventManager() { }

        public TowEntity SetTowEvent(XElement e) {

            TowEntity tow = new TowEntity(e);

            Console.WriteLine($"Set Tow Event {tow.towID}");
            // The constructor of TowEntity parses out the key data and sets a flag
            // if the ActualTime is set.

            if (tow.isActualSet) {
                // The ActualTime is set, so the the flights should be updated that
                // the tow has started and any timer task should be cancelled and the 
                // TowEntity removed from the map
                UpdateFlightTowStarted(tow);
                RemoveTow(tow.towID);
                return null;
            } else {

                // Remove the current event if it is already there
                 RemoveTow(tow.towID);

                //Create the Timer Task
                double timeToTrigger = (tow.schedTime  - DateTime.Now).TotalMilliseconds;

                //It may have been in the past, so schedule it straight away
                timeToTrigger = Math.Max(timeToTrigger, 1000);
                Timer alertTimer = new Timer() {
                    AutoReset = false,
                    Interval = timeToTrigger
                };

                // The code to execute when the alert time happend
                    alertTimer.Elapsed += async (source, eventArgs) =>  {
                        UpdateFlightTowNotStarted(tow);
                };

                // Initiate the timer
                alertTimer.Start();

                // Add the timer to the TowEntity
                tow.alertTimer = alertTimer;

                // Add the Tow Entity to the towMap
                towMap.Add(tow.towID, tow);

                return tow;
            }
        }

        private void UpdateFlightTowStarted(TowEntity tow) {
            Console.WriteLine($"CLEAR ALERT ---> Towing HAS started {tow.towID}");
            AMSIntegrationServiceClient client = new AMSIntegrationServiceClient();
          
            
        }

        public void UpdateFlightTowUnset(TowEntity tow) {
            Console.WriteLine($"CLEAR ALERT ---> Towing HAS been Unset {tow.towID}");
        }

        private void UpdateFlightTowNotStarted(TowEntity tow) {
            Console.WriteLine($"ALERT --->Towing not started {tow.towID}");
        }

        public void DeleteTowEvent(string towID) {
            lock (padlock) {
                RemoveTow(towID);
            }
        }

        public void Clear() {
            lock (padlock) {

                // Make sure any timers are disabled
                foreach (TowEntity tow in towMap.Values) {
                    try {
                        if (tow.alertTimer != null) {
                            tow.alertTimer.Stop();
                            tow.alertTimer.Dispose();
                        }
                    } catch (Exception e) {
                        Console.WriteLine(e.Message);
                    }
                }
                // Clear the map
                towMap.Clear();
            }
        }

        public void RemoveTow(XElement e) {
            TowEntity tow = new TowEntity(e);
            RemoveTow(tow.towID);
        }
        public void RemoveTow(string key) {
             lock (padlock) {
                try {
                    TowEntity tow = towMap[key];
                    if (tow.alertTimer != null) {
                        tow.alertTimer.Stop();
                        tow.alertTimer.Dispose();
                       
                    }
                  
                    towMap.Remove(key);
                } catch { }
            }
        }

        public TowEntity GetTowEntity(string key) {
            lock (padlock) {
                try {
                    return towMap[key];
                } catch {
                    return null;
                }
            }
        }


        private void SendAlertStatus(TowEntity tow, string alertStatus) {

 
            // The parameter in the Update Flight request is an array of "PropertyValue"s. Which have the AMS external field name 
            // of the field to update and the value itself (booleans are sent as strings with values "true", "false" or "" for unset.

            PropertyValue pv = new PropertyValue();
            pv.propertyNameField = Parameters.ALERT_FIELD;
            pv.valueField = alertStatus;
            PropertyValue[] val = { pv };

            // The web services client which does the work talking to the AMS WebServices EndPoint
            AMSIntegrationServiceClient client = new AMSIntegrationServiceClient();

            // The Arrival flight (if any)
            FlightId flightID = GetFlightID(tow, true);
            if (flightID != null) {
                //The WebServices Call which Sends the Update Request
                client.UpdateFlight(Parameters.TOKEN, flightID, val);
            }

            // The Departure flight (if any)
            flightID = GetFlightID(tow, false);
            if (flightID != null) {
                //The WebServices Call which Sends the Update Request
                client.UpdateFlight(Parameters.TOKEN, flightID, val);
            }

        }

        private FlightId GetFlightID(TowEntity tow, bool arr) {

            // arr = true for the arrival flight, false for the departing flight

            LookupCode apCode = new LookupCode();
            apCode.codeContextField = CodeContext.ICAO;
            apCode.valueField = Parameters.APT_CODE_ICAO;
            LookupCode[] ap = { apCode };

            LookupCode alCode = new LookupCode();
            alCode.codeContextField = CodeContext.IATA;
            alCode.valueField = "QR";
            LookupCode[] al = { alCode };


            FlightId flightID = new FlightId();
            flightID.flightKindField = FlightKind.Arrival;
            flightID.airportCodeField = ap;
            flightID.airlineDesignatorField = al;
            flightID.scheduledDateField = Convert.ToDateTime("2019-11-05");
            flightID.flightNumberField = "744";

            return flightID;

        }
    }
}

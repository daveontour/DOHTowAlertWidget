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


    // Class for holding the flight information that is contained in the Towing message
    class FlightNode {
        public string nature;
        public string airlineCode;
        public string fltNumber;
        public string schedDate;

        public FlightNode(XElement node) {

            this.nature = node.Element("Nature").Value;
            this.airlineCode = node.Element("AirlineCode").Value;
            this.fltNumber = node.Element("FlightNumber").Value;
            this.schedDate = node.Element("ScheduledDate").Value;
        }
    }

    // Hold all the information about each towing in a convenient package
    class TowEntity {
        public string towID;

        // The timer which goes off at the time of the scheduled start
        public Timer alertTimer;
        public XElement xmlNode;
        public DateTime schedTime;
        public bool isActualSet = false;

        // The flights associated with the the tow, might be arrival, departure or both
        public List<FlightNode> flights = new List<FlightNode>();

        public TowEntity(XElement xmlNode) {
            this.towID = xmlNode.Element("TowingId").Value;
            this.xmlNode = xmlNode;
            this.schedTime = Convert.ToDateTime(xmlNode.Element("ScheduledStart").Value);

            // Flag to indicate if an ActualStart or ActualEnd has been entered
            this.isActualSet = xmlNode.Element("ActualStart").Value != "" || xmlNode.Element("ActualEnd").Value != "";

            // Get the flight information for the flights connected to the tow
            IEnumerable<XElement> flightNodes = xmlNode.Element("FlightIdentifiers").Elements("FlightIdentifier");
            foreach (XElement fltNode in flightNodes) {
                flights.Add(new FlightNode(fltNode));
            }
        }

         public new string ToString() {
            return $"TowID: {towID},  ScheduleTime: {schedTime}, isActualSet: {isActualSet}";
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
                SendAlertStatus(tow, "false"); ;
                RemoveTow(tow.towID);
                return null;
            } else {

                // Remove the current event if it is already there
                 RemoveTow(tow.towID);

                //Create the Timer Task
                double timeToTrigger = (tow.schedTime  - DateTime.Now).TotalMilliseconds;

                // Add the Grace time, that is the amount of time *after* the scheduled start that the alert will be raised. 
                timeToTrigger = timeToTrigger + Parameters.GRACE_PERIOD;

                //It may have been in the past, so schedule it straight away
                timeToTrigger = Math.Max(timeToTrigger, 1000);
                Timer alertTimer = new Timer() {
                    AutoReset = false,
                    Interval = timeToTrigger
                };

                // The code to execute when the alert time happend
                    alertTimer.Elapsed += async (source, eventArgs) =>  {
                        // Call the method to set the custom field on AMS
                        SendAlertStatus(tow, "true");
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
                    SendAlertStatus(tow, "false");
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

        // The FlightId is a structure required by WebService to make specify the flight to make the update to
        private FlightId GetFlightID(TowEntity tow, bool arr) {

            // arr = true for the arrival flight, false for the departing flight
            
            FlightNode flt = null;

            foreach (FlightNode fltNode in tow.flights) {
                if (fltNode.nature == "Arrival" && arr) {
                    flt = fltNode;
                }
                if (fltNode.nature == "Departure" && !arr) {
                    flt = fltNode;
                }
            }

            if (flt == null) {
                return null;
            }

            LookupCode apCode = new LookupCode();
            apCode.codeContextField = CodeContext.ICAO;
            apCode.valueField = Parameters.APT_CODE_ICAO;
            LookupCode[] ap = { apCode };

            LookupCode alCode = new LookupCode();
            alCode.codeContextField = CodeContext.IATA;
            alCode.valueField = flt.airlineCode; ;
            LookupCode[] al = { alCode };


            FlightId flightID = new FlightId();
            flightID.flightKindField = arr?FlightKind.Arrival:FlightKind.Departure;
            flightID.airportCodeField = ap;
            flightID.airlineDesignatorField = al;
            flightID.scheduledDateField = Convert.ToDateTime(flt.schedDate);
            flightID.flightNumberField = flt.fltNumber;

            return flightID;

        }
    }
}

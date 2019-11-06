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
    class TowEventManager {

        public static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, TowEntity> towMap = new Dictionary<string, TowEntity>();
        private static readonly object padlock = new object();

        public TowEventManager() { }

        public TowEntity SetTowEvent(XElement e) {

            TowEntity tow = new TowEntity(e);

            Logger.Trace($"Set Tow Event {tow.ToString()}");
            // The constructor of TowEntity parses out the key data and sets a flag
            // if either ActualStart or ActualEnd is set.

            if (tow.isActualSet) {
                // The ActualTime is set, so the the flights should be updated that
                // the tow has started and any timer task should be cancelled and the 
                // TowEntity removed from the map
                SendAlertStatusClear(tow);
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
                        SendAlertStatusSet(tow);
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

        public void Clear() {
            lock (padlock) {

                // Make sure any timers are disabled
                foreach (TowEntity tow in towMap.Values) {
                    tow.StopTimer();
                }
                // Clear the map
                towMap.Clear();
            }
        }

        public void RemoveTow(string key) {
             lock (padlock) {
                try {
                    TowEntity tow = towMap[key];
                    tow.StopTimer();
                    towMap.Remove(key);
                    SendAlertStatusClear(tow);
                } catch { }
            }
        }

        public void SendAlertStatusClear(TowEntity tow) {
            SendAlertStatus(tow, "false");
        }
        public void SendAlertStatusSet(TowEntity tow) {
            SendAlertStatus(tow, "true");
        }
        private void SendAlertStatus(TowEntity tow, string alertStatus) {

            // The parameter in the Update Flight request is an array of "PropertyValue"s. which have the AMS external field name 
            // of the field to update and the value itself (booleans are sent as strings with values "true", "false" or "" for unset.

            PropertyValue pv = new PropertyValue();
            pv.propertyNameField = Parameters.ALERT_FIELD;
            pv.valueField = alertStatus;
            PropertyValue[] val = { pv };

            // The web services client which does the work talking to the AMS WebServices EndPoint
            AMSIntegrationServiceClient client = new AMSIntegrationServiceClient();

            // The Arrival flight (if any)
            FlightId flightID = tow.GetArrivalFlightID();
            if (flightID != null) {
                //The WebServices Call which Sends the Update Request
                client.UpdateFlight(Parameters.TOKEN, flightID, val);
            }

            // The Departure flight (if any)
            flightID = tow.GetDepartureFlightID();
            if (flightID != null) {
                //The WebServices Call which Sends the Update Request
                client.UpdateFlight(Parameters.TOKEN, flightID, val);
            }
        }
    }
}

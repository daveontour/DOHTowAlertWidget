using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;
using System.Xml.Linq;
using WorkBridge.Modules.AMS.AMSIntegrationAPI.Mod.Intf.DataTypes;

//Version RC 2.0

namespace DOH_AMSTowingWidget {
    class TowEventManager {

        public static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, TowEntity> towMap = new Dictionary<string, TowEntity>(); // The cache which holds all the towing event entities
        private static readonly object padlock = new object();

        public TowEventManager() { }

        public TowEntity SetTowEvent(XElement e) {

            TowEntity tow = new TowEntity(e);

            Logger.Info($"Set of Tow Event Resquest, begin processing {tow.ToString()}");

            // The constructor of TowEntity parses out the key data and sets a flag
            // if either ActualStart or ActualEnd is set.
            if (tow.isActualSet) {
                // The ActualTime is set, so the the flights should be updated that
                // the tow has started and any timer task should be cancelled and the 
                // TowEntity removed from the map
                RemoveTowAndClear(tow, false);
                return null;

            } else {

                // Remove the current event if it is already there
                RemoveTow(tow.towID, false);

                //Create the Timer Task
                double timeToTrigger = (tow.schedTime - DateTime.Now).TotalMilliseconds;

                // Add the Grace time, that is the amount of time *after* the scheduled start that the alert will be raised. 
                timeToTrigger += Parameters.GRACE_PERIOD;

                // The tow may have previously been in the past
                // and now is in the future, so clear any existing alerts 
                // if they should be in the future
                if (timeToTrigger > 10000) {
                    SendAlertStatusClear(tow);
                }

                //It may have been in the past, so schedule it straight away
                timeToTrigger = Math.Max(timeToTrigger, 1000);
                Timer alertTimer = new Timer() {
                    AutoReset = false,
                    Interval = timeToTrigger
                };

                // The code to execute when the alert time happend
                alertTimer.Elapsed += (source, eventArgs) =>
                {
                    Logger.Info($"Timer fired: {tow.towID }, Flights: {tow.fltStr}");
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

                Logger.Trace("Clearing Tow Cache");
                // Make sure any timers are disabled
                foreach (TowEntity tow in towMap.Values) {
                    tow.StopTimer();
                }
                // Clear the map
                towMap.Clear();
            }
        }

        public void RemoveTow(string key, bool logError = true) {
            lock (padlock) {
                try {
                    TowEntity tow = towMap[key];
                    Logger.Info($"Removing Tow Event {tow.ToString()}, Flights {tow.fltStr}");
                    tow.StopTimer();
             //       SendAlertStatusClear(tow);
                    towMap.Remove(key);
                } catch (Exception e) {
                    if (logError) {
                        Logger.Error(e.Message);
                    }
                }
            }
        }


        public void RemoveTowAndClear(string key, bool logError = true) {
            lock (padlock) {
                // If there is an exisiting tow event in the cache, delete it.
                try {
                    TowEntity towExisting = towMap[key];
                    Logger.Info($"Removing Tow Event {towExisting.ToString()}, Flights {towExisting.fltStr}");
                    towExisting.StopTimer();
                    SendAlertStatusClear(towExisting);
                    towMap.Remove(key);
                } catch (Exception e) {
                    if (logError) {
                        Logger.Error(e.Message);
                    }
                }
            }
        }
        public void RemoveTowAndClear(TowEntity tow, bool logError = true) {
            lock (padlock) {
                // If there is an exisiting tow event in the cache, delete it.
                try {
                    TowEntity towExisting = towMap[tow.towID];
                    Logger.Trace($"Removing Tow Event {towExisting.ToString()}");
                    towExisting.StopTimer();
                    towMap.Remove(tow.towID);
                } catch (Exception e) {
                    if (logError) {
                        Logger.Error(e.Message);
                    }
                }
            }
            SendAlertStatusClear(tow);
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

            PropertyValue pv = new PropertyValue {
                propertyNameField = Parameters.ALERT_FIELD,
                valueField = alertStatus
            };
            PropertyValue[] val = { pv };

            // The web services client which does the work talking to the AMS WebServices EndPoint
            using (AMSIntegrationServiceClient client = new AMSIntegrationServiceClient()) {

                // The Arrival flight (if any)
                FlightId flightID = tow.GetArrivalFlightID();

                bool callOK = true;
                do {
                    try {
                        if (flightID != null) {
                                client.UpdateFlight(Parameters.TOKEN, flightID, val);
                                Logger.Trace($"Update Written to AMS (Arrival Flight)  {tow.towID}");
                        } else {
                            Logger.Trace($"No Arrival Flight for:  {tow.towID}");
                        }
                    } catch (Exception e) {
                        Logger.Error("Failed to update the custom field");
                        Logger.Error(e.Message);
                        Logger.Error($"1. Check AMS Web API Server is running ");
                        Logger.Error($"2. Check AMS Access Token is correct\n");
                        System.Threading.Thread.Sleep(Parameters.RESTSERVER_RETRY_INTERVAL);
                        callOK = false;
                    }
                } while (!callOK);

                callOK = true;

                // The Departure flight (if any)
                flightID = tow.GetDepartureFlightID();

                do {
                    try {
                        if (flightID != null) {
                                client.UpdateFlight(Parameters.TOKEN, flightID, val);
                                Logger.Trace($"Update Written to AMS (Departure Flight)  {tow.towID}");
                        } else {
                            Logger.Trace($"No Departure Flight for:  {tow.towID}");
                        }
                    } catch (Exception e) {
                        Logger.Error("Failed to update the custom field");
                        Logger.Error(e.Message);
                        Logger.Error($"1. Check AMS Web API Server is running ");
                        Logger.Error($"2. Check AMS Access Token is correct\n");

                        System.Threading.Thread.Sleep(Parameters.RESTSERVER_RETRY_INTERVAL);
                        callOK = false;
                    }
                } while (!callOK);
            }
        }
     }
}

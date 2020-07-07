using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Linq;
using WorkBridge.Modules.AMS.AMSIntegrationAPI.Mod.Intf.DataTypes;

//Version 4.0

namespace AMSTowingAlertWidget
{
    class TowEventManager
    {

        public static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, TowEntity> towMap = new Dictionary<string, TowEntity>(); // The cache which holds all the towing event entities
        private static readonly object padlock = new object();
        BasicHttpBinding binding;
        EndpointAddress address;

        public StandManager StandManager { get; set; }

        public TowEventManager()
        {

            binding = new BasicHttpBinding
            {
                MaxReceivedMessageSize = 20000000,
                MaxBufferSize = 20000000,
                MaxBufferPoolSize = 20000000
            };

            address = new EndpointAddress(Parameters.AMS_WEB_SERVICE_URI);
        }

        public TowEntity SetTowEvent(XElement e)
        {
            TowEntity tow;
            try
            {
                tow = new TowEntity(e);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error Creating Tow Entity:  {ex.Message}");
                Logger.Error(e.ToString());
                throw new Exception();
            }
            Logger.Info($"Set of Tow Event Resquest, begin processing {tow.ToString()}");

            // The constructor of TowEntity parses out the key data and sets a flag
            // if either ActualStart or ActualEnd is set.
            if (tow.isAllActualSet)
            {
                // The ActualTimes are set, so the the flights should be updated that
                // the tow has started and any timer task should be cancelled and the 
                // TowEntity removed from the map

                if (Parameters.ALERT_FLIGHT)
                {
                    RemoveTowAndClear(tow, false);
                }

                if (Parameters.ALERT_STAND)
                {
                    Task.Run(() => StandManager.UpdateStandAsync(tow)).Wait();
                }

                Logger.Trace($"No need to set Tow Event Timer - All tow events completed for {tow.towID}");
                return null;
            }
            else
            {
                // Remove the current event if it is already there
                RemoveTow(tow.towID, false);

                // Add the Tow Entity to the towMap (needs to be in the map here so it is checks itself when testing if it is set.
                towMap.Add(tow.towID, tow);


                double timeToStartTrigger = (tow.schedStartTime - DateTime.Now).TotalMilliseconds;
                timeToStartTrigger += Parameters.GRACE_PERIOD;

                double timeToEndTrigger = (tow.schedEndTime - DateTime.Now).TotalMilliseconds;
                timeToEndTrigger += Parameters.GRACE_PERIOD;

                bool setStartTimer = !tow.isActualStartSet;
                bool setEndTimer = !tow.isActualEndSet;


                // Start Timer, but only if the end time is not going to be set.
                if (setStartTimer && !(setEndTimer && timeToEndTrigger < 0))
                {
                    // The tow may have previously been in the past
                    // and now is in the future, so clear any existing alerts 
                    // if they should be in the future
                    if (timeToStartTrigger > 10000)
                    {
                        if (Parameters.ALERT_FLIGHT)
                        {
                            SendAlertStatus_Conditionlly_Clear(tow);
                        }

                        if (Parameters.ALERT_STAND)
                        {
                            Task.Run(() => StandManager.UpdateStandAsync(tow)).Wait();
                        }

                    }

                    //It may have been in the past, so schedule it straight away
                    timeToStartTrigger = Math.Max(timeToStartTrigger, 1000);
                    Timer alertStartTimer = new Timer()
                    {
                        AutoReset = false,
                        Interval = timeToStartTrigger
                    };

                    // The code to execute when the alert time happend
                    alertStartTimer.Elapsed += (source, eventArgs) =>
                    {
                        if (Parameters.ALERT_FLIGHT)
                        {
                            Logger.Info($"Timer fired (Actual Start): {tow.towID }, Flights: {tow.fltStr}");
                            // Call the method to set the custom field on AMS
                            SendAlertStatusSet(tow);
                        }

                        if (Parameters.ALERT_STAND)
                        {
                            Logger.Info($"Timer fired (Actual Start): {tow.towID },  Updating Stand: {tow.fromStand}");
                            Task.Run(() => StandManager.UpdateStandAsync(tow)).Wait();
                        }

                    };

                    // Initiate the timer
                    alertStartTimer.Start();
                    // Add the timer to the TowEntity
                    tow.alertStartTimer = alertStartTimer;

                }

                // End Timer
                if (!tow.isActualEndSet)
                {

                    // The tow may have previously been in the past
                    // and now is in the future, so clear any existing alerts 
                    // if they should be in the future
                    if (timeToEndTrigger > 10000)
                    {
                        if (Parameters.ALERT_FLIGHT)
                        {
                            SendAlertStatus_Conditionlly_Clear(tow);
                        }

                        if (Parameters.ALERT_STAND)
                        {
                            Task.Run(() => StandManager.UpdateStandAsync(tow)).Wait();
                        }
                    }

                    //It may have been in the past, so schedule it straight away
                    timeToEndTrigger = Math.Max(timeToEndTrigger, 1000);
                    Timer alertEndTimer = new Timer()
                    {
                        AutoReset = false,
                        Interval = timeToEndTrigger
                    };

                    // The code to execute when the alert time happend
                    alertEndTimer.Elapsed += (source, eventArgs) =>
                    {
                        //                       Logger.Info($"Timer fired  (Actual End): {tow.towID }, Flights: {tow.fltStr}");
                        // Call the method to set the custom field on AMS
                        //                       SendAlertStatusSet(tow);

                        if (Parameters.ALERT_FLIGHT)
                        {
                            Logger.Info($"Timer fired (Actual End): {tow.towID }, Flights: {tow.fltStr}");
                            // Call the method to set the custom field on AMS
                            SendAlertStatusSet(tow);
                        }

                        if (Parameters.ALERT_STAND)
                        {
                            Logger.Info($"Timer fired (Actual End): {tow.towID }, Updating Stand: {tow.fromStand}");
                            Task.Run(() => StandManager.UpdateStandAsync(tow)).Wait();
                        }
                    };

                    // Initiate the timer
                    alertEndTimer.Start();
                    // Add the timer to the TowEntity
                    tow.alertEndTimer = alertEndTimer;

                }

                return tow;
            }
        }

        public void Clear()
        {
            lock (padlock)
            {

                Logger.Trace("Clearing Tow Cache");
                // Make sure any timers are disabled
                foreach (TowEntity tow in towMap.Values)
                {
                    tow.StopTimer();
                }
                // Clear the map
                towMap.Clear();
            }
        }


        public void RemoveTow(string key, bool logError = true)
        {
            lock (padlock)
            {
                try
                {
                    TowEntity tow = towMap[key];
                    Logger.Info($"Removing Tow Event {tow.ToString()}, Flights {tow.fltStr}");
                    tow.StopTimer();
                    towMap.Remove(key);
                }
                catch (Exception e)
                {
                    if (logError)
                    {
                        Logger.Error(e.Message);
                    }
                }
            }
        }
        public void RemoveTowAndClear(TowEntity tow, bool logError = true)
        {
            lock (padlock)
            {
                // If there is an exisiting tow event in the cache, delete it.
                try
                {
                    TowEntity towExisting = towMap[tow.towID];
                    Logger.Trace($"Removing Tow Event {towExisting}");
                    SendAlertStatus_Conditionlly_Clear(towExisting);
                    towExisting.StopTimer();
                    towMap.Remove(tow.towID);
                    return;
                }
                catch (Exception e)
                {
                    SendAlertStatus_Conditionlly_Clear(tow);
                    if (logError)
                    {
                        Logger.Error(e.Message);
                    }
                }
            }
            SendAlertStatus_Conditionlly_Clear(tow);
        }

        public void SendAlertStatus_Conditionlly_Clear(TowEntity tow)
        {
            // The particular tow event is potentially clear, but there may be other
            // tow events associated with this flight which are not clear.
            // so we need to check the other tow events to see if they are applicable.

            //First check if there are any other tow events for the flights which would require the status to still be set

            if (SetForOtherTows(tow))
            {
                // Other tow events were found for this flight, so make sure the flag is set
                SendAlertStatus(tow, "true");
            }
            else
            {
                // No other tow events were set for the flights in this tow event, so the alert can be cleared. 
                SendAlertStatus(tow, "false");
            }
        }
        public void SendAlertStatusSet(TowEntity tow)
        {
            SendAlertStatus(tow, "true");
        }

        public bool SetForOtherTows(TowEntity tow)
        {

            // Go through the other tow events and see if any of them are alerted for the flights in this tow.
            foreach (FlightNode flight in tow.flights)
            {
                foreach (TowEntity t in towMap.Values)
                {
                    if (t.towID == tow.towID)
                    {
                        // Skip the tow we are currently dealing with
                        continue;
                    }
                    if (t.isActiveForFlight(flight))
                    {
                        return true;
                    }
                }
            }

            // None found
            return false;
        }

        // Check alert status for a particular flight
        public bool SetForFlight(FlightNode flt)
        {

            // Go through the other tow events and see if any of them are alerted for the flights in this tow.
            foreach (TowEntity t in towMap.Values)
            {
                if (t.isActiveForFlight(flt))
                {
                    return true;
                }
            }

            // None found
            return false;
        }


        // Set the alert for a individual flight
        public void SendAlertStatus(FlightNode flt, string alertStatus)
        {

            // The parameter in the Update Flight request is an array of "PropertyValue"s. which have the AMS external field name 
            // of the field to update and the value itself (booleans are sent as strings with values "true", "false" or "" for unset.

            PropertyValue pv = new PropertyValue
            {
                propertyNameField = Parameters.FLIGHTALERTFIELD,
                valueField = alertStatus
            };
            PropertyValue[] val = { pv };


            // The web services client which does the work talking to the AMS WebServices EndPoint
            using (AMSIntegrationServiceClient client = new AMSIntegrationServiceClient(binding, address))
            {

                LookupCode apCode = new LookupCode();
                apCode.codeContextField = CodeContext.ICAO;
                apCode.valueField = Parameters.APT_CODE_ICAO;
                LookupCode[] ap = { apCode };

                LookupCode alCode = new LookupCode();
                alCode.codeContextField = CodeContext.IATA;
                alCode.valueField = flt.airlineCode; ;
                LookupCode[] al = { alCode };

                FlightId flightID = new FlightId();
                flightID.flightKindField = flt.nature == "Arrival" ? FlightKind.Arrival : FlightKind.Departure;
                flightID.airportCodeField = ap;
                flightID.airlineDesignatorField = al;
                flightID.scheduledDateField = Convert.ToDateTime(flt.schedDate);
                flightID.flightNumberField = flt.fltNumber;

                bool callOK = true;
                do
                {
                    try
                    {
                        if (flightID != null)
                        {
                            System.Xml.XmlElement res = client.UpdateFlight(Parameters.TOKEN, flightID, val);
                            if (Parameters.DEEPTRACE)
                            {
                                Logger.Trace($"DEEP TRACE - AMS Update Response =====>>\n{res.OuterXml}\n <<==== DEEP TRACE");
                            }
                        }
                    }
                    catch (Exception e)
                    {
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


        private void SendAlertStatus(TowEntity tow, string alertStatus)
        {

            // The parameter in the Update Flight request is an array of "PropertyValue"s. which have the AMS external field name 
            // of the field to update and the value itself (booleans are sent as strings with values "true", "false" or "" for unset.

            PropertyValue pv = new PropertyValue
            {
                propertyNameField = Parameters.FLIGHTALERTFIELD,
                valueField = alertStatus
            };
            PropertyValue[] val = { pv };

            //Update the stand
            //if (Parameters.ALERT_STAND)
            //{
            //    try
            //    {
            //        var t = Task.Run(() => StandManager.UpdateStandAsync(tow));
            //        t.Wait();
            //    }
            //    catch (Exception)
            //    {
            //        Logger.Error($"Failed to update stand for {tow}");
            //    }
            //}

            if (Parameters.ALERT_FLIGHT)
            {

                // The web services client which does the work talking to the AMS WebServices EndPoint
                using (AMSIntegrationServiceClient client = new AMSIntegrationServiceClient(binding, address))
                {

                    // The Arrival flight (if any)
                    FlightId flightID = tow.GetArrivalFlightID();

                    bool callOK = true;
                    do
                    {
                        try
                        {
                            if (flightID != null)
                            {
                                System.Xml.XmlElement res = client.UpdateFlight(Parameters.TOKEN, flightID, val);
                                if (Parameters.DEEPTRACE)
                                {
                                    Logger.Trace($"DEEP TRACE - AMS Update Response =====>>\n{res.OuterXml}\n <<==== DEEP TRACE");
                                }
                                Logger.Trace($"Update Written to AMS (Arrival Flight)  {tow.towID}");
                            }
                            else
                            {
                                Logger.Trace($"No Arrival Flight for:  {tow.towID}");
                            }
                        }
                        catch (Exception e)
                        {
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

                    do
                    {
                        try
                        {
                            if (flightID != null)
                            {
                                System.Xml.XmlElement res = client.UpdateFlight(Parameters.TOKEN, flightID, val);
                                if (Parameters.DEEPTRACE)
                                {
                                    Logger.Trace($"DEEP TRACE - AMS Update Response =====>>\n{res.OuterXml}\n <<==== DEEP TRACE");
                                }
                                Logger.Trace($"Update Written to AMS (Departure Flight)  {tow.towID}");
                            }
                            else
                            {
                                Logger.Trace($"No Departure Flight for:  {tow.towID}");
                            }
                        }
                        catch (Exception e)
                        {
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
}

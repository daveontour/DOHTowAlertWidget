using System;
using System.Collections.Generic;
using System.Timers;
using System.Xml.Linq;
using WorkBridge.Modules.AMS.AMSIntegrationAPI.Mod.Intf.DataTypes;

//Version RC 3.5

namespace DOH_AMSTowingWidget {
    // Hold all the information about each towing in a convenient package
    class TowEntity {
        public string towID;
        public Timer alertStartTimer;
        public Timer alertEndTimer;
        public DateTime schedStartTime;
        public DateTime schedEndTime;
        public bool isAllActualSet = false;
        public bool isActualStartSet = false;
        public bool isActualEndSet = false;
        public List<FlightNode> flights = new List<FlightNode>(); // The flights associated with the the tow, might be arrival, departure or both
        public string fltStr = "";

        public TowEntity(XElement xmlNode) {
            this.towID = xmlNode.Element("TowingId").Value;
            this.schedStartTime = Convert.ToDateTime(xmlNode.Element("ScheduledStart").Value);
            this.schedEndTime = Convert.ToDateTime(xmlNode.Element("ScheduledEnd").Value);

            // Flag to indicate if an ActualStart or ActualEnd has been entered
            this.isActualStartSet = xmlNode.Element("ActualStart").Value != "";
            this.isActualEndSet =  xmlNode.Element("ActualEnd").Value != "";

            // Flag to indicate whether both the start and end actual times are set
            this.isAllActualSet = this.isActualStartSet && this.isActualEndSet;

            // Get the flight information for the flights connected to the tow
            IEnumerable<XElement> flightNodes = xmlNode.Element("FlightIdentifiers").Elements("FlightIdentifier");
            foreach (XElement fltNode in flightNodes) {
                FlightNode fn = new FlightNode(fltNode);
                flights.Add(fn);
                fltStr += fn.airlineCode+fn.fltNumber + "  ";
            }
        }

        // Returns boolean to indicate whether this tow event should be alerted
        public bool isAlerted() {

            // Now is after Schedule StartTime and no Actual Time Set
            if (DateTime.Compare(DateTime.Now, this.schedStartTime) > 0  && !isActualStartSet) {
                return true;
            }

            // Now is after Schedule EndTime and no Actual Time Set
            if (DateTime.Compare(DateTime.Now, this.schedEndTime) > 0 && (!isActualStartSet || !isActualEndSet)) {
                return true;
            }

            return false;
        }

        // Checks for a particular flight if the event is alerted
        public bool isActiveForFlight(FlightNode flt) {

            foreach (FlightNode f in flights) {
                if (f.Equals(flt) && this.isAlerted()) {
                    return true;
                }
            }

            return false;
        }
        public new string ToString() {
            return $"TowID: {towID},  ScheduleStartTime: {schedStartTime},  isActualStartSet: {isActualStartSet},  ScheduleEndTime: {schedEndTime},  isActualEndSet: {isActualEndSet}";
        }

        public void StopStartTimer() {
            try {
                if (alertStartTimer != null) {
                    alertStartTimer.Stop();
                    alertStartTimer.Dispose();
                    alertStartTimer = null;
                }
            } catch (Exception e) {
                TowEventManager.Logger.Error("Error stopping start alert timer. See next message");
                TowEventManager.Logger.Error(e.Message);
            }
        }
        public void StopEndTimer() {
            try {
                if (alertEndTimer != null) {
                    alertEndTimer.Stop();
                    alertEndTimer.Dispose();
                    alertEndTimer = null;
                }
            } catch (Exception e) {
                TowEventManager.Logger.Error("Error stopping end alert timer. See next message");
                TowEventManager.Logger.Error(e.Message);
            }
        }
        public void StopTimer() {
            try {
                StopStartTimer();
            } catch (Exception e) {
                TowEventManager.Logger.Error("Error stopping alert timers. See next message");
                TowEventManager.Logger.Error(e.Message);
            }
            try {
                StopEndTimer();
            } catch (Exception e) {
                TowEventManager.Logger.Error("Error stopping alert timers. See next message");
                TowEventManager.Logger.Error(e.Message);
            }
        }
        public FlightId GetArrivalFlightID() {
            return GetFlightID(true);
        }
        public FlightId GetDepartureFlightID() {
            return GetFlightID(false);
        }

        // The FlightId is a structure required by WebService to make specify the flight to make the update to
        private FlightId GetFlightID(bool arr) {

            // arr = true for the arrival flight, false for the departing flight

            FlightNode flt = null;

            foreach (FlightNode fltNode in flights) {
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
            flightID.flightKindField = arr ? FlightKind.Arrival : FlightKind.Departure;
            flightID.airportCodeField = ap;
            flightID.airlineDesignatorField = al;
            flightID.scheduledDateField = Convert.ToDateTime(flt.schedDate);
            flightID.flightNumberField = flt.fltNumber;

            return flightID;

        }
    }
}

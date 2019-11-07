using System;
using System.Collections.Generic;
using System.Timers;
using System.Xml.Linq;
using WorkBridge.Modules.AMS.AMSIntegrationAPI.Mod.Intf.DataTypes;

//Version RC 1.0

namespace DOH_AMSTowingWidget {
    // Hold all the information about each towing in a convenient package
    class TowEntity {
        public string towID;
        public Timer alertTimer;
        public DateTime schedTime;
        public bool isActualSet = false;
        public List<FlightNode> flights = new List<FlightNode>(); // The flights associated with the the tow, might be arrival, departure or both
        public string fltStr = "";

        public TowEntity(XElement xmlNode) {
            this.towID = xmlNode.Element("TowingId").Value;
            this.schedTime = Convert.ToDateTime(xmlNode.Element("ScheduledStart").Value);

            // Flag to indicate if an ActualStart or ActualEnd has been entered
            this.isActualSet = xmlNode.Element("ActualStart").Value != "" || xmlNode.Element("ActualEnd").Value != "";

            // Get the flight information for the flights connected to the tow
            IEnumerable<XElement> flightNodes = xmlNode.Element("FlightIdentifiers").Elements("FlightIdentifier");
            foreach (XElement fltNode in flightNodes) {
                FlightNode fn = new FlightNode(fltNode);
                flights.Add(fn);
                fltStr += fn.airlineCode+fn.fltNumber + "  ";
            }
        }

        public new string ToString() {
            return $"TowID: {towID},  ScheduleTime: {schedTime}, isActualSet: {isActualSet}";
        }

        public void StopTimer() {
            try {
                if (alertTimer != null) {
                    alertTimer.Stop();
                    alertTimer.Dispose();
                    alertTimer = null;
                }
            } catch (Exception e) {
                TowEventManager.Logger.Error("Error stopping alert timer. See next message");
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

using System.Xml.Linq;

//Version RC 2.0

namespace DOH_AMSTowingWidget {

    // Class for holding the flight information that is contained in the Towing message
    class FlightNode {
        public string nature;
        public string airlineCode;
        public string fltNumber;
        public string schedDate;

        // The "node" parameter is one the XElement of the "FlightIndentifier" element of the Towing message 
        public FlightNode(XElement node) {

            this.nature = node.Element("Nature").Value;
            this.airlineCode = node.Element("AirlineCode").Value;
            this.fltNumber = node.Element("FlightNumber").Value;
            this.schedDate = node.Element("ScheduledDate").Value;
        }
    }
}

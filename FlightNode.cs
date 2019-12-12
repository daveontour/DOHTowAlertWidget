using System.Xml;
using System.Xml.Linq;

//Version RC 3.5

namespace DOH_AMSTowingWidget {

    // Class for holding the flight information that is contained in the Towing message
    class FlightNode {
        public string nature;
        public string airlineCode;
        public string fltNumber;
        public string schedDate;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();


        // The "node" parameter is one the XElement of the "FlightIndentifier" element of the Towing message 
        public FlightNode(XmlNode node, XmlNamespaceManager nsmgr) {
  
            this.nature = node.SelectSingleNode(".//ams:FlightKind", nsmgr).InnerText;
            this.airlineCode = node.SelectSingleNode(".//ams:AirlineDesignator[@codeContext='IATA']", nsmgr).InnerText;
            this.fltNumber = node.SelectSingleNode(".//ams:FlightNumber", nsmgr).InnerText;
            this.schedDate = node.SelectSingleNode(".//ams:ScheduledDate", nsmgr).InnerText;
        }

        // The "node" parameter is one the XElement of the "FlightIndentifier" element of the Towing message 
        public FlightNode(XElement node) {

            this.nature = node.Element("Nature").Value;
            this.airlineCode = node.Element("AirlineCode").Value;
            this.fltNumber = node.Element("FlightNumber").Value;
            this.schedDate = node.Element("ScheduledDate").Value;
        }

        // Is the supplied node referring to the same flight as this node?
        public bool Equals(FlightNode node) {
            if (node.nature == this.nature 
                && node.airlineCode == this.airlineCode
                && node.fltNumber == this.fltNumber
                && node.schedDate == this.schedDate) {
                return true;
            } else {
                return false;
            }
        }

        public new string ToString() {
            return $"AirlineCode: {airlineCode}, Flight Number: {fltNumber}, Nature: {nature}, Scheuled Date: {schedDate}";

        }
    }
}

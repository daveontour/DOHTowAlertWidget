using System;
using System.Linq;
using System.Xml.Linq;

namespace DOH_AMSTowingWidget
{
    public class StandEntity
    {
        public string Id;
        public string Name;
        public string SortOrder;
        public string Area;
        public bool towAlert;
        public string alertTowId;

        public StandEntity(XElement el)
        {
            this.Id = el.Element("Id")?.Value;
            this.Name = el.Element("Name")?.Value;
            this.SortOrder = el.Element("SortOrder")?.Value;
            this.Area = el.Element("Area")?.Value;

            XElement db = (from n in el.Descendants() where (n.Name == "CustomField" && n.Elements("Name").FirstOrDefault().Value == "B---_TowingNotStarted") select n).FirstOrDefault();
            try
            {
                this.towAlert = bool.Parse(db.Element("Value")?.Value);
            }
            catch (Exception)
            {
                this.towAlert = false;
            }
            db = (from n in el.Descendants() where (n.Name == "CustomField" && n.Elements("Name").FirstOrDefault().Value == "S---_TowId") select n).FirstOrDefault();
            this.alertTowId = db.Element("Value")?.Value;
        }

        public override string ToString()
        {
            return $"Stand  Id: {Id}, Name: {Name}, SortOrder: {SortOrder}, Area: {Area}, TowAlert: {towAlert},TowAlert: {alertTowId}";
        }
    }
}

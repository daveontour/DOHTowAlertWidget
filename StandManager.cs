using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DOH_AMSTowingWidget
{
    public class StandManager
    {
        private string standUpdate = @"<FixedResource>
      <Id>@StandId</Id>
      <ResourceTypeCode>Stand</ResourceTypeCode>
      <Name>@StandName</Name>
      <SortOrder>@StandSortOrder</SortOrder>
      <Area>@StandArea</Area>
      <CustomFields>
         <CustomField>
            <Name>S---_TowId</Name>
            <Value>@GateTowId</Value>
            <Type>String</Type>
         </CustomField>
         <CustomField>
            <Name>B---_TowingNotStarted</Name>
            <Value>@GateTowAlert</Value>
            <Type>Boolean</Type>
         </CustomField>
      </CustomFields>
   </FixedResource>";

        private readonly Dictionary<string, StandEntity> standMap = new Dictionary<string, StandEntity>();
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public StandManager() { }

        public async void UpdateStandAsync(TowEntity tow)
        {
            StandEntity stand = await GetStand(tow.fromStand);

            if (stand == null)
            {
                Logger.Error($"Stand for tow {tow.towID} not found");
                return;
            }

            string update = standUpdate.Replace("@StandId", stand.Id);
            update = update.Replace("@StandName", stand.Name);
            update = update.Replace("@StandSortOrder", stand.SortOrder);
            update = update.Replace("@StandArea", stand.Area);

            if (tow.isAlerted())
            {
                update = update.Replace("@GateTowAlert", "true");
                update = update.Replace("@GateTowId", tow.towID);
            }
            else
            {
                if (stand.alertTowId != tow.towID)
                {
                    Logger.Info("Tow update not the one flagged by this stand at the moment");
                    return;
                }
                else
                {
                    update = update.Replace("@GateTowAlert", "false");
                    update = update.Replace("@GateTowId", null);
                }

            }

            using (var client = new HttpClient())
            {
                string uri = Parameters.BASE_URI + $"{Parameters.APT_CODE}/Stands/{stand.Id}";
                HttpContent httpContent = new StringContent(update, Encoding.UTF8, "application/xml");
                client.DefaultRequestHeaders.Add("Authorization", Parameters.TOKEN);

                try
                {
                    HttpResponseMessage response = await client.PutAsync(uri, httpContent);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        Logger.Info($"Updated Stand {stand.Id}");
                        if (Parameters.DEEPTRACE)
                        {
                            Logger.Error(update);
                        }
                    }
                }
                catch (System.Exception)
                {
                    Logger.Error($"Failed to update Stand {stand.Id}");
                    if (Parameters.DEEPTRACE)
                    {
                        Logger.Error(update);
                    }
                    return;
                }


            }
        }

        public async void ClearStandAsync(StandEntity stand)
        {

            if (stand == null)
            {
                return;
            }

            Logger.Info($"Clearing Stand {stand}");

            string update = standUpdate.Replace("@StandId", stand.Id);
            update = update.Replace("@StandName", stand.Name);
            update = update.Replace("@StandSortOrder", stand.SortOrder);
            update = update.Replace("@StandArea", stand.Area);
            update = update.Replace("@GateTowAlert", "false");
            update = update.Replace("@GateTowId", null);


            using (var client = new HttpClient())
            {
                string uri = Parameters.BASE_URI + $"{Parameters.APT_CODE}/Stands/{stand.Id}";
                HttpContent httpContent = new StringContent(update, Encoding.UTF8, "application/xml");
                client.DefaultRequestHeaders.Add("Authorization", Parameters.TOKEN);

                try
                {
                    HttpResponseMessage response = await client.PutAsync(uri, httpContent);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        Logger.Info($"CLEARED Stand {stand.Id}");
                        return;
                        //if (Parameters.DEEPTRACE)
                        //{
                        //    Logger.Error(update);
                        //}
                    }
                }
                catch (System.Exception)
                {
                    Logger.Error($"Failed to update Stand {stand.Id}");
                    if (Parameters.DEEPTRACE)
                    {
                        Logger.Error(update);
                    }
                    return;
                }

                Logger.Info($"Updated Stand {stand.Id}");
                if (Parameters.DEEPTRACE)
                {
                    Logger.Trace(update);
                }
            }
        }


        public async Task<StandEntity> GetStand(string standID)
        {

            using (var client = new HttpClient())
            {

                client.DefaultRequestHeaders.Add("Authorization", Parameters.TOKEN);
                string uri = Parameters.BASE_URI + $"{Parameters.APT_CODE}/Stands";


                var result = await client.GetAsync(uri);
                XElement xmlRoot = XDocument.Parse(await result.Content.ReadAsStringAsync()).Root;
                XElement db = (from n in xmlRoot.Descendants() where (n.Name == "FixedResource" && n.Elements("Name").FirstOrDefault().Value == standID) select n).FirstOrDefault<XElement>();

                return new StandEntity(db);
            }

        }
    }
}

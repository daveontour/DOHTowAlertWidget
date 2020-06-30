using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AMSTowingAlertWidget
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

        public async void UpdateStandAsync(TowEntity tow, bool delete = false)
        {
            if (tow == null)
            {
                Logger.Error($"Update Stand Async: TOW IS NULL ERROR");
                return;
            }

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

            if (tow.isAlerted() && !delete)
            {
                update = update.Replace("@GateTowAlert", "true");
                update = update.Replace("@GateTowId", tow.towID);
            }
            else
            {
                if (stand.alertTowId != tow.towID && stand.alertTowId != null)
                {
                    Logger.Info("\nTow update not the one flagged by this stand at the moment");
                    Logger.Info($"TowID = {tow.towID}, Current ID = {stand.alertTowId}");
                    return;
                }
                else
                {
                    update = update.Replace("@GateTowAlert", "false");
                    update = update.Replace("@GateTowId", null);
                }
            }


            Logger.Info($"Updating Stand {stand.Id}");

            using (var client = new HttpClient())
            {
                string uri = Parameters.AMS_REST_SERVICE_URI + $"{Parameters.APT_CODE}/Stands/{stand.Id}";
                HttpContent httpContent = new StringContent(update, Encoding.UTF8, "application/xml");
                client.DefaultRequestHeaders.Add("Authorization", Parameters.TOKEN);

                try
                {
                    HttpResponseMessage response = await client.PutAsync(uri, httpContent);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        if (Parameters.DEEPTRACE)
                        {
                            Logger.Trace(update);
                            Logger.Trace(await response.Content.ReadAsStringAsync());
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

            Logger.Info($"Stand Updated {stand.Id}");

            stand = await GetStand(tow.fromStand);
            Logger.Trace($"===========>  {stand}");
        }

        public async void ClearStandAsync(StandEntity stand)
        {

            if (stand == null || (stand.towAlert == false && (stand.alertTowId == null || stand.alertTowId == "")))
            {
                return;
            }
            else
            {
                Logger.Trace($"Need to Clear: {stand}");
            }

            string update = standUpdate.Replace("@StandId", stand.Id);
            update = update.Replace("@StandName", stand.Name);
            update = update.Replace("@StandSortOrder", stand.SortOrder);
            update = update.Replace("@StandArea", stand.Area);
            update = update.Replace("@GateTowAlert", "false");
            update = update.Replace("@GateTowId", null);


            using (var client = new HttpClient())
            {
                string uri = Parameters.AMS_REST_SERVICE_URI + $"{Parameters.APT_CODE}/Stands/{stand.Id}";
                HttpContent httpContent = new StringContent(update, Encoding.UTF8, "application/xml");
                client.DefaultRequestHeaders.Add("Authorization", Parameters.TOKEN);

                try
                {
                    HttpResponseMessage response = await client.PutAsync(uri, httpContent);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        Logger.Info($"CLEARED Stand {stand.Id}");
                        return;
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.Error($"Failed to update Stand {stand.Id}. {ex.Message}");
                    if (Parameters.DEEPTRACE)
                    {
                        Logger.Error(update);
                    }
                    return;
                }
            }
        }


        public async Task<StandEntity> GetStand(string standID)
        {

            using (var client = new HttpClient())
            {

                client.DefaultRequestHeaders.Add("Authorization", Parameters.TOKEN);
                string uri = Parameters.AMS_REST_SERVICE_URI + $"{Parameters.APT_CODE}/Stands";


                var result = await client.GetAsync(uri);
                XElement xmlRoot = XDocument.Parse(await result.Content.ReadAsStringAsync()).Root;
                XElement db = (from n in xmlRoot.Descendants() where (n.Name == "FixedResource" && n.Elements("Name").FirstOrDefault().Value == standID) select n).FirstOrDefault<XElement>();

                return new StandEntity(db);
            }

        }
    }
}

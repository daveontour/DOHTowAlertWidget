using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOH_AMSTowingWidget {
    public class Parameters {

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static string TOKEN;
        public static string BASE_URI;
        public static string APT_CODE;
        public static string RECVQ;
        public static string ALERT_FIELD;
        public static string APT_CODE_ICAO;

        static Parameters() {

  
            try {

                APT_CODE = (string)ConfigurationManager.AppSettings["IATAAirportCode"];
                APT_CODE_ICAO = (string)ConfigurationManager.AppSettings["ICAOAirportCode"];
                TOKEN = (string)ConfigurationManager.AppSettings["Token"];
                BASE_URI = (string)ConfigurationManager.AppSettings["BaseURI"];
                RECVQ = (string)ConfigurationManager.AppSettings["NotificationQueue"];
                ALERT_FIELD  = (string)ConfigurationManager.AppSettings["AlertField"];

            } catch (Exception ex) {
                Logger.Error(ex.Message);
            }
        }
    }
}

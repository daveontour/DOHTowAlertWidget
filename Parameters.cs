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
        public static int BIG_RESET_TIME;
        public static string LOGFILEPATH;
        public static bool CONSOLE_LOG;
        public static int EARLIEST_DOWNGRADE_DAYS;
        public static int LATEST_DOWNGRADE_DAYS;
        public static string RECVQ;
        public static string RESTAPIBASE;
        public static string SENDQ;
        public static DateTime EARLIEST_DOWNGRADE;
        public static DateTime LATEST_DOWNGRADE;
        public static bool LOGEVENTS;
        public static bool EVENT_LOG_ERROR_ONLY;
        public static string ALERT_FIELD;
        public static string APT_CODE_ICAO;

        public static int cl;
        public static int le;
        public static int eo;

        static Parameters() {

  
            try {

                APT_CODE = (string)ConfigurationManager.AppSettings["IATAAirportCode"];
                APT_CODE_ICAO = (string)ConfigurationManager.AppSettings["ICAOAirportCode"];
                TOKEN = (string)ConfigurationManager.AppSettings["Token"];
                LOGFILEPATH = (string)ConfigurationManager.AppSettings["LogFilePath"];
                BASE_URI = (string)ConfigurationManager.AppSettings["BaseURI"];
                BIG_RESET_TIME = Int32.Parse((string)ConfigurationManager.AppSettings["ResetTime"]);
                RECVQ = (string)ConfigurationManager.AppSettings["NotificationQueue"];
                SENDQ = (string)ConfigurationManager.AppSettings["RequestQueue"];
                EARLIEST_DOWNGRADE_DAYS = Int32.Parse((string)ConfigurationManager.AppSettings["EarliestDowngradeOffset"]);
                LATEST_DOWNGRADE_DAYS = Int32.Parse((string)ConfigurationManager.AppSettings["LatestDowngradeOffset"]);
                EARLIEST_DOWNGRADE = DateTime.Now.AddDays(Parameters.EARLIEST_DOWNGRADE_DAYS);
                LATEST_DOWNGRADE = DateTime.Now.AddDays(Parameters.LATEST_DOWNGRADE_DAYS);
                RESTAPIBASE = @"/api/v1/" + APT_CODE + "/{0}s";
                ALERT_FIELD  = (string)ConfigurationManager.AppSettings["AlertField"];

                cl = Int32.Parse((string)ConfigurationManager.AppSettings["ConsoleLog"]);
                le = Int32.Parse((string)ConfigurationManager.AppSettings["EventLog"]);
                eo = Int32.Parse((string)ConfigurationManager.AppSettings["EventLogErrorOnly"]);

                if (cl > 0) {
                    CONSOLE_LOG = true;
                } else {
                    CONSOLE_LOG = false;
                }

                if (le > 0) {
                    LOGEVENTS = true;
                } else {
                    LOGEVENTS = false;
                }

                if (eo > 0) {
                    EVENT_LOG_ERROR_ONLY = true;
                } else {
                    EVENT_LOG_ERROR_ONLY = false;
                }

            } catch (Exception ex) {
                Logger.Error(ex.Message);
            }
        }
    }
}

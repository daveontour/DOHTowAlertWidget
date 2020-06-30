using System;
using System.Configuration;

//Version 4.0

namespace AMSTowingAlertWidget
{

    /*
     * Class to make the configuration parameters available. 
     * The static constructor makes sure the parameters are initialised the first time the 
     * class is accessed
     * 
     * 
     */
    public class Parameters
    {

        static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static string TOKEN;
        public static string AMS_REST_SERVICE_URI;
        public static string AMS_WEB_SERVICE_URI;
        public static string APT_CODE;
        public static string RECVQ;
        public static string FLIGHTALERTFIELD;
        public static string APT_CODE_ICAO;
        public static int REFRESH_INTERVAL;
        public static int GRACE_PERIOD;
        public static double FROM_HOURS;
        public static double TO_HOURS;
        public static int RESTSERVER_RETRY_INTERVAL;
        public static bool STARTUP_FLIGHT_PROCESSING;
        public static bool STARTUP_STAND_PROCESSING;
        public static string VERSION = "Version 4.0, 20200625";
        public static bool DEEPTRACE;
        public static bool ALERT_FLIGHT;
        public static bool ALERT_STAND;

        public static string STANDALERTFIELD;
        public static string STANDTOWIDFIELD;





        static Parameters()
        {
            try
            {
                STANDALERTFIELD = (string)ConfigurationManager.AppSettings["StandAlertField"];
                STANDTOWIDFIELD = (string)ConfigurationManager.AppSettings["StandTowIDField"];

                FLIGHTALERTFIELD = (string)ConfigurationManager.AppSettings["FlightAlertField"];

                APT_CODE = (string)ConfigurationManager.AppSettings["IATAAirportCode"];
                APT_CODE_ICAO = (string)ConfigurationManager.AppSettings["ICAOAirportCode"];
                TOKEN = (string)ConfigurationManager.AppSettings["Token"];
                AMS_REST_SERVICE_URI = (string)ConfigurationManager.AppSettings["AMSRestServiceURI"];
                AMS_WEB_SERVICE_URI = (string)ConfigurationManager.AppSettings["AMSWebServiceURI"];
                RECVQ = (string)ConfigurationManager.AppSettings["NotificationQueue"];
                GRACE_PERIOD = Int32.Parse((string)ConfigurationManager.AppSettings["GracePeriod"]);
                REFRESH_INTERVAL = Int32.Parse((string)ConfigurationManager.AppSettings["RefreshInterval"]);
                RESTSERVER_RETRY_INTERVAL = Int32.Parse((string)ConfigurationManager.AppSettings["ResetServerRetryInterval"]);
                FROM_HOURS = double.Parse((string)ConfigurationManager.AppSettings["FromHours"]);
                TO_HOURS = double.Parse((string)ConfigurationManager.AppSettings["ToHours"]);

                STARTUP_FLIGHT_PROCESSING = bool.Parse((string)ConfigurationManager.AppSettings["StartUpFlightProcessing"]);
                STARTUP_STAND_PROCESSING = bool.Parse((string)ConfigurationManager.AppSettings["StartUpStandProcessing"]);

                ALERT_FLIGHT = bool.Parse((string)ConfigurationManager.AppSettings["AlertFlight"]);
                ALERT_STAND = bool.Parse((string)ConfigurationManager.AppSettings["AlertStand"]);

                try
                {
                    DEEPTRACE = bool.Parse((string)ConfigurationManager.AppSettings["DeepTrace"]);
                }
                catch (Exception)
                {
                    DEEPTRACE = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
        }


    }
}

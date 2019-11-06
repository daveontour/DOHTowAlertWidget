﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOH_AMSTowingWidget {

    /*
     * Class to make the configuration parameters available. 
     * The static constructor makes sure the parameters are initialised the first time the 
     * class is accessed
     * 
     * 
     */
    public class Parameters {

        static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static string TOKEN;
        public static string BASE_URI;
        public static string APT_CODE;
        public static string RECVQ;
        public static string ALERT_FIELD;
        public static string APT_CODE_ICAO;
        public static int REFRESH_INTERVAL;
        public static int GRACE_PERIOD;
        public static double FROM_HOURS;
        public static double TO_HOURS;
        public static int RESTSERVER_RETRY_INTERVAL;


        static Parameters() { 
            try {
                APT_CODE = (string)ConfigurationManager.AppSettings["IATAAirportCode"];
                APT_CODE_ICAO = (string)ConfigurationManager.AppSettings["ICAOAirportCode"];
                TOKEN = (string)ConfigurationManager.AppSettings["Token"];
                BASE_URI = (string)ConfigurationManager.AppSettings["BaseURI"];
                RECVQ = (string)ConfigurationManager.AppSettings["NotificationQueue"];
                ALERT_FIELD  = (string)ConfigurationManager.AppSettings["AlertField"];
                GRACE_PERIOD = Int32.Parse((string)ConfigurationManager.AppSettings["GracePeriod"]);
                REFRESH_INTERVAL = Int32.Parse((string)ConfigurationManager.AppSettings["RefreshInterval"]);
                RESTSERVER_RETRY_INTERVAL = Int32.Parse((string)ConfigurationManager.AppSettings["ResetServerRetryInterval"]);
                FROM_HOURS = double.Parse((string)ConfigurationManager.AppSettings["FromHours"]);
                TO_HOURS = double.Parse((string)ConfigurationManager.AppSettings["ToHours"]);
            } catch (Exception ex) {
                Logger.Error(ex.Message);
            }
        }

        
    }
}

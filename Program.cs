using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;



//Version RC 1.0

namespace DOH_AMSTowingWidget {
    class Program {

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        // Primarily skeleton code for defining the ConsoleApp/Service to be managed by TopShelf
        static void Main(string[] args) {
            var exitCode = HostFactory.Run(x =>
            {
                /*
                 * The 'TowNotStarted' class is the class that provides the actual functionality.
                 * The two key methods that Exchange has to implement are "Start()" and "Stop()"
                 */

                x.Service<TowNotStarted>(s =>
                {
                    s.ConstructUsing(core => new TowNotStarted());
                    s.WhenStarted(core => core.Start());
                    s.WhenStopped(core => core.Stop());
                });

                x.RunAsLocalSystem();
                x.StartAutomatically();
                x.EnableServiceRecovery(rc =>
                {
                    rc.RestartService(1); // restart the service after 1 minute
                });

                /*
                 * Get any customisation for the Service Name and description from the configuration file
                 * This is useful is multiple instance of the service are run from different directories
                 */
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                string serviceName = string.IsNullOrEmpty(appSettings["ServiceName"]) ? "SITAAMSTowNotStarted" : appSettings["ServiceName"];
                string serviceDisplayName = string.IsNullOrEmpty(appSettings["ServiceDisplayName"]) ? "SITA AMS - Tow Not Started Utility Service" : appSettings["ServiceDisplayName"];
                string serviceDescription = string.IsNullOrEmpty(appSettings["ServiceDescription"]) ? "Writes to a custom field on the arrival and departure flight if a towing has not started by the schedued time" : appSettings["ServiceDescription"];

                x.SetServiceName(serviceName);
                x.SetDisplayName(serviceDisplayName);
                x.SetDescription(serviceDescription);
            });

            int exitCodeValue = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
            Environment.ExitCode = exitCodeValue;
        }
    }

}

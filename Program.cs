﻿using System;
using System.Collections.Specialized;
using System.Configuration;
using Topshelf;



//Version 4.0.1

namespace AMSTowingAlertWidget
{
    class Program
    {

        // Primarily skeleton code for defining the ConsoleApp/Service to be managed by TopShelf
        static void Main(string[] args)
        {
            var exitCode = HostFactory.Run(x =>
            {
                /*
                 * The 'TowNotStarted' class is the class that provides the actual functionality.
                 * The two key methods that Exchange has to implement are "Start()" and "Stop()"
                 */

                x.Service<TowMonitor>(s =>
                {
                    s.ConstructUsing(core => new TowMonitor());
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
                string serviceName = string.IsNullOrEmpty(appSettings["ServiceName"]) ? $"SITA MEIA - Towing Actual Not Available {Parameters.VERSION}" : appSettings["ServiceName"];
                string serviceDisplayName = string.IsNullOrEmpty(appSettings["ServiceDisplayName"]) ? $"SITA MEIA - AMS Tow Actual Not Available Utility Service ({Parameters.VERSION})" : appSettings["ServiceDisplayName"];
                string serviceDescription = string.IsNullOrEmpty(appSettings["ServiceDescription"]) ? "Writes to a custom field on the arrival and departure flight if a towing actual has not been entered and 'now' is past the scheduled time" : appSettings["ServiceDescription"];

                x.SetServiceName(serviceName);
                x.SetDisplayName(serviceDisplayName);
                x.SetDescription(serviceDescription);
            });

            int exitCodeValue = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
            Environment.ExitCode = exitCodeValue;
        }
    }

}

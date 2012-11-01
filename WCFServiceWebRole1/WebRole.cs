using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Diagnostics.Management;

namespace WCFServiceWebRole1
{
    public class WebRole : RoleEntryPoint
    {
        public override bool OnStart()
        {
            // To enable the AzureLocalStorageTraceListner, uncomment relevent section in the web.config  
            DiagnosticMonitorConfiguration diagnosticConfig = DiagnosticMonitor.GetDefaultInitialConfiguration();
            diagnosticConfig.Directories.ScheduledTransferPeriod = TimeSpan.FromMinutes(1);
            diagnosticConfig.Directories.DataSources.Add(AzureLocalStorageTraceListener.GetLogDirectory());

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            ///////////////////////////////////////////////////////////////////
            //
            // Setup Logging
            //
            ///////////////////////////////////////////////////////////////////
            string wadConnectionString = "Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString";
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue(wadConnectionString));

            RoleInstanceDiagnosticManager roleInstanceDiagnosticManager = storageAccount.CreateRoleInstanceDiagnosticManager(RoleEnvironment.DeploymentId,
                                                                                                                             RoleEnvironment.CurrentRoleInstance.Role.Name,
                                                                                                                             RoleEnvironment.CurrentRoleInstance.Id);
            DiagnosticMonitorConfiguration config = roleInstanceDiagnosticManager.GetCurrentConfiguration();

            if (config == null)
            {
                config = DiagnosticMonitor.GetDefaultInitialConfiguration();
            }

            //
            // Capture logs to WADLogsTable every 2 minutes.
            //
            var transferTime = 2;

            config.Logs.ScheduledTransferPeriod = TimeSpan.FromMinutes(transferTime);
            config.Logs.ScheduledTransferLogLevelFilter = LogLevel.Verbose;

            roleInstanceDiagnosticManager.SetCurrentConfiguration(config);
            ///////////////////////////////////////////////////////////////////

            return base.OnStart();
        }
    }
}

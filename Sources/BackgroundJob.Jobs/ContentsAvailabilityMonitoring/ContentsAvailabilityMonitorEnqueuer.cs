using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BackgroundJob.Core;

namespace BackgroundJob.Jobs.ContentsAvailabilityMonitoring
{
    internal class ContentsAvailabilityMonitorEnqueuer : IRecurringJobBase
    {
        private readonly IEnumerable<IJobConfiguration> _jobConfigurations;

        public ContentsAvailabilityMonitorEnqueuer(IEnumerable<IJobConfiguration> jobConfigurations)
        {
            _jobConfigurations = jobConfigurations;
        }

        public void Enqueue()
        {
            var jobConfiguration = _jobConfigurations.Single(c => c.Type.Contains(GetType().FullName));
            var queueName = jobConfiguration.QueueName;
            var date = DateTime.Now.ToString("yyyy-MM-dd H:mm:ss");
            Core.Helpers.BackgroundJob.Enqueue<IContentsAvailabilityMonitor>(queueName,
                c => c.CheckAndReport(date, new CancellationToken()), "ContentsAvailabilityMonitor - " + date, jobConfiguration.MaxReplay);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BackgroundJob.Configuration;
using BackgroundJob.Core;
using BackgroundJob.Host.Quartz;
using Quartz;

namespace BackgroundJob.Host.Example
{
    internal class TestRecurringJobEnqueuer : IRecurringJobBase
    {
        private readonly IEnumerable<IJobConfiguration> _jobConfigurations;

        public TestRecurringJobEnqueuer(IEnumerable<IJobConfiguration> jobConfigurations)
        {
            _jobConfigurations = jobConfigurations;
        }

        public void Enqueue()
        {
            var jobConfiguration = _jobConfigurations.Single(c => c.Type == GetType().FullName);
            var queueName = jobConfiguration.QueueName;
            var someParameter = Guid.NewGuid().ToString();
            Core.Helpers.BackgroundJob.Enqueue<ITestJob>(queueName,
                c => c.Process(someParameter, new CancellationToken()), "TestRecurringJob - " + someParameter, jobConfiguration.MaxReplay);
        }

        public void Execute(IJobExecutionContext context)
        {
            Enqueue();
        }
    }
}
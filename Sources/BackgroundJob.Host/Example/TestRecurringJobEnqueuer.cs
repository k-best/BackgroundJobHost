using System;
using System.Linq;
using System.Threading;
using BackgroundJob.Configuration;
using BackgroundJob.Host.Quartz;
using Quartz;

namespace BackgroundJob.Host.Example
{
    internal class TestRecurringJobEnqueuer : IRecurringJobBase
    {
        private readonly JobConfigurations _jobConfigurations;

        public TestRecurringJobEnqueuer(JobConfigurations jobConfigurations)
        {
            _jobConfigurations = jobConfigurations;
        }

        public void Enqueue()
        {
            var queueName = _jobConfigurations.Jobs.Cast<JobConfiguration>().Single(c => c.Type == GetType().FullName).QueueName;
            var someParameter = Guid.NewGuid().ToString();
            BackgroundJob.Enqueue<ITestJob>(queueName,
                c => c.Process(someParameter, new CancellationToken()), "someParameter: " + someParameter);
        }

        public void Execute(IJobExecutionContext context)
        {
            Enqueue();
        }
    }
}
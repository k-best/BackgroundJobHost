using System;
using Quartz;

namespace BackgroundJob.Host.Quartz
{
    class ExceptionJobListener : IJobListener
    {
        private readonly Action<JobExecutionException> _exceptionHandler;

        public ExceptionJobListener(Action<JobExecutionException> exceptionHandler)
        {
            _exceptionHandler = exceptionHandler;
        }

        public void JobToBeExecuted(IJobExecutionContext context)
        {
        }

        public void JobExecutionVetoed(IJobExecutionContext context)
        {
        }

        public void JobWasExecuted(IJobExecutionContext context, JobExecutionException jobException)
        {
            if (jobException != null)
                _exceptionHandler(jobException);
        }

        public string Name
        {
            get { return GetType().FullName; }
        }
    }
}
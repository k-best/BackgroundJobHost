using System;
using Autofac;
using Quartz;
using Quartz.Spi;

namespace BackgroundJob.Host.Quartz
{
    class AutofacQuartzJobFactory : IJobFactory, IEnqueuerFactory, IDisposable
    {
        private readonly ILifetimeScope _lifetimeScope;

        public AutofacQuartzJobFactory(ILifetimeScope scope)
        {
            _lifetimeScope = scope;
        }

        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            try
            {
                return (IJob)_lifetimeScope.Resolve(bundle.JobDetail.JobType);
            }
            catch (Exception e)
            {
                throw new SchedulerException("Problem instantiating class: " + e.Message, e);
            }
        }

        public void ReturnJob(IJob job)
        {
        }

        public RecurringJobWrapper Create(Type enqueuerType)
        {
            try
            {
                var wrapperType = RecurringJobWrapper.CreateType(enqueuerType);
                return (RecurringJobWrapper)_lifetimeScope.Resolve(wrapperType);
            }
            catch (Exception e)
            {
                throw new SchedulerException("Problem instantiating class: " + e.Message, e);
            }
        }

        public void Dispose()
        {
            _lifetimeScope.Dispose();
        }
    }
}
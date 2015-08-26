using System;
using Microsoft.Practices.Unity;
using Quartz;
using Quartz.Spi;

namespace BackgroundJob.Host.Quartz
{
    class UnityJobFactory : IJobFactory, IEnqueuerFactory
    {
        private readonly IUnityContainer _container;

        public UnityJobFactory(IUnityContainer container)
        {
            _container = container;
        }

        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            try
            {
                return (IJob)_container.Resolve(bundle.JobDetail.JobType);
            }
            catch (Exception e)
            {
                throw new SchedulerException("Problem instantiating class: " + e.Message, e);
            }
        }

        public void ReturnJob(IJob job)
        {
        }

        public IRecurringJobBase Create(Type enqueuerType)
        {
            try
            {
                return (IRecurringJobBase)_container.Resolve(enqueuerType);
            }
            catch (Exception e)
            {
                throw new SchedulerException("Problem instantiating class: " + e.Message, e);
            }
        }
    }

    public interface IEnqueuerFactory
    {
        IRecurringJobBase Create(Type enqueuerType);
    }
}
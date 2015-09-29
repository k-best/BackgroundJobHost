using System;
using Quartz;

namespace BackgroundJob.Host.Quartz
{
    public interface IEnqueuerFactory
    {
        RecurringJobWrapper Create(Type enqueuerType);
        void ReturnJob(IJob job);
    }
}
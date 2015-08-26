using Quartz;

namespace BackgroundJob.Host.Quartz
{
    public interface IRecurringJobBase : IJob
    {
        void Enqueue();
    }
}
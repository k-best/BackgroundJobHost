using System;
using Quartz;

namespace BackgroundJob.Host.Quartz
{
    public static class SchedulerHelper
    {
        public static void Add<T>(this IScheduler scheduler, JobKey jobKey, string triggerTime) where T : IJob
        {
            var trigger = TriggerBuilder.Create().WithIdentity(jobKey.Name + "trigger").WithCronSchedule(triggerTime).ForJob(jobKey).Build();
            var job = JobBuilder.Create<T>().WithIdentity(jobKey).Build();
            scheduler.ScheduleJob(job, trigger);
        }

        public static void Add(this IScheduler scheduler, Type jobType, JobKey jobKey, string triggerTime)
        {
            var trigger = TriggerBuilder.Create().WithIdentity(jobKey.Name + "trigger").WithCronSchedule(triggerTime).ForJob(jobKey).Build();
            var job = JobBuilder.Create(jobType).WithIdentity(jobKey).Build();
            scheduler.ScheduleJob(job, trigger);
        }
    }
}
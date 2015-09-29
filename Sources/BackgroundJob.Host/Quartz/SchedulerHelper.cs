using System;
using System.Linq;
using BackgroundJob.Core;
using Quartz;

namespace BackgroundJob.Host.Quartz
{
    public static class SchedulerHelper
    {
        public static void AddRecurringJob<T>(this IScheduler scheduler, JobKey jobKey, string triggerTime) where T : IRecurringJobBase
        {
            var trigger = TriggerBuilder.Create().WithIdentity(jobKey.Name + "trigger").WithCronSchedule(triggerTime).ForJob(jobKey).Build();
            var job = JobBuilder.Create<RecurringJobWrapper<T>>().WithIdentity(jobKey).Build();
            scheduler.ScheduleJob(job, trigger);
        }

        public static void Add<T>(this IScheduler scheduler, JobKey jobKey, string triggerTime) where T : IJob
        {
            var trigger = TriggerBuilder.Create().WithIdentity(jobKey.Name + "trigger").WithCronSchedule(triggerTime).ForJob(jobKey).Build();
            var job = JobBuilder.Create<T>().WithIdentity(jobKey).Build();
            scheduler.ScheduleJob(job, trigger);
        }

        public static void AddRecurringJob(this IScheduler scheduler, Type jobType, JobKey jobKey, string triggerTime)
        {
            if(!typeof(IRecurringJobBase).IsAssignableFrom(jobType))
                throw new ArgumentException("Wrong type of recurrent task enqueuer. It should be assignable from IRecurringJobBase");
            var wrapperType = RecurringJobWrapper.CreateType(jobType);
            var trigger = TriggerBuilder.Create().WithIdentity(jobKey.Name + "trigger").WithCronSchedule(triggerTime).ForJob(jobKey).Build();
            var job = JobBuilder.Create(wrapperType).WithIdentity(jobKey).Build();
            scheduler.ScheduleJob(job, trigger);
        }

        public static void Remove(this IScheduler scheduler, JobKey jobKey)
        {
            var triggers = scheduler.GetTriggersOfJob(jobKey);
            scheduler.UnscheduleJobs(triggers.Select(c => c.Key).ToList());
        }
    }
}
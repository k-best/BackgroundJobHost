using System;
using BackgroundJob.Core;
using Quartz;

namespace BackgroundJob.Host.Quartz
{
    public abstract class RecurringJobWrapper : IJob, IRecurringJobBase
    {
        public static Type CreateType(Type type)
        {
            var genericType = typeof(RecurringJobWrapper<>);
            var result = genericType.MakeGenericType(type);
            return result;
        }

        public abstract void Execute(IJobExecutionContext context);


        public abstract void Enqueue();
    }

    public class RecurringJobWrapper<T> :RecurringJobWrapper where T : IRecurringJobBase
    {
        private readonly T _enqueuer;

        public RecurringJobWrapper(T enqueuer)
        {
            _enqueuer = enqueuer;
        }

        public override void Execute(IJobExecutionContext context)
        {
            _enqueuer.Enqueue();
        }

        public override void Enqueue()
        {
            _enqueuer.Enqueue();
        }
    }
}
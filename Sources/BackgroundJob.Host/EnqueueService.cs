using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using BackgroundJob.Configuration;
using BackgroundJob.Core;
using BackgroundJob.Host.Quartz;

namespace BackgroundJob.Host
{
    public class EnqueueService:IEnqueueService
    {
        private readonly IEnumerable<IJobConfiguration> _jobsConfig;
        private readonly IEnqueuerFactory _enqueuerFactory;

        public EnqueueService(IEnumerable<IJobConfiguration> jobsConfig, IEnqueuerFactory enqueuerFactory)
        {
            _jobsConfig = jobsConfig;
            _enqueuerFactory = enqueuerFactory;
        }

        public void Enqueue(string jobName)
        {
            var jobConfiguration = _jobsConfig.FirstOrDefault(c => c.Name == jobName);
            if(jobConfiguration==null)
                throw new InvalidOperationException(string.Format("Не найден зарегистрированный обработчик {0}", jobName));
            var jobType = Type.GetType(jobConfiguration.Type);
            var enqueuer = _enqueuerFactory.Create(jobType);
            enqueuer.Enqueue();
            _enqueuerFactory.ReturnJob(enqueuer);
        }

        public void ChangeScheduleForJob(string jobName)
        {
            
        }
    }

    [ServiceContract(Namespace = "")]
    public interface IEnqueueService
    {
        [OperationContract]
        void Enqueue(string jobName);

        [OperationContract]
        void ChangeScheduleForJob(string jobName);
    }
}
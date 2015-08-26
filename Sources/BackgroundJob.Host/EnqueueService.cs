using System;
using System.Linq;
using System.ServiceModel;
using BackgroundJob.Configuration;
using BackgroundJob.Host.Quartz;

namespace BackgroundJob.Host
{
    public class EnqueueService:IEnqueueService
    {
        private readonly JobConfigurations _jobsConfig;
        private readonly IEnqueuerFactory _enqueuerFactory;

        public EnqueueService(JobConfigurations jobsConfig, IEnqueuerFactory enqueuerFactory)
        {
            _jobsConfig = jobsConfig;
            _enqueuerFactory = enqueuerFactory;
        }

        public void Enqueue(string jobName)
        {
            var jobs = _jobsConfig.Jobs.Cast<JobConfiguration>().ToArray();
            var jobConfiguration = jobs.FirstOrDefault(c => c.Name == jobName);
            if(jobConfiguration==null)
                throw new InvalidOperationException(string.Format("Не найден зарегистрированный обработчик {0}", jobName));
            var jobType = Type.GetType(jobConfiguration.Type);
            var enqueuer = _enqueuerFactory.Create(jobType);
            enqueuer.Enqueue();
        }
    }

    [ServiceContract(Namespace = "")]
    public interface IEnqueueService
    {
        [OperationContract]
        void Enqueue(string jobName);
    }
}
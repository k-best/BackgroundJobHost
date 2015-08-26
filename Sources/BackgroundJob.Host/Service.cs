using System;
using System.Messaging;
using System.Monads;
using System.ServiceModel;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BackgroundJob.Configuration;
using BackgroundJob.Host.Quartz;
using NLog;
using Quartz;
using Quartz.Spi;

namespace BackgroundJob.Host
{
    public class Service : ServiceBase
    {
        private readonly Logger _logger;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IJobFactory _jobFactory;
        private readonly IJobActivator _jobActivator;
        private IScheduler _scheduler;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly JobConfigurations _jobsConfig;
        private ServiceHost _serviceHost=null;
        private readonly ServiceHostFactory _serviceHostFactory;


        public Service(Logger logger, ISchedulerFactory schedulerFactory, IJobFactory jobFactory, IJobActivator jobActivator, JobConfigurations jobsConfig, ServiceHostFactory serviceHostFactory)
        {
            _logger = logger;
            _schedulerFactory = schedulerFactory;
            _jobFactory = jobFactory;
            _jobActivator = jobActivator;
            _jobsConfig = jobsConfig;
            _serviceHostFactory = serviceHostFactory;
            ServiceName = Program.ServiceName;
        }

        protected override void Dispose(bool disposing)
        {
            _cancellationTokenSource.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnStart(string[] args)
        {
            _logger.Info("Starting as service...");
            base.OnStart(args);
            StartImpl();
        }

        public void StartImpl()
        {
            StartBackgroundJobService();
            StartServiceHost();
        }

        public void StartBackgroundJobService()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _logger.Trace("Starting scheduler...");
            _scheduler = _schedulerFactory.GetScheduler();
            _scheduler.JobFactory = _jobFactory;

            _scheduler.ListenerManager.AddJobListener(new ExceptionJobListener(OnException));
            EnqueueRecurringJobs(_scheduler);

            _scheduler.Start();
        }

        private void StartServiceHost()
        {
            if (_serviceHost != null)
            {
                _serviceHost.Close();
            }
            _serviceHost = _serviceHostFactory.Create();
            _serviceHost.Open();
        }

        private static void ListenQueue(string queueName, IJobActivator jobActivator, Logger logger, CancellationToken cancellationToken)
        {
            if (!MessageQueue.Exists(queueName))
            {
                throw new InvalidOperationException(string.Format("Очередь {0} отсутствует.", queueName));
            }
            var mq = new MessageQueue(queueName);
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    logger.Info("Операция отменена");
                }
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using (var messageQueueTransaction = new MessageQueueTransaction())
                    {
                        messageQueueTransaction.Begin();
                        var mes = mq.Receive(messageQueueTransaction);
                        if (mes == null)
                        {
                            logger.Error("Получено пустое сообщение");
                            continue;
                        }
                        mes.Formatter = new XmlMessageFormatter(new[] {"System.String,mscorlib"});
                        var serializedjob = JobHelper.FromJson<SerializedJob>(mes.Body.ToString());
                        var job = serializedjob.Deserialize();
                        job.Perform(jobActivator, cancellationToken);
                        messageQueueTransaction.Commit();
                    }
                }
                catch (JobFailedException e)
                {
                    logger.Error(e.GetAllInnerExceptionMessagesAndTrace());
                    Thread.Sleep(60000);
                }
                catch (Exception e)
                {
                    logger.Fatal(e.GetAllInnerExceptionMessagesAndTrace());
                    throw;
                }
            }
        }

        private void EnqueueRecurringJobs(IScheduler scheduler)
        {
            foreach (JobConfiguration jobConfiguration in _jobsConfig.Jobs)
            {
                var jobType = Type.GetType(jobConfiguration.Type);
                var jobKey = new JobKey(jobConfiguration.QueueName);
                scheduler.Add(jobType, jobKey, jobConfiguration.SchedulingTime);
                Task.Factory.StartNew(() => ListenQueue(jobKey.Name, _jobActivator, _logger, _cancellationTokenSource.Token)).ContinueWith(
                    t => Stop(), TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        protected override void OnStop()
        {
            _logger.Info("Stopping as service...");
            StopImpl();
            base.OnStop();
        }

        public void StopImpl()
        {
            _logger.Trace("Stopping scheduler...");
            _scheduler.Do(s => s.Shutdown(false));
            _scheduler = null;
            _serviceHost.Close();
            _serviceHost = null;
            _cancellationTokenSource.Cancel();
            _logger.Info("Scheduler stopped");
        }


        private void OnException(JobExecutionException exception)
        {
            _logger.Fatal("Unhandled error while running a job");
            _logger.Fatal(exception.ToString);
            ExitCode = 1;
            StopImpl();
        }
    }

    public static class ExceptionHelper
    {
        public static string GetAllInnerExceptionMessagesAndTrace(this Exception e)
        {
            var result = new StringBuilder();
            var innerException = e;
            while (innerException != null)
            {
                result.AppendLine(string.Format("Message: {0}", innerException.Message));
                result.AppendLine(string.Format("StackTrace: {0}", innerException.StackTrace));
                innerException = innerException.InnerException;
            }
            return result.ToString();
        }
    }
}

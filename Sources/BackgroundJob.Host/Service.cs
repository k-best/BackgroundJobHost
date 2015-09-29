using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Messaging;
using System.Monads;
using System.ServiceModel;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BackgroundJob.Configuration;
using BackgroundJob.Core;
using BackgroundJob.Core.Helpers;
using BackgroundJob.Core.Serialization;
using BackgroundJob.Host.Quartz;
using NLog;
using Quartz;
using Quartz.Spi;

namespace BackgroundJob.Host
{
    public class Service : ServiceBase
    {
        private readonly Logger _logger;
        private readonly ConcurrentHashSet<InWorkMessage> _inWorkMessages = new ConcurrentHashSet<InWorkMessage>();
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IJobFactory _jobFactory;
        private readonly IJobActivator _jobActivator;
        private IScheduler _scheduler;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly IEnumerable<IJobConfiguration> _jobsConfig;
        private ServiceHost _serviceHost;
        private readonly WcfServiceHostFactory _serviceHostFactory;
        private readonly object _locker = new object();

        public Service(Logger logger, ISchedulerFactory schedulerFactory, IJobFactory jobFactory, IJobActivator jobActivator, IEnumerable<IJobConfiguration> jobsConfig, WcfServiceHostFactory serviceHostFactory)
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
            try
            {
                base.OnStart(args);
                StartImpl();
            }
            catch (Exception e)
            {
                ExitCode = GetExitCodeFromException(e);
                throw;
            }
        }

        public void StartImpl()
        {
            try
            {
                StartBackgroundJobService();
                if (Settings.Default.EnableWcfEndpoint)
                    StartServiceHost();
            }
            catch (Exception e)
            {
                _logger.Fatal(e, e.Message);
            }
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

        private static void ListenQueue(string queueName, IJobActivator jobActivator, Logger logger, ConcurrentHashSet<InWorkMessage> inworkMessages, CancellationToken cancellationToken)
        {
            if (!MessageQueue.Exists(queueName))
            {
                throw new InvalidOperationException(string.Format("Очередь {0} отсутствует.", queueName));
            }
            var mq = new MessageQueue(queueName);
            while (true)
            {
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
                        if (cancellationToken.IsCancellationRequested)
                        {
                            logger.Info("Операция отменена");
                            return;
                        }
                        mes.Formatter = new XmlMessageFormatter(new[] {typeof(MessageWrapper)});
                        var inWorkMessage = new InWorkMessage { Job = (MessageWrapper)mes.Body, QueueName = queueName, Label = mes.Label};
                        if(!inworkMessages.TryAdd(inWorkMessage))
                            continue;
                        if (inWorkMessage.Job.RetryCount==0)
                            logger.Info("Запущена задача {0}", inWorkMessage.Label);
                        else
                            logger.Info("Запущена задача {0}. Повторная попытка {1}", inWorkMessage.Label, inWorkMessage.Job.RetryCount);
                        var serializedjob = JobHelper.FromJson<SerializedJob>(inWorkMessage.Job.SerializedJob);
                        var job = serializedjob.Deserialize();
                        //Отправляем задачу в работу и добавляем обработчик который в случае ошибки или отмены задачи вернет сообщение в очередь.
                        //Если задача завершилась успешно, проставляем флаг об этом.
                        Task.Factory.StartNew(() => job.Perform(jobActivator, cancellationToken), cancellationToken)
                            .ContinueWith(t =>
                            {
                                if (t.Exception != null)
                                {
                                    t.Exception.Handle(ex =>
                                    {
                                        if (ex.GetType() == typeof (JobFailedException))
                                        {
                                            logger.Info("При выполнении задачи {0} возникла ошибка.", inWorkMessage.Label);
                                            logger.Error(ex.GetAllInnerExceptionMessagesAndTrace());
                                            Thread.Sleep(60000);
                                            ReturnMessageToQueue(inWorkMessage, inworkMessages, cancellationToken);
                                            return true;
                                        }
                                        logger.Fatal(ex.GetAllInnerExceptionMessagesAndTrace());
                                        ReturnMessageToQueue(inWorkMessage, inworkMessages, cancellationToken);
                                        return false;
                                    });
                                }
                                else
                                {
                                    logger.Info("Задача {0} завершилась успешно.", inWorkMessage.Label);
                                    inWorkMessage.CompleteMessage();
                                }
                            }, TaskContinuationOptions.NotOnCanceled);
                        messageQueueTransaction.Commit();
                    }
                }
                catch (Exception e)
                {
                    logger.Fatal(e.GetAllInnerExceptionMessagesAndTrace());
                    //inworkMessages.CompleteAdding();
                    throw;
                }
            }
        }

        private static void ReturnMessageToQueue(InWorkMessage message, ConcurrentHashSet<InWorkMessage> inworkMessages, CancellationToken cancellationToken)
        {
            message.ReturnMessage();
            if (inworkMessages != null)
                inworkMessages.TryRemove(message);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private void EnqueueRecurringJobs(IScheduler scheduler)
        {
            foreach (var jobConfiguration in _jobsConfig)
            {
                var jobType = Type.GetType(jobConfiguration.Type);
                var jobKey = new JobKey(jobConfiguration.Name);
                var queueName = jobConfiguration.QueueName;
                scheduler.AddRecurringJob(jobType, jobKey, jobConfiguration.SchedulingTime);
                Task.Factory.StartNew(() =>{ ListenQueue(queueName, _jobActivator, _logger, _inWorkMessages, _cancellationTokenSource.Token);})
                            .ContinueWith(t => t.Exception.Handle(StopAsService), TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        
        private bool StopAsService(Exception ex)
        {
            if (!Monitor.TryEnter(_locker))
            {
                _logger.Info("Skip stopping");
                return true;
            }
            try
            {
                ExitCode = GetExitCodeFromException(ex);
                _logger.Info(string.Format("Stop with ExitCode {0}", ExitCode));
                Stop();
                return true;
            }
            finally
            {
                Monitor.Exit(_locker);
            }
        }

        protected override void OnStop()
        {
            _logger.Info("Stopping as service...");
            StopImpl();
            base.OnStop();
            //Даем возможность таскам завершится по CancellationToken
            RequestAdditionalTime(20000);
            Thread.Sleep(10000);
        }

        public void StopImpl()
        {
            try
            {
                _logger.Trace("Stopping scheduler...");
                _scheduler.Do(s => s.Shutdown(false));
                _scheduler = null;
                if (_serviceHost != null)
                    _serviceHost.Close();
                _serviceHost = null;
                _inWorkMessages.CompleteAdding();
                _cancellationTokenSource.Cancel();
                //возвращаем все не обработанные сообщения в очередь.
                foreach (var inWorkMessage in _inWorkMessages)
                {
                    ReturnMessageToQueue(inWorkMessage, null, CancellationToken.None);
                }
                _logger.Info("Scheduler stopped");
            }
            catch (Exception e)
            {
                _logger.Info("Exception during stopping");
                _logger.Fatal(e, e.Message);
            }
        }


        private void OnException(JobExecutionException exception)
        {
            _logger.Fatal("Unhandled error while running a job");
            _logger.Fatal(exception.ToString);
            ExitCode = 1;
            StopImpl();
        }

        private static int GetExitCodeFromException(Exception exception)
        {
            return exception.HResult != 0 ? exception.HResult : -1;
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

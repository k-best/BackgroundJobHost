using System;
using System.Linq.Expressions;
using System.Messaging;
using BackgroundJob.Core.Serialization;
using BackgroundJob.Host;

namespace BackgroundJob.Core.Helpers
{
    public static class BackgroundJob
    {
        public static void Enqueue<T>(string queueName, Expression<Action<T>> methodCall, string jobLabel, int? maxRetryCount)
        {
            if (!MessageQueue.Exists(queueName))
            {
                throw new InvalidOperationException(string.Format("Очередь {0} отсутствует.", queueName));
            }
            var mq = new MessageQueue(queueName);
            using (var messageQueueTransaction = new MessageQueueTransaction())
            {
                messageQueueTransaction.Begin();
                var message = new Message(new MessageWrapper(maxRetryCount)
                {
                    SerializedJob =
                        JobHelper.ToJson(SerializedJob.Serialize(BackgroundJobDetail.FromExpression(methodCall)))
                })
                {
                    Label = jobLabel,
                };
                mq.Send(message, messageQueueTransaction);
                messageQueueTransaction.Commit();
            }
        }

        public static void Enqueue(string queueName, Expression<Action> methodCall, string jobLabel, int? maxRetryCount)
        {
            if (!MessageQueue.Exists(queueName))
            {
                throw new InvalidOperationException(string.Format("Очередь {0} отсутствует.", queueName));
            }
            var mq = new MessageQueue(queueName);
            using (var messageQueueTransaction = new MessageQueueTransaction())
            {
                messageQueueTransaction.Begin();
                var message = new Message(new MessageWrapper(maxRetryCount){SerializedJob = 
                    JobHelper.ToJson(SerializedJob.Serialize(BackgroundJobDetail.FromExpression(methodCall)))})
                {
                    Label = jobLabel,
                };
                mq.Send(message, messageQueueTransaction);
                messageQueueTransaction.Commit();
            }
        }
    }

    public class MessageWrapper
    {
        private int? _maxRetryCount;

        public MessageWrapper()
        {
            
        }

        public MessageWrapper(int? maxRetryCount)
        {
            _maxRetryCount = maxRetryCount;
        }

        public int MaxRetryCount
        {
            get { return _maxRetryCount??0; }
            set { _maxRetryCount = value; }
        }

        public int RetryCount { get; set; }
        public string SerializedJob { get; set; }
    }
}
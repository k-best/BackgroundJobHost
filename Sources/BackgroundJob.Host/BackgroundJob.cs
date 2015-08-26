using System;
using System.Linq.Expressions;
using System.Messaging;

namespace BackgroundJob.Host
{
    public static class BackgroundJob
    {
        public static void Enqueue<T>(string queueName, Expression<Action<T>> methodCall, string jobLabel)
        {
            if (!MessageQueue.Exists(queueName))
            {
                throw new InvalidOperationException(string.Format("Очередь {0} отсутствует.", queueName));
            }
            var mq = new MessageQueue(queueName);
            using (var messageQueueTransaction = new MessageQueueTransaction())
            {
                messageQueueTransaction.Begin();
                var message = new Message(
                    JobHelper.ToJson(SerializedJob.Serialize(BackgroundJobDetail.FromExpression(methodCall))))
                {
                    Label = jobLabel,
                };
                mq.Send(message, messageQueueTransaction);
                messageQueueTransaction.Commit();
            }
        }

        public static void Enqueue(string queueName, Expression<Action> methodCall, string jobLabel)
        {
            if (!MessageQueue.Exists(queueName))
            {
                throw new InvalidOperationException(string.Format("Очередь {0} отсутствует.", queueName));
            }
            var mq = new MessageQueue(queueName);
            using (var messageQueueTransaction = new MessageQueueTransaction())
            {
                messageQueueTransaction.Begin();
                var message = new Message(
                    JobHelper.ToJson(SerializedJob.Serialize(BackgroundJobDetail.FromExpression(methodCall))))
                {
                    Label = jobLabel,
                };
                mq.Send(message, messageQueueTransaction);
                messageQueueTransaction.Commit();
            }
        }
    }
}
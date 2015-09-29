using System;
using System.Messaging;
using System.Threading;
using BackgroundJob.Core.Helpers;

namespace BackgroundJob.Host
{
    internal class InWorkMessage
    {
        private readonly object _locker=new object();
        public MessageWrapper Job { get; set; }
        public string Label { get; set; }
        public string QueueName { get; set; }
        private bool _isWorkCompletedOrMessageReturned;

        public void ReturnMessage()
        {
            if (!Monitor.TryEnter(_locker))
            {
                return;
            }
            if(_isWorkCompletedOrMessageReturned)
                return;
            try
            {
                if (!MessageQueue.Exists(QueueName))
                {
                    throw new InvalidOperationException(string.Format("Очередь {0} отсутствует.", QueueName));
                }
                var mq = new MessageQueue(QueueName);
                using (var messageQueueTransaction = new MessageQueueTransaction())
                {
                    messageQueueTransaction.Begin();
                    if (!_isWorkCompletedOrMessageReturned)
                    {
                        Job.RetryCount++;
                        if(Job.RetryCount>Job.MaxRetryCount)
                            return;
                        var message = new Message(Job)
                        {
                            Label = Label,
                        };
                        mq.Send(message, messageQueueTransaction);
                        _isWorkCompletedOrMessageReturned = true;
                    }
                    messageQueueTransaction.Commit();
                }
            }
            finally
            {
                Monitor.Exit(_locker);
            }
        }

        public void CompleteMessage()
        {
            if (!Monitor.TryEnter(_locker))
            {
                return;
            }
            if (_isWorkCompletedOrMessageReturned)
                return;
            try
            {
                _isWorkCompletedOrMessageReturned = true;
            }
            finally
            {
                Monitor.Exit(_locker);
            }
        }
    }
}
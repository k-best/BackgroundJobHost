using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace BackgroundJob.Host
{
    public class ConcurrentHashSet<T>:IEnumerable<T>
    {
        private ConcurrentDictionary<T, byte> _innerSet = new ConcurrentDictionary<T,byte>();

        private bool _isCompleted;

        public bool TryAdd(T element)
        {
            if (_isCompleted)
                return false;
            return _innerSet.TryAdd(element, 0);
        }

        public bool TryRemove(T element)
        {
            byte value;
            return _innerSet.TryRemove(element, out value);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _innerSet.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void CompleteAdding()
        {
            _isCompleted = true;
        }
    }
}
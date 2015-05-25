using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace pitermarx.Utils
{
    public class BlockingQueue<T>
    {
        // the collection of items and the action to be performed on each one
        private readonly BlockingCollection<T> collection = new BlockingCollection<T>();
        private readonly Action<T> action;

        // helpers for stopping mechanism
        private CancellationTokenSource cancelation;
        private AutoResetEvent waiter;

        /// <summary> An error method to be called if action throws </summary>
        public Action<Exception, T> OnError { private get; set; }

        /// <summary>
        /// Instantiate with the action to run.
        /// Wrap the action in a catch.
        /// </summary>
        public BlockingQueue(Action<T> action)
        {
            this.action = action;
        }

        private void DoAction(T item)
        {
            try
            {
                action(item);
            }
            catch (Exception e)
            {
                if (OnError != null)
                {
                    OnError(e, item);
                }
            }
        }

        /// <summary>
        /// Always allow to add items.
        /// The queue may be stopped when adding.
        /// </summary>
        public void Enqueue(params T[] items)
        {
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }

        public void Start()
        {
            if (cancelation != null)
            {
                // already started
                return;
            }

            // create waiter and cancelation
            this.cancelation = new CancellationTokenSource();
            this.waiter = new AutoResetEvent(false);

            // create a long running task
            new Task(() =>
            {
                try
                {
                    foreach (var item in collection.GetConsumingEnumerable(cancelation.Token))
                    {
                        DoAction(item);
                    }
                }
                catch (OperationCanceledException)
                {
                }

                // on exit, set the waiter
                waiter.Set();
            }, TaskCreationOptions.LongRunning).Start();
        }

        public T[] Stop(bool waitForCompletion = false)
        {
            // cancel the consuing enumerable
            cancelation.Cancel();
            // wait for exit
            waiter.WaitOne();

            // cleanup
            cancelation = null;
            waiter = null;

            // if waits for all items to be completed, do it here
            if (waitForCompletion)
            {
                foreach (var item in collection.ToArray())
                {
                    DoAction(item);
                }

                return new T[0];
            }

            // return remaining items
            return collection.ToArray();
        }
    }
}

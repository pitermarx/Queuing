using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;

namespace pitermarx.Utils
{
    [TestFixture]
    public class BlockingQueueTests
    {
        private List<int> processed;
        private int exceptionCount;

        public BlockingQueue<int> Prepare(int wait, int count, bool throwing = false)
        {
            exceptionCount = 0;
            processed = new List<int>();
            var q = new BlockingQueue<int>(id =>
            {
                var w = Stopwatch.StartNew();
                processed.Add(id);
                if (throwing)
                {
                    throw new Exception("test");
                }
                while (true)
                {
                    if (w.ElapsedMilliseconds >= wait)
                    {
                        break;
                    }
                }
            });
            Enumerable.Range(0, count).ToList().ForEach(i => q.Enqueue(i));
            q.OnError = (e, i) => exceptionCount++;
            q.Start();
            return q;
        }

        [Test]
        public void Test1_AverageTimePerItem()
        {
            var q = Prepare(5, 1000);

            var w = Stopwatch.StartNew();
            while (w.ElapsedMilliseconds <= 500) {}

            var remaining = q.Stop();
            Assert.AreEqual(remaining.Count() + processed.Count(), 1000);
            Assert.GreaterOrEqual(remaining.Count(), 800);
            Assert.LessOrEqual(processed.Count(), 200);
            Assert.Greater(processed.Count(), 85);
        }

        [Test]
        public void Test2_WaitForCompletion()
        {
            var remaining = Prepare(1, 300).Stop(true);
            Assert.AreEqual(remaining.Count() + processed.Count(), 300);
            Assert.AreEqual(remaining.Count(), 0);
            Assert.AreEqual(processed.Count(), 300);
        }

        [Test]
        public void Test3_WaitForCompletion_ExceptionsSaved()
        {
            var remaining = Prepare(1, 300, true).Stop(true);
            Assert.AreEqual(remaining.Count() + processed.Count(), 300);
            Assert.AreEqual(remaining.Count(), 0);
            Assert.AreEqual(processed.Count(), 300);
            Assert.AreEqual(exceptionCount, 300);
        }
    }
}

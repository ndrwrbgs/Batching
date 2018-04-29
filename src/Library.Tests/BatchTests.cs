using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Library.Tests
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Batch;

    [TestClass]
    public class BatchTests
    {
        [TestMethod]
        public void CountBatch()
        {
            var batcher = BatchExecutorFactory.Create(
                new BatchPolicy(
                    maxAge: TimeSpan.FromDays(1),
                    maxBatchSize: 3),
                (IEnumerable<int> values) => Task.FromResult((IList<int>)values.ToList()),
                OutputToInputMatchFunction.OutputOrderMatchesInputOrder);

            var task1 = batcher.ExecuteAsync(1);
            var task2 = batcher.ExecuteAsync(2);

            task1.Wait(TimeSpan.FromSeconds(1));

            Assert.IsFalse(task1.IsCompleted);
            Assert.IsFalse(task2.IsCompleted);

            var task3 = batcher.ExecuteAsync(3);

            task3.Wait(TimeSpan.FromSeconds(2));

            Assert.IsTrue(task1.IsCompleted);
            Assert.IsTrue(task2.IsCompleted);
            Assert.IsTrue(task3.IsCompleted);

            Assert.AreEqual(1, task1.Result);
            Assert.AreEqual(2, task2.Result);
            Assert.AreEqual(3, task3.Result);
        }

        [TestMethod]
        public void DisposeCancels()
        {
            var batcher = BatchExecutorFactory.Create(
                new BatchPolicy(
                    maxAge: TimeSpan.FromSeconds(1),
                    maxBatchSize: int.MaxValue),
                (IEnumerable<int> values) => Task.FromResult((IList<int>)values.ToList()),
                OutputToInputMatchFunction.OutputOrderMatchesInputOrder);

            var task1 = batcher.ExecuteAsync(1);
            var task2 = batcher.ExecuteAsync(2);

            Assert.IsFalse(task1.IsCompleted);
            Assert.IsFalse(task2.IsCompleted);

            batcher.Dispose();

            Assert.IsFalse(task1.IsCanceled);
            Assert.IsFalse(task2.IsCanceled);
        }

        [TestMethod]
        public async Task TimeBatch()
        {
            var batcher = BatchExecutorFactory.Create(
                new BatchPolicy(
                    maxAge: TimeSpan.FromSeconds(1),
                    maxBatchSize: int.MaxValue),
                (IEnumerable<int> values) => Task.FromResult((IList<int>)values.ToList()),
                OutputToInputMatchFunction.OutputOrderMatchesInputOrder);

            var task1 = batcher.ExecuteAsync(1);
            var task2 = batcher.ExecuteAsync(2);

            await Task.Delay(100);

            Assert.IsFalse(task1.IsCompleted);
            Assert.IsFalse(task1.IsCompleted);

            task1.Wait(TimeSpan.FromSeconds(1));

            Assert.IsTrue(task1.IsCompleted);
            Assert.IsTrue(task2.IsCompleted);

            Assert.AreEqual(1, task1.Result);
            Assert.AreEqual(2, task2.Result);
        }

        [TestMethod]
        public async Task SingleItemTest()
        {
            int singleCalls = 0;
            int batchCalls = 0;
            var batcher = BatchExecutorFactory.Create(
                new BatchPolicy(
                    maxAge: TimeSpan.FromDays(1),
                    maxBatchSize: 1),
                (int value) =>
                {
                    singleCalls++;
                    return Task.FromResult(value);
                },
                (IEnumerable<int> values) =>
                {
                    batchCalls++;
                    return Task.FromResult((IList<int>)values.ToList());
                },
                OutputToInputMatchFunction.OutputOrderMatchesInputOrder);

            var task1 = batcher.ExecuteAsync(1);

            task1.Wait(TimeSpan.FromMilliseconds(100));

            Assert.IsTrue(task1.IsCompleted);

            Assert.AreEqual(1, task1.Result);

            Assert.AreEqual(1, singleCalls);
            Assert.AreEqual(0, batchCalls);
        }

        [TestMethod]
        public async Task MultiItemTest()
        {
            int singleCalls = 0;
            int batchCalls = 0;
            var batcher = BatchExecutorFactory.Create(
                new BatchPolicy(
                    maxAge: TimeSpan.FromDays(1),
                    maxBatchSize: 2),
                (int value) =>
                {
                    singleCalls++;
                    return Task.FromResult(value);
                },
                (IEnumerable<int> values) =>
                {
                    batchCalls++;
                    return Task.FromResult((IList<int>)values.ToList());
                },
                OutputToInputMatchFunction.OutputOrderMatchesInputOrder);

            var task1 = batcher.ExecuteAsync(1);
            var task2 = batcher.ExecuteAsync(2);

            task1.Wait(TimeSpan.FromMilliseconds(100));

            Assert.IsTrue(task1.IsCompleted);
            Assert.IsTrue(task2.IsCompleted);

            Assert.AreEqual(1, task1.Result);
            Assert.AreEqual(2, task2.Result);

            Assert.AreEqual(0, singleCalls);
            Assert.AreEqual(1, batchCalls);
        }
    }
}

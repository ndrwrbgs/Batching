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
    public class OutputToInputMatchTests
    {
        [TestMethod]
        public async Task MatchOutputsByResult()
        {
            var batcher = BatchExecutorFactory.Create(
                new BatchPolicy(
                    maxAge: TimeSpan.FromDays(1),
                    maxBatchSize: 3),
                async (IEnumerable<int> values) =>
                {
                    var result = values.Reverse().Select(i => Tuple.Create(i, i.ToString())).ToList();
                    return (IList<Tuple<int, string>>)result;
                },
                OutputToInputMatchFunction.CreateByMatchingPartOfResult(
                    (int resultIndex, Tuple<int, string> resultValue) => resultValue.Item1));

            var task1 = batcher.ExecuteAsync(1);
            var task2 = batcher.ExecuteAsync(2);
            var task3 = batcher.ExecuteAsync(3);

            task1.Wait(TimeSpan.FromMilliseconds(100));

            Assert.AreEqual("1", task1.Result.Item2);
            Assert.AreEqual("2", task2.Result.Item2);
            Assert.AreEqual("3", task3.Result.Item2);
        }

        [TestMethod]
        public async Task MatchOutputsByInputAndResult()
        {
            var batcher = BatchExecutorFactory.Create(
                new BatchPolicy(
                    maxAge: TimeSpan.FromDays(1),
                    maxBatchSize: 3),
                async (IEnumerable<double> values) =>
                {
                    var result = values.Reverse().Select(i => Tuple.Create((int)i, i.ToString())).ToList();
                    return (IList<Tuple<int, string>>)result;
                },
                OutputToInputMatchFunction.CreateByMatchingPartOfInputAndResult(
                    (double input) => (int)input,
                    (int resultIndex, Tuple<int, string> resultValue) => resultValue.Item1));

            var task1 = batcher.ExecuteAsync(1.0);
            var task2 = batcher.ExecuteAsync(2.0);
            var task3 = batcher.ExecuteAsync(3.0);

            task1.Wait(TimeSpan.FromMilliseconds(100));

            Assert.AreEqual("1", task1.Result.Item2);
            Assert.AreEqual("2", task2.Result.Item2);
            Assert.AreEqual("3", task3.Result.Item2);
        }
    }
}

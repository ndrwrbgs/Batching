namespace Batch
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    public static class BatchExecutorFactory
    {
        [NotNull]
        public static IAsyncExecutor<TInput, TOutput> Create<TInput, TOutput>(
            [NotNull] BatchPolicy batchPolicy,
            Func<TInput, Task<TOutput>> singleItemMethod,
            Func<IEnumerable<TInput>, Task<IList<TOutput>>> batchedItemsMethod,
            OutputToInputMatchFunction.Delegate<TInput, TOutput> ouputToInputMatchFunction)
        {
            return new BatchExecutor<TInput, TOutput>(
                batchPolicy,
                singleItemMethod,
                batchedItemsMethod,
                ouputToInputMatchFunction);
        }

        [NotNull]
        public static IAsyncExecutor<TInput, TOutput> Create<TInput, TOutput>(
            [NotNull] BatchPolicy batchPolicy,
            Func<IEnumerable<TInput>, Task<IList<TOutput>>> batchedItemsMethod,
            OutputToInputMatchFunction.Delegate<TInput, TOutput> ouputToInputMatchFunction)
        {
            return new BatchExecutor<TInput, TOutput>(
                batchPolicy,
                batchedItemsMethod,
                ouputToInputMatchFunction);
        }
    }
}
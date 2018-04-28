namespace Batch
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using JetBrains.Annotations;

    public static class OutputToInputMatchFunction
    {
        public delegate TInput Delegate<TInput, TOutput>(IList<TInput> batchedInputs, int resultIndex, TOutput resultValue);

        public static TInput OutputOrderMatchesInputOrder<TInput, TOutput>([NotNull] IList<TInput> batchedInputs, int resultIndex, TOutput resultValue)
        {
            return batchedInputs[resultIndex];
        }

        [NotNull]
        public static Delegate<TInput, TOutput> CreateByMatchingPartOfInputAndResult<TInput, TOutput, TMatch>(
            // TODO: Func isn't as descriptive as a delegate
            Func<TInput, TMatch> projectInputs,
            Func<int, TOutput, TMatch> projectOutputs,
            // null = Default
            IEqualityComparer<TMatch> matchEqualityComparer = null)
        {
            matchEqualityComparer = matchEqualityComparer ?? EqualityComparer<TMatch>.Default;

            return (IList<TInput> batchedInputs, int resultIndex, TOutput resultValue) 
                =>
            {
                // TODO: Creating for each result :(
                // Duplicate will throw, as it's improper usage and we cannot match (TODO: throw descriptive exception)
                Dictionary<TMatch, TInput> inputDictionary = batchedInputs.ToDictionary(projectInputs, matchEqualityComparer);
                TMatch seekingValue = projectOutputs(resultIndex, resultValue);

                // Will throw if not found TODO: throw more descriptive exception
                TInput matchingInput = inputDictionary[seekingValue];
                return matchingInput;
            };
        }

        [NotNull]
        public static Delegate<TInput, TOutput> CreateByMatchingPartOfResult<TInput, TOutput>(
            Func<int, TOutput, TInput> projectOutputs,
            // null = Default
            [CanBeNull] IEqualityComparer<TInput> matchEqualityComparer = null)
        {
            return CreateByMatchingPartOfInputAndResult<TInput, TOutput, TInput>(
                input => input,
                projectOutputs,
                matchEqualityComparer);
        }
    }
}
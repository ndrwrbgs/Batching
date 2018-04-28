namespace Batch
{
    using System;

    public sealed class BatchPolicy
    {
        public BatchPolicy(TimeSpan maxAge, int maxBatchSize)
        {
            this.MaxAge = maxAge;
            this.MaxBatchSize = maxBatchSize;
        }

        public TimeSpan MaxAge { get; set; }

        public int MaxBatchSize { get; set; }
    }
}
namespace Batch
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    ///     Dispose cancels all pending work
    /// </summary>
    public interface IAsyncExecutor<TInput, TOutput> : IDisposable
    {
        Task<TOutput> ExecuteAsync(TInput input);
    }
}
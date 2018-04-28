namespace Batch
{
    using System.Threading.Tasks;

    public interface IAsyncExecutor<TInput, TOutput>
    {
        Task<TOutput> ExecuteAsync(TInput input);
    }
}
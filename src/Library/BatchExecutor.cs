namespace Batch
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    internal sealed class BatchExecutor<TInput, TOutput> : IAsyncExecutor<TInput, TOutput>
    {
        private readonly Func<IEnumerable<TInput>, Task<IList<TOutput>>> _batchedItemsMethod;
        private readonly BatchPolicy _batchPolicy;
        private readonly OutputToInputMatchFunction.Delegate<TInput, TOutput> _ouputToInputMatchFunction;
        private readonly Func<TInput, Task<TOutput>> _singleItemMethod;
        private readonly CancellationTokenSource cts;

        private readonly ISubject<UnitOfWork> incomingWork;

        private readonly IDisposable subscription;

        public BatchExecutor(
            [NotNull] BatchPolicy batchPolicy,
            Func<TInput, Task<TOutput>> singleItemMethod,
            // IList b/c OutputMatchFunction.OutputOrderMatchesInputOrder presumes we got it in order, and want the data type to specify that rather than implicit assumption
            Func<IEnumerable<TInput>, Task<IList<TOutput>>> batchedItemsMethod,
            OutputToInputMatchFunction.Delegate<TInput, TOutput> ouputToInputMatchFunction)
        {
            this._batchPolicy = batchPolicy;
            this._singleItemMethod = singleItemMethod;
            this._batchedItemsMethod = batchedItemsMethod;
            this._ouputToInputMatchFunction = ouputToInputMatchFunction;

            this.cts = new CancellationTokenSource();

            this.incomingWork = new Subject<UnitOfWork>();
            this.subscription = this.incomingWork
                .Buffer(batchPolicy.MaxAge, batchPolicy.MaxBatchSize)
                .Subscribe(this.ProcessBatch);
        }

        public BatchExecutor(
            [NotNull] BatchPolicy batchPolicy,
            Func<IEnumerable<TInput>, Task<IList<TOutput>>> batchedItemsMethod,
            OutputToInputMatchFunction.Delegate<TInput, TOutput> ouputToInputMatchFunction)
            : this(
                batchPolicy,
                null,
                batchedItemsMethod,
                ouputToInputMatchFunction)
        {
        }

        public Task<TOutput> ExecuteAsync(TInput input)
        {
            var unitOfWork = new UnitOfWork(input, new TaskCompletionSource<TOutput>());
            this.incomingWork.OnNext(unitOfWork);

            return unitOfWork.TaskCompletionSource.Task;
        }

        public void Dispose()
        {
            this.cts.Cancel();
            this.incomingWork.OnCompleted();
            this.subscription.Dispose();
        }

        private void ProcessBatch([NotNull] IList<UnitOfWork> batch)
        {
            if (this._singleItemMethod != null && batch.Count == 1)
            {
                UnitOfWork item = batch.Single();

                TInput itemInput = item.Input;
                TaskCompletionSource<TOutput> tcs = item.TaskCompletionSource;

                // Task.Run b/c it's possible that the Func<Task> throws on run rather than on await if they aren't async
                Task<TOutput> task = Task.Run(
                    // TODO: Take cancellationToken? not of super importance since BatchExecutor usage advice is to live for the whole process
                    () => this._singleItemMethod(itemInput),
                    this.cts.Token);

                ConnectTaskToTaskCompletionSource(task, tcs, this.cts.Token);
            }
            else
            {
                // A batch or single-item is disabled (so handled as a batch)

                List<TInput> allInputs = batch
                    .Select(item => item.Input)
                    .ToList();

                // Task.Run b/c it's possible that the Func<Task> throws on run rather than on await if they aren't async
                Task<IList<TOutput>> task = Task.Run(
                    () => this._batchedItemsMethod(allInputs),
                    this.cts.Token);

                // Orphaned, observed on tcs's
                Task orphan = task.ContinueWith(
                    t =>
                    {
                        switch (t.Status)
                        {
                            case TaskStatus.Canceled:
                                foreach (TaskCompletionSource<TOutput> tcs in batch.Select(item => item.TaskCompletionSource))
                                {
                                    tcs.SetCanceled();
                                }

                                break;
                            case TaskStatus.RanToCompletion:
                                IList<TOutput> results = t.Result;

                                Dictionary<TInput, TaskCompletionSource<TOutput>> inputToTcsDictionary = batch
                                    .ToDictionary(
                                        item => item.Input,
                                        item => item.TaskCompletionSource /* TODO: contract of _outputToInputMatchFunction -- use ReferenceEquals dictionary */);

                                Dictionary<TInput, TaskCompletionSource<TOutput>> unhandledItems = batch
                                    .ToDictionary(
                                        item => item.Input,
                                        item => item.TaskCompletionSource /* TODO: contract of _outputToInputMatchFunction -- use ReferenceEquals dictionary */);
                                try
                                {
                                    for (var index = 0; index < results.Count; index++)
                                    {
                                        TOutput result = results[index];
                                        TInput resultIsForInput = this._ouputToInputMatchFunction(allInputs, index, result);

                                        if (inputToTcsDictionary.ContainsKey(resultIsForInput))
                                        {
                                            TaskCompletionSource<TOutput> tcs = inputToTcsDictionary[resultIsForInput];

                                            tcs.SetResult(result);

                                            // Remove so the exception logic doesn't set exception on this item
                                            unhandledItems.Remove(resultIsForInput);
                                        }
                                    }

                                    // Some outputs did not have matches in the inputs
                                    if (unhandledItems.Any())
                                    {
                                        foreach (TaskCompletionSource<TOutput> tcs in unhandledItems.Values)
                                        {
                                            tcs.SetException(
                                                new InvalidOperationException(
                                                    "A result was processed for the batched inputs, but no result item was matched to this input item."));
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    foreach (TaskCompletionSource<TOutput> tcs in unhandledItems.Values)
                                    {
                                        tcs.SetException(
                                            new InvalidOperationException(
                                                "OuputToInputMatchFunction threw, meaning we cannot properly match the outputs back to the inputs!",
                                                e));
                                    }
                                }

                                break;
                            case TaskStatus.Faulted:
                                foreach (TaskCompletionSource<TOutput> tcs in batch.Select(item => item.TaskCompletionSource))
                                {
                                    tcs.SetException(task.Exception);
                                }

                                break;
                            default:
                                // Pipe the exception back to somewhere we will observe it
                                foreach (TaskCompletionSource<TOutput> tcs in batch.Select(item => item.TaskCompletionSource))
                                {
                                    tcs.SetException(new InvalidOperationException("Invalid continuation call."));
                                }

                                break;
                        }
                    },
                    this.cts.Token);
            }
        }

        private static void ConnectTaskToTaskCompletionSource<TResult>([NotNull] Task<TResult> task, TaskCompletionSource<TResult> tcs, CancellationToken ctx)
        {
            // Orphan the continuationTask
            Task orphaned = task.ContinueWith(
                t =>
                {
                    switch (t.Status)
                    {
                        case TaskStatus.Canceled:
                            tcs.SetCanceled();
                            break;
                        case TaskStatus.RanToCompletion:
                            tcs.SetResult(t.Result);
                            break;
                        case TaskStatus.Faulted:
                            tcs.SetException(task.Exception);
                            break;
                        default:
                            // Pipe the exception back to somewhere we will observe it
                            tcs.SetException(new InvalidOperationException("Invalid continuation call."));
                            break;
                    }
                },
                ctx);
        }

        private sealed class UnitOfWork
        {
            public UnitOfWork(TInput input, TaskCompletionSource<TOutput> taskCompletionSource)
            {
                this.Input = input;
                this.TaskCompletionSource = taskCompletionSource;
            }

            public TInput Input { get; }

            public TaskCompletionSource<TOutput> TaskCompletionSource { get; }
        }
    }
}
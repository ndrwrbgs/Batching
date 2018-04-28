# Batching
This library enables you to expose a single-request-API style but underneath it perform batch requests. This is useful if, for example, your service exposes both a single-request and a batch-request endpoint, and you offer a client. In said client, you would allow users to request a single resource, but if they were to do so in quick succession you probably today throttle them. Instead, you can utilize the library to batch those requests together into a single batch request which is friendly to your servers.

## Usage example
```C#
public static async Task<string> GetStringByIndex(int id)
{
    Task<string> getStringTask = getStringByIndex_Batcher
        .ExecuteAsync(id);
    return await getStringTask;
}

// Static if you want all application requests to be able to be batched together.
private static IAsyncExecutor<int, string> getStringByIndex_Batcher = BatchExecutorFactory.Create(
    // Specify how this batcher will batch items together
    new BatchPolicy(
        // The maximum time an item is allowed to wait for more requests to be batched with
        // (e.g. the max time between the request and start of processing the batch that contains that request)
        maxAge: TimeSpan.FromSeconds(5),
        // The maximum number of items to include in a single batch
        maxBatchSize: 100),
    // The batch function. There is an overload that accepts a separate function in the case that the batch contains only one item, but you should probably only
    // add that complexity if your service handles single item requests much better than a batch with a single item
    GetStringByIndex_Batched,
    // Since batch results may not match the order requested (e.g. they may come back sorted by some feature), you must specify how we match each result item from
    // 'GetImageByIndex_Batched' to the original input 'int'
    // OutputToInputMatchFunction.CreateByMatchingPartOfResult
    OutputToInputMatchFunction.CreateByMatchingPartOfResult<int, string>(
        (index, result) => { /* TODO: extract the index from the result string for each output */ }));

private static async Task<IList<string>> GetStringByIndex_Batched(IEnumerable<int> imageIds)
{
    // TODO: Request each string from the indexes. E.g. http://www.example.com/?ids=1,2,3,4 w/ output like { 1: "hi", 2: "bye", 3: "test" }
}
```

## To Implement Yourself
1) How to map the result items back to the input items
  e.g. if each item is JSON and has the input key, or if it's an HttpRequest that has the RequestUri and Body
2) How to request batches of items
  e.g. take IEnumerable<int> and make a web request to something like http://www.example.com/?ids=1,2,3,4

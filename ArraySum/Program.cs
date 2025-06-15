using System.Diagnostics;
using ArraySum.SumStrategies;


//const long ExpectedSum = 500_490_436_924;

var filePath = Path.Combine(Directory.GetCurrentDirectory(), "../../../numbers.data");

var strategies = new SortStrategy[]
{
    // new SimpleBufferReader(filePath),
    new ThreadPoolStackBuffer(filePath, 100_000_000, Environment.ProcessorCount),
    new ThreadPoolArrayPoolBufferAsyncWorker(filePath, 100_000_000, Environment.ProcessorCount),    
    new ThreadPoolArrayPoolBufferSyncWorker(filePath, 100_000_000, Environment.ProcessorCount)
};


var cancellationTokenSource = new CancellationTokenSource();

foreach (var sortStrategy in strategies)
{
    var stopwatch = Stopwatch.StartNew();
    var sum = await sortStrategy.Run(cancellationTokenSource.Token);
    stopwatch.Stop();
    
    Console.WriteLine($"[{sortStrategy.Description}] Сумма чисел: {sum} ({stopwatch.Elapsed.TotalMilliseconds} мс)");
    Console.WriteLine();
}
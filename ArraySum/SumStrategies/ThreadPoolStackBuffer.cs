using System.Runtime.InteropServices;

namespace ArraySum.SumStrategies;

public class ThreadPoolStackBuffer : SumStrategy
{
    private const int AvailableStackSize = 1_048_576 / 2; // 50% от размера стека процесса можно потратить на буферы
    
    private readonly int _readerStackBufferSize;
    
    private long _arrayPosition;

    public ThreadPoolStackBuffer(string fileName, int chunkSize, int numberOfWorkers)  : base(fileName, chunkSize, numberOfWorkers)
    { 
        _readerStackBufferSize = (AvailableStackSize / NumberOfWorkers / ElementSize) * ElementSize;
    }

    public override string Description => "Стратегия c буфером на стеке";

    public override async Task<long> Run(CancellationToken token = default)
    {
        // Сбрасываем позицию перед каждым запуском
        _arrayPosition = 0;
        
        var workersTotalCount = (ArrayLength + ChunkSize - 1) / ChunkSize;
        var tasks = new Task<long>[workersTotalCount];

        var semaphore = new SemaphoreSlim(NumberOfWorkers, NumberOfWorkers);
        
        for (var i = 0; i < workersTotalCount; i++)
        {
            tasks[i] = Task.Run(
                async () =>
                {
                    try
                    {
                        await semaphore.WaitAsync(token);
                        return RunWorker(token);
                    }
                    finally
                    {
                      semaphore.Release();  
                    }
                }
                , token);
        }
        
        var sums = await Task.WhenAll(tasks);

        return sums.Sum(); 
    }

    private long RunWorker(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var startArray = Interlocked.Add(ref _arrayPosition, ChunkSize) - ChunkSize;
        if(startArray >= ArrayLength)
        {
            return 0L;
        }
        
        var count = Math.Min(ChunkSize, ArrayLength - startArray);
        var begin = startArray * ElementSize;
        var end = begin + count * ElementSize;
        
        Span<byte> buffer = stackalloc byte[_readerStackBufferSize];
        
        using var reader = new BinaryReader(File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.Read));
        reader.BaseStream.Position = begin;

        var sum = 0L;
        
        token.ThrowIfCancellationRequested();
        var bytesRead = buffer.Length;
        
        while (begin < end && bytesRead == buffer.Length && !token.IsCancellationRequested)
        {
            bytesRead = reader.BaseStream.Read(buffer);
            if (bytesRead % ElementSize != 0)
            {
                throw new InvalidDataException($"Кол-во прочитанных байт [{bytesRead}] не кратно размеру элемента - {ElementSize} байт");
            }
            var remaining = (int) (end - begin);
            var validBytes = Math.Min(bytesRead, remaining);
            var subArray = MemoryMarshal.Cast<byte, int>(buffer[..validBytes]);

            foreach (var t in subArray)
            {
                sum += t;
            }
            
            begin += bytesRead;
        } 

        return sum;
    }
}
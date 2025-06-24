using System.Buffers;
using System.Runtime.InteropServices;

namespace ArraySum.SumStrategies;

public class ThreadPoolArrayPoolBufferSyncWorker : SumStrategy
{
    private const int ArrayPoolBufferSize = 512 * 1024;

    private long _arrayPosition;

    public ThreadPoolArrayPoolBufferSyncWorker(string fileName, int chunkSize, int numberOfWorkers) : base(fileName, chunkSize, numberOfWorkers)
    {

    }

    public override string Description => "Стратегия c буфером в ArrayPool и CИНХРОННЫМИ workerами";
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

        var buffer = ArrayPool<byte>.Shared.Rent(ArrayPoolBufferSize);
        try
        {
            token.ThrowIfCancellationRequested();

            using var fileStream = File.OpenRead(FileName);
            fileStream.Position = begin;

            var sum = 0L;
        
            while ((begin < end) && !token.IsCancellationRequested)
            {
                var bytesToRead = (int) Math.Min(buffer.Length, end - begin);
                
                var bytesRead = fileStream.Read(buffer, 0, bytesToRead);

                if (bytesRead == 0)
                {
                    break;
                }

                if (bytesRead % ElementSize != 0)
                {
                    throw new InvalidDataException($"Кол-во прочитанных байт [{bytesRead}] не кратно размеру элемента - {ElementSize} байт");
                }
                
                var subArray = MemoryMarshal.Cast<byte, int>(buffer.AsSpan(0, bytesRead));

                foreach (var t in subArray)
                {
                    sum += t;
                }
            
                begin += bytesRead;
            } 

            return sum;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
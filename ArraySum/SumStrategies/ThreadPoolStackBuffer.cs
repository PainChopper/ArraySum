using System.Runtime.InteropServices;

namespace ArraySum.SumStrategies;

public class ThreadPoolStackBuffer : ISortStrategy
{
    private const int ElementSize = sizeof(int);
    private const int AvailableStackSize = 1_048_576 / 2; // 50% от размера стека процесса можно потратить на буферы
    
    private readonly string _fileName;
    private readonly int _chunkSize;
    private readonly int _numberOfWorkers;
    private readonly long _arrayLength;
    private readonly int _readerStackBufferSize;
    
    private long _arrayPosition;

    public ThreadPoolStackBuffer(string fileName, int chunkSize, int numberOfWorkers)
    { 
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numberOfWorkers);

        _fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException("Файл не найден", fileName);
        }
        var fileSize = new FileInfo(fileName).Length;
        if (fileSize % ElementSize != 0)
        {
            throw new ArgumentException("Размер файла не кратен размеру элемента", nameof(fileName));
        }

        _arrayLength = fileSize / ElementSize;
        _chunkSize = chunkSize;
        _numberOfWorkers = numberOfWorkers;

        _readerStackBufferSize = (AvailableStackSize / _numberOfWorkers / ElementSize) * ElementSize;
    }

    public string Description => "Стратегия буфером на стеке";

    public async Task<long> Run(CancellationToken token = default)
    {
        // Сбрасываем позицию перед каждым запуском
        _arrayPosition = 0;
        
        var workersTotalCount = (_arrayLength + _chunkSize - 1) / _chunkSize;
        var tasks = new Task<long>[workersTotalCount];

        var semaphore = new SemaphoreSlim(_numberOfWorkers, _numberOfWorkers);
        
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

        var startArray = Interlocked.Add(ref _arrayPosition, _chunkSize) - _chunkSize;
        if(startArray >= _arrayLength)
        {
            return 0L;
        }
        
        var count = Math.Min(_chunkSize, _arrayLength - startArray);
        var begin = startArray * ElementSize;
        var end = begin + count * ElementSize;
        
        Span<byte> buffer = stackalloc byte[_readerStackBufferSize];
        
        using var reader = new BinaryReader(File.Open(_fileName, FileMode.Open, FileAccess.Read, FileShare.Read));
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
namespace ArraySum.SumStrategies;

public abstract class SumStrategy
{
    protected readonly string FileName;
    protected readonly int ChunkSize;
    protected readonly int NumberOfWorkers;
    protected readonly long ArrayLength;
    protected const int ElementSize = sizeof(int);

    
    protected SumStrategy(string fileName)
    {
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException("Файл не найден", fileName);
        }
        var fileSize = new FileInfo(fileName).Length;
        if (fileSize % ElementSize != 0)
        {
            throw new ArgumentException("Размер файла не кратен размеру элемента", nameof(fileName));
        }

        ArrayLength = fileSize / ElementSize;
    }
    protected SumStrategy(string fileName, int chunkSize, int numberOfWorkers) : this(fileName)
    {
        
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numberOfWorkers);


        FileName = fileName;
        ChunkSize = chunkSize;
        NumberOfWorkers = numberOfWorkers;
    }

    public abstract string Description { get; }
    
    /// <summary>
    /// Выполняет чтение файла и подсчет суммы всех чисел
    /// </summary>
    /// <returns>Сумма всех чисел в файле</returns>
    public abstract Task<long> Run(CancellationToken token = default);
}
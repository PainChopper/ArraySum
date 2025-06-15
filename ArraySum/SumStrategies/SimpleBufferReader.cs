namespace ArraySum.SumStrategies;

/// <summary>
/// Класс для наивного чтения и подсчета суммы чисел из бинарного файла
/// </summary>
public class SimpleBufferReader : SortStrategy
{
    private readonly string _fileName;

    public SimpleBufferReader(string fileName) : base(fileName)
    {
        _fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException("Файл не найден", fileName);
        }
    }

    public override string Description => "Наивная стратегия, с отдельным чтением каждого элемента";

    /// <summary>
    /// Выполняет чтение файла и подсчет суммы всех чисел
    /// </summary>
    /// <returns>Сумма всех чисел в файле</returns>
    public override Task<long> Run(CancellationToken token = default)
    {
        var sum = 0L;

        using var reader = new BinaryReader(File.Open(_fileName, FileMode.Open, FileAccess.Read, FileShare.Read));

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            sum += reader.ReadInt32();
        }

        return Task.FromResult(sum);
    }
}

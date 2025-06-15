namespace ArraySum.SumStrategies;

public interface ISortStrategy
{
    public string Description { get; }
    
    /// <summary>
    /// Выполняет чтение файла и подсчет суммы всех чисел
    /// </summary>
    /// <returns>Сумма всех чисел в файле</returns>
    Task<long> Run(CancellationToken token = default);
}
using System.Buffers;
using System.Threading.Channels;
using System.Runtime.InteropServices;

namespace ArraySum.SumStrategies;

public class ChannelWorker : SumStrategy
{
    public ChannelWorker(string fileName, int chunkSize, int numberOfWorkers) : base(fileName, chunkSize, numberOfWorkers)
    {

    }

    public override string Description => "Стратегия c каналом";

    // Основной метод, запускающий producer и consumers
    public override async Task<long> Run(CancellationToken token = default)
    {
        var channel = Channel.CreateBounded<(byte[] buffer, int bytesRead)>(
            new BoundedChannelOptions(NumberOfWorkers * 2)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        var producerTask = ProduceAsync(channel.Writer, token);

        var consumerTasks = Enumerable.Range(0, NumberOfWorkers)
            .Select(_ => ConsumeAsync(channel.Reader, token))
            .ToArray();

        await producerTask;
        var sums = await Task.WhenAll(consumerTasks);

        return sums.Sum();
    }


    // Метод для чтения файла и отправки данных в канал
    private async Task ProduceAsync(ChannelWriter<(byte[] buffer, int bytesRead)> writer, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        await using var fileStream = File.OpenRead(FileName);

        while (true)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
            var bytesRead = await fileStream.ReadAsync(buffer, token);

            if (bytesRead == 0)
            {
                ArrayPool<byte>.Shared.Return(buffer);
                break;
            }

            await writer.WriteAsync((buffer, bytesRead), token);
        }

        writer.Complete();
    }

    // Метод для чтения из канала и суммирования
    private static async Task<long> ConsumeAsync(ChannelReader<(byte[] buffer, int bytesRead)> reader, CancellationToken token)
    {
        long sum = 0;
        while (await reader.WaitToReadAsync(token))
        {
            while (reader.TryRead(out var item))
            {
                var (buffer, bytesRead) = item;
                try
                {
                    if (bytesRead % sizeof(int) != 0)
                        throw new InvalidDataException(
                            $"Кол-во прочитанных байт [{bytesRead}] не кратно размеру int ({sizeof(int)})");

                    var span = buffer.AsSpan(0, bytesRead);
                    var ints = MemoryMarshal.Cast<byte, int>(span);
                    
                    foreach (var val in ints)
                        sum += val;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        return sum;
    }
}
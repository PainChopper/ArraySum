namespace ArraySum;

public class DataFileOperations
{
   
    public static void GenerateFile()
    {
        var filePath = "numbers.data";
        var random = new Random();

        using var writer = new BinaryWriter(File.Open(filePath, FileMode.Create));

        for (var i = 0; i < 1_000_000_000; i++)
        {
            var number = random.Next(1, 1001); // от 1 до 1000 включительно
            writer.Write(number);
        }

        Console.WriteLine("Файл numbers.data успешно создан");
    }
}
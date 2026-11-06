using FileProcessor.LineFormat;
using System.Text;

namespace FileProcessor.Preprocess
{
    public class LargeFileSortDivider
    {
        private readonly long _chunkSize;
        private readonly IComparer<Entry> _comparer;
        private readonly int maxParallelSorts;

        public LargeFileSortDivider(long chunkSize, IComparer<Entry> comparer, int maxParallelSorts = 8)
        {
            if (chunkSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be greater than zero.");

            _chunkSize = chunkSize;
            _comparer = comparer;
            this.maxParallelSorts = maxParallelSorts;
        }



        public async Task<DivisionResult> Divide(string inputFilePath, string outputDirectory)
        {
            if (!File.Exists(inputFilePath))
                throw new FileNotFoundException("Input file not found.", inputFilePath);

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var files = new List<string>();
            var runningTasks = new List<Task>();


            using var reader = new StreamReader(inputFilePath);


            try                 
            {
                while (true)
                {
                    if (runningTasks.Count >= maxParallelSorts) {
                        var completed = await Task.WhenAny(runningTasks);
                        await completed;
                        runningTasks.Remove(completed);
                    }

                    var chunk = ReadChunk(reader);

                    if (chunk.Count == 0)
                        break;

                    string chunkPath = Path.Combine(
                        outputDirectory, $"chunk_{files.Count:D6}");

                    files.Add(chunkPath);

                    runningTasks.Add(Task.Run(() =>
                    {
                        chunk.Sort(_comparer);

                        using var writer = new StreamWriter(
                            chunkPath,
                            append: false,
                            encoding: new UTF8Encoding(false)
                            );

                        foreach (var entry in chunk)
                            writer.WriteLine($"{entry.Number}. {entry.Text}");
                    }));
                }

                await Task.WhenAll(runningTasks);
            }
            catch 
            {
                try 
                {
                    await Task.WhenAll(runningTasks);
                }
                catch 
                {
                }

                throw;
            }



            return new DivisionResult(outputDirectory, files);
        }

        private List<Entry> ReadChunk(StreamReader reader) {
            var entries = new List<Entry>();
            long currentSize = 0;

            while (currentSize < _chunkSize) {
                string? line = reader.ReadLine();

                if (line is null)
                    break;

                var entry = Entry.Parse(line);
                entries.Add(entry);
                // здесь считаю размер строки на диске, не в массиве. Учитывая структуру объекта Entry, в оперативной памяти будет занимать больше. Я бы закладывал запас в 20% чтобы избежать переполнения и свопа
                currentSize += Encoding.UTF8.GetByteCount(line) + Encoding.UTF8.GetByteCount(Environment.NewLine); 
            }

            return entries;
        }

    }
}

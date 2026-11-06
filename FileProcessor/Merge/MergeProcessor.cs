using FileProcessor.LineFormat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FileProcessor.Merge
{
    public class MergeProcessor
    {
        private readonly int _maxParallelMerges;
        private readonly int _filesPerMerge;
        private readonly IComparer<Entry> _comparer;
        
        public MergeProcessor(int maxParallelMerges, int filesPerMerge, IComparer<Entry> comparer)
        {
            if (maxParallelMerges <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxParallelMerges), "Max parallel merges must be greater than zero.");
            if (filesPerMerge < 2)
                throw new ArgumentOutOfRangeException(nameof(filesPerMerge), "Files per merge must be greater than two.");
            _maxParallelMerges = maxParallelMerges;
            _filesPerMerge = filesPerMerge;
            _comparer = comparer;
        }


        public async Task<string> Merge(string inputDirectory, string outputDirectory)
        {
            var files = Directory.GetFiles(inputDirectory, "chunk_*").ToList();
            if (files == null || files.Count() == 0)
                throw new ArgumentException("Input files list cannot be null or empty.", nameof(files));
            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);



            while (files.Count > 1) 
            {
                var chunks = files.Chunk(_filesPerMerge).ToArray();
                var results = new string[chunks.Length];

                await Parallel.ForEachAsync(
                    Enumerable.Range(0, chunks.Length),
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = _maxParallelMerges
                    },
                    (index, cancellationToken) => {
                        var chunk = chunks[index];

                        if (chunk.Length == 1) 
                        {
                            results[index] = chunk[0];
                            return ValueTask.CompletedTask;
                        }

                        string mergedPath = Path.Combine(
                            inputDirectory, $"merge_{Guid.NewGuid()}");


                        var readers = new List<StreamReader>();
                        var queue = new PriorityQueue<StreamReader, Entry>(_comparer);

                        try 
                        {

                            try 
                            {
                                foreach (string file in chunk) {
                                    var reader = new StreamReader(file);
                                    readers.Add(reader);

                                    string? line = reader.ReadLine();

                                    if (line is not null)
                                        queue.Enqueue(reader, Entry.Parse(line));
                                }

                                using var writer = new StreamWriter(mergedPath);

                                while (queue.TryDequeue(out var reader, out var entry)) {
                                    cancellationToken.ThrowIfCancellationRequested();

                                    writer.WriteLine($"{entry.Number}. {entry.Text}");

                                    string? line = reader.ReadLine();

                                    if (line is not null)
                                        queue.Enqueue(reader, Entry.Parse(line));
                                }
                            }
                            finally {
                                foreach (var reader in readers)
                                    reader.Dispose();
                            }
                        }
                        catch 
                        {
                            File.Delete(mergedPath);
                            throw;
                        }

                        foreach (string file in chunk)
                            File.Delete(file);


                        results[index] = mergedPath;
                        return ValueTask.CompletedTask;
                    });


                files = results.ToList();
            }

            var path = Path.Combine(outputDirectory, Path.GetFileName(files.First()));

            File.Move(files.First(), path, overwrite: true);
            return path;
        }
    }
}

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters.Csv;
using FileProcessor.LineFormat;
using FileProcessor.Merge;
using FileProcessor.Preprocess;

namespace FileProcessor.Benchmarks;

[CsvMeasurementsExporter]
public class SortingBenchmarks
{
    private const int LineBytes = 128;
    private readonly EntryComparer _comparer = new();
    private string _root = null!;
    private string _input = null!;
    private string _baseline = null!;
    private string _work = null!;
    private string _chunks = null!;
    private string _output = null!;
    private string? _result;
    private long _expectedCount;
    private long _expectedNumberSum;

    private static string Profile => Environment.GetEnvironmentVariable("FILEPROCESSOR_BENCHMARK_PROFILE") ?? "tuning";

    [ParamsSource(nameof(InputSizes))]
    public int InputMiB { get; set; }

    [ParamsSource(nameof(Configurations))]
    public SortSettings Settings { get; set; } = null!;

    public IEnumerable<int> InputSizes => Profile switch
    {
        "smoke" => [4],
        "tuning" or "merge-tuning" or "sort-tuning" => [128],
        _ => [128, 256, 512, 1024]
    };

    public IEnumerable<SortSettings> Configurations => Profile switch
    {
        "smoke" => [new(1, 2, 2, 2)],
        "tuning" or "merge-tuning" => from parallel in new[] { 1, 2, 4, 8 }
                          from fanIn in new[] { 2, 4, 8, 16 }
                          select new SortSettings(8, 4, parallel, fanIn),
        "sort-tuning" => new[] { 1, 2, 4, 8 }
            .Select(parallel => new SortSettings(8, parallel, 4, 4)),
        _ => [new(64, 4, 4, 4)]
    };

    [GlobalSetup]
    public void GenerateInput()
    {
        var parent = Environment.GetEnvironmentVariable("FILEPROCESSOR_BENCHMARK_WORKDIR") ?? Path.GetTempPath();
        _root = Path.Combine(Path.GetFullPath(parent), $"FileProcessor.Benchmarks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _input = Path.Combine(_root, "input.txt");
        _baseline = Path.Combine(_root, "baseline");
        _work = Path.Combine(_root, "iteration");
        _chunks = Path.Combine(_work, "chunks");
        _output = Path.Combine(_work, "output");
        var random = new Random(1729);
        _expectedCount = InputMiB * 1024L * 1024L / LineBytes;
        using var writer = new StreamWriter(_input, false, new UTF8Encoding(false), 64 * 1024);
        writer.NewLine = "\n";
        for (long i = 0; i < _expectedCount; i++)
        {
            var number = random.Next(10000);
            var text = $"Text {random.Next(1000):D4} repeated words for external sorting".PadRight(115, 'x');
            writer.WriteLine(number.ToString("D10", CultureInfo.InvariantCulture) + ". " + text);
            _expectedNumberSum += number;
        }
    }

    [GlobalSetup(Target = nameof(Merge))]
    public void GenerateMergeInput()
    {
        GenerateInput();
        Divider().Divide(_input, _baseline).GetAwaiter().GetResult();
    }

    [IterationSetup]
    public void PrepareIteration()
    {
        Directory.CreateDirectory(_chunks);
        _result = null;
    }

    [IterationSetup(Target = nameof(Merge))]
    public void PrepareMergeIteration()
    {
        PrepareIteration();
        foreach (var file in Directory.EnumerateFiles(_baseline))
            File.Copy(file, Path.Combine(_chunks, Path.GetFileName(file)));
    }

    [Benchmark]
    public Task<DivisionResult> Divide() => Divider().Divide(_input, _chunks);

    [Benchmark]
    public async Task Merge() => _result = await Merger().Merge(_chunks, _output);

    [Benchmark]
    public async Task FullPipeline()
    {
        await Divider().Divide(_input, _chunks);
        _result = await Merger().Merge(_chunks, _output);
    }

    [IterationCleanup]
    public void ValidateAndCleanIteration()
    {
        try
        {
            var files = _result is null ? Directory.GetFiles(_chunks, "chunk_*") : [_result];
            long count = 0;
            long sum = 0;
            foreach (var file in files)
            {
                using var reader = new StreamReader(file);
                Entry? previous = null;
                while (reader.ReadLine() is { } line)
                {
                    var entry = Entry.Parse(line);
                    if (previous is { } last && _comparer.Compare(last, entry) > 0)
                        throw new InvalidDataException($"Unsorted output: {file}");
                    previous = entry;
                    count++;
                    sum += entry.Number;
                }
            }
            if (count != _expectedCount || sum != _expectedNumberSum)
                throw new InvalidDataException("Output count or number checksum differs from input.");
        }
        finally
        {
            if (Directory.Exists(_work))
                Directory.Delete(_work, recursive: true);
        }
    }

    [GlobalCleanup]
    public void CleanRun()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private LargeFileSortDivider Divider() => new(Settings.ChunkMiB * 1024L * 1024L, _comparer, Settings.ParallelSorts);
    private MergeProcessor Merger() => new(Settings.ParallelMerges, Settings.FilesPerMerge, _comparer);
}

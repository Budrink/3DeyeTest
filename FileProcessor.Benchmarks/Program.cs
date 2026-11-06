using System.Globalization;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using FileProcessor.Benchmarks;

// Environment variables are inherited by BenchmarkDotNet's isolated worker processes.
var arguments = args.ToList();
var profileIndex = arguments.IndexOf("--profile");
if (profileIndex >= 0)
{
    if (profileIndex + 1 >= arguments.Count ||
        !new[] { "scaling", "tuning", "smoke", "merge-tuning", "sort-tuning" }.Contains(arguments[profileIndex + 1]))
        throw new ArgumentException("--profile requires scaling, tuning, smoke, merge-tuning or sort-tuning.");
    Environment.SetEnvironmentVariable("FILEPROCESSOR_BENCHMARK_PROFILE", arguments[profileIndex + 1]);
    arguments.RemoveRange(profileIndex, 2);
}
if (!arguments.Any(argument => argument is "--filter" or "-f" or "--list" or "--help" or "-h"))
{
    var profile = Environment.GetEnvironmentVariable("FILEPROCESSOR_BENCHMARK_PROFILE") ?? "tuning";
    var filter = profile switch
    {
        "tuning" or "merge-tuning" => "*SortingBenchmarks.Merge",
        "sort-tuning" => "*SortingBenchmarks.Divide",
        _ => "*"
    };
    arguments.AddRange(["--filter", filter]);
}

var config = ManualConfig.Create(DefaultConfig.Instance).AddJob(
    Job.Default.WithStrategy(RunStrategy.Monitoring).WithLaunchCount(1)
        .WithWarmupCount(1).WithIterationCount(5).AsDefault());
var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(arguments.ToArray(), config).ToArray();
var failed = false;
foreach (var summary in summaries)
{
    var path = Path.Combine(summary.ResultsDirectoryPath, $"{summary.Title}-graph.csv");
    using var writer = new StreamWriter(path);
    writer.WriteLine("Scenario,Job,InputMiB,ChunkMiB,ParallelSorts,ParallelMerges,FilesPerMerge,MeanSeconds,MedianSeconds,StdDevSeconds,MiBPerSecond");
    foreach (var report in summary.Reports)
    {
        if (report.ResultStatistics is not { } statistics)
        {
            failed = true;
            continue;
        }
        var parameters = report.BenchmarkCase.Parameters;
        var size = (int)parameters.Items.Single(item => item.Name == "InputMiB").Value;
        var settings = (SortSettings)parameters.Items.Single(item => item.Name == "Settings").Value;
        var meanSeconds = statistics.Mean / 1e9;
        writer.WriteLine(string.Join(",", new object[]
        {
            report.BenchmarkCase.Descriptor.WorkloadMethod.Name, report.BenchmarkCase.Job.Id, size, settings.ChunkMiB,
            settings.ParallelSorts, settings.ParallelMerges, settings.FilesPerMerge,
            statistics.Mean / 1e9, statistics.Median / 1e9, statistics.StandardDeviation / 1e9,
            size / meanSeconds
        }.Select(value => CsvField(Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""))));
    }
    Console.WriteLine($"Graph CSV: {path}");
}
if (failed || summaries.Length == 0)
    Environment.ExitCode = 1;

static string CsvField(string value) =>
    value.IndexOfAny([',', '"', '\r', '\n']) >= 0
        ? "\"" + value.Replace("\"", "\"\"") + "\""
        : value;
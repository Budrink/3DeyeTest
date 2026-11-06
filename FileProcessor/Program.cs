using FileProcessor.LineFormat;
using FileProcessor.Merge;
using FileProcessor.Preprocess;
using System.Diagnostics;

const int parallelSorts = 4;
const int parallelMerges = 8;
const int filesPerMerge = 8;

Console.WriteLine("Enter the path of the input file:");
var inputFilePath = Console.ReadLine();

if (string.IsNullOrWhiteSpace(inputFilePath) || !File.Exists(inputFilePath))
{
    Console.Error.WriteLine("Input file not found.");
    Environment.ExitCode = 1;
    return;
}

Console.WriteLine("Enter the size of each chunk in MiB:");
if (!long.TryParse(Console.ReadLine(), out var chunkSizeInMiB)
    || chunkSizeInMiB <= 0
    || chunkSizeInMiB > long.MaxValue / (1024L * 1024L))
{
    Console.Error.WriteLine("Chunk size must be a positive integer within the supported range.");
    Environment.ExitCode = 1;
    return;
}

var workingDirectory = Directory.GetCurrentDirectory();
var temporaryDirectory = Path.Combine(workingDirectory, $"temp_{Guid.NewGuid():N}");
var outputDirectory = Path.Combine(workingDirectory, $"output_{Guid.NewGuid():N}");
var comparer = new EntryComparer();
string? finalOutputFilePath = null;

try
{
    var divider = new LargeFileSortDivider(
        chunkSizeInMiB * 1024L * 1024L, comparer, parallelSorts);

    Console.WriteLine();
    Console.WriteLine($"Starting file division and sorting (max parallel sorts: {parallelSorts})...");
    var stopwatch = Stopwatch.StartNew();
    var division = await divider.Divide(inputFilePath, temporaryDirectory);
    stopwatch.Stop();
    Console.WriteLine($"File division and sorting completed in {stopwatch.Elapsed}.");

    if (division.Files.Count == 0)
    {
        Console.WriteLine("Input file is empty. No output file was created.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine(
        $"Starting merge (max parallel merges: {parallelMerges}, files per merge: {filesPerMerge})...");
    var merger = new MergeProcessor(parallelMerges, filesPerMerge, comparer);
    stopwatch.Restart();
    finalOutputFilePath = await merger.Merge(division.DirectoryPath, outputDirectory);
    stopwatch.Stop();

    Console.WriteLine($"Merge completed in {stopwatch.Elapsed}.");
    Console.WriteLine($"Final output file: {finalOutputFilePath}");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"File processing failed: {exception.Message}");
    Environment.ExitCode = 1;
}
finally
{
    DeleteWorkingDirectory(temporaryDirectory);
    if (finalOutputFilePath is null)
        DeleteWorkingDirectory(outputDirectory);
}

static void DeleteWorkingDirectory(string path)
{
    try
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Could not delete working directory '{path}': {exception.Message}");
        Environment.ExitCode = 1;
    }
}

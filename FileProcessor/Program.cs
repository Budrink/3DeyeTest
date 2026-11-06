using FileProcessor.LineFormat;
using FileProcessor.Merge;
using FileProcessor.Preprocess;
using FileProcessor.Utils;
using System.Diagnostics;
using System.IO;

Console.WriteLine("Enter the path of the input file:");

var inputFilePath = Console.ReadLine()
    ?? throw new ArgumentNullException("Input file path cannot be null.");

Console.WriteLine("Enter the size of each chunk in Mb:");

var chunkSizeInMb = Console.ReadLine()
    ?? throw new ArgumentNullException("Chunk size cannot be null.");

string tmpDirectory = Path.Combine(
    Directory.GetCurrentDirectory(),
    $"temp_{Guid.NewGuid():N}");

var comparer = new EntryComparer();


Console.WriteLine();
Console.WriteLine("Starting file division and sorting...");
Console.WriteLine($"Started at: {DateTimeOffset.Now:O}");

//for (var i = 2; i < 3; i++) 
//{
var divider = new LargeFileSortDivider(
Convert.ToInt64(chunkSizeInMb) * 1024L * 1024L,
comparer, 4);

//Console.WriteLine($"Iteration {i}...");
if (Directory.Exists(tmpDirectory))
    Directory.Delete(tmpDirectory, recursive: true);

var dividerStopwatch = Stopwatch.StartNew();

await divider.Divide(inputFilePath, tmpDirectory);

dividerStopwatch.Stop(); Console.WriteLine($"File division and sorting completed. Current threads count: 4");
Console.WriteLine($"Finished at: {DateTimeOffset.Now:O}");
Console.WriteLine($"Divider elapsed time: {dividerStopwatch.Elapsed}");
Console.WriteLine($"Divider elapsed milliseconds: {dividerStopwatch.ElapsedMilliseconds}");
Console.WriteLine(
    $"Working set after divider: {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB");

//}


//(int Threads, int Files)[] configurations =
//[
//    (1, 2),
//    (2, 2),
//    (4, 4),
//    (4, 8),
//    (8, 2),
//    (8, 4),
//    (8, 8),
//    (8, 16),
//];


//foreach (var (threads, files) in configurations) {

var mergeProcessor = new MergeProcessor(8, 8, comparer);

var outputDirectory = Path.Combine(
    Directory.GetCurrentDirectory(),
    $"output_{Guid.NewGuid():N}");

var copyTmp = $"{tmpDirectory}(1)";
DirectoryHelper.CopyDirectory(
    tmpDirectory, copyTmp);

Console.WriteLine();
Console.WriteLine("Starting merge process...");
Console.WriteLine($"Started at: {DateTimeOffset.Now:O}");

var mergeStopwatch = Stopwatch.StartNew();

var finalOutputFilePath = await mergeProcessor.Merge(
    copyTmp,
    outputDirectory);

mergeStopwatch.Stop();

Console.WriteLine($"Merge process completed. Threads: 8, Files: 8");
Console.WriteLine($"Finished at: {DateTimeOffset.Now:O}");
Console.WriteLine($"Merge elapsed time: {mergeStopwatch.Elapsed}");
Console.WriteLine($"Merge elapsed milliseconds: {mergeStopwatch.ElapsedMilliseconds}");
Console.WriteLine(
    $"Working set after merge: {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB");

Console.WriteLine($"Final output file: {finalOutputFilePath}");
//}

Console.ReadLine();

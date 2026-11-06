using System.Text;
using FileProcessor.LineFormat;
using FileProcessor.Merge;
using FileProcessor.Preprocess;

namespace FileProcessor.Tests;

public sealed class FileSortingTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "FileProcessor.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(1, 1, 1, 2)]
    [InlineData(128, 2, 2, 2)]
    [InlineData(256, 4, 4, 3)]
    [InlineData(512, 2, 4, 8)]
    [InlineData(1000000, 1, 1, 2)]
    public async Task DivideAndMerge_GeneratedInput_MatchesExpectedSort(
        long chunkSize, int parallelSorts, int parallelMerges, int filesPerMerge)
    {
        var random = new Random(1729);
        string[] texts = ["Apple", "apple", "Banana", "Orange", "🍎", "", "First. Second"];
        var entries = Enumerable.Range(0, 200)
            .Select(_ => new Entry(random.Next(-20, 21), texts[random.Next(texts.Length)]))
            .Concat([new Entry(2, "Apple"), new Entry(10, "Apple"),
                new Entry(2, "Apple"), new Entry(int.MinValue, "Boundary"),
                new Entry(int.MaxValue, "Boundary"), new Entry(7, new string('é', 600))])
            .ToArray();

        await AssertSortedOutput(entries, chunkSize, parallelSorts, parallelMerges, filesPerMerge);
    }

    [Fact]
    public async Task DivideAndMerge_SingleEntry_PreservesEntry()
    {
        await AssertSortedOutput([new Entry(42, "Single line")], 1, 1, 1, 2);
    }

    [Fact]
    public async Task DivideAndMerge_FiveChunksWithPairs_HandlesUnpairedChunksAcrossPasses()
    {
        Entry[] entries = [new(10, "B"), new(3, "A"), new(2, "A"), new(1, "C"), new(2, "A")];
        await AssertSortedOutput(entries, 1, 2, 2, 2);
    }

    private async Task AssertSortedOutput(
        Entry[] entries, long chunkSize, int parallelSorts, int parallelMerges, int filesPerMerge)
    {
        Directory.CreateDirectory(_directory);
        var inputPath = Path.Combine(_directory, "input.txt");
        var chunksDirectory = Path.Combine(_directory, "chunks");
        var outputDirectory = Path.Combine(_directory, "output");
        // Exercise LF input and a last line without a terminating newline.
        var input = string.Join("\n", entries.Select(entry => $"{entry.Number}. {entry.Text}"));
        await File.WriteAllTextAsync(inputPath, input, new UTF8Encoding(false));

        var comparer = new EntryComparer();
        var division = await new LargeFileSortDivider(chunkSize, comparer, parallelSorts)
            .Divide(inputPath, chunksDirectory);

        if (chunkSize == 1)
            Assert.Equal(entries.Length, division.Files.Count);
        else if (chunkSize >= Encoding.UTF8.GetByteCount(input) + entries.Length * 2)
            Assert.Single(division.Files);
        else
            Assert.True(division.Files.Count > 1);

        var outputPath = await new MergeProcessor(parallelMerges, filesPerMerge, comparer)
            .Merge(division.DirectoryPath, outputDirectory);

        Assert.True(File.Exists(outputPath));
        Assert.Equal(Path.GetFullPath(outputDirectory), Path.GetDirectoryName(Path.GetFullPath(outputPath)));
        var actualLines = await File.ReadAllLinesAsync(outputPath);
        // Independent oracle: do not calculate expected results with EntryComparer.
        var expectedLines = entries.OrderBy(entry => entry.Text, StringComparer.Ordinal)
            .ThenBy(entry => entry.Number)
            .Select(entry => $"{entry.Number}. {entry.Text}").ToArray();
        Assert.Equal(expectedLines, actualLines);
        Assert.Empty(Directory.GetFiles(chunksDirectory));
        Assert.Equal(input, await File.ReadAllTextAsync(inputPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}

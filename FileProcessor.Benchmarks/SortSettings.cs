namespace FileProcessor.Benchmarks;

public sealed record SortSettings(int ChunkMiB, int ParallelSorts, int ParallelMerges, int FilesPerMerge)
{
    public override string ToString() => $"C{ChunkMiB}-S{ParallelSorts}-M{ParallelMerges}-F{FilesPerMerge}";
}

namespace FileProcessor.Preprocess
{
    public sealed record DivisionResult(
    string DirectoryPath,
    IReadOnlyList<string> Files);
}

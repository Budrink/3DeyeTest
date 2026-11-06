namespace FileProcessor.Utils
{
    public static class DirectoryHelper
    {
        public static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(
                    file,
                Path.Combine(destination, Path.GetFileName(file)),
                overwrite: true);
        }

            foreach (string directory in Directory.GetDirectories(source))
            {
                CopyDirectory(
                    directory,
                    Path.Combine(destination, Path.GetFileName(directory)));
            }
        }
    }
}

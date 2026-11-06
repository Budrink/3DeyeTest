using System;
using System.Text;

Console.WriteLine("Please enter a filesize (gb):");

var sizeInGb = Console.ReadLine();


var sizeInBytes = (long)(Convert.ToDouble(sizeInGb) * 1024 * 1024 * 1024);


string[] texts =
[
    "Apple",
    "Banana is yellow",
    "Cherry is the best",
    "Something something something",
    "Hello world",
    "A quiet evening",
    "The quick brown fox",
    "Coffee before coding",
    "Rain over the city",
    "Blue sky",
    "Apple pie",
    "A very long road to nowhere",
    "Small steps",
    "Nothing to see here",
    "The quick brown fox jumps over the lazy dog"
];

long currentSize = 0;

var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")); // todo: replace with a config file or environment variable
var path = Path.Combine(projectRoot, $"TestFile_{sizeInGb}Gb");


using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
using var writer = new StreamWriter(fileStream,new UTF8Encoding(), 64 * 1024);

var random = new Random(DateTime.Now.Millisecond);

while (currentSize < sizeInBytes) {
    var randomText = $"{random.Next(10000)}. {texts[random.Next(texts.Length)]}";
    writer.WriteLine(randomText);
    currentSize += writer.Encoding.GetByteCount(randomText)
                 + writer.Encoding.GetByteCount(writer.NewLine);
}

Console.WriteLine("File generation completed.");
Console.ReadLine();

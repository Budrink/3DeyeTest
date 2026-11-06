namespace FileProcessor.LineFormat
{
    public readonly record struct Entry(int Number, string Text)
    {
        public static Entry Parse(string line) {
            var separator = line.IndexOf(". ");

            if (separator <= 0)
                throw new FormatException("Expected format: <Number>. <String>");

            var number = int.Parse(line.AsSpan(0, separator));
            var text = line[(separator + 2)..];

            return new Entry(number, text);
        }
    }
}

using FileProcessor.LineFormat;

namespace FileProcessor.Tests;

public class EntryParseTests
{
    [Theory]
    [InlineData("123. Apple", 123, "Apple")]
    [InlineData("0. Text 🍎", 0, "Text 🍎")]
    [InlineData("-42. Negative", -42, "Negative")]
    [InlineData("2147483647. Maximum", int.MaxValue, "Maximum")]
    [InlineData("-2147483648. Minimum", int.MinValue, "Minimum")]
    [InlineData("12. First. Second", 12, "First. Second")]
    [InlineData("12. ", 12, "")]
    [InlineData("12.  spaced text ", 12, " spaced text ")]
    public void Parse_ValidLine_ReturnsNumberAndUnmodifiedText(string line, int number, string text)
    {
        Assert.Equal(new Entry(number, text), Entry.Parse(line));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("123.Apple")]
    [InlineData(". Apple")]
    [InlineData("abc. Apple")]
    [InlineData("1.5. Apple")]
    public void Parse_InvalidFormat_ThrowsFormatException(string line)
    {
        Assert.Throws<FormatException>(() => Entry.Parse(line));
    }

    [Theory]
    [InlineData("2147483648. Apple")]
    [InlineData("-2147483649. Apple")]
    public void Parse_NumberOutsideIntRange_ThrowsOverflowException(string line)
    {
        Assert.Throws<OverflowException>(() => Entry.Parse(line));
    }
}

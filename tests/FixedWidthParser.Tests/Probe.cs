using csFastFloat;

namespace FixedWidthParser.Tests;

public class Probe
{
    [Theory]
    [InlineData("60000.00")]
    [InlineData("60000.00 ")]
    [InlineData(" 60000.00")]
    [InlineData("60000     ")]
    public void FastDoubleParser_Behavior(string input)
    {
        bool ok = FastDoubleParser.TryParseDouble(input, out var value);
        Assert.True(ok, $"input='{input}' ok={ok} value={value}");
    }
}

using QRScanner.Core.Barcodes;

namespace QRScanner.Core.Tests;

public class BarcodeParserTests
{
    [Fact]
    public void Extracts_gtin_from_marking_code()
    {
        const string code = "010460000000000121ABC";
        Assert.Equal("4600000000001", BarcodeParser.ExtractGtin(code));
    }

    [Fact]
    public void Returns_null_when_gtin_is_missing()
    {
        Assert.Null(BarcodeParser.ExtractGtin("ABC-123"));
    }

    [Theory]
    [InlineData("17", true)]
    [InlineData("0001", true)]
    [InlineData("0104600000000001", false)]
    [InlineData("ABC", false)]
    [InlineData("", false)]
    public void Recognizes_corob_code(string code, bool expected)
    {
        Assert.Equal(expected, BarcodeParser.IsCorobCode(code));
    }
}

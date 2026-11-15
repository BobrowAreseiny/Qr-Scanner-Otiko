using QRScanner.Core.Barcodes;

namespace QRScanner.Core.Tests;

public sealed class GtinValidatorTests
{
    [Theory]
    [InlineData("04810498044822")]
    [InlineData("4810498044822")]
    public void IsValid_AcceptsCorrectCheckDigit(string gtin)
    {
        Assert.True(GtinValidator.IsValid(gtin));
    }

    [Theory]
    [InlineData("04810498044821")]
    [InlineData("04810498044820")]
    public void IsValid_RejectsWrongCheckDigit(string gtin)
    {
        Assert.False(GtinValidator.IsValid(gtin));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0481049804482A")]
    [InlineData("12345")]
    public void IsValid_RejectsInvalidFormat(string gtin)
    {
        Assert.False(GtinValidator.IsValid(gtin));
    }

    [Fact]
    public void Normalize_PadsToFourteenDigits()
    {
        Assert.Equal("04810498044822", GtinValidator.Normalize("4810498044822"));
    }

    [Fact]
    public void ExtractGtin_AndValidate_FromMarkingCode()
    {
        const string code = "0104810498044822212UJogcqL51WQE91000592TEST";

        var gtin = BarcodeParser.ExtractGtin(code);

        Assert.Equal("04810498044822", gtin);
        Assert.True(GtinValidator.IsValid(gtin));
    }
}

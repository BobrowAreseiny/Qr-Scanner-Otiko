using System.Text;
using QRScanner.Core.Export;

namespace QRScanner.Core.Tests;

public sealed class OrderExportWriterTests
{
    [Fact]
    public async Task Writes_one_marking_per_line_with_gs_and_without_prefix()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "qrscanner-export-tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var codes = new[]
            {
                "è010460000000000121ABC\u001d910005\u001d92XYZ",
                "е010460000000000221DEF\u001d910005\u001d92UVW"
            };

            var path = await OrderExportWriter.WriteAsync(directory, "Заказ", codes);
            var bytes = await File.ReadAllBytesAsync(path);
            var text = Encoding.UTF8.GetString(bytes);

            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            Assert.Equal(
                "010460000000000121ABC\u001d910005\u001d92XYZ\n" +
                "010460000000000221DEF\u001d910005\u001d92UVW\n",
                text);
            Assert.DoesNotContain("è", text);
            Assert.DoesNotContain("е", text);
            Assert.Equal(2, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

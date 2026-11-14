namespace QRScanner.App.Services;

public interface IQrCodeService
{
    Task<string> GenerateAsync(int corobId, string corobName);
}

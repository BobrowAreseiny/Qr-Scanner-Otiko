namespace QRScanner.App.Services;

public interface IScannerService
{
    event EventHandler<BarcodeScannedEventArgs>? BarcodeScanned;

    void Publish(string? data, string? symbology);

    void EnableBroadcastMode();
}

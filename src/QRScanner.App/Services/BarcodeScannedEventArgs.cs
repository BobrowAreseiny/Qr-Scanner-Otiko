namespace QRScanner.App.Services;

public sealed class BarcodeScannedEventArgs : EventArgs
{
    public BarcodeScannedEventArgs(string data, string? symbology, DateTime scannedAtUtc)
    {
        Data = data;
        Symbology = symbology;
        ScannedAtUtc = scannedAtUtc;
    }

    public string Data { get; }

    public string? Symbology { get; }

    public DateTime ScannedAtUtc { get; }
}

using Android.Content;

namespace QRScanner.App.Services;

public sealed class ScannerService : IScannerService
{
    private const string ControlAction = "com.xcheng.scanner.action.CONTROL_DATA_EVENT";
    private const string ControlExtra = "extra_data_event";
    private const string ControlValue = "BROADCAST_EVENT";

    private AtolScannerSettings? _atolSettings;

    public event EventHandler<BarcodeScannedEventArgs>? BarcodeScanned;

    public void Publish(string? data, string? symbology)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return;
        }

        BarcodeScanned?.Invoke(
            this,
            new BarcodeScannedEventArgs(data.Trim(), symbology, DateTime.UtcNow));
    }

    public void EnableBroadcastMode()
    {
        var intent = new Intent(ControlAction);
        intent.PutExtra(ControlExtra, ControlValue);
        Android.App.Application.Context.SendBroadcast(intent);

        if (_atolSettings == null)
        {
            _atolSettings = new AtolScannerSettings(
                Android.App.Application.Context);
        }

        _atolSettings.Bind();
    }
}

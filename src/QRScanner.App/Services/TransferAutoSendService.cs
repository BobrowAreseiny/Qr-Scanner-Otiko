using System.Diagnostics;

namespace QRScanner.App.Services;

public interface ITransferAutoSendService
{
    Task TrySendAsync();
}

/// <summary>
/// Автоматически отправляет накопленные файлы, когда появляется Wi-Fi.
/// Если сеть пропала во время отправки, файлы остаются в очереди.
/// </summary>
public sealed class TransferAutoSendService : ITransferAutoSendService
{
    private readonly ITransferQueueService _queue;
    private readonly INetworkStatusService _network;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastAttemptUtc = DateTime.MinValue;

    public TransferAutoSendService(
        ITransferQueueService queue,
        INetworkStatusService network)
    {
        _queue = queue;
        _network = network;
        _network.WifiAvailabilityChanged += OnWifiAvailabilityChanged;
    }

    public async Task TrySendAsync()
    {
        if (!_network.IsWifiConnected)
        {
            return;
        }

        if (!await _gate.WaitAsync(0))
        {
            return;
        }

        try
        {
            if (DateTime.UtcNow - _lastAttemptUtc < TimeSpan.FromSeconds(3))
            {
                return;
            }

            _lastAttemptUtc = DateTime.UtcNow;

            var pending = await _queue.CountPendingAsync();
            if (pending == 0)
            {
                return;
            }

            var result = await _queue.SendPendingAsync();
            Debug.WriteLine("QRSCANNER_AUTOSEND: " + result.Message);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("QRSCANNER_AUTOSEND_ERROR: " + ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void OnWifiAvailabilityChanged(object? sender, EventArgs e)
    {
        if (_network.IsWifiConnected)
        {
            _ = TrySendAsync();
        }
    }
}

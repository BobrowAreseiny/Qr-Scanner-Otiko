using System.Linq;
using Microsoft.Maui.Networking;

namespace QRScanner.App.Services;

public interface INetworkStatusService
{
    bool IsWifiConnected { get; }

    event EventHandler? WifiAvailabilityChanged;
}

public sealed class NetworkStatusService : INetworkStatusService, IDisposable
{
    public NetworkStatusService()
    {
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    public bool IsWifiConnected =>
        Connectivity.Current.ConnectionProfiles.Contains(ConnectionProfile.WiFi);

    public event EventHandler? WifiAvailabilityChanged;

    private void OnConnectivityChanged(
        object? sender,
        ConnectivityChangedEventArgs e)
    {
        WifiAvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }
}

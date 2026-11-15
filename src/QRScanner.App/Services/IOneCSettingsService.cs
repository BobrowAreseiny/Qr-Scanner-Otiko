using QRScanner.Core.Integration;

namespace QRScanner.App.Services;

public interface IOneCSettingsService
{
    Task<OneCConnectionSettings> LoadAsync();

    Task SaveAsync(OneCConnectionSettings settings);
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using QRScanner.App.Services.Datamark;
using QRScanner.Core;

namespace QRScanner.App.Services;

public interface IDatamarkApiClient
{
    Task<bool> TestConnectionAsync(ApiSettings settings, CancellationToken cancellationToken = default);

    Task<ProductInfo?> FindProductAsync(
        string gtin,
        ApiSettings settings,
        CancellationToken cancellationToken = default);

    Task<LabelCheckResult?> CheckLabelAsync(
        string code,
        ApiSettings settings,
        CancellationToken cancellationToken = default);
}

using QRScanner.App.Data;
using QRScanner.App.Data.Entities;
using QRScanner.App.Services.Datamark;

namespace QRScanner.App.Services;

public static class BarcodeConflictInfo
{
    public static async Task<string> BuildAsync(
        IDatabaseService database,
        IDatamarkApiClient apiClient,
        BoxBarcodeEntity existing,
        ApiSettings settings)
    {
        var corob = await database.GetCorobAsync(existing.CorobId);
        var corobName = corob?.Name ?? $"№{existing.CorobId}";
        var corobStatus = corob?.IsShipped == true ? "Отгружен" : "Активен";

        var lines = new List<string>
        {
            $"Марка уже находится в коробе {corobName}.",
            $"Статус короба: {corobStatus}."
        };

        if (corob?.IsShipped == true)
        {
            var order = await database.GetShippedOrderForCorobAsync(existing.CorobId);
            if (order is not null)
            {
                lines.Add(
                    $"Заказ: «{order.Name}» от {order.CreatedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm}.");
            }
        }

        if (!string.IsNullOrWhiteSpace(existing.ProductDetails))
        {
            lines.Add($"Товар: {existing.DisplayTitle}");
            lines.Add(existing.ProductDetails);
        }

        if (settings.HasCredentials)
        {
            try
            {
                var label = await apiClient.CheckLabelAsync(existing.Barcode, settings);
                if (label is not null)
                {
                    lines.Add(string.Empty);
                    lines.Add("Электронный знак:");
                    lines.Add(DatamarkLabelStatus.Describe(label));
                }
            }
            catch (Exception ex)
            {
                lines.Add(string.Empty);
                lines.Add($"Электронный знак: проверка не удалась ({ex.Message}).");
            }
        }

        return string.Join("\n", lines);
    }
}

namespace QRScanner.App.Services.Datamark;

using QRScanner.App.Services;

public static class DatamarkLabelStatus
{
    public static bool IsShipped(string? status)
    {
        return Contains(status, "отгруж") || Contains(status, "shipped");
    }

    public static bool IsNotInStock(string? status)
    {
        return IsShipped(status) ||
               Contains(status, "принят") ||
               Contains(status, "в пути") ||
               Contains(status, "ожидает прием") ||
               Contains(status, "accepted") ||
               Contains(status, "in transit");
    }

    public static string Describe(LabelCheckResult label)
    {
        if (!label.Found)
        {
            return "Код не найден в Электронном знаке.";
        }

        var lines = new List<string>
        {
            DescribeStatus(label.Status)
        };

        if (label.IsMy == false)
        {
            lines.Add("Код принадлежит другому участнику.");
        }

        if (!string.IsNullOrWhiteSpace(label.Type))
        {
            lines.Add($"Тип кода: {label.Type}.");
        }

        if (!string.IsNullOrWhiteSpace(label.ShipmentNumber))
        {
            lines.Add($"Отгрузка: №{label.ShipmentNumber}.");
        }

        if (!string.IsNullOrWhiteSpace(label.ShipmentDate))
        {
            lines.Add($"Дата отгрузки: {label.ShipmentDate}.");
        }

        if (!string.IsNullOrWhiteSpace(label.Recipient))
        {
            lines.Add($"Получатель: {label.Recipient}.");
        }

        if (IsShipped(label.Status) &&
            string.IsNullOrWhiteSpace(label.ShipmentNumber) &&
            string.IsNullOrWhiteSpace(label.ShipmentDate) &&
            string.IsNullOrWhiteSpace(label.Recipient))
        {
            lines.Add("Детали отгрузки API не вернул.");
        }

        return string.Join("\n", lines);
    }

    public static string ShortStatus(string? status)
    {
        return DescribeStatus(status).TrimEnd('.');
    }

    public static async Task<bool> ConfirmAddAsync(
        IDialogService dialogs,
        LabelCheckResult? label)
    {
        if (label is null || !label.Found || !IsNotInStock(label.Status))
        {
            return true;
        }

        var title = IsShipped(label.Status)
            ? "Марка уже отгружена"
            : "Марка не на складе";

        return await dialogs.ShowConfirmationAsync(
            title,
            $"Электронный знак:\n{Describe(label)}\n\nДобавить марку?",
            "Добавить",
            "Отмена");
    }

    private static string DescribeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Статус в Электронном знаке не указан.";
        }

        var value = status.Trim();

        if (Contains(value, "отгруж") || Contains(value, "shipped"))
        {
            return "Отгружен в Электронном знаке.";
        }

        if (Contains(value, "принят") || Contains(value, "accepted"))
        {
            return "Принят получателем.";
        }

        if (Contains(value, "в пути") || Contains(value, "in transit"))
        {
            return "В пути.";
        }

        if (Contains(value, "ожидает прием"))
        {
            return "Ожидает приёмку.";
        }

        if (Contains(value, "введен в оборот") ||
            Contains(value, "введён в оборот") ||
            Contains(value, "в обороте"))
        {
            return "В обороте.";
        }

        if (Contains(value, "промаркирован"))
        {
            return "Промаркирован, не отгружен.";
        }

        if (Contains(value, "выдан код"))
        {
            return "Выдан код, не введён в оборот.";
        }

        if (Contains(value, "нанесен") || Contains(value, "нанесён"))
        {
            return "Нанесён, не введён в оборот.";
        }

        return $"Статус: {value}.";
    }

    private static bool Contains(string? value, string fragment)
    {
        return value?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true;
    }
}

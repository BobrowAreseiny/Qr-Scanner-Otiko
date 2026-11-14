namespace QRScanner.App.ViewModels;

public sealed class OrderHistoryItem
{
    public int OrderId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string DateText { get; init; } = string.Empty;

    public string CountText { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;
}

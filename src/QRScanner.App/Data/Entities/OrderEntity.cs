using SQLite;

namespace QRScanner.App.Data.Entities;

[Table("Order")]
public sealed class OrderEntity
{
    [PrimaryKey, AutoIncrement, Column("_id")]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public bool IsShipped { get; set; }

    public bool IsExported { get; set; }

    public DateTime? ExportedAtUtc { get; set; }

    public string ExternalId { get; set; } = string.Empty;

    public bool IsSentTo1C { get; set; }

    public DateTime? SentTo1CAtUtc { get; set; }

    public string? OneCNumber { get; set; }

    public string? OneCError { get; set; }
}

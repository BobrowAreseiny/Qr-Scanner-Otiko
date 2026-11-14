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
}

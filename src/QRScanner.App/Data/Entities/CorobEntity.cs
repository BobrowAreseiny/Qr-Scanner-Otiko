using SQLite;

namespace QRScanner.App.Data.Entities;

[Table("Corob")]
public sealed class CorobEntity
{
    [PrimaryKey, AutoIncrement, Column("_id")]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Capacity { get; set; } = 12;

    public bool IsShipped { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

using SQLite;

namespace QRScanner.App.Data.Legacy;

[Table("CorobSQL")]
internal sealed class LegacyCorobRow
{
    [PrimaryKey, Column("_id")]
    public int Id { get; set; }

    public string? CorobName { get; set; }

    public bool IsScanned { get; set; }
}

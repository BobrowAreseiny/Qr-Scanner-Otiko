using SQLite;

namespace QRScanner.App.Data.Entities;

[Table("TransferQueue")]
public sealed class TransferQueueEntity
{
    [PrimaryKey, AutoIncrement, Column("_id")]
    public int Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string LocalPath { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Status { get; set; } = "pending";

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }
}

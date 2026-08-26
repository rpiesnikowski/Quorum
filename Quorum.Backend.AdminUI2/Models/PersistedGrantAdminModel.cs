namespace Quorum.Backend.AdminUI2.Models;

public class PersistedGrantAdminModel
{
    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? SubjectId { get; set; }
    public string? SessionId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? Expiration { get; set; }
    public DateTime? ConsumedTime { get; set; }
    public string? Data { get; set; }

    public bool IsExpired => Expiration.HasValue && Expiration.Value < DateTime.UtcNow;
    public bool IsConsumed => ConsumedTime.HasValue;
}

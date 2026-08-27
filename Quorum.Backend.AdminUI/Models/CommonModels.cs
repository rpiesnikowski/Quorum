namespace Quorum.Backend.AdminUI.Models;

public class ClientSecretModel
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime? Expiration { get; set; }
    public string Type { get; set; } = "SharedSecret";
    public DateTime Created { get; set; } = DateTime.UtcNow;
}

public class ClientClaimModel
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class UserClaimModel
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }

    public PagedResult() { }

    public PagedResult(IReadOnlyList<T> items, int totalCount, int pageIndex, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageIndex = pageIndex;
        PageSize = pageSize;
    }
}

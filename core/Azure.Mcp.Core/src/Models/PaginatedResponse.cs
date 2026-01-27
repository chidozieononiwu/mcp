namespace Azure.Mcp.Core.Models
{
    public record PaginatedResponse<T>(List<T> Items, string? NextCursor, int TotalCount);
}

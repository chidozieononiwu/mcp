namespace Azure.Mcp.Core.Models
{
    public class PaginationParams
    {
        public string? NextCursor { get; set; } = null;
        public int? PageSize { get; set; }
    }
}

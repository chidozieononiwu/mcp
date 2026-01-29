namespace Azure.Mcp.Core.Models
{
    public class PaginationParams
    {
        public string? NextCursor { get; set; } = null;
        public int PageSize { get; set; } = 50;

        public void Validate()
        {
            if (PageSize < 1)
            {
                PageSize = 1;
            }
            else if (PageSize > 100)
            {
                PageSize = 100;
            }
        }

        public int GetEffectivePageSize() => PageSize;
    }
}

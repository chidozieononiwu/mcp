using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Mcp.Core.Models
{
    public record PaginatedResults<T>(List<T> Results, string? NextCursor);
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Mcp.Core.Options;

/// <summary>
/// Base options class for commands that support paginated list operations.
/// Provides common pagination parameters including page size and continuation token.
/// </summary>
public class PaginatedListOptions : SubscriptionOptions
{
    /// <summary>
    /// Maximum number of results to return per page. If not specified, service-specific defaults will be used (typically 50).
    /// </summary>
    [JsonPropertyName("page-size")]
    public int? PageSize { get; set; }

    /// <summary>
    /// Continuation token from a previous request to get the next page of results.
    /// </summary>
    [JsonPropertyName("skip-token")]
    public string? SkipToken { get; set; }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Mcp.Tools.Cosmos.Options;

public class CosmosListOptions : BaseDatabaseOptions
{
    /// <summary>
    /// Maximum number of results to return per page. If not specified, defaults to 50.
    /// </summary>
    [JsonPropertyName("page-size")]
    public int? PageSize { get; set; }

    /// <summary>
    /// Continuation token from a previous request to get the next page of results.
    /// </summary>
    [JsonPropertyName("skip-token")]
    public string? SkipToken { get; set; }
}

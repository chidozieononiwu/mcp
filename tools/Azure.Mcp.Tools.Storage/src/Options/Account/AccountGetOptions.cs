// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Models;

namespace Azure.Mcp.Tools.Storage.Options.Account;

public class AccountGetOptions : BaseStorageOptions
{
    public PaginationParams Pagination { get; set; } = new();
}

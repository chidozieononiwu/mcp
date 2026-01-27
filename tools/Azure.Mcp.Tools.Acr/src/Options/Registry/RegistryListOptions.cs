// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Models;

namespace Azure.Mcp.Tools.Acr.Options.Registry;

public class RegistryListOptions : BaseAcrOptions
{
    // Inherits subscription and resource group filtering from base class
    public PaginationParams Pagination { get; set; } = new();
}

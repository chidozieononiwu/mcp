// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Azure.Mcp.Core.Models;
using Azure.Mcp.Tools.Acr.Commands.Registry;
using Azure.Mcp.Tools.Acr.Models;
using Azure.Mcp.Tools.Acr.Services.Models;

namespace Azure.Mcp.Tools.Acr.Commands;

[JsonSerializable(typeof(PaginatedResponse<AcrRegistryInfo>))]
[JsonSerializable(typeof(RegistryRepositoryListCommand.RegistryRepositoryListCommandResult))]
[JsonSerializable(typeof(Models.AcrRegistryInfo))]
[JsonSerializable(typeof(ContainerRegistryData))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class AcrJsonContext : JsonSerializerContext
{
}

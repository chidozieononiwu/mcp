// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Mcp.Core.Services.Pagination;
using Azure.Mcp.Tools.Acr.Models;
using Azure.Mcp.Tools.Acr.Options.Registry;
using Azure.Mcp.Tools.Acr.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Mcp.Core.Commands;
using Microsoft.Mcp.Core.Extensions;
using Microsoft.Mcp.Core.Models.Command;
using Microsoft.Mcp.Core.Models.Option;
using Microsoft.Mcp.Core.Services.Pagination;

namespace Azure.Mcp.Tools.Acr.Commands.Registry;

public sealed class RegistryListCommand : BaseAcrCommand<RegistryListOptions>
{
    private const string CommandTitle = "List Container Registries";
    private const string OperationName = "acr.registry.list";
    private readonly ILogger<RegistryListCommand> _logger;
    private readonly IAcrService _acrService;
    private readonly IPaginationService? _paginationService;

    public RegistryListCommand(ILogger<RegistryListCommand> logger, IAcrService acrService)
        : this(logger, acrService, null)
    {
    }

    public RegistryListCommand(ILogger<RegistryListCommand> logger, IAcrService acrService, IPaginationService? paginationService)
    {
        _logger = logger;
        _acrService = acrService;
        _paginationService = paginationService;
    }

    public override string Id => "796f8778-2fa7-4343-87ad-06bdcf6b296c";

    public override string Name => "list";

    public override string Description =>
        $"""
        List Azure Container Registries in a subscription. Optionally filter by resource group. Each registry result
        includes: name, location, loginServer, skuName, skuTier. If no registries are found the tool returns null results
        (consistent with other list commands).
        """;

    public override string Title => CommandTitle;

    public override ToolMetadata Metadata => new()
    {
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        ReadOnly = true,
        LocalRequired = false,
        Secret = false,
        SupportsPagination = _paginationService is not null
    };

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.Options.Add(OptionDefinitions.Pagination.Cursor.AsOptional());
    }

    protected override RegistryListOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.Cursor = parseResult.GetValueOrDefault<string>(OptionDefinitions.Pagination.Cursor.Name);
        return options;
    }

    public override async Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult, CancellationToken cancellationToken)
    {
        if (!Validate(parseResult.CommandResult, context.Response).IsValid)
        {
            return context.Response;
        }

        var options = BindOptions(parseResult);

        try
        {
            if (_paginationService is not null)
            {
                if (context.SupportsApps)
                {
                    return await GetPagedResourceUriAsync(context, options, cancellationToken);
                }

                return await GetPagedResultsAsync(context, options, cancellationToken);
            }

            var registries = await _acrService.ListRegistries(
                options.Subscription!,
                options.ResourceGroup,
                options.Tenant,
                options.RetryPolicy,
                cancellationToken);

            context.Response.Results = ResponseResult.Create(new RegistryListCommandResult(registries?.Results ?? [], registries?.AreResultsTruncated ?? false), AcrJsonContext.Default.RegistryListCommandResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error listing container registries. Subscription: {Subscription}, ResourceGroup: {ResourceGroup}, Options: {@Options}",
                options.Subscription, options.ResourceGroup, options);
            HandleException(context, ex);
        }

        return context.Response;
    }

    private async Task<CommandResponse> GetPagedResourceUriAsync(CommandContext context, RegistryListOptions options, CancellationToken cancellationToken)
    {
        var fingerprint = ComputeFingerprint(options);

        PageFetchDelegate fetcher = async (nativeState, ct) =>
        {
            var adapter = new KqlPaginationAdapter<AcrRegistryInfo>(
                skipToken => _acrService.ListRegistriesPaged(
                    options.Subscription!, options.ResourceGroup, options.Tenant, options.RetryPolicy, skipToken, ct));

            var page = nativeState is null
                ? await adapter.FetchFirstPageAsync(ct)
                : await adapter.FetchNextPageAsync(nativeState, ct);

            var itemsJson = JsonSerializer.Serialize(page.Items, AcrJsonContext.Default.ListAcrRegistryInfo);
            return new PaginationPageData(itemsJson, page.NativeState);
        };

        var cursorId = await _paginationService!.SaveCursorAsync(
            KqlPaginationAdapter<AcrRegistryInfo>.ProviderName, OperationName,
            fingerprint, PaginationResource.InitialNativeState,
            fetcher: fetcher,
            cancellationToken: cancellationToken);

        var resourceUri = $"{PaginationResource.UriPrefix}{cursorId}";

        context.Response.Results = ResponseResult.Create(
            new RegistryListResourceResult(resourceUri, new ResponseMeta(new ResponseMetaUi(TableAppResource.UriPrefix))),
            AcrJsonContext.Default.RegistryListResourceResult);

        return context.Response;
    }

    private async Task<CommandResponse> GetPagedResultsAsync(CommandContext context, RegistryListOptions options, CancellationToken cancellationToken)
    {
        var fingerprint = ComputeFingerprint(options);

        var adapter = new KqlPaginationAdapter<AcrRegistryInfo>(
            skipToken => _acrService.ListRegistriesPaged(
                options.Subscription!, options.ResourceGroup, options.Tenant, options.RetryPolicy, skipToken, cancellationToken));

        PageResult<AcrRegistryInfo>? pagedResult;
        if (string.IsNullOrEmpty(options.Cursor))
        {
            pagedResult = await adapter.FetchFirstPageAsync(cancellationToken);
        }
        else
        {
            var cursorRecord = await _paginationService!.LoadAndValidateCursorAsync(
                options.Cursor!, fingerprint, cancellationToken);
            pagedResult = await adapter.FetchNextPageAsync(cursorRecord.NativeState, cancellationToken);
        }

        string? nextCursor = null;
        if (pagedResult.NativeState is not null)
        {
            nextCursor = await _paginationService!.SaveCursorAsync(
                KqlPaginationAdapter<AcrRegistryInfo>.ProviderName, OperationName,
                fingerprint, pagedResult.NativeState,
                cancellationToken: cancellationToken);
        }

        context.Response.Results = ResponseResult.Create(
            new RegistryListCommandResult(pagedResult.Items, false, nextCursor),
            AcrJsonContext.Default.RegistryListCommandResult);

        return context.Response;
    }

    private string ComputeFingerprint(RegistryListOptions options) =>
        _paginationService!.ComputeRequestFingerprint(new Dictionary<string, string?>
        {
            ["operation"] = OperationName,
            ["subscription"] = options.Subscription,
            ["resourceGroup"] = options.ResourceGroup,
        });

    internal record RegistryListCommandResult(List<AcrRegistryInfo> Registries, bool AreResultsTruncated, string? NextCursor = null);

    internal record RegistryListResourceResult(string PagedResourceUri, [property: JsonPropertyName("_meta")] ResponseMeta? Meta = null);

    internal record ResponseMeta(ResponseMetaUi? Ui = null);

    internal record ResponseMetaUi(string? ResourceUri = null);
}

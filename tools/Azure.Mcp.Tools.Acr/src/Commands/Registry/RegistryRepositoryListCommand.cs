// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Mcp.Core.Services.Pagination;
using Azure.Mcp.Tools.Acr.Options;
using Azure.Mcp.Tools.Acr.Options.Registry;
using Azure.Mcp.Tools.Acr.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Mcp.Core.Commands;
using Microsoft.Mcp.Core.Extensions;
using Microsoft.Mcp.Core.Models.Command;
using Microsoft.Mcp.Core.Models.Option;
using Microsoft.Mcp.Core.Services.Pagination;

namespace Azure.Mcp.Tools.Acr.Commands.Registry;

public sealed class RegistryRepositoryListCommand : BaseAcrCommand<RegistryRepositoryListOptions>
{
    private const string CommandTitle = "List Container Registry Repositories";
    private const string OperationName = "acr.registry.repository.list";
    private readonly ILogger<RegistryRepositoryListCommand> _logger;
    private readonly IAcrService _acrService;
    private readonly IPaginationService? _paginationService;

    public RegistryRepositoryListCommand(ILogger<RegistryRepositoryListCommand> logger, IAcrService acrService)
        : this(logger, acrService, null)
    {
    }

    public RegistryRepositoryListCommand(ILogger<RegistryRepositoryListCommand> logger, IAcrService acrService, IPaginationService? paginationService)
    {
        _logger = logger;
        _acrService = acrService;
        _paginationService = paginationService;
    }

    public override string Id => "adc6eb20-ad98-4624-954d-61581f6fbca9";

    public override string Name => "list";

    public override string Description =>
        """
        List repositories in Azure Container Registries. By default, lists repositories for all registries in the subscription.
        You can narrow the scope using --resource-group and/or --registry to list repositories for a specific registry only.
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
        command.Options.Add(AcrOptionDefinitions.Registry);
        command.Options.Add(OptionDefinitions.Pagination.Cursor.AsOptional());
    }

    protected override RegistryRepositoryListOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.Registry ??= parseResult.GetValueOrDefault<string>(AcrOptionDefinitions.Registry.Name);
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
            if (_paginationService is not null && !string.IsNullOrEmpty(options.Registry))
            {
                if (context.SupportsApps)
                {
                    return await GetPagedResourceUriAsync(context, options, cancellationToken);
                }

                return await GetPagedResultsAsync(context, options, cancellationToken);
            }

            var map = await _acrService.ListRegistryRepositories(
                options.Subscription!,
                options.ResourceGroup,
                options.Registry,
                options.Tenant,
                options.RetryPolicy,
                cancellationToken);

            context.Response.Results = ResponseResult.Create(new RegistryRepositoryListCommandResult(map ?? []), AcrJsonContext.Default.RegistryRepositoryListCommandResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error listing ACR repositories. Subscription: {Subscription}, ResourceGroup: {ResourceGroup}, Registry: {Registry}",
                options.Subscription, options.ResourceGroup, options.Registry);
            HandleException(context, ex);
        }

        return context.Response;
    }

    private async Task<CommandResponse> GetPagedResourceUriAsync(CommandContext context, RegistryRepositoryListOptions options, CancellationToken cancellationToken)
    {
        var fingerprint = ComputeFingerprint(options);

        PageFetchDelegate fetcher = async (nativeState, ct) =>
        {
            var adapter = new KqlPaginationAdapter<string>(
                continuationToken => _acrService.ListRegistryRepositoriesPaged(
                    options.Subscription!, options.Registry!, options.ResourceGroup,
                    options.Tenant, options.RetryPolicy, continuationToken, ct));

            var page = nativeState is null
                ? await adapter.FetchFirstPageAsync(ct)
                : await adapter.FetchNextPageAsync(nativeState, ct);

            var itemsJson = JsonSerializer.Serialize(page.Items, AcrJsonContext.Default.ListString);
            return new PaginationPageData(itemsJson, page.NativeState);
        };

        var cursorId = await _paginationService!.SaveCursorAsync(
            KqlPaginationAdapter<string>.ProviderName, OperationName,
            fingerprint, PaginationResource.InitialNativeState,
            fetcher: fetcher,
            cancellationToken: cancellationToken);

        var resourceUri = $"{PaginationResource.UriPrefix}{cursorId}";

        context.Response.Results = ResponseResult.Create(
            new RegistryRepositoryListResourceResult(resourceUri, new RegistryListCommand.ResponseMeta(new RegistryListCommand.ResponseMetaUi(TableAppResource.UriPrefix))),
            AcrJsonContext.Default.RegistryRepositoryListResourceResult);

        return context.Response;
    }

    private async Task<CommandResponse> GetPagedResultsAsync(CommandContext context, RegistryRepositoryListOptions options, CancellationToken cancellationToken)
    {
        var fingerprint = ComputeFingerprint(options);

        var adapter = new KqlPaginationAdapter<string>(
            continuationToken => _acrService.ListRegistryRepositoriesPaged(
                options.Subscription!, options.Registry!, options.ResourceGroup,
                options.Tenant, options.RetryPolicy, continuationToken, cancellationToken));

        PageResult<string>? pagedResult;
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
                KqlPaginationAdapter<string>.ProviderName, OperationName,
                fingerprint, pagedResult.NativeState,
                cancellationToken: cancellationToken);
        }

        context.Response.Results = ResponseResult.Create(
            new RegistryRepositoryListPagedResult(options.Registry!, pagedResult.Items, nextCursor),
            AcrJsonContext.Default.RegistryRepositoryListPagedResult);

        return context.Response;
    }

    private string ComputeFingerprint(RegistryRepositoryListOptions options) =>
        _paginationService!.ComputeRequestFingerprint(new Dictionary<string, string?>
        {
            ["operation"] = OperationName,
            ["subscription"] = options.Subscription,
            ["resourceGroup"] = options.ResourceGroup,
            ["registry"] = options.Registry,
        });

    internal record RegistryRepositoryListCommandResult(Dictionary<string, List<string>> RepositoriesByRegistry);

    internal record RegistryRepositoryListPagedResult(string Registry, List<string> Repositories, string? NextCursor = null);

    internal record RegistryRepositoryListResourceResult(string PagedResourceUri, [property: JsonPropertyName("_meta")] RegistryListCommand.ResponseMeta? Meta = null);
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Mcp.Core.Services.Pagination;
using Azure.Mcp.Tools.Storage.Commands.Account;
using Azure.Mcp.Tools.Storage.Options;
using Azure.Mcp.Tools.Storage.Options.Blob.Container;
using Azure.Mcp.Tools.Storage.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Mcp.Core.Commands;
using Microsoft.Mcp.Core.Extensions;
using Microsoft.Mcp.Core.Models.Command;
using Microsoft.Mcp.Core.Models.Option;
using Microsoft.Mcp.Core.Services.Pagination;

namespace Azure.Mcp.Tools.Storage.Commands.Blob.Container;

public sealed class ContainerGetCommand : BaseStorageCommand<ContainerGetOptions>
{
    private const string CommandTitle = "Get Storage Container Details";
    private const string OperationName = "storage.blob.container.get";
    private readonly ILogger<ContainerGetCommand> _logger;
    private readonly IStorageService _storageService;
    private readonly IPaginationService? _paginationService;

    public ContainerGetCommand(ILogger<ContainerGetCommand> logger, IStorageService storageService)
        : this(logger, storageService, null)
    {
    }

    public ContainerGetCommand(ILogger<ContainerGetCommand> logger, IStorageService storageService, IPaginationService? paginationService)
    {
        _logger = logger;
        _storageService = storageService;
        _paginationService = paginationService;
    }

    public override string Id => "e96eb850-abb8-431d-bdc6-7ccd0a24838e";

    public override string Name => "get";

    public override string Description =>
        $"""
        Show/list containers in a storage account. Use this tool to list all blob containers in the storage account or show details for a specific Storage container. Displays container properties including access policies, lease status, and metadata. If no container specified, shows all containers in the storage account. Required: account <account>, subscription <subscription>. Optional: container <container>, tenant <tenant>. Returns: container name, lastModified, leaseStatus, publicAccessLevel, metadata, and container properties. Do not use this tool to list blobs in a container.
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
        command.Options.Add(StorageOptionDefinitions.Container.AsOptional());
        command.Options.Add(OptionDefinitions.Pagination.Cursor.AsOptional());
    }

    protected override ContainerGetOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.Container = parseResult.GetValueOrDefault<string>(StorageOptionDefinitions.Container.Name);
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
            if (_paginationService is not null && string.IsNullOrEmpty(options.Container))
            {
                if (context.SupportsApps)
                {
                    return await GetPagedResourceUriAsync(context, options, cancellationToken);
                }

                return await GetPagedResultsAsync(context, options, cancellationToken);
            }

            var containers = await _storageService.GetContainerDetails(
                options.Account!,
                options.Container,
                options.Subscription!,
                options.Tenant,
                options.RetryPolicy,
                cancellationToken
            );

            context.Response.Results = ResponseResult.Create(new ContainerGetCommandResult(containers ?? []), StorageJsonContext.Default.ContainerGetCommandResult);
            return context.Response;
        }
        catch (Exception ex)
        {
            if (options.Container is null)
            {
                _logger.LogError(ex, "Error listing container details. Account: {Account}.", options.Account);
            }
            else
            {
                _logger.LogError(ex, "Error getting container details. Account: {Account}, Container: {Container}.", options.Account, options.Container);
            }
            HandleException(context, ex);
            return context.Response;
        }
    }

    private async Task<CommandResponse> GetPagedResourceUriAsync(CommandContext context, ContainerGetOptions options, CancellationToken cancellationToken)
    {
        var fingerprint = ComputeFingerprint(options);

        PageFetchDelegate fetcher = async (nativeState, ct) =>
        {
            var adapter = new KqlPaginationAdapter<ContainerInfo>(
                continuationToken => _storageService.GetContainerDetailsPaged(
                    options.Account!, options.Subscription!, options.Tenant, options.RetryPolicy, continuationToken, ct));

            var page = nativeState is null
                ? await adapter.FetchFirstPageAsync(ct)
                : await adapter.FetchNextPageAsync(nativeState, ct);

            var itemsJson = JsonSerializer.Serialize(page.Items, StorageJsonContext.Default.ListContainerInfo);
            return new PaginationPageData(itemsJson, page.NativeState);
        };

        var cursorId = await _paginationService!.SaveCursorAsync(
            KqlPaginationAdapter<ContainerInfo>.ProviderName, OperationName,
            fingerprint, PaginationResource.InitialNativeState,
            fetcher: fetcher,
            cancellationToken: cancellationToken);

        var resourceUri = $"{PaginationResource.UriPrefix}{cursorId}";

        context.Response.Results = ResponseResult.Create(
            new ContainerGetResourceResult(resourceUri, new AccountGetCommand.ResponseMeta(new AccountGetCommand.ResponseMetaUi(TableAppResource.UriPrefix))),
            StorageJsonContext.Default.ContainerGetResourceResult);

        return context.Response;
    }

    private async Task<CommandResponse> GetPagedResultsAsync(CommandContext context, ContainerGetOptions options, CancellationToken cancellationToken)
    {
        var fingerprint = ComputeFingerprint(options);

        var adapter = new KqlPaginationAdapter<ContainerInfo>(
            continuationToken => _storageService.GetContainerDetailsPaged(
                options.Account!, options.Subscription!, options.Tenant, options.RetryPolicy, continuationToken, cancellationToken));

        PageResult<ContainerInfo>? pagedResult;
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
                KqlPaginationAdapter<ContainerInfo>.ProviderName, OperationName,
                fingerprint, pagedResult.NativeState,
                cancellationToken: cancellationToken);
        }

        context.Response.Results = ResponseResult.Create(
            new ContainerGetCommandResult(pagedResult.Items, nextCursor),
            StorageJsonContext.Default.ContainerGetCommandResult);

        return context.Response;
    }

    private string ComputeFingerprint(ContainerGetOptions options) =>
        _paginationService!.ComputeRequestFingerprint(new Dictionary<string, string?>
        {
            ["operation"] = OperationName,
            ["subscription"] = options.Subscription,
            ["account"] = options.Account,
        });

    internal record ContainerGetCommandResult(List<ContainerInfo> Containers, string? NextCursor = null);

    internal record ContainerGetResourceResult(string PagedResourceUri, [property: JsonPropertyName("_meta")] AccountGetCommand.ResponseMeta? Meta = null);
}

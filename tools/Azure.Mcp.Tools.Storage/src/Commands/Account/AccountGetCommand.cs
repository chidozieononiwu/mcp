// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Commands.Subscription;
using Azure.Mcp.Core.Extensions;
using Azure.Mcp.Core.Models.Option;
using Azure.Mcp.Tools.Storage.Models;
using Azure.Mcp.Tools.Storage.Options;
using Azure.Mcp.Tools.Storage.Options.Account;
using Azure.Mcp.Tools.Storage.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Mcp.Core.Commands;
using Microsoft.Mcp.Core.Models.Command;
using Microsoft.Mcp.Core.Models.Option;

namespace Azure.Mcp.Tools.Storage.Commands.Account;

public sealed class AccountGetCommand(ILogger<AccountGetCommand> logger, IStorageService storageService) : SubscriptionCommand<AccountGetOptions>()
{
    private const string CommandTitle = "Get Storage Account Details";
    private readonly ILogger<AccountGetCommand> _logger = logger;
    private readonly IStorageService _storageService = storageService;

    public override string Id => "eb2363f1-f21f-45fc-ad63-bacfbae8c45c";

    public override string Name => "get";

    public override string Description =>
        """
        Retrieves detailed information about Azure Storage accounts, including account name, location, SKU, kind, hierarchical namespace status, HTTPS-only settings, and blob public access configuration. If a specific account name is not provided, the command will return details for all accounts in a subscription.
        """;

    public override string Title => CommandTitle;

    public override ToolMetadata Metadata => new()
    {
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        ReadOnly = true,
        LocalRequired = false,
        Secret = false
    };

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.Options.Add(StorageOptionDefinitions.Account.AsOptional());
        command.Options.Add(OptionDefinitions.Pagination.PageSize);
        command.Options.Add(OptionDefinitions.Pagination.SkipToken);
    }

    protected override AccountGetOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.Account = parseResult.GetValueOrDefault<string>(StorageOptionDefinitions.Account.Name);
        options.PageSize = parseResult.GetValueOrDefault(OptionDefinitions.Pagination.PageSize);
        options.SkipToken = parseResult.GetValueOrDefault(OptionDefinitions.Pagination.SkipToken);
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
            if (string.IsNullOrEmpty(options.Account))
            {
                // List all accounts using paginated method
                var paginatedResults = await _storageService.ListAccountDetails(
                    options.Subscription!,
                    limit: options.PageSize ?? 50,
                    skipToken: options.SkipToken,
                    options.Tenant,
                    options.RetryPolicy,
                    cancellationToken);

                // Return paginated result with continuation token
                context.Response.Results = ResponseResult.Create(
                    new AccountGetCommandPaginatedResult(paginatedResults.Results, paginatedResults.ContinuationToken),
                    StorageJsonContext.Default.AccountGetCommandPaginatedResult);
            }
            else
            {
                // Get specific account
                var accounts = await _storageService.GetAccountDetails(
                    options.Account,
                    options.Subscription!,
                    options.Tenant,
                    options.RetryPolicy,
                    cancellationToken);

                context.Response.Results = ResponseResult.Create(
                    new AccountGetCommandResult(accounts.Results, accounts.AreResultsTruncated),
                    StorageJsonContext.Default.AccountGetCommandResult);
            }
        }
        catch (Exception ex)
        {
            if (options.Account is null)
            {
                _logger.LogError(ex, "Error listing account details. Subscription: {Subscription}, Options: {@Options}", options.Subscription, options);
            }
            else
            {
                _logger.LogError(ex, "Error getting storage account details. Account: {Account}, Subscription: {Subscription}, Options: {@Options}",
                    options.Account, options.Subscription, options);
            }
            HandleException(context, ex);
        }

        return context.Response;
    }

    // Strongly-typed result record
    internal record AccountGetCommandResult(List<StorageAccountInfo> Accounts, bool AreResultsTruncated);

    // Strongly-typed result record with pagination support
    internal record AccountGetCommandPaginatedResult(List<StorageAccountInfo> Accounts, string? ContinuationToken);
}

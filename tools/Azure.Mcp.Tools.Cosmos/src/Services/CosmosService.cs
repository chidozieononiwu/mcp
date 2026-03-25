// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Core.Options;
using Azure.Mcp.Core.Services.Azure;
using Azure.Mcp.Core.Services.Azure.Authentication;
using Azure.Mcp.Core.Services.Azure.Subscription;
using Azure.Mcp.Core.Services.Azure.Tenant;
using Azure.Mcp.Core.Services.Caching;
using Azure.ResourceManager.CosmosDB;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Mcp.Core.Models;

namespace Azure.Mcp.Tools.Cosmos.Services;

public sealed class CosmosService(ISubscriptionService subscriptionService, ITenantService tenantService, ICacheService cacheService, IHttpClientFactory httpClientFactory, ILogger<CosmosService> logger)
    : BaseAzureService(tenantService), ICosmosService, IAsyncDisposable
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
    private readonly ITenantService _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly ICacheService _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    private readonly ILogger<CosmosService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private const string CacheGroup = "cosmos";
    private const string CosmosClientsCacheKeyPrefix = "clients_";
    private const string CosmosDatabasesCacheKeyPrefix = "databases_";
    private const string CosmosContainersCacheKeyPrefix = "containers_";
    private static readonly TimeSpan s_cacheDurationClients = CacheDurations.AuthenticatedClient;
    private static readonly TimeSpan s_cacheDurationResources = CacheDurations.ServiceData;
    private bool _disposed;

    private async Task<CosmosDBAccountResource> GetCosmosAccountAsync(
        string subscription,
        string accountName,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredParameters((nameof(subscription), subscription), (nameof(accountName), accountName));

        var subscriptionResource = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy, cancellationToken);

        await foreach (var account in subscriptionResource.GetCosmosDBAccountsAsync(cancellationToken))
        {
            if (account.Data.Name == accountName)
            {
                return account;
            }
        }
        throw new Exception($"Cosmos DB account '{accountName}' not found in subscription '{subscription}'");
    }

    private async Task<CosmosClient> CreateCosmosClientWithAuth(
        string accountName,
        string subscription,
        AuthMethod authMethod,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        // Enable bulk execution and distributed tracing telemetry features once they are supported by the Microsoft.Azure.Cosmos.Aot package.
        // var clientOptions = new CosmosClientOptions { AllowBulkExecution = true };
        // clientOptions.CosmosClientTelemetryOptions.DisableDistributedTracing = false;
        var clientOptions = new CosmosClientOptions();
        clientOptions.CustomHandlers.Add(new UserPolicyRequestHandler(UserAgent));

        if (retryPolicy != null)
        {
            clientOptions.MaxRetryAttemptsOnRateLimitedRequests = retryPolicy.MaxRetries;
            clientOptions.MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(retryPolicy.MaxDelaySeconds);
        }

        clientOptions.HttpClientFactory = () => _httpClientFactory.CreateClient();

        CosmosClient cosmosClient;
        switch (authMethod)
        {
            case AuthMethod.Key:
                var cosmosAccount = await GetCosmosAccountAsync(subscription, accountName, tenant, cancellationToken: cancellationToken);
                var keys = await cosmosAccount.GetKeysAsync(cancellationToken);
                cosmosClient = new(GetCosmosBaseUri(accountName), keys.Value.PrimaryMasterKey, clientOptions);
                break;

            case AuthMethod.Credential:
            default:
                cosmosClient = new(GetCosmosBaseUri(accountName), await GetCredential(tenant, cancellationToken), clientOptions);
                break;
        }

        // Validate the client by performing a lightweight operation
        await ValidateCosmosClientAsync(cosmosClient, cancellationToken);

        return cosmosClient;
    }

    private string GetCosmosBaseUri(string accountName)
    {
        return _tenantService.CloudConfiguration.CloudType switch
        {
            AzureCloudConfiguration.AzureCloud.AzurePublicCloud => $"https://{accountName}.documents.azure.com:443/",
            AzureCloudConfiguration.AzureCloud.AzureUSGovernmentCloud => $"https://{accountName}.documents.azure.us:443/",
            AzureCloudConfiguration.AzureCloud.AzureChinaCloud => $"https://{accountName}.documents.azure.cn:443/",
            _ => $"https://{accountName}.documents.azure.com:443/"
        };
    }

    private async Task ValidateCosmosClientAsync(CosmosClient client, CancellationToken cancellationToken = default)
    {
        // Perform a lightweight operation to validate the client
        await client.ReadAccountAsync().WaitAsync(cancellationToken);
    }

    private async Task<CosmosClient> GetCosmosClientAsync(
        string accountName,
        string subscription,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredParameters((nameof(accountName), accountName), (nameof(subscription), subscription));

        var key = CosmosClientsCacheKeyPrefix + accountName + "_" + authMethod;
        var cosmosClient = await _cacheService.GetAsync<CosmosClient>(CacheGroup, key, s_cacheDurationClients, cancellationToken);
        if (cosmosClient != null)
            return cosmosClient;

        cosmosClient = await CreateCosmosClientWithAuth(
            accountName,
            subscription,
            authMethod,
            tenant,
            retryPolicy,
            cancellationToken);

        await _cacheService.SetAsync(CacheGroup, key, cosmosClient, s_cacheDurationClients, cancellationToken);
        return cosmosClient;
    }

    public async Task<List<string>> GetCosmosAccounts(string subscription, string? tenant = null, RetryPolicyOptions? retryPolicy = null, CancellationToken cancellationToken = default)
    {
        ValidateRequiredParameters((nameof(subscription), subscription));

        var allAccounts = new List<string>();
        string? continuationToken = null;

        do
        {
            var result = await GetPaginatedCosmosAccounts(
                subscription,
                limit: null,
                continuationToken,
                tenant,
                retryPolicy,
                cancellationToken);

            allAccounts.AddRange(result.Results);
            continuationToken = result.NextCursor;
        }
        while (continuationToken != null);

        return allAccounts;
    }

    public async Task<PaginatedResults<string>> GetPaginatedCosmosAccounts(
        string subscription,
        int? limit = null,
        string? continuationToken = null,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredParameters((nameof(subscription), subscription));

        var subscriptionResource = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy, cancellationToken);
        var accounts = new List<string>();
        int pageSize = limit ?? 50;

        // Use AsPages() to get access to ARM's native continuation tokens
        var pages = subscriptionResource.GetCosmosDBAccounts(cancellationToken: cancellationToken)
            .AsPages(continuationToken: continuationToken, pageSizeHint: pageSize);

        foreach (var page in pages)
        {
            foreach (var account in page.Values)
            {
                if (account?.Data?.Name != null)
                {
                    accounts.Add(account.Data.Name);
                }
            }

            return new PaginatedResults<string>(accounts, page.ContinuationToken);
        }

        return new PaginatedResults<string>(accounts, null);
    }

    public async Task<List<string>> ListDatabases(
        string accountName,
        string subscription,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredParameters((nameof(accountName), accountName), (nameof(subscription), subscription));

        var cacheKey = CosmosDatabasesCacheKeyPrefix + accountName;

        var cachedDatabases = await _cacheService.GetAsync<List<string>>(CacheGroup, cacheKey, s_cacheDurationResources, cancellationToken);
        if (cachedDatabases != null)
        {
            return cachedDatabases;
        }

        var allDatabases = new List<string>();
        string? continuationToken = null;

        do
        {
            var result = await ListPaginatedDatabases(
                accountName,
                subscription,
                limit: null,
                continuationToken,
                authMethod,
                tenant,
                retryPolicy,
                cancellationToken);

            allDatabases.AddRange(result.Results);
            continuationToken = result.NextCursor;
        }
        while (continuationToken != null);

        await _cacheService.SetAsync(CacheGroup, cacheKey, allDatabases, s_cacheDurationResources, cancellationToken);
        return allDatabases;
    }

    public async Task<PaginatedResults<string>> ListPaginatedDatabases(
        string accountName,
        string subscription,
        int? limit = null,
        string? continuationToken = null,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredParameters((nameof(accountName), accountName), (nameof(subscription), subscription));

        var client = await GetCosmosClientAsync(accountName, subscription, authMethod, tenant, retryPolicy, cancellationToken);
        var databases = new List<string>();
        int pageSize = limit ?? 50;

        var queryOptions = new QueryRequestOptions
        {
            MaxItemCount = pageSize
        };

        var iterator = client.GetDatabaseQueryStreamIterator(continuationToken: continuationToken, requestOptions: queryOptions);

        if (iterator.HasMoreResults)
        {
            using ResponseMessage dbResponse = await iterator.ReadNextAsync(cancellationToken);
            if (!dbResponse.IsSuccessStatusCode)
            {
                throw new Exception(dbResponse.ErrorMessage);
            }

            using JsonDocument dbsQueryResultDoc = JsonDocument.Parse(dbResponse.Content);
            if (dbsQueryResultDoc.RootElement.TryGetProperty("Databases", out JsonElement documentsElement))
            {
                foreach (JsonElement databaseElement in documentsElement.EnumerateArray())
                {
                    string? databaseId = databaseElement.GetProperty("id").GetString();
                    if (!string.IsNullOrEmpty(databaseId))
                    {
                        databases.Add(databaseId);
                    }
                }
            }

            return new PaginatedResults<string>(databases, dbResponse.ContinuationToken);
        }

        return new PaginatedResults<string>(databases, null);
    }

    public async Task<List<string>> ListContainers(
        string accountName,
        string databaseName,
        string subscription,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredParameters((nameof(accountName), accountName), (nameof(databaseName), databaseName), (nameof(subscription), subscription));

        var cacheKey = CosmosContainersCacheKeyPrefix + accountName + "_" + databaseName;

        var cachedContainers = await _cacheService.GetAsync<List<string>>(CacheGroup, cacheKey, s_cacheDurationResources, cancellationToken);
        if (cachedContainers != null)
        {
            return cachedContainers;
        }

        var allContainers = new List<string>();
        string? continuationToken = null;

        do
        {
            var result = await ListPaginatedContainers(
                accountName,
                databaseName,
                subscription,
                limit: null,
                continuationToken,
                authMethod,
                tenant,
                retryPolicy,
                cancellationToken);

            allContainers.AddRange(result.Results);
            continuationToken = result.NextCursor;
        }
        while (continuationToken != null);

        await _cacheService.SetAsync(CacheGroup, cacheKey, allContainers, s_cacheDurationResources, cancellationToken);
        return allContainers;
    }

    public async Task<PaginatedResults<string>> ListPaginatedContainers(
        string accountName,
        string databaseName,
        string subscription,
        int? limit = null,
        string? continuationToken = null,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredParameters((nameof(accountName), accountName), (nameof(databaseName), databaseName), (nameof(subscription), subscription));

        var client = await GetCosmosClientAsync(accountName, subscription, authMethod, tenant, retryPolicy, cancellationToken);
        var containers = new List<string>();
        int pageSize = limit ?? 50;

        var database = client.GetDatabase(databaseName);

        var queryOptions = new QueryRequestOptions
        {
            MaxItemCount = pageSize
        };

        var iterator = database.GetContainerQueryStreamIterator(continuationToken: continuationToken, requestOptions: queryOptions);

        if (iterator.HasMoreResults)
        {
            using ResponseMessage containerResponse = await iterator.ReadNextAsync(cancellationToken);
            if (!containerResponse.IsSuccessStatusCode)
            {
                throw new Exception(containerResponse.ErrorMessage);
            }

            using JsonDocument containersQueryResultDoc = JsonDocument.Parse(containerResponse.Content);
            if (containersQueryResultDoc.RootElement.TryGetProperty("DocumentCollections", out JsonElement containersElement))
            {
                foreach (JsonElement containerElement in containersElement.EnumerateArray())
                {
                    string? containerId = containerElement.GetProperty("id").GetString();
                    if (!string.IsNullOrEmpty(containerId))
                    {
                        containers.Add(containerId);
                    }
                }
            }

            return new PaginatedResults<string>(containers, containerResponse.ContinuationToken);
        }

        return new PaginatedResults<string>(containers, null);
    }

    public async Task<List<JsonElement>> QueryItems(
        string accountName,
        string databaseName,
        string containerName,
        string? query,
        string subscription,
        AuthMethod authMethod = AuthMethod.Credential,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredParameters((nameof(accountName), accountName), (nameof(databaseName), databaseName), (nameof(containerName), containerName), (nameof(subscription), subscription));

        var client = await GetCosmosClientAsync(accountName, subscription, authMethod, tenant, retryPolicy, cancellationToken);

        var container = client.GetContainer(databaseName, containerName);
        var baseQuery = string.IsNullOrEmpty(query) ? "SELECT * FROM c" : query;
        var queryDef = new QueryDefinition(baseQuery);

        var items = new List<JsonElement>();
        var queryIterator = container.GetItemQueryStreamIterator(
            queryDef,
            requestOptions: new() { MaxItemCount = -1 }
        );

        while (queryIterator.HasMoreResults)
        {
            using ResponseMessage response = await queryIterator.ReadNextAsync(cancellationToken);
            using var document = JsonDocument.Parse(response.Content);
            items.Add(document.RootElement.Clone());
        }

        return items;
    }

    private static readonly TimeSpan s_disposeTimeout = TimeSpan.FromSeconds(2);

    private async ValueTask DisposeAsyncCore()
    {
        // Use a bounded timeout so disposal can never hang indefinitely.
        // We do not use CancellationToken.None (unbounded) nor any IHostApplicationLifetime
        // token (already cancelled by the time DisposeAsync runs).
        using var cts = new CancellationTokenSource(s_disposeTimeout);

        IEnumerable<string> keys;
        try
        {
            // Get all cached client keys
            keys = await _cacheService.GetGroupKeysAsync(CacheGroup, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve cached CosmosClient keys during disposal");
            return;
        }

        // Filter for client keys only (those that start with the client prefix)
        var clientKeys = keys.Where(k => k.StartsWith(CosmosClientsCacheKeyPrefix));

        // Retrieve and dispose each client
        foreach (var key in clientKeys)
        {
            try
            {
                var client = await _cacheService.GetAsync<CosmosClient>(CacheGroup, key, cancellationToken: cts.Token);
                client?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispose CosmosClient for cache key {CacheKey}", key);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    internal class UserPolicyRequestHandler : RequestHandler
    {
        private readonly string userAgent;

        internal UserPolicyRequestHandler(string userAgent) => this.userAgent = userAgent;

        public override Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Set(UserAgentPolicy.UserAgentHeader, userAgent);
            return base.SendAsync(request, cancellationToken);
        }
    }
}

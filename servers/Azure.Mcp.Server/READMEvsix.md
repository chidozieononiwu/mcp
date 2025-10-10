# Azure MCP Server Extension for Visual Studio Code

All Azure MCP tools in a single server. The Azure MCP Server implements the [MCP specification](https://modelcontextprotocol.io) to create a seamless connection between AI agents and Azure services. Azure MCP Server can be used alone or with the [GitHub Copilot for Azure extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azure-github-copilot) in VS Code.

## Table of Contents
- [Overview](#overview)
- [Installation](#installation)
- [Usage](#usage)
    - [Getting Started](#getting-started)
    - [What can you do with the Azure MCP Server?](#what-can-you-do-with-the-azure-mcp-server)
    - [Complete List of Supported Azure Services](#complete-list-of-supported-azure-services)
- [Support and Reference](#support-and-reference)
    - [Documentation](#documentation)
    - [Feedback and Support](#feedback-and-support)
    - [Security](#security)
    - [Data Collection](#data-collection)
    - [Contributing](#contributing)
    - [Code of Conduct](#code-of-conduct)

# Overview

**Azure MCP Server** supercharges your agents with Azure context across **40+ different Azure services**.

# Installation
- Install the [Azure MCP Server Visual Studio Code extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azure-mcp-server)
- Start (or Auto-Start) the MCP Server
   > **VS Code (version 1.103 or above):** You can now configure MCP servers to start automatically using the `chat.mcp.autostart` setting, instead of manually restarting them after configuration changes.
   #### **Enable Autostart**
   1. Open **Settings** in VS Code.
   2. Search for `chat.mcp.autostart`.
   3. Select **newAndOutdated** to automatically start MCP servers without manual refresh.
   4. You can also set this from the **refresh icon tooltip** in the Chat view, which also shows which servers will auto-start.
      ![VS Code MCP Autostart Tooltip](https://raw.githubusercontent.com/microsoft/mcp/main/eng/vscode/resources/Walkthrough/ToolTip.png)
   #### **Manual Start (if autostart is off)**
   1. Open Command Palette (`Ctrl+Shift+P` / `Cmd+Shift+P`).
   2. Run `MCP: List Servers`.
      ![List Servers](https://raw.githubusercontent.com/microsoft/mcp/main/eng/vscode/resources/Walkthrough/ListServers.png)
   3. Select `Azure MCP Server ext`, then click **Start Server**.
      ![Select Server](https://raw.githubusercontent.com/microsoft/mcp/main/eng/vscode/resources/Walkthrough/SelectServer.png)
      ![Start Server](https://raw.githubusercontent.com/microsoft/mcp/main/eng/vscode/resources/Walkthrough/StartServer.png)
   4. **Check That It's Running**
      - Go to the **Output** tab in VS Code.
      - Look for log messages confirming the server started successfully.
      ![Output](https://raw.githubusercontent.com/microsoft/mcp/main/eng/vscode/resources/Walkthrough/Output.png)
- (Optional) Configure tools and behavior
    - Full options: control how tools are exposed and whether mutations are allowed:
       ```json
      // Server Mode: collapse per service (default), single tool, or expose every tool
      "azureMcp.serverMode": "namespace", // one of: "single" | "namespace" (default) | "all"
       // Filter which namespaces to expose
       "azureMcp.enabledServices": ["storage", "keyvault"],
       // Run the server in read-only mode (prevents write operations)
       "azureMcp.readOnly": false
       ```
   - Changes take effect after restarting the Azure MCP server from the MCP: List Servers view. (Step 2)
You’re all set! Azure MCP Server is now ready to help you work smarter with Azure resources in VS Code.

# Usage

## Getting Started

1. Open GitHub Copilot in [VS Code](https://code.visualstudio.com/docs/copilot/chat/chat-agent-mode)  and switch to Agent mode.
1. Click `refresh` on the tools list
    - You should see the Azure MCP Server in the list of tools
1. Try a prompt that tells the agent to use the Azure MCP Server, such as `List my Azure Storage containers`
    - The agent should be able to use the Azure MCP Server tools to complete your query
1. Check out the [documentation](https://learn.microsoft.com/azure/developer/azure-mcp-server/) and review the [troubleshooting guide](https://github.com/microsoft/mcp/blob/main/servers/Azure.Mcp.Server/TROUBLESHOOTING.md) for commonly asked questions
1. We're building this in the open. Your feedback is much appreciated, and will help us shape the future of the Azure MCP server
    - 👉 [Open an issue in the public repository](https://github.com/microsoft/mcp/issues/new/choose)

## What can you do with the Azure MCP Server?

✨ The Azure MCP Server supercharges your agents with Azure context. Here are some cool prompts you can try:

### 🧮 Azure AI Foundry

* List Azure Foundry models
* Deploy foundry models
* List foundry model deployments
* List knowledge indexes
* Get knowledge index schema configuration

### 🔎 Azure AI Search

* "What indexes do I have in my Azure AI Search service 'mysvc'?"
* "Let's search this index for 'my search query'"

### 🎤 Azure AI Services Speech

* "Convert this audio file to text using Azure Speech Services"
* "Recognize speech from my audio file with language detection"
* "Transcribe speech from audio with profanity filtering"
* "Transcribe audio with phrase hints for better accuracy"

### ⚙️ Azure App Configuration

* "List my App Configuration stores"
* "Show my key-value pairs in App Config"

### ⚙️ Azure App Lens

* "Help me diagnose issues with my app"

### 🕸️ Azure App Service

* "List the websites in my subscription"
* "Show me the websites in my 'my-resource-group' resource group"
* "Get the details for website 'my-website'"
* "Get the details for app service plan 'my-app-service-plan'"

### 🖥️ Azure CLI Generate

* Generate Azure CLI commands based on user intent

### 📞 Azure Communication Services

* "Send an SMS message to +1234567890"
* "Send SMS with delivery reporting enabled"
* "Send a broadcast SMS to multiple recipients"
* "Send SMS with custom tracking tag"
* "Send an email from 'sender@example.com' to 'recipient@example.com' with subject 'Hello' and message 'Welcome!'"
* "Send an HTML email to multiple recipients with CC and BCC using Azure Communication Services"
* "Send an email with reply-to address 'reply@example.com' and subject 'Support Request'"
* "Send an email from my communication service endpoint with custom sender name and multiple recipients"
* "Send an email to 'user1@example.com' and 'user2@example.com' with subject 'Team Update' and message 'Please review the attached document.'"

### 📦 Azure Container Apps

* "List the container apps in my subscription"
* "Show me the container apps in my 'my-resource-group' resource group"

### 🔐 Azure Confidential Ledger

* "Append entry {"foo":"bar"} to ledger contoso"
* "Get entry with id 2.40 from ledger contoso"

### 📦 Azure Container Registry (ACR)

* "List all my Azure Container Registries"
* "Show me my container registries in the 'my-resource-group' resource group"
* "List all my Azure Container Registry repositories"

### 📊 Azure Cosmos DB

* "Show me all my Cosmos DB databases"
* "List containers in my Cosmos DB database"

### 🧮 Azure Data Explorer

* "Get Azure Data Explorer databases in cluster 'mycluster'"
* "Sample 10 rows from table 'StormEvents' in Azure Data Explorer database 'db1'"

### 📣 Azure Event Grid

* "List all Event Grid topics in subscription 'my-subscription'"
* "Show me the Event Grid topics in my subscription"
* "List all Event Grid topics in resource group 'my-resourcegroup' in my subscription"
* "List Event Grid subscriptions for topic 'my-topic' in resource group 'my-resourcegroup'"
* "List Event Grid subscriptions for topic 'my-topic' in subscription 'my-subscription'"
* "List Event Grid Subscriptions in subscription 'my-subscription'"
* "List Event Grid subscriptions for topic 'my-topic' in location 'my-location'"
* "Publish an event with data '{\"name\": \"test\"}' to topic 'my-topic' using CloudEvents schema"
* "Send custom event data to Event Grid topic 'analytics-events' with EventGrid schema"

### 🔑 Azure Key Vault

* "List all secrets in my key vault 'my-vault'"
* "Create a new secret called 'apiKey' with value 'xyz' in key vault 'my-vault'"
* "List all keys in key vault 'my-vault'"
* "Create a new RSA key called 'encryption-key' in key vault 'my-vault'"
* "List all certificates in key vault 'my-vault'"
* "Import a certificate file into key vault 'my-vault' using the name 'tls-cert'"
* "Get the account settings for my key vault 'my-vault'"

### ☸️ Azure Kubernetes Service (AKS)

* "List my AKS clusters in my subscription"
* "Show me all my Azure Kubernetes Service clusters"
* "List the node pools for my AKS cluster"
* "Get details for the node pool 'np1' of my AKS cluster 'my-aks-cluster' in the 'my-resource-group' resource group"

### ⚡ Azure Managed Lustre

* "List the Azure Managed Lustre clusters in resource group 'my-resource-group'"
* "How many IP Addresses I need to create a 128 TiB cluster of AMLFS 500?"
* "Check if 'my-subnet-id' can host an Azure Managed Lustre with 'my-size' TiB and 'my-sku' in 'my-region'
* Create a 4 TIB Azure Managed Lustre filesystem in 'my-region' attaching to 'my-subnet' in virtual network 'my-virtual-network'

### 📊 Azure Monitor

* "Query my Log Analytics workspace"

### 🔧 Azure Resource Management

* "List my resource groups"
* "List my Azure CDN endpoints"
* "Help me build an Azure application using Node.js"

### 🗄️ Azure SQL Database

* "List all SQL servers in my subscription"
* "List all SQL servers in my resource group 'my-resource-group'"
* "Show me details about my Azure SQL database 'mydb'"
* "List all databases in my Azure SQL server 'myserver'"
* "Update the performance tier of my Azure SQL database 'mydb'"
* "Rename my Azure SQL database 'mydb' to 'newname'"
* "List all firewall rules for my Azure SQL server 'myserver'"
* "Create a firewall rule for my Azure SQL server 'myserver'"
* "Delete a firewall rule from my Azure SQL server 'myserver'"
* "List all elastic pools in my Azure SQL server 'myserver'"
* "List Active Directory administrators for my Azure SQL server 'myserver'"
* "Create a new Azure SQL server in my resource group 'my-resource-group'"
* "Show me details about my Azure SQL server 'myserver'"
* "Delete my Azure SQL server 'myserver'"

### 💾 Azure Storage

* "List my Azure storage accounts"
* "Get details about my storage account 'mystorageaccount'"
* "Create a new storage account in East US with Data Lake support"
* "Get details about my Storage container"
* "Upload my file to the blob container"


## Complete List of Supported Azure Services

The Azure MCP Server provides tools for interacting with **40+ Azure service areas**:

- 🧮 **Azure AI Foundry** - AI model management, AI model deployment, and knowledge index management
- 🔎 **Azure AI Search** - Search engine/vector database operations
- 🎤 **Azure AI Services Speech** - Speech-to-text recognition
- ⚙️ **Azure App Configuration** - Configuration management
- 🕸️ **Azure App Service** - Web app hosting
- 🛡️ **Azure Best Practices** - Secure, production-grade guidance
- 🖥️ **Azure CLI Generate** - Generate Azure CLI commands from natural language
- 📞 **Azure Communication Services** - SMS messaging and communication
- 🔐 **Azure Confidential Ledger** - Tamper-proof ledger operations
- 📦 **Azure Container Apps** - Container hosting
- 📦 **Azure Container Registry (ACR)** - Container registry management
- 📊 **Azure Cosmos DB** - NoSQL database operations
- 🧮 **Azure Data Explorer** - Analytics queries and KQL
- 🐬 **Azure Database for MySQL** - MySQL database management
- 🐘 **Azure Database for PostgreSQL** - PostgreSQL database management
- 📊 **Azure Event Grid** - Event routing and management
- ⚡ **Azure Functions** - Function App management
- 🔑 **Azure Key Vault** - Secrets, keys, and certificates
- ☸️ **Azure Kubernetes Service (AKS)** - Container orchestration
- 📦 **Azure Load Testing** - Performance testing
- 🚀 **Azure Managed Grafana** - Monitoring dashboards
- 🗃️ **Azure Managed Lustre** - High-performance Lustre filesystem operations
- 🏪 **Azure Marketplace** - Product discovery
- 📈 **Azure Monitor** - Logging, metrics, and health monitoring
- ⚙️ **Azure Native ISV Services** - Third-party integrations
- 🛡️ **Azure Quick Review CLI** - Compliance scanning
- 📊 **Azure Quota** - Resource quota and usage management
- 🎭 **Azure RBAC** - Access control management
- 🔴 **Azure Redis Cache** - In-memory data store
- 🏗️ **Azure Resource Groups** - Resource organization
- 🚌 **Azure Service Bus** - Message queuing
- 🏥 **Azure Service Health** - Resource health status and availability
- 🗄️ **Azure SQL Database** - Relational database management
- 🗄️ **Azure SQL Elastic Pool** - Database resource sharing
- 🗄️ **Azure SQL Server** - Server administration
- 💾 **Azure Storage** - Blob storage
- 📋 **Azure Subscription** - Subscription management
- 🏗️ **Azure Terraform Best Practices** - Infrastructure as code guidance
- 🖥️ **Azure Virtual Desktop** - Virtual desktop infrastructure
- 📊 **Azure Workbooks** - Custom visualizations
- 🏗️ **Bicep** - Azure resource templates
- 🏗️ **Cloud Architect** - Guided architecture design

# Support and Reference

## Documentation

- See our [official documentation on learn.microsoft.com](https://learn.microsoft.com/azure/developer/azure-mcp-server/) to learn how to use the Azure MCP Server to interact with Azure resources through natural language commands from AI agents and other types of clients.
- For additional command documentation and examples, see [Azure MCP Commands](https://github.com/microsoft/mcp/blob/main/servers/Azure.Mcp.Server/docs/azmcp-commands.md).

## Feedback and Support

- Check the [Troubleshooting guide](https://aka.ms/azmcp/troubleshooting) to diagnose and resolve common issues with the Azure MCP Server.
- We're building this in the open. Your feedback is much appreciated, and will help us shape the future of the Azure MCP server.
    - 👉 [Open an issue](https://github.com/microsoft/mcp/issues) in the public GitHub repository — we’d love to hear from you!

## Security

Your credentials are always handled securely through the official [Azure Identity SDK](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/README.md) - **we never store or manage tokens directly**.

MCP as a phenomenon is very novel and cutting-edge. As with all new technology standards, consider doing a security review to ensure any systems that integrate with MCP servers follow all regulations and standards your system is expected to adhere to. This includes not only the Azure MCP Server, but any MCP client/agent that you choose to implement down to the model provider.

## Data Collection

The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry by following the instructions [here](https://code.visualstudio.com/docs/configure/telemetry#_disable-telemetry-reporting).


## Contributing

We welcome contributions to the Azure MCP Server! Whether you're fixing bugs, adding new features, or improving documentation, your contributions are welcome.

Please read our [Contributing Guide](https://github.com/microsoft/mcp/blob/main/CONTRIBUTING.md) for guidelines on:

* 🛠️ Setting up your development environment
* ✨ Adding new commands
* 📝 Code style and testing requirements
* 🔄 Making pull requests


## Code of Conduct

This project has adopted the
[Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information, see the
[Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/)
or contact [open@microsoft.com](mailto:open@microsoft.com)
with any additional questions or comments.

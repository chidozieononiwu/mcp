#!/bin/env pwsh
#Requires -Version 7


<#
.SYNOPSIS
    Builds and runs the Tool Description Evaluator application with default settings

.DESCRIPTION
    This script performs the following steps:
    - Dynamically load tools from the selected MCP Server Debug or Release build, preferring the former
    - Load prompts from the selected server's docs/e2eTestPrompts.md file
    - Build the Tool Description Evaluator application in Debug configuration and run it

.PARAMETER ServerName
    Server project name under the servers directory. Defaults to Azure.Mcp.Server.

.PARAMETER BuildServer
    Optionally build the selected server in Debug mode to ensure tools can be loaded dynamically.
    BuildAzureMcp is retained as an alias for backward compatibility.

.PARAMETER Area
    Filter prompts by tool name prefix. Service names are auto-prefixed with "azmcp_" (e.g., "keyvault" becomes "azmcp_keyvault")

.EXAMPLE
    ./Run-ToolDescriptionEvaluator.ps1
    Builds and runs the Tool Description Evaluator application with default settings

.EXAMPLE
    ./Run-ToolDescriptionEvaluator.ps1 -BuildServer
    Builds the Azure MCP Server project in Debug mode, then builds and runs the Tool Description Evaluator application
    with default settings

.EXAMPLE
    ./Run-ToolDescriptionEvaluator.ps1 -ServerName Fabric.Mcp.Server -BuildServer -Area "workspace"
    Builds the Fabric MCP Server, then runs the Tool Description Evaluator for Fabric workspace tools

.EXAMPLE
    ./Run-ToolDescriptionEvaluator.ps1 -Area "keyvault"
    Runs the Tool Description Evaluator filtering prompts to only tools with the azmcp_keyvault prefix

#>

[CmdletBinding()]
param(
    [string]$ServerName = 'Azure.Mcp.Server',
    [Alias('BuildAzureMcp')]
    [switch]$BuildServer,
    [string]$Area
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

try {
    # Get absolute paths
    $repoRoot = Resolve-Path "$PSScriptRoot/../../../../" | Select-Object -ExpandProperty Path
    $toolDir = Resolve-Path "$PSScriptRoot/../src" | Select-Object -ExpandProperty Path
    $serverDirectory = Join-Path $repoRoot "servers/$ServerName"
    $serverProject = Join-Path $serverDirectory "src/$ServerName.csproj"
    $serverSolution = Join-Path $serverDirectory "$ServerName.slnx"

    if (!(Test-Path $serverProject)) {
        throw "Server project not found: $serverProject"
    }

    if ($BuildServer)
    {
        if (!(Test-Path $serverSolution)) {
            throw "Server solution not found: $serverSolution"
        }

        Write-Host "Building $ServerName to enable dynamic tool loading..." -ForegroundColor Yellow

        & dotnet build $serverSolution
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to build $ServerName"
        }

        Write-Host "$ServerName build completed successfully!" -ForegroundColor Green
    }

    Write-Host "Building and running tool selection confidence score calculation app..." -ForegroundColor Green
    Write-Host "Building application..." -ForegroundColor Yellow

    & dotnet build "$toolDir/ToolDescriptionEvaluator.csproj"

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build application"
    }

    Write-Host "Build completed successfully!" -ForegroundColor Green
    
    # Build the command arguments
    $server = $ServerName -replace '\.Mcp\.Server$', ''
    $runArgs = @('--server', $server)
    
    if ($Area) {
        $runArgs += "--area"
        $runArgs += $Area
        Write-Host "Running with area filter: $Area" -ForegroundColor Cyan
    }
    
    $runCommand = "dotnet run"
    if ($runArgs.Count -gt 0) {
        $runCommand += " -- " + ($runArgs -join " ")
    }
    
    Write-Host "Running with: $runCommand" -ForegroundColor Cyan
    Push-Location $toolDir

    & dotnet run -- @runArgs

    Pop-Location
}
catch {
    Write-Error "Build failed: $_"

    exit 1
}

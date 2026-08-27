#!/usr/bin/env pwsh
#Requires -Version 7

<#
.SYNOPSIS
    Updates the tools.json file by calling the selected server's "tools list" command

.DESCRIPTION
    Generates a fresh tools.json by executing the selected MCP Server Debug or Release build,
    preferring the former.

.PARAMETER Force
    Overwrite the existing tools.json file without prompting

.PARAMETER ServerName
    Server project name under the servers directory. Defaults to Azure.Mcp.Server.

.PARAMETER BuildServer
    Build the selected server in Debug mode before generating tools.json.
    BuildAzureMcp is retained as an alias for backward compatibility.

.PARAMETER OutputPath
    Output path for the generated JSON. Defaults to the evaluator's src/tools.json file.

.EXAMPLE
    ./Update-ToolsJson.ps1
    Updates the tools.json file, prompting before overwriting if it exists

.EXAMPLE
    ./Update-ToolsJson.ps1 -Force
    Updates the tools.json file, overwriting without prompting
    
.EXAMPLE
    ./Update-ToolsJson.ps1 -BuildServer
    Updates the tools.json file after building the Azure MCP Server project in Debug mode

.EXAMPLE
    ./Update-ToolsJson.ps1 -ServerName Fabric.Mcp.Server -BuildServer -Force
    Updates the tools.json file using the Fabric MCP Server
#>

[CmdletBinding()]
param(
    [switch]$Force,
    [string]$ServerName = 'Azure.Mcp.Server',
    [Alias('BuildAzureMcp')]
    [switch]$BuildServer,
    [string]$OutputPath
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

# Resolve important paths
$repoRoot = Resolve-Path "$PSScriptRoot/../../../../" | Select-Object -ExpandProperty Path
$toolDir  = Resolve-Path "$PSScriptRoot/../src" | Select-Object -ExpandProperty Path
$jsonFile = $OutputPath ? $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath) : "$toolDir/tools.json"
$serverDirectory = Join-Path $repoRoot "servers/$ServerName"
$serverProject = Join-Path $serverDirectory "src/$ServerName.csproj"
$serverSolution = Join-Path $serverDirectory "$ServerName.slnx"

if (!(Test-Path $serverProject)) {
    throw "Server project not found: $serverProject"
}

$serverProperties = & "$repoRoot/eng/scripts/Get-ProjectProperties.ps1" -Path $serverProject
$cliName = $serverProperties.CliName
if ([string]::IsNullOrWhiteSpace($cliName)) {
    throw "CliName is not defined for $ServerName in $serverProject"
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

$candidateNames = if ($IsWindows) { @("$cliName.exe", $cliName, "$cliName.dll") } else { @($cliName, "$cliName.dll") }
$searchRoots = @(
    "$serverDirectory/src/bin/Debug",
    "$serverDirectory/src/bin/Release"
) | Where-Object { Test-Path $_ }

$cliArtifact = $null

foreach ($root in $searchRoots) {
    foreach ($name in $candidateNames) {
        $found = Get-ChildItem -Path $root -Filter $name -Recurse -ErrorAction SilentlyContinue |
                 Where-Object { -not $_.PSIsContainer } |
                 Select-Object -First 1

        if ($found) {
            $cliArtifact = $found
            
            break
        }   
    }

    if ($cliArtifact) {
        break
    }
}

if (-not $cliArtifact) {
    Write-Error "Could not locate '$cliName' CLI for $ServerName under: $($searchRoots -join ', ')"
    Write-Host "Try building the solution first:" -ForegroundColor Yellow
    Write-Host "  dotnet build `"$serverSolution`"" -ForegroundColor Yellow

    exit 1
}

# Confirm overwrite unless -Force
if ((Test-Path $jsonFile) -and -not $Force) {
    $response = Read-Host "tools.json already exists. Overwrite? (y/N)"

    if ($response -notmatch '^[Yy]') {
        Write-Host "Operation cancelled." -ForegroundColor Yellow

        exit 0
    }
}

Write-Host "Generating tools.json..." -ForegroundColor Green

try {
    # Execute the selected server's tools list command and capture output
    if ($cliArtifact.Extension -ieq '.dll') {
        $output = & dotnet $cliArtifact.FullName tools list 2>&1
    }
    else {
        $output = & $cliArtifact.FullName tools list 2>&1
    }

    # Extract pure JSON in case the CLI prints extra logs
    if ($null -eq $output) {
        throw "No output received from $cliName."
    }

    $outputText = $output | Out-String
    $start = $outputText.IndexOf('{')
    $end   = $outputText.LastIndexOf('}')
    $jsonText = if ($start -ge 0 -and $end -ge $start) { $outputText.Substring($start, $end - $start + 1) } else { $outputText }

    $jsonText | Out-File -FilePath $jsonFile -Encoding utf8

    # Verify the file was created and has content
    if ((Test-Path $jsonFile) -and ((Get-Item $jsonFile).Length -gt 0)) {
        $fileSize = [math]::Round((Get-Item $jsonFile).Length / 1KB, 2)

        Write-Host "Successfully generated tools.json ($fileSize KB)" -ForegroundColor Green

        # Try to parse the JSON to verify it's valid
        try {
            $json = Get-Content $jsonFile -Raw | ConvertFrom-Json
            $toolCount = if ($null -ne $json.results) { $json.results.Count } elseif ($null -ne $json.tools) { $json.tools.Count } else { $null }

            if ($null -ne $toolCount) {
                Write-Host "Contains $toolCount tools" -ForegroundColor Cyan
            }
        }
        catch {
            Write-Warning "Generated JSON file may not be valid: $_"
        }
    }
    else {
        Write-Error "Failed to generate tools.json or file is empty"
    }
}
catch {
    Write-Error "Failed to execute $cliName for ${ServerName}: $_"

    exit 1
}

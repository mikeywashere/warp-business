#Requires -Version 5.1
<#
.SYNOPSIS
    Install Warp Business development prerequisites on Windows.
.DESCRIPTION
    Uses winget to install .NET 10 SDK, Docker Desktop, kubectl, skaffold,
    GitHub CLI, and optionally Node.js LTS. Runs 'dotnet workload install aspire'
    after installs. Skips tools that are already present.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ─── Helpers ─────────────────────────────────────────────────────────────────

function Write-Header {
    param([string]$Title)
    Write-Host ""
    Write-Host "══════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host "══════════════════════════════════════════" -ForegroundColor Cyan
}

function Write-Ok     { param([string]$Msg) Write-Host "  ✅  $Msg" -ForegroundColor Green }
function Write-Skip   { param([string]$Msg) Write-Host "  ⏭️   $Msg — already installed, skipping" -ForegroundColor Yellow }
function Write-Info   { param([string]$Msg) Write-Host "  ℹ️   $Msg" -ForegroundColor Gray }
function Write-Warn   { param([string]$Msg) Write-Host "  ⚠️   $Msg" -ForegroundColor Yellow }

$Installed = [System.Collections.Generic.List[string]]::new()
$Skipped   = [System.Collections.Generic.List[string]]::new()

function Test-CommandExists {
    param([string]$Name)
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

# ─── Winget check ────────────────────────────────────────────────────────────

Write-Header "Checking winget"

if (-not (Test-CommandExists 'winget')) {
    Write-Host ""
    Write-Host "  ❌  winget is not available." -ForegroundColor Red
    Write-Host "      Please install 'App Installer' from the Microsoft Store:" -ForegroundColor Red
    Write-Host "      https://aka.ms/getwinget" -ForegroundColor Red
    Write-Host "      Then re-run this script." -ForegroundColor Red
    exit 1
}

Write-Ok "winget is available"

# ─── Optional: Node.js ───────────────────────────────────────────────────────

Write-Host ""
$installNode = $false
$yn = Read-Host "Install Node.js LTS? (optional, only needed for frontend tooling) [y/N]"
if ($yn -match '^[Yy]') { $installNode = $true }
else { Write-Info "Skipping Node.js" }

# ─── Helper: winget install ───────────────────────────────────────────────────

function Install-WithWinget {
    param(
        [string]$DisplayName,
        [string]$PackageId,
        [string]$TestCommand = ''
    )

    Write-Header $DisplayName

    $alreadyInstalled = $false
    if ($TestCommand -and (Test-CommandExists $TestCommand)) {
        $alreadyInstalled = $true
    } else {
        # Check winget list as a fallback
        $listOutput = winget list --id $PackageId 2>$null
        if ($LASTEXITCODE -eq 0 -and ($listOutput | Select-String -Quiet $PackageId)) {
            $alreadyInstalled = $true
        }
    }

    if ($alreadyInstalled) {
        Write-Skip $DisplayName
        $Skipped.Add($DisplayName)
        return
    }

    Write-Info "Installing $DisplayName ($PackageId)..."
    winget install --id $PackageId --accept-package-agreements --accept-source-agreements --silent
    if ($LASTEXITCODE -ne 0) {
        Write-Warn "$DisplayName install returned exit code $LASTEXITCODE — check output above."
    } else {
        Write-Ok "$DisplayName installed"
        $Installed.Add($DisplayName)
    }
}

# ─── .NET 10 SDK ─────────────────────────────────────────────────────────────

Write-Header ".NET 10 SDK"

$dotnetInstalled = $false
if (Test-CommandExists 'dotnet') {
    $ver = & dotnet --version 2>$null
    if ($ver -match '^10\.') { $dotnetInstalled = $true }
}
if (-not $dotnetInstalled) {
    # Also check winget list
    $listOutput = winget list --id 'Microsoft.DotNet.SDK.10' 2>$null
    if ($LASTEXITCODE -eq 0 -and ($listOutput | Select-String -Quiet 'Microsoft.DotNet.SDK.10')) {
        $dotnetInstalled = $true
    }
}

if ($dotnetInstalled) {
    Write-Skip ".NET 10 SDK"
    $Skipped.Add(".NET 10 SDK")
} else {
    Write-Info "Installing .NET 10 SDK..."
    winget install --id 'Microsoft.DotNet.SDK.10' --accept-package-agreements --accept-source-agreements --silent
    if ($LASTEXITCODE -ne 0) {
        Write-Warn ".NET 10 SDK install returned exit code $LASTEXITCODE — check output above."
    } else {
        Write-Ok ".NET 10 SDK installed"
        $Installed.Add(".NET 10 SDK")
    }
}

# ─── Docker Desktop ───────────────────────────────────────────────────────────

Install-WithWinget -DisplayName "Docker Desktop" -PackageId "Docker.DockerDesktop" -TestCommand "docker"

# ─── kubectl ──────────────────────────────────────────────────────────────────

Install-WithWinget -DisplayName "kubectl" -PackageId "Kubernetes.kubectl" -TestCommand "kubectl"

# ─── skaffold ─────────────────────────────────────────────────────────────────

Install-WithWinget -DisplayName "skaffold" -PackageId "GoogleContainerTools.Skaffold" -TestCommand "skaffold"

# ─── GitHub CLI ───────────────────────────────────────────────────────────────

Install-WithWinget -DisplayName "GitHub CLI (gh)" -PackageId "GitHub.cli" -TestCommand "gh"

# ─── Node.js LTS (optional) ───────────────────────────────────────────────────

if ($installNode) {
    Install-WithWinget -DisplayName "Node.js LTS" -PackageId "OpenJS.NodeJS.LTS" -TestCommand "node"
}

# ─── .NET Aspire workload ────────────────────────────────────────────────────

Write-Header ".NET Aspire workload"

# Refresh PATH so dotnet is available if it was just installed
$env:PATH = [System.Environment]::GetEnvironmentVariable('PATH', 'Machine') + ';' +
            [System.Environment]::GetEnvironmentVariable('PATH', 'User')

if (Test-CommandExists 'dotnet') {
    Write-Info "Running: dotnet workload install aspire"
    & dotnet workload install aspire
    if ($LASTEXITCODE -eq 0) {
        Write-Ok ".NET Aspire workload installed"
        $Installed.Add(".NET Aspire workload")
    } else {
        Write-Warn "dotnet workload install aspire returned exit code $LASTEXITCODE"
    }
} else {
    Write-Warn "dotnet not found on PATH yet. Open a new terminal and run: dotnet workload install aspire"
}

# ─── Summary ─────────────────────────────────────────────────────────────────

Write-Header "Installation Summary"

if ($Installed.Count -gt 0) {
    Write-Host "  Installed:"
    foreach ($item in $Installed) { Write-Host "    ✅  $item" -ForegroundColor Green }
}

if ($Skipped.Count -gt 0) {
    Write-Host "  Already present (skipped):"
    foreach ($item in $Skipped) { Write-Host "    ⏭️   $item" -ForegroundColor Yellow }
}

Write-Host ""
Write-Warn "Restart your terminal (or open a new PowerShell window) for PATH changes to take effect."
Write-Info "Visual Studio and VS Code are not installed by this script — install them manually."
Write-Host ""
Write-Host "  Happy developing! 🚀" -ForegroundColor Green
Write-Host ""

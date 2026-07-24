#!/usr/bin/env pwsh
# Start ASP.NET Core and Vite dev server for development

Write-Host "Starting React + ASP.NET Core Development Environment..." -ForegroundColor Green
Write-Host ""

# Check if Node.js is installed
try {
    $nodeVersion = node --version
    Write-Host "Node.js version: $nodeVersion" -ForegroundColor Cyan
} catch {
    Write-Host "ERROR: Node.js is not installed or not in PATH" -ForegroundColor Red
    Write-Host "Please install Node.js from https://nodejs.org/" -ForegroundColor Yellow
    exit 1
}

# Navigate to ClientApp and check for node_modules
$clientAppPath = Join-Path $PSScriptRoot "ClientApp"
$nodeModulesPath = Join-Path $clientAppPath "node_modules"

if (-not (Test-Path $nodeModulesPath)) {
    Write-Host "Installing npm dependencies..." -ForegroundColor Yellow
    Set-Location $clientAppPath
    npm install
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to install npm dependencies" -ForegroundColor Red
        exit 1
    }
    Set-Location $PSScriptRoot
}

Write-Host ""
Write-Host "==============================================================" -ForegroundColor Green
Write-Host "Starting ASP.NET Core + Vite dev server..." -ForegroundColor Green
Write-Host "==============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "✨ Vite dev server will be started and stopped with this script" -ForegroundColor Cyan
Write-Host "✨ Hot Module Replacement (HMR) is enabled" -ForegroundColor Cyan
Write-Host "✨ React Fast Refresh is configured" -ForegroundColor Cyan
Write-Host "✨ Backend rebuilds regenerate the OpenAPI client" -ForegroundColor Cyan
Write-Host ""
Write-Host "React App will be available at: https://localhost:XXXX/app" -ForegroundColor Yellow
Write-Host ""
Write-Host "Press Ctrl+C to stop all servers" -ForegroundColor Yellow
Write-Host "==============================================================" -ForegroundColor Green
Write-Host ""

# Start Vite dev server in background
Write-Host ""
Write-Host "Starting Vite dev server..." -ForegroundColor Cyan
Set-Location $clientAppPath
$viteProcess = Start-Process -FilePath "npm" -ArgumentList "run", "dev" -PassThru

Start-Sleep -Seconds 2

# Start ASP.NET Core app (in background, we'll wait for it to be ready)
Write-Host ""
Write-Host "Starting ASP.NET Core application..." -ForegroundColor Cyan
Set-Location $PSScriptRoot
$dotnetProcess = Start-Process -FilePath "dotnet" -ArgumentList "watch", "run", "--non-interactive" -PassThru

# Wait a bit for app to start
Start-Sleep -Seconds 3

Write-Host ""
Write-Host "All services started! Press Ctrl+C to stop all servers" -ForegroundColor Green
Write-Host ""

# Function to cleanup on exit
function Cleanup {
    Write-Host ""
    Write-Host "Stopping all services..." -ForegroundColor Yellow
    
    Get-Process npm -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Get-Process node -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    
    Write-Host "Cleanup complete" -ForegroundColor Green
}

# Set up Ctrl+C handler
$null = Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action { Cleanup }

# Keep running until user presses Ctrl+C
try {
    while ($true) {
        Start-Sleep -Seconds 1
    }
} catch {
    Cleanup
}

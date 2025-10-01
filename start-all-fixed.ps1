# SearchEngine Startup Script
# This script starts all components of the SearchEngine application

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "        SearchEngine Startup Script" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""

# Set the base directory
$baseDir = $PSScriptRoot
Write-Host "Base directory: $baseDir" -ForegroundColor Yellow
Write-Host ""

# Function to start a service in a new PowerShell window
function Start-Service {
    param(
        [string]$ServiceName,
        [string]$Directory,
        [string]$Command,
        [string]$Color = "Green"
    )
    
    Write-Host "Starting $ServiceName..." -ForegroundColor $Color
    
    $fullPath = Join-Path $baseDir $Directory
    $windowTitle = "SearchEngine - $ServiceName"
    
    # Start the service in a new PowerShell window
    Start-Process powershell -ArgumentList @(
        "-NoExit",
        "-Command",
        "& { Set-Location '$fullPath'; Write-Host 'Starting $ServiceName...' -ForegroundColor $Color; `$Host.UI.RawUI.WindowTitle = '$windowTitle'; $Command }"
    )
    
    Write-Host "✓ $ServiceName started in new window" -ForegroundColor Green
    Start-Sleep -Seconds 2
}

# Function to check if a port is available
function Test-Port {
    param([int]$Port)
    
    try {
        $connection = New-Object System.Net.Sockets.TcpClient
        $connection.Connect("127.0.0.1", $Port)
        $connection.Close()
        return $true
    }
    catch {
        return $false
    }
}

# Function to wait for service to start
function Wait-ForService {
    param(
        [string]$ServiceName,
        [int]$Port,
        [int]$TimeoutSeconds = 30
    )
    
    Write-Host "Waiting for $ServiceName to start on port $Port..." -ForegroundColor Yellow
    
    $timeout = (Get-Date).AddSeconds($TimeoutSeconds)
    
    while ((Get-Date) -lt $timeout) {
        if (Test-Port -Port $Port) {
            Write-Host "✓ $ServiceName is ready on port $Port" -ForegroundColor Green
            return $true
        }
        Start-Sleep -Seconds 1
        Write-Host "." -NoNewline -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "⚠ $ServiceName did not start within $TimeoutSeconds seconds" -ForegroundColor Red
    return $false
}

Write-Host "Step 1: Starting SearchAPI instances..." -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Cyan

# Start SearchAPI Instance 1 (Ole profile - port 5154)
Start-Service -ServiceName "SearchAPI Instance 1 (Port 5154)" -Directory "SearchAPI" -Command "dotnet run --launch-profile Ole" -Color "Blue"

# Wait for first instance to start
Wait-ForService -ServiceName "SearchAPI Instance 1" -Port 5154

# Start SearchAPI Instance 2 (Henrik profile - port 5155)
Start-Service -ServiceName "SearchAPI Instance 2 (Port 5155)" -Directory "SearchAPI" -Command "dotnet run --launch-profile Henrik" -Color "Magenta"

# Wait for second instance to start
Wait-ForService -ServiceName "SearchAPI Instance 2" -Port 5155

Write-Host ""
Write-Host "Step 2: Starting Load Balancer..." -ForegroundColor Cyan
Write-Host "-----------------------------------" -ForegroundColor Cyan

# Start Load Balancer (port 5000)
Start-Service -ServiceName "Load Balancer (Port 5000)" -Directory "Loadbalencer" -Command "dotnet run" -Color "Red"

# Wait for load balancer to start
Wait-ForService -ServiceName "Load Balancer" -Port 5000

Write-Host ""
Write-Host "Step 3: Starting Web Applications..." -ForegroundColor Cyan
Write-Host "-------------------------------------" -ForegroundColor Cyan

# Start SearchWeb
Start-Service -ServiceName "SearchWeb" -Directory "SearchWeb" -Command "dotnet run" -Color "Green"

# Wait a bit for SearchWeb to start
Start-Sleep -Seconds 3

Write-Host ""
Write-Host "Step 4: Console Application Available..." -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Cyan

Write-Host "ConsoleSearch can be started manually with:" -ForegroundColor Yellow
Write-Host "  cd ConsoleSearch" -ForegroundColor Gray
Write-Host "  dotnet run" -ForegroundColor Gray

Write-Host ""
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "            🚀 All Services Started! 🚀" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Service URLs:" -ForegroundColor White
Write-Host "  • SearchAPI Instance 1:  http://localhost:5154" -ForegroundColor Blue
Write-Host "  • SearchAPI Instance 2:  http://localhost:5155" -ForegroundColor Magenta  
Write-Host "  • Load Balancer:         http://localhost:5000" -ForegroundColor Red
Write-Host "  • SearchWeb:             http://localhost:5xxx (check terminal)" -ForegroundColor Green
Write-Host ""

Write-Host "Health Check URLs:" -ForegroundColor White
Write-Host "  • API 1 Ping:            http://localhost:5154/api/ping" -ForegroundColor Gray
Write-Host "  • API 2 Ping:            http://localhost:5155/api/ping" -ForegroundColor Gray
Write-Host "  • Load Balancer Ping:    http://localhost:5000/api/ping" -ForegroundColor Gray
Write-Host ""

Write-Host "Press any key to exit this script..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

Write-Host ""
Write-Host "Script completed. All services are running in separate windows." -ForegroundColor Green
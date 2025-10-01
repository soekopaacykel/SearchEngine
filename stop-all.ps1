# SearchEngine Stop Script
# This script stops all SearchEngine services

Write-Host "===============================================" -ForegroundColor Red
Write-Host "         SearchEngine Stop Script" -ForegroundColor Red
Write-Host "===============================================" -ForegroundColor Red
Write-Host ""

# Function to kill processes on specific ports
function Stop-ServiceOnPort {
    param(
        [int]$Port,
        [string]$ServiceName
    )
    
    Write-Host "Stopping $ServiceName on port $Port..." -ForegroundColor Yellow
    
    try {
        # Get processes using the port
        $netstat = netstat -ano | Select-String ":$Port.*LISTENING"
        
        if ($netstat) {
            foreach ($line in $netstat) {
                # Extract PID from netstat output
                $parts = $line.ToString().Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
                $pid = $parts[-1]
                
                if ($pid -match '^\d+$') {
                    Write-Host "  Killing process $pid..." -ForegroundColor Gray
                    Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
                    Write-Host "  ✓ Process $pid terminated" -ForegroundColor Green
                }
            }
        } else {
            Write-Host "  No process found on port $Port" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "  Error stopping $ServiceName`: $_" -ForegroundColor Red
    }
}

# Function to kill processes by name
function Stop-ProcessByName {
    param(
        [string]$ProcessName,
        [string]$ServiceName
    )
    
    Write-Host "Stopping $ServiceName processes..." -ForegroundColor Yellow
    
    try {
        $processes = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue
        
        if ($processes) {
            foreach ($process in $processes) {
                Write-Host "  Killing $ProcessName process $($process.Id)..." -ForegroundColor Gray
                Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
                Write-Host "  ✓ Process $($process.Id) terminated" -ForegroundColor Green
            }
        } else {
            Write-Host "  No $ProcessName processes found" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "  Error stopping $ServiceName`: $_" -ForegroundColor Red
    }
}

Write-Host "Stopping all SearchEngine services..." -ForegroundColor Cyan
Write-Host ""

# Stop services by port
Stop-ServiceOnPort -Port 5154 -ServiceName "SearchAPI Instance 1"
Stop-ServiceOnPort -Port 5155 -ServiceName "SearchAPI Instance 2" 
Stop-ServiceOnPort -Port 5000 -ServiceName "Load Balancer"

# Stop SearchWeb (usually runs on random port, so we'll kill by process name)
Stop-ProcessByName -ProcessName "SearchWeb" -ServiceName "SearchWeb"

# Stop any remaining dotnet processes that might be our services
Write-Host ""
Write-Host "Checking for remaining SearchEngine processes..." -ForegroundColor Yellow

$dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
    $_.MainWindowTitle -like "*SearchEngine*" -or 
    $_.ProcessName -eq "SearchAPI" -or
    $_.ProcessName -eq "Loadbalencer" -or
    $_.ProcessName -eq "SearchWeb"
}

if ($dotnetProcesses) {
    Write-Host "Found additional processes to stop:" -ForegroundColor Yellow
    foreach ($process in $dotnetProcesses) {
        Write-Host "  Killing dotnet process $($process.Id) ($($process.MainWindowTitle))..." -ForegroundColor Gray
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Write-Host "  ✓ Process $($process.Id) terminated" -ForegroundColor Green
    }
} else {
    Write-Host "  No additional processes found" -ForegroundColor Gray
}

Write-Host ""
Write-Host "===============================================" -ForegroundColor Red
Write-Host "          🛑 All Services Stopped! 🛑" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Red
Write-Host ""

Write-Host "All SearchEngine services have been terminated." -ForegroundColor Green
Write-Host "You can restart them using start-all.ps1 or start-all.bat" -ForegroundColor Yellow
Write-Host ""

Write-Host "Press any key to exit..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
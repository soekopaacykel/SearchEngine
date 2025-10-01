# SearchEngine Startup Scripts

This directory contains scripts to easily start and stop all SearchEngine services.

## Available Scripts

### 🚀 Start Scripts

#### `start-all.ps1` (PowerShell - Recommended)
- **Features**: 
  - Colored output and progress indicators
  - Service health checks and port verification
  - Proper error handling and timeout detection
  - Detailed status messages
- **Usage**: Right-click → "Run with PowerShell" or `./start-all.ps1`

#### `start-all.bat` (Batch File)
- **Features**: 
  - Simple and fast
  - Works on any Windows system
  - No PowerShell execution policy issues
- **Usage**: Double-click or `start-all.bat`

### 🛑 Stop Scripts

#### `stop-all.ps1` (PowerShell)
- **Features**:
  - Stops all services by port number
  - Kills remaining processes by name
  - Comprehensive cleanup
- **Usage**: Right-click → "Run with PowerShell" or `./stop-all.ps1`

## Services Started

| Service | Port | Launch Profile | Description |
|---------|------|----------------|-------------|
| SearchAPI Instance 1 | 5154 | Ole | Primary API instance |
| SearchAPI Instance 2 | 5155 | Henrik | Secondary API instance |
| Load Balancer | 5000 | loadbalancer | Distributes requests between APIs |
| SearchWeb | Dynamic | default | Web interface |

## Service URLs

After starting, you can access:

- **Load Balancer**: http://localhost:5000
- **SearchAPI Instance 1**: http://localhost:5154
- **SearchAPI Instance 2**: http://localhost:5155
- **SearchWeb**: Check the SearchWeb terminal window for the actual port

## Health Check URLs

- http://localhost:5000/api/ping (Load Balancer)
- http://localhost:5154/api/ping (API Instance 1)
- http://localhost:5155/api/ping (API Instance 2)

## Manual Console Search

ConsoleSearch is not auto-started. To run it manually:

```bash
cd ConsoleSearch
dotnet run
```

## Startup Sequence

1. **SearchAPI Instance 1** starts first on port 5154
2. **SearchAPI Instance 2** starts second on port 5155
3. **Load Balancer** starts after both APIs are ready
4. **SearchWeb** starts last

## Troubleshooting

### If PowerShell script doesn't run:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### If ports are already in use:
1. Run `stop-all.ps1` to clean up
2. Check with `netstat -an | findstr ":5000\|:5154\|:5155"`
3. Manually kill processes if needed

### If services don't start:
1. Check that .NET SDK is installed
2. Ensure all projects compile: `dotnet build`
3. Check individual project directories for errors

## Configuration

The scripts use the launch profiles defined in each project's `launchSettings.json`:

- **SearchAPI**: Uses "Ole" and "Henrik" profiles for different ports
- **Load Balancer**: Uses "loadbalancer" profile
- **SearchWeb**: Uses default profile

## Architecture

```
[Client] → [Load Balancer:5000] → [API1:5154] or [API2:5155]
                                 ↗ (Random distribution)
[SearchWeb] → [Load Balancer:5000]
[ConsoleSearch] → [Load Balancer:5000]
```

## Notes

- Each service runs in its own terminal window
- Close terminal windows to stop individual services
- Use `stop-all.ps1` for complete cleanup
- Load balancer randomly distributes requests between API instances
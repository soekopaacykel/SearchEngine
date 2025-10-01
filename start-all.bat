@echo off
title SearchEngine Startup Script

echo ===============================================
echo         SearchEngine Startup Script
echo ===============================================
echo.

echo Starting SearchAPI Instance 1 (Port 5154)...
start "SearchAPI Instance 1" cmd /k "cd SearchAPI && dotnet run --launch-profile Ole"
timeout /t 5 /nobreak > nul

echo Starting SearchAPI Instance 2 (Port 5155)...
start "SearchAPI Instance 2" cmd /k "cd SearchAPI && dotnet run --launch-profile Henrik"
timeout /t 5 /nobreak > nul

echo Starting Load Balancer (Port 5000)...
start "Load Balancer" cmd /k "cd Loadbalencer && dotnet run"
timeout /t 5 /nobreak > nul

echo Starting SearchWeb...
start "SearchWeb" cmd /k "cd SearchWeb && dotnet run"
timeout /t 3 /nobreak > nul

echo.
echo ===============================================
echo            🚀 All Services Started! 🚀
echo ===============================================
echo.
echo Service URLs:
echo   • SearchAPI Instance 1:  http://localhost:5154
echo   • SearchAPI Instance 2:  http://localhost:5155
echo   • Load Balancer:         http://localhost:5000
echo   • SearchWeb:             http://localhost:5xxx (check terminal)
echo.
echo Health Check URLs:
echo   • API 1 Ping:            http://localhost:5154/api/ping
echo   • API 2 Ping:            http://localhost:5155/api/ping
echo   • Load Balancer Ping:    http://localhost:5000/api/ping
echo.
echo ConsoleSearch can be started manually with:
echo   cd ConsoleSearch
echo   dotnet run
echo.
echo Press any key to exit...
pause > nul
@echo off
echo Starting Pizzafy Web Application...
echo.

REM Start the application
start /B dotnet run --project PizzafyWeb.csproj --launch-profile https

REM Wait a moment for the application to start
timeout /t 3 /nobreak > nul

REM Open Microsoft Edge
echo Opening Microsoft Edge...
"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --new-window --start-maximized https://localhost:7042

echo.
echo Application started successfully!
echo Press any key to stop the application...
pause > nul

REM Kill the dotnet process when done
taskkill /F /IM dotnet.exe 2>nul
echo Application stopped.
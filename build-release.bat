@echo off
setlocal

cd /d "%~dp0"

set "PUBLISH_DIR=%~dp0Release

echo Stopping running QDXM Avalon process...
taskkill /IM QDXM.Avalon.exe /F >nul 2>nul

echo.
echo Running release tests...
dotnet test "QDXM.Avalon.Tests\QDXM.Avalon.Tests.csproj" -c Release --results-directory "Testing\TestResults"
if errorlevel 1 goto fail

echo.
echo Publishing win-x64 framework-dependent release...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
dotnet publish "QDXM.Avalon\QDXM.Avalon.csproj" -c Release -r win-x64 --self-contained false -o "%PUBLISH_DIR%"
if errorlevel 1 goto fail

echo.
echo Release publish complete:
echo %PUBLISH_DIR%\
echo.
pause
exit /b 0

:fail
echo.
echo Release build failed.
echo.
pause
exit /b 1

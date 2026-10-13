@echo off
setlocal
rem ============================================================================
rem  Town of GHost - one-time package builder (run this on YOUR PC only)
rem
rem  1) Builds TownOfGHost.dll against your BepInEx copy of Among Us.
rem  2) Makes TownOfGHost-Package\ and TownOfGHost.zip next to this file, laid out
rem     like Town of Host's download: BepInEx\ (core + plugins\TownOfGHost.dll),
rem     dotnet\, winhttp.dll, doorstop_config.ini, .doorstop_version.
rem  Your own config, logs, interop and cache are NOT included: everyone gets the
rem  defaults, and BepInEx prepares the rest on their first (slower) launch.
rem
rem  Use:  double-click it and paste/drag your BepInEx Among Us folder when asked,
rem        or run:  make-package.bat "D:\Games\Among Us BepInEx"
rem ============================================================================

cd /d "%~dp0"
set "AU=%~1"
if not "%AU%"=="" goto :havepath
echo.
echo Drag your BepInEx Among Us folder (the one with Among Us.exe) into this window,
set /p "AU=then press Enter: "
:havepath
set "AU=%AU:"=%"
if "%AU:~-1%"=="\" set "AU=%AU:~0,-1%"

if not exist "%AU%\Among Us.exe" goto :badgame
if not exist "%AU%\BepInEx\core" goto :nobepinex
if not exist "%AU%\BepInEx\interop\Assembly-CSharp.dll" goto :nointerop
if not exist "%AU%\winhttp.dll" goto :nobepinex
if not exist "%AU%\dotnet" goto :nobepinex

echo.
echo [1/3] Building TownOfGHost.dll ...
dotnet build -c Release "-p:AmongUsDir=%AU%"
if errorlevel 1 goto :buildfail
if not exist "bin\Release\net6.0\TownOfGHost.dll" goto :buildfail

echo.
echo [2/3] Assembling TownOfGHost-Package ...
set "OUT=%~dp0TownOfGHost-Package"
if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%\BepInEx\plugins"

copy /y "%AU%\winhttp.dll" "%OUT%\" >nul
if exist "%AU%\doorstop_config.ini" copy /y "%AU%\doorstop_config.ini" "%OUT%\" >nul
if exist "%AU%\.doorstop_version" copy /y "%AU%\.doorstop_version" "%OUT%\" >nul

robocopy "%AU%\dotnet" "%OUT%\dotnet" /E /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 goto :copyfail
robocopy "%AU%\BepInEx\core" "%OUT%\BepInEx\core" /E /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 goto :copyfail

copy /y "bin\Release\net6.0\TownOfGHost.dll" "%OUT%\BepInEx\plugins\" >nul
if errorlevel 1 goto :copyfail

echo.
echo [3/3] Zipping ...
if exist "%~dp0TownOfGHost.zip" del /q "%~dp0TownOfGHost.zip"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path (Get-ChildItem -Force -LiteralPath '%OUT%' | ForEach-Object { $_.FullName }) -DestinationPath '%~dp0TownOfGHost.zip' -Force"
if errorlevel 1 goto :zipfail

echo.
echo Done.
echo   Folder: TownOfGHost-Package
echo   Zip:    TownOfGHost.zip
echo Players unzip it into their Among Us folder (next to Among Us.exe) and start the game.
echo The first launch is slower while BepInEx prepares itself; after that it is normal.
echo Rebuild this package after every Among Us update.
goto :end

:badgame
echo.
echo ERROR: "Among Us.exe" was not found in:
echo   %AU%
echo Give the folder that contains Among Us.exe.
goto :end

:nobepinex
echo.
echo ERROR: BepInEx is not installed in that folder (missing BepInEx\core, dotnet or winhttp.dll).
goto :end

:nointerop
echo.
echo ERROR: BepInEx\interop is missing. Start Among Us once from that folder (wait for the
echo main menu), close it, then run this again.
goto :end

:buildfail
echo.
echo ERROR: the build failed - see the messages above.
goto :end

:copyfail
echo.
echo ERROR: copying files into TownOfGHost-Package failed.
goto :end

:zipfail
echo.
echo ERROR: making the zip failed. The folder TownOfGHost-Package is still ready to use.
goto :end

:end
echo.
pause
endlocal

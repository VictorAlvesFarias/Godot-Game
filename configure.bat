@echo off
setlocal EnableDelayedExpansion

:: ==============================
:: Check for ADMIN rights
:: ==============================
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"%~f0\"' -Verb RunAs"
    exit /b
)

:: ==============================
:: Configuration
:: ==============================
set version=4.6
set url=https://github.com/godotengine/godot/releases/download/%version%-stable/Godot_v%version%-stable_mono_win64.zip

set tempZip=%TEMP%\godot_dotnet.zip
set tempExtract=%TEMP%\godot_dotnet
set installDir=C:\Tools\Godot

echo.
echo ==============================
echo Installing Godot .NET v%version%
echo ==============================
echo.

:: ==============================
:: Download
:: ==============================
echo [1/6] Downloading...

where curl >nul 2>&1
if %errorlevel%==0 (
    curl -L "%url%" -o "%tempZip%"
) else (
    echo curl not found, using bitsadmin...
    bitsadmin /transfer myDownload "%url%" "%tempZip%"
)

if not exist "%tempZip%" (
    echo ERROR: Download failed.
    pause
    exit /b
)

:: ==============================
:: Extract
:: ==============================
echo [2/6] Extracting...

if exist "%tempExtract%" rmdir /s /q "%tempExtract%"
mkdir "%tempExtract%"

tar -xf "%tempZip%" -C "%tempExtract%" >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR while extracting.
    pause
    exit /b
)

:: ==============================
:: Prepare install directory
:: ==============================
echo [3/6] Preparing install directory...

if exist "%installDir%" rmdir /s /q "%installDir%"
mkdir "%installDir%"

for /d %%i in ("%tempExtract%\*") do (
    xcopy "%%i\*" "%installDir%\" /E /H /Y >nul
    goto :copied
)

:copied

:: ==============================
:: Rename executables
:: ==============================
echo [4/6] Adjusting executables...

set foundMain=0
set foundConsole=0

for %%f in ("%installDir%\*.exe") do (
    echo %%~nxf | findstr /i "console" >nul
    if !errorlevel! == 0 (
        ren "%%f" godot_console.exe
        set foundConsole=1
    ) else (
        if !foundMain! == 0 (
            ren "%%f" godot.exe
            set foundMain=1
        )
    )
)

:: ==============================
:: Cleanup
:: ==============================
echo [5/6] Cleaning up...

rmdir /s /q "%tempExtract%"
del "%tempZip%"

:: ==============================
:: Update PATH
:: ==============================
echo [6/6] Updating PATH...

echo %PATH% | find /i "%installDir%" >nul
if errorlevel 1 (
    setx PATH "%PATH%;%installDir%" /M >nul
    echo PATH updated.
) else (
    echo PATH already contains the directory.
)

:: ==============================
:: Done
:: ==============================
echo.
echo ==============================
echo Installation completed!
echo Location: %installDir%
echo Executable: godot.exe
echo Console: godot_console.exe
echo ==============================
echo.

pause
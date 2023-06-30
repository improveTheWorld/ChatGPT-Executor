@echo off
setlocal enabledelayedexpansion

:: Install/Uninstall Inno Setup

:: Set paths
set "InnoVersion=6.2.2"
set "InnoPath=%ProgramFiles(x86)%\Inno Setup 6"

:: Check if Inno Setup is installed
if exist "%InnoPath%" goto checkISCC
goto installPrompt

:checkISCC
if exist "%InnoPath%\ISCC.exe" goto uninstallPrompt
goto installPrompt

:installPrompt
set /p UserInput=Inno Setup is not installed. Do you want to install it (y/n)?
if /i "%UserInput%"=="y" goto install
goto end

:install
:: Install Inno Setup
powershell -Command "Invoke-WebRequest -Uri 'http://files.jrsoftware.org/is/6/innosetup-%InnoVersion%.exe' -OutFile 'innosetup-%InnoVersion%.exe'"
innosetup-%InnoVersion%.exe /VERYSILENT

:addPath
:: Add to system PATH
for /F "tokens=2,* delims= " %%A in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v PATH ^| findstr /i path') do set "SysPath=%%B"
setx /M PATH "%SysPath%;%InnoPath%"

goto end

:uninstallPrompt
set /p UserInput=Inno Setup is installed. Do you want to uninstall it (y/n)?
if /i "%UserInput%"=="y" goto uninstall
goto end

:uninstall
:: Uninstall Inno Setup
"%InnoPath%\unins000.exe" /silent

:: Remove from PATH
for /F "tokens=2,* delims= " %%A in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v PATH ^| findstr /i path') do set "SysPath=%%B"
set "NewPath="
for %%A in ("%SysPath:;=" "%"") do (
    if /I not "%%~A"=="%InnoPath%" (
        if "!NewPath!"=="" (
            set "NewPath=%%~A"
        ) else (
            set "NewPath=!NewPath!;%%~A"
        )
    )
)

:: Update system PATH
setx /M PATH "%NewPath%"

echo Uninstallation complete.
goto end

:end
endlocal
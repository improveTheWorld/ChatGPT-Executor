@echo off
setlocal enabledelayedexpansion

rem Get the version number from the file
set "version="
for /f "tokens=* delims=" %%F in (..\Version) do set "version=%%F"

rem Check if version is not empty
if not "!version!"=="" (
    rem Build the project and redirect only the error output
    dotnet build ../src --configuration Release /p:Version=%version% /p:Force=true 2> build_errors.txt

    rem Check if the error output is empty
    rem ...
) else (
    echo "Error: Failed to retrieve the version number from the file."
)


rem Check if the error output is empty
for %%I in (build_errors.txt) do set fileSize=%%~zI
if !fileSize!==0 (
    echo Compilation succeeded, creating zip file...
    
    rem Delete existing zip file if it exists
    if exist "..\dist\Executor_%version%.zip" (
        del "..\dist\Executor_%version%.zip"
    )

    ISCC CreateSetup.iss /DMyAppVersion=%version% 
    rem Zip the output files
    powershell -nologo -noprofile -command "& { Add-Type -A 'System.IO.Compression.FileSystem'; [IO.Compression.ZipFile]::CreateFromDirectory('..\src\bin\Release\net6.0', '..\dist\Executor_%version%.zip'); }"
    
    echo Zip file created successfully.

    rem Clean the bin and obj folders
    echo Cleaning up the bin and obj folders...
    rmdir /s /q "..\src\bin"
    rmdir /s /q "..\src\obj"
    echo Clean-up completed.
    
) else (
    echo Compilation failed. Please check the build_errors.txt for more information.
)
rem Clean up
del build_errors.txt

:end

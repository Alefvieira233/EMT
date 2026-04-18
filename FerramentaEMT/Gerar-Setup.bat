@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "POWERSHELL_EXE=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
set "BUILD_SCRIPT=%SCRIPT_DIR%installer\Build-SetupExe.ps1"

if not exist "%BUILD_SCRIPT%" (
    echo Script nao encontrado:
    echo %BUILD_SCRIPT%
    exit /b 1
)

echo Gerando setup da versao atual...
"%POWERSHELL_EXE%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%BUILD_SCRIPT%"
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
    echo.
    echo Falha ao gerar o setup. Codigo: %EXIT_CODE%
    exit /b %EXIT_CODE%
)

echo.
echo Setup gerado com sucesso.
echo Verifique a pasta:
echo %SCRIPT_DIR%artifacts\installer
exit /b 0

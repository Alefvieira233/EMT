@echo off
setlocal EnableDelayedExpansion
chcp 65001 >nul

REM ============================================================
REM  FerramentaEMT - Compilar Debug (com PDB para debugging)
REM ============================================================
REM  Compila em modo Debug com símbolos PDB completos.
REM  Use este script quando precisar anexar o debugger do Visual
REM  Studio ao processo Revit.exe e colocar breakpoints.
REM ============================================================

echo.
echo ============================================================
echo   FerramentaEMT - Compilacao DEBUG
echo ============================================================
echo.

REM Verificar se Revit esta aberto
tasklist /FI "IMAGENAME eq Revit.exe" 2>NUL | find /I /N "Revit.exe" >NUL
if "%ERRORLEVEL%"=="0" (
    echo [ERRO] Revit.exe esta em execucao!
    echo Feche o Revit antes de compilar.
    echo.
    pause
    exit /b 1
)

REM Verificar dotnet
where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERRO] dotnet nao encontrado no PATH.
    echo Execute Instalar-DotNet-SDK.bat primeiro.
    pause
    exit /b 1
)

cd /d "%~dp0"

echo [1/3] Limpando build anterior...
if exist "bin\Debug" rmdir /S /Q "bin\Debug"
if exist "obj\Debug" rmdir /S /Q "obj\Debug"

echo.
echo [2/3] Compilando em modo DEBUG...
dotnet build -c Debug --verbosity minimal
if errorlevel 1 (
    echo.
    echo [ERRO] Falha na compilacao.
    pause
    exit /b 1
)

echo.
echo [3/3] Gerando .addin e copiando para AppData...

set "ADDIN_DIR=%AppData%\Autodesk\Revit\Addins\2025"
set "DEPLOY_DIR=%~dp0artifacts\deploy\Debug\net8.0-windows"
set "DLL_PATH=%ADDIN_DIR%\FerramentaEMT\FerramentaEMT.dll"

if not exist "%ADDIN_DIR%" mkdir "%ADDIN_DIR%"
if not exist "%ADDIN_DIR%\FerramentaEMT" mkdir "%ADDIN_DIR%\FerramentaEMT"

REM Copiar binarios DEBUG
xcopy "%DEPLOY_DIR%\*" "%ADDIN_DIR%\FerramentaEMT\" /E /Y /Q >nul
if errorlevel 1 (
    echo [ERRO] Falha ao copiar binarios.
    pause
    exit /b 1
)

REM Gerar .addin
(
echo ^<?xml version="1.0" encoding="utf-8" standalone="no"?^>
echo ^<RevitAddIns^>
echo   ^<AddIn Type="Application"^>
echo     ^<Name^>FerramentaEMT [DEBUG]^</Name^>
echo     ^<Assembly^>%DLL_PATH%^</Assembly^>
echo     ^<AddInId^>4F1C4FBE-DEBE-4DEB-DEBE-DEBE4F1C4FBE^</AddInId^>
echo     ^<FullClassName^>FerramentaEMT.App^</FullClassName^>
echo     ^<VendorId^>EMT^</VendorId^>
echo     ^<VendorDescription^>EMT Estruturas Metalicas - DEBUG BUILD^</VendorDescription^>
echo   ^</AddIn^>
echo ^</RevitAddIns^>
) > "%ADDIN_DIR%\FerramentaEMT.addin"

echo.
echo ============================================================
echo   BUILD DEBUG CONCLUIDO COM SUCESSO!
echo ============================================================
echo.
echo Para anexar o debugger:
echo   1. Abra o Visual Studio
echo   2. Abra o projeto FerramentaEMT.sln
echo   3. Inicie o Revit 2025
echo   4. No VS: Debug ^> Attach to Process ^> Revit.exe
echo   5. Coloque breakpoints no codigo
echo.
pause

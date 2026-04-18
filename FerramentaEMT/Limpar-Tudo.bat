@echo off
setlocal EnableDelayedExpansion
chcp 65001 >nul

REM ============================================================
REM  FerramentaEMT - Limpar Tudo
REM ============================================================
REM  Remove:
REM   - Pastas bin/, obj/, artifacts/ do projeto
REM   - Instalacao em %AppData%\Autodesk\Revit\Addins\2025\
REM   - Cache do .NET (.dotnet, .nuget temp)
REM ============================================================

echo.
echo ============================================================
echo   FerramentaEMT - LIMPEZA COMPLETA
echo ============================================================
echo.
echo Esta operacao vai REMOVER:
echo   [1] Pastas de build local (bin/, obj/, artifacts/)
echo   [2] Instalacao do Revit em %%AppData%%\Autodesk\Revit\Addins\2025\
echo.
set /p CONFIRM="Confirmar? (S/N): "
if /I not "%CONFIRM%"=="S" (
    echo Cancelado.
    exit /b 0
)

REM Verificar se Revit esta aberto
tasklist /FI "IMAGENAME eq Revit.exe" 2>NUL | find /I /N "Revit.exe" >NUL
if "%ERRORLEVEL%"=="0" (
    echo [ERRO] Revit.exe esta em execucao!
    echo Feche o Revit antes de limpar.
    pause
    exit /b 1
)

cd /d "%~dp0"

echo.
echo [1/3] Removendo bin/, obj/, artifacts/...
if exist "bin" rmdir /S /Q "bin"
if exist "obj" rmdir /S /Q "obj"
if exist "artifacts" rmdir /S /Q "artifacts"
echo   OK

echo.
echo [2/3] Removendo instalacao do Revit...
set "ADDIN_DIR=%AppData%\Autodesk\Revit\Addins\2025"
if exist "%ADDIN_DIR%\FerramentaEMT" (
    rmdir /S /Q "%ADDIN_DIR%\FerramentaEMT"
    echo   Pasta removida.
)
if exist "%ADDIN_DIR%\FerramentaEMT.addin" (
    del /F /Q "%ADDIN_DIR%\FerramentaEMT.addin"
    echo   .addin removido.
)
echo   OK

echo.
echo [3/3] Limpando logs antigos...
set "LOG_DIR=%LocalAppData%\FerramentaEMT\logs"
if exist "%LOG_DIR%" (
    rmdir /S /Q "%LOG_DIR%"
    echo   Logs removidos.
)
echo   OK

echo.
echo ============================================================
echo   LIMPEZA CONCLUIDA
echo ============================================================
echo.
echo Para recompilar do zero, execute: Compilar-e-Instalar.bat
echo.
pause

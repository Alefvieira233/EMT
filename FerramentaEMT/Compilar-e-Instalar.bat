@echo off
setlocal EnableDelayedExpansion
chcp 65001 >nul

REM ============================================================
REM   FerramentaEMT - Compilar e Instalar no Revit 2025
REM   Criado para o ALEF compilar sem depender do Victor
REM ============================================================

cd /d "%~dp0"

echo.
echo ============================================================
echo   FerramentaEMT - Build ^& Deploy
echo ============================================================
echo.
echo Pasta do projeto: %CD%
echo.

REM --- 1. Verificar se o Revit esta aberto -------------------
tasklist /FI "IMAGENAME eq Revit.exe" 2>NUL | find /I "Revit.exe" >NUL
if not errorlevel 1 (
    echo [AVISO] O Revit esta aberto!
    echo         Feche o Revit antes de continuar para evitar
    echo         erro de "arquivo em uso" ao copiar a DLL.
    echo.
    pause
    exit /b 1
)

REM --- 2. Localizar o dotnet --------------------------------
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERRO] dotnet nao encontrado no PATH.
    echo        Instale o .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [OK] .NET SDK encontrado:
dotnet --version
echo.

REM --- 3. Limpar build anterior ------------------------------
echo [1/4] Limpando build anterior...
if exist "bin"        rd /s /q "bin"        2>nul
if exist "obj"        rd /s /q "obj"        2>nul
if exist "artifacts"  rd /s /q "artifacts"  2>nul
echo.

REM --- 4. Compilar -------------------------------------------
echo [2/4] Compilando FerramentaEMT (Release)...
echo.
dotnet build "FerramentaEMT.csproj" -c Release --nologo
if errorlevel 1 (
    echo.
    echo [ERRO] Build falhou. Veja as mensagens acima.
    pause
    exit /b 1
)
echo.

REM --- 5. Localizar a DLL gerada -----------------------------
set "DLL_SRC=%CD%\bin\Release\net8.0-windows\FerramentaEMT.dll"
if not exist "%DLL_SRC%" (
    echo [ERRO] DLL nao encontrada em: %DLL_SRC%
    pause
    exit /b 1
)

REM --- 6. Pasta de add-ins do Revit --------------------------
set "ADDIN_DIR=%APPDATA%\Autodesk\Revit\Addins\2025"
if not exist "%ADDIN_DIR%" mkdir "%ADDIN_DIR%"

REM --- 7. Gerar .addin apontando pro DLL local ---------------
echo [3/4] Gerando manifesto .addin para Revit 2025...
set "DLL_DEST=%ADDIN_DIR%\FerramentaEMT\FerramentaEMT.dll"
if not exist "%ADDIN_DIR%\FerramentaEMT" mkdir "%ADDIN_DIR%\FerramentaEMT"

(
echo ^<?xml version="1.0" encoding="utf-8" standalone="no"?^>
echo ^<RevitAddIns^>
echo 	^<AddIn Type="Application"^>
echo 		^<Name^>FerramentaEMT^</Name^>
echo 		^<Assembly^>%DLL_DEST%^</Assembly^>
echo 		^<AddInId^>610FE337-F95D-4813-8BF8-2CE11C9948C1^</AddInId^>
echo 		^<FullClassName^>FerramentaEMT.App^</FullClassName^>
echo 		^<VendorId^>EMT^</VendorId^>
echo 		^<VendorDescription^>Ferramenta EMT^</VendorDescription^>
echo 	^</AddIn^>
echo ^</RevitAddIns^>
) > "%ADDIN_DIR%\FerramentaEMT.addin"
echo.

REM --- 8. Copiar binarios pro AppData ------------------------
echo [4/4] Copiando binarios para o Revit...
xcopy /Y /E /I /Q "bin\Release\net8.0-windows\*" "%ADDIN_DIR%\FerramentaEMT\" >nul
if errorlevel 1 (
    echo [ERRO] Falha ao copiar arquivos.
    pause
    exit /b 1
)
echo.

echo ============================================================
echo   SUCESSO! FerramentaEMT instalado para Revit 2025
echo ============================================================
echo.
echo Manifesto: %ADDIN_DIR%\FerramentaEMT.addin
echo DLL:       %DLL_DEST%
echo.
echo Agora abra o Revit 2025 e a aba "Fabricacao" deve aparecer
echo com os botoes da FerramentaEMT.
echo.
pause

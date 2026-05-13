@echo off
setlocal EnableDelayedExpansion

rem ============================================================
rem INSTALAR.bat - Compila e instala SteelBIM no Revit 2025
rem ============================================================

set "REPO_DIR=%~dp0"
set "PROJ_DIR=%REPO_DIR%SteelBIM"
set "BUILD_OUT=%PROJ_DIR%\bin\Release\net8.0-windows"
set "DLL_SRC=%BUILD_OUT%\SteelBIM.dll"
set "ADDIN_DIR=%APPDATA%\Autodesk\Revit\Addins\2025"
set "ADDIN_SUBDIR=%ADDIN_DIR%\SteelBIM"
set "ADDIN_MANIFEST=%ADDIN_DIR%\SteelBIM.addin"

echo.
echo ============================================================
echo   SteelBIM - Compilar e Instalar no Revit 2025
echo ============================================================
echo.
echo [DEBUG] REPO_DIR  = [%REPO_DIR%]
echo [DEBUG] PROJ_DIR  = [%PROJ_DIR%]
echo [DEBUG] BUILD_OUT = [%BUILD_OUT%]
echo.

rem ---- 1. Verificar se Revit esta aberto ---------------------
tasklist /FI "IMAGENAME eq Revit.exe" 2>nul | find /I "Revit.exe" >nul
if not errorlevel 1 (
    echo [AVISO] O Revit esta aberto. Feche-o e rode de novo.
    pause
    exit /b 1
)

rem ---- 2. Verificar dotnet ----------------------------------
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERRO] dotnet nao encontrado no PATH.
    echo Instale: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [OK] .NET SDK encontrado:
dotnet --version
echo.

rem ---- 3. Limpar build anterior -----------------------------
echo [1/4] Limpando build anterior...
if exist "%PROJ_DIR%\bin" rd /s /q "%PROJ_DIR%\bin"
if exist "%PROJ_DIR%\obj" rd /s /q "%PROJ_DIR%\obj"
echo.

rem ---- 4. Compilar ------------------------------------------
echo [2/4] Compilando SteelBIM (Release)...
echo.
dotnet build "%PROJ_DIR%\SteelBIM.csproj" -c Release --nologo
if errorlevel 1 (
    echo.
    echo [ERRO] Build falhou.
    pause
    exit /b 1
)
echo.

rem ---- 5. Conferir se a DLL foi gerada ----------------------
if not exist "%DLL_SRC%" (
    echo [ERRO] SteelBIM.dll nao encontrada em:
    echo %DLL_SRC%
    pause
    exit /b 1
)

rem ---- 6. Preparar pasta destino ----------------------------
echo [3/4] Preparando pasta de instalacao...
if not exist "%ADDIN_DIR%" mkdir "%ADDIN_DIR%"
if not exist "%ADDIN_SUBDIR%" mkdir "%ADDIN_SUBDIR%"
echo       %ADDIN_SUBDIR%
echo.

rem ---- 7. Gerar .addin manifest -----------------------------
echo [4/4] Gerando manifest e copiando binarios...

> "%ADDIN_MANIFEST%" echo ^<?xml version="1.0" encoding="utf-8" standalone="no"?^>
>> "%ADDIN_MANIFEST%" echo ^<RevitAddIns^>
>> "%ADDIN_MANIFEST%" echo   ^<AddIn Type="Application"^>
>> "%ADDIN_MANIFEST%" echo     ^<Name^>SteelBIM^</Name^>
>> "%ADDIN_MANIFEST%" echo     ^<Assembly^>%ADDIN_SUBDIR%\SteelBIM.dll^</Assembly^>
>> "%ADDIN_MANIFEST%" echo     ^<AddInId^>610FE337-F95D-4813-8BF8-2CE11C9948C1^</AddInId^>
>> "%ADDIN_MANIFEST%" echo     ^<FullClassName^>SteelBIM.App^</FullClassName^>
>> "%ADDIN_MANIFEST%" echo     ^<VendorId^>EMT^</VendorId^>
>> "%ADDIN_MANIFEST%" echo     ^<VendorDescription^>Ferramenta EMT^</VendorDescription^>
>> "%ADDIN_MANIFEST%" echo   ^</AddIn^>
>> "%ADDIN_MANIFEST%" echo ^</RevitAddIns^>

echo       Manifest: %ADDIN_MANIFEST%

rem ---- 8. Copiar binarios -----------------------------------
xcopy /Y /E /I /Q "%BUILD_OUT%\*" "%ADDIN_SUBDIR%\" >nul
if errorlevel 1 (
    echo [ERRO] Falha ao copiar arquivos.
    pause
    exit /b 1
)

echo       Arquivos copiados.
echo.

rem ---- 9. Resumo final --------------------------------------
echo ============================================================
echo   SUCESSO! SteelBIM instalado para Revit 2025
echo ============================================================
echo.
echo   DLL:      %ADDIN_SUBDIR%\SteelBIM.dll
echo   Manifest: %ADDIN_MANIFEST%
echo.
echo   Abra o Revit 2025. Se pedir autorizacao, permita.
echo.
pause

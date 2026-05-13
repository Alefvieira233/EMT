@echo off
chcp 65001 >nul
echo ============================================================
echo   Diagnostico de SDKs/Build para FerramentaEMT
echo ============================================================
echo.

echo --- Onde esta o dotnet ---
where dotnet
echo.

echo --- Lista de SDKs instalados ---
dotnet --list-sdks
echo.

echo --- Lista de Runtimes ---
dotnet --list-runtimes
echo.

echo --- Procurando MSBuild.exe em locais padrao ---
for %%P in (
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
) do (
    if exist %%P (
        echo [ENCONTRADO] %%P
    ) else (
        echo [nao existe] %%P
    )
)
echo.

echo --- vswhere (descobre VS instalado) ---
if exist "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" (
    "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -all -property installationPath
) else (
    echo vswhere nao encontrado
)
echo.

echo --- Verificando winget ---
where winget
echo.

pause

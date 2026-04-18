@echo off
chcp 65001 >nul
echo ============================================================
echo   Instalando .NET 8 SDK via winget
echo ============================================================
echo.
echo Pode aparecer um prompt do Windows pedindo permissao (UAC).
echo Se aparecer, clique SIM para autorizar a instalacao.
echo.
echo Aguardando download e instalacao (pode levar alguns minutos)...
echo.

winget install Microsoft.DotNet.SDK.8 --silent --accept-source-agreements --accept-package-agreements

echo.
echo ============================================================
echo   Verificando instalacao
echo ============================================================
dotnet --list-sdks
echo.
echo Se aparecer "8.x.xxx" acima, o SDK foi instalado com sucesso.
echo Agora rode o "Compilar-e-Instalar.bat" novamente.
echo.
pause

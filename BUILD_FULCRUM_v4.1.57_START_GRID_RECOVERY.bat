@echo off
setlocal
title Fulcrum v4.1.57 - Start Grid Recovery

set "ROOT=%~dp0"
set "SCRIPT=%ROOT%Build-Fulcrum-v4.1.57-START-GRID-RECOVERY.ps1"
set "DIST=%ROOT%dist"

if not exist "%SCRIPT%" (
  echo.
  echo ERROR: No se encontro el script de compilacion:
  echo %SCRIPT%
  echo.
  pause
  exit /b 2
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%"
if errorlevel 1 (
  echo.
  echo ERROR DE COMPILACION
  echo Revisa el mensaje mostrado arriba.
  pause
  exit /b 1
)

if not exist "%DIST%\Fulcrum.Core.dll" (
  echo.
  echo ERROR: No se genero Fulcrum.Core.dll
  pause
  exit /b 3
)
if not exist "%DIST%\Fulcrum.Plugin.dll" (
  echo.
  echo ERROR: No se genero Fulcrum.Plugin.dll
  pause
  exit /b 4
)

echo.
echo ================================================
echo  BUILD COMPLETADO CORRECTAMENTE
echo ================================================
echo.
echo   %DIST%\Fulcrum.Core.dll
echo   %DIST%\Fulcrum.Plugin.dll
echo.
pause
exit /b 0

@echo off
setlocal
cd /d "%~dp0"
set "EE_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "EE_PY=%~1"
if "%EE_PY%"=="" set "EE_PY=python"
if not exist Evidence mkdir Evidence
if not exist Runs mkdir Runs
if not exist Runtime\Lib mkdir Runtime\Lib
set "EE_SRC=Source"
if not exist Source\EngineModel.cs set "EE_SRC=."
"%EE_CSC%" /nologo /target:winexe /platform:x64 /out:EvansEngines.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.DataVisualization.dll "%EE_SRC%\EngineModel.cs" "%EE_SRC%\CamProfile.cs" "%EE_SRC%\EvanEngines.cs" "%EE_SRC%\LiveBench.cs" "%EE_SRC%\Checks.cs"
if errorlevel 1 exit /b 1
"%EE_PY%" -m pip install --target Runtime\Lib -r requirements.txt --upgrade
if errorlevel 1 exit /b 1
"%EE_PY%" "%EE_SRC%\package_runtime.py"
if errorlevel 1 exit /b 1
set "EE_SW_API=C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll"
if exist "%EE_SW_API%" (
  "%EE_CSC%" /nologo /target:exe /platform:x64 /out:CadReadback.exe /reference:"%EE_SW_API%" /reference:System.Web.Extensions.dll "%EE_SRC%\CadReadback.cs"
  if errorlevel 1 exit /b 1
  copy /y "%EE_SW_API%" . >nul
)
start /wait "" EvansEngines.exe --self-test
if errorlevel 1 exit /b 1
Runtime\Python\python.exe "%EE_SRC%\cycle_solver.py" --self-test
if errorlevel 1 exit /b 1
echo Build and numerical checks passed.

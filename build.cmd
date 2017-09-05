:: the windows shell, so amazing

:: options
@echo Off
cd %~dp0
setlocal

:: determine dirs
set NUGET_CACHE_DIR=%LocalAppData%\.nuget\v4.1.0
set NUGET_LOCAL_DIR=.nuget\v4.1.0

:: download nuget to cache dir
set NUGET_URL=https://dist.nuget.org/win-x86-commandline/v4.1.0/nuget.exe
if not exist %NUGET_CACHE_DIR%\NuGet.exe (
  if not exist %NUGET_CACHE_DIR% md %NUGET_CACHE_DIR%
  echo Downloading '%NUGET_URL%'' to '%NUGET_CACHE_DIR%\NuGet.exe'...
  @powershell -NoProfile -ExecutionPolicy unrestricted -Command "$ProgressPreference = 'SilentlyContinue'; Invoke-WebRequest '%NUGET_URL%' -OutFile '%NUGET_CACHE_DIR%\NuGet.exe'"
)

:: copy nuget locally
if not exist %NUGET_LOCAL_DIR% md %NUGET_LOCAL_DIR%
if not exist %NUGET_LOCAL_DIR%\NuGet.exe (
  copy %NUGET_CACHE_DIR%\NuGet.exe %NUGET_LOCAL_DIR%\NuGet.exe > nul
)

:: restore packages
%NUGET_LOCAL_DIR%\NuGet.exe restore .\packages.config -PackagesDirectory ./build-packages -MSBuildVersion 15 -Verbosity quiet

:: find VS install path
for /f "usebackq tokens=*" %%i in (`build-packages\vswhere.2.1.3\tools\vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath`) do (
  set VS_INSTALL_PATH=%%i
)

:: run script
"%VS_INSTALL_PATH%\MSBuild\15.0\Bin\Roslyn\csi.exe" build.csx %*

:: the windows shell, so amazing

:: options
@echo Off
cd %~dp0
setlocal

:: determine dirs
set NUGET_CACHE_DIR=%LocalAppData%\.nuget\v4.0.0
set NUGET_LOCAL_DIR=src\.nuget\v4.0.0

:: download nuget to cache dir
set NUGET_URL=https://dist.nuget.org/win-x86-commandline/v4.0.0/nuget.exe
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
%NUGET_LOCAL_DIR%\NuGet.exe restore .\packages.config -PackagesDirectory ./src/packages -MSBuildVersion 15 -Verbosity quiet

:: run script
"%ProgramFiles(x86)%\MSBuild\14.0\Bin\csi.exe" build.csx %*

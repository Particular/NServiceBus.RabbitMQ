:: the windows shell, so amazing

:: options
@echo Off
cd %~dp0
setlocal

:: determine cache dir
set COLA_CACHE_DIR=%LocalAppData%\Cola





:: download cola to cache dir
set COLA_URL="https://bintray.com/adamralph/apps/download_file?file_path=cola.exe"
if not exist %COLA_CACHE_DIR%\cola.exe (
  if not exist %COLA_CACHE_DIR% md %COLA_CACHE_DIR%
  echo Downloading latest version of cola.exe...
  @powershell -NoProfile -ExecutionPolicy unrestricted -Command "$ProgressPreference = 'SilentlyContinue'; Invoke-WebRequest '%COLA_URL%' -OutFile '%COLA_CACHE_DIR%\cola.exe'"
  if %errorlevel% neq 0 exit /b %errorlevel%
)

:: copy cola locally
if not exist .cola\cola.exe (
  if not exist .cola md .cola
  copy %COLA_CACHE_DIR%\cola.exe .cola\cola.exe > nul
  if %errorlevel% neq 0 exit /b %errorlevel%
)

:: run script
.cola\cola.exe build.linq
if %errorlevel% neq 0 exit /b %errorlevel%

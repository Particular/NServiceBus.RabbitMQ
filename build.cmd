:: the windows shell, so amazing

:: options
@echo Off
cd %~dp0
setlocal

:: determine cache dir
set DUDE_CACHE_DIR=%LocalAppData%\dude





:: download dude to cache dir
set DUDE_URL="https://bintray.com/adamralph/apps/download_file?file_path=dude.exe"
if not exist %DUDE_CACHE_DIR%\dude.exe (
  if not exist %DUDE_CACHE_DIR% md %DUDE_CACHE_DIR%
  echo Downloading latest version of dude.exe...
  @powershell -NoProfile -ExecutionPolicy unrestricted -Command "$ProgressPreference = 'SilentlyContinue'; Invoke-WebRequest '%DUDE_URL%' -OutFile '%DUDE_CACHE_DIR%\dude.exe'"
  if %errorlevel% neq 0 exit /b %errorlevel%
)

:: copy dude locally
if not exist .dude\dude.exe (
  if not exist .dude md .dude
  copy %DUDE_CACHE_DIR%\dude.exe .dude\dude.exe > nul
  if %errorlevel% neq 0 exit /b %errorlevel%
)

:: run script
.dude\dude.exe build.csx %*
if %errorlevel% neq 0 exit /b %errorlevel%

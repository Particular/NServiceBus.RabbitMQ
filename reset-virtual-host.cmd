@echo Off
cd %~dp0

dotnet run --project src/targets -- %*

# Code Coverage Guide

## PowerShell Script for Windows

**Generate local coverage report:**

```powershell
.\run-coverage.ps1                    # With automatic browser opening
.\run-coverage.ps1 -OpenReport:$false # Generate report only
```

## Bash Script for Linux/macOS

**Generate local coverage report:**

```bash
chmod +x run-coverage.sh
./run-coverage.sh                    # With automatic browser opening
./run-coverage.sh false              # Generate report only
```

## Additional Coverage Options

### 1. Using MSBuild Coverage (alternative method)

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=TestResults/coverage.cobertura.xml
```

### 2. Test specific assemblies only

```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverage.runsettings
```

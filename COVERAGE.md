# Code Coverage Guide

## Direct Commands (Cross-Platform)

**Generate local coverage report:**

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"Html;Badges;Cobertura;SonarQube"
```

Open the HTML report:

```bash
# Windows PowerShell
Start-Process "coverage-report/index.html"

# Linux
xdg-open coverage-report/index.html

# macOS
open coverage-report/index.html
```

If `reportgenerator` is missing:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
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

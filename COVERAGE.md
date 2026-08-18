# Code Coverage Guide

## Direct Commands (Cross-Platform)

**Generate local coverage report:**

```bash
dotnet test --project DenoHost.Tests/DenoHost.Tests.csproj --results-directory TestResults -- --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
reportgenerator -reports:"TestResults/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"Html;Badges;Cobertura;SonarQube"
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

### 1. Other coverage output formats

```bash
dotnet test --project DenoHost.Tests/DenoHost.Tests.csproj -- --coverage --coverage-output-format xml
```

Supported formats: `coverage` (binary), `xml`, `cobertura`.

### 2. Restrict what gets instrumented

```bash
dotnet test --project DenoHost.Tests/DenoHost.Tests.csproj -- --coverage --coverage-settings coverage.config
```

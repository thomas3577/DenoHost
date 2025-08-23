param(
    [switch]$OpenReport = $true,
    [string]$ReportTypes = "Html;Badges;Cobertura;SonarQube"
)

# Change to the project root directory (parent of tools folder)
Set-Location (Split-Path -Parent $PSScriptRoot)

Write-Host "Running tests with coverage collection..."

dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults

if ($LASTEXITCODE -ne 0) {
  Write-Host "Tests failed!" -ForegroundColor Red
  exit $LASTEXITCODE
}

Write-Host "Generating coverage report..."

try {
  reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:$ReportTypes

  if ($LASTEXITCODE -ne 0) {
    Write-Host "Report generation failed!" -ForegroundColor Red
    exit $LASTEXITCODE
  }
} catch {
  Write-Host "Report generation failed! ReportGenerator tool not found." -ForegroundColor Red
  Write-Host "Install with: dotnet tool install -g dotnet-reportgenerator-globaltool"
  exit 1
}

$coberturaPath = "coverage-report/Cobertura.xml"
if (Test-Path $coberturaPath) {
  [xml]$cobertura = Get-Content $coberturaPath
  $lineRate = [math]::Round([double]$cobertura.coverage.'line-rate' * 100, 1)
  $branchRate = [math]::Round([double]$cobertura.coverage.'branch-rate' * 100, 1)

  Write-Host ""
  Write-Host "Coverage Summary:"
  Write-Host "  Line Coverage:   $lineRate%"
  Write-Host "  Branch Coverage: $branchRate%"
  Write-Host ""
}

if ($OpenReport -and (Test-Path "coverage-report/index.html")) {
  Write-Host "Opening coverage report in browser..."
  Start-Process "coverage-report/index.html"
}

Write-Host "Coverage report generated successfully!"
Write-Host "Report location: coverage-report/index.html"

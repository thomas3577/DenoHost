param(
    [switch]$OpenReport = $true,
    [string]$ReportTypes = "Html;Badges;Cobertura;SonarQube"
)

Write-Host "🧪 Running tests with coverage collection..." -ForegroundColor Cyan

dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Tests failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "📊 Generating coverage report..." -ForegroundColor Cyan

reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:$ReportTypes

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Report generation failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

$coberturaPath = "coverage-report/Cobertura.xml"
if (Test-Path $coberturaPath) {
    [xml]$cobertura = Get-Content $coberturaPath
    $lineRate = [math]::Round([double]$cobertura.coverage.'line-rate' * 100, 1)
    $branchRate = [math]::Round([double]$cobertura.coverage.'branch-rate' * 100, 1)
    
    Write-Host ""
    Write-Host "📈 Coverage Summary:" -ForegroundColor Green
    Write-Host "   Line Coverage:   $lineRate%" -ForegroundColor Yellow
    Write-Host "   Branch Coverage: $branchRate%" -ForegroundColor Yellow
    Write-Host ""
}

if ($OpenReport -and (Test-Path "coverage-report/index.html")) {
    Write-Host "🌐 Opening coverage report in browser..." -ForegroundColor Cyan
    Start-Process "coverage-report/index.html"
}

Write-Host "✅ Coverage report generated successfully!" -ForegroundColor Green
Write-Host "📁 Report location: coverage-report/index.html" -ForegroundColor Gray
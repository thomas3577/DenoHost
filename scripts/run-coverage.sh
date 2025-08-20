#!/bin/bash

# Parameters with default values (matching PowerShell script)
OPEN_REPORT=${1:-true}
REPORT_TYPES=${2:-"Html;Badges;Cobertura;SonarQube"}

# Change to the project root directory (parent of tools folder)
cd "$(dirname "$0")/.."

echo "Running tests with coverage collection..."

dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults

if [ $? -ne 0 ]; then
  echo "Tests failed!"
  exit $?
fi

echo "Generating coverage report..."

if ! command -v reportgenerator > /dev/null; then
  echo "Report generation failed! ReportGenerator tool not found."
  echo "Install with: dotnet tool install -g dotnet-reportgenerator-globaltool"
  exit 1
fi

reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"$REPORT_TYPES"

if [ $? -ne 0 ]; then
  echo "Report generation failed!"
  exit $?
fi

# Extract coverage percentages from Cobertura.xml if it exists
COBERTURA_PATH="coverage-report/Cobertura.xml"
if [ -f "$COBERTURA_PATH" ]; then
  # Use xmllint to extract coverage rates (if available)
  if command -v xmllint > /dev/null; then
    LINE_RATE=$(xmllint --xpath "string(//@line-rate)" "$COBERTURA_PATH" 2>/dev/null)
    BRANCH_RATE=$(xmllint --xpath "string(//@branch-rate)" "$COBERTURA_PATH" 2>/dev/null)

    if [ -n "$LINE_RATE" ] && [ -n "$BRANCH_RATE" ]; then
      # Calculate percentages with one decimal place (matching PowerShell)
      LINE_PERCENT=$(echo "scale=1; $LINE_RATE * 100" | bc 2>/dev/null || echo "$LINE_RATE * 100" | awk '{printf "%.1f", $1 * 100}')
      BRANCH_PERCENT=$(echo "scale=1; $BRANCH_RATE * 100" | bc 2>/dev/null || echo "$BRANCH_RATE * 100" | awk '{printf "%.1f", $1 * 100}')

      echo ""
      echo "Coverage Summary:"
      echo "  Line Coverage:   ${LINE_PERCENT}%"
      echo "  Branch Coverage: ${BRANCH_PERCENT}%"
      echo ""
    fi
  fi
fi

# Open report if requested
if [ "$OPEN_REPORT" = "true" ] && [ -f "coverage-report/index.html" ]; then
  echo "Opening coverage report in browser..."

  # Open HTML report (Linux)
  if command -v xdg-open > /dev/null; then
    xdg-open coverage-report/index.html
  # Open HTML report (macOS)
  elif command -v open > /dev/null; then
    open coverage-report/index.html
  else
    echo "Could not open browser automatically. Please open coverage-report/index.html manually."
  fi
fi

echo "Coverage report generated successfully!"
echo "Report location: coverage-report/index.html"

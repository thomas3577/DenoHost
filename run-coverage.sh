#!/bin/bash

# Parameters with default values (matching PowerShell script)
OPEN_REPORT=${1:-true}
REPORT_TYPES=${2:-"Html;Badges;Cobertura;SonarQube"}

# Color definitions for consistent output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

echo -e "${CYAN}🧪 Running tests with coverage collection...${NC}"

dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults

if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Tests failed!${NC}"
    exit $?
fi

echo -e "${CYAN}📊 Generating coverage report...${NC}"

reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"$REPORT_TYPES"

if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Report generation failed!${NC}"
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
            echo -e "${GREEN}📈 Coverage Summary:${NC}"
            echo -e "${YELLOW}   Line Coverage:   ${LINE_PERCENT}%${NC}"
            echo -e "${YELLOW}   Branch Coverage: ${BRANCH_PERCENT}%${NC}"
            echo ""
        fi
    fi
fi

# Open report if requested
if [ "$OPEN_REPORT" = "true" ] && [ -f "coverage-report/index.html" ]; then
    echo -e "${CYAN}🌐 Opening coverage report in browser...${NC}"
    
    # Open HTML report (Linux)
    if command -v xdg-open > /dev/null; then
        xdg-open coverage-report/index.html
    # Open HTML report (macOS)  
    elif command -v open > /dev/null; then
        open coverage-report/index.html
    else
        echo -e "${YELLOW}⚠️  Could not open browser automatically. Please open coverage-report/index.html manually.${NC}"
    fi
fi

echo -e "${GREEN}✅ Coverage report generated successfully!${NC}"
echo -e "${GRAY}📁 Report location: coverage-report/index.html${NC}"

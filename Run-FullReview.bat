@echo off
REM Simple batch wrapper to run full review
echo.
echo ========================================
echo ScottWisper Full Codebase Review Tool
echo ========================================
echo.

if "%~1"=="--help" goto :help
if "%~1"=="-h" goto :help
if "%~1"=="/help" goto :help

REM Check if PowerShell is available
powershell -Command "Get-Variable" >nul 2>&1
if errorlevel 1 (
    echo ERROR: PowerShell is not available
    exit /b 1
)

REM Run the PowerShell script with all parameters
powershell -ExecutionPolicy Bypass -File "%~dp0Run-FullReview.ps1" %*
goto :end

:help
echo Usage: Run-FullReview.bat [options]
echo.
echo Options:
echo   -SkipBeads              Skip creating beads issues (reports only)
echo   -ReviewTypes "Type1,Type2"  Run specific reviews only
echo   -OutputDirectory "path"     Output directory for reports (default: current)
echo   -OpenReports            Open all generated reports after completion
echo   --help, -h, /help       Show this help message
echo.
echo Review Types:
echo   All                     Run all 7 reviews (default)
echo   Deployment              Deployment readiness
echo   Security                Security vulnerabilities
echo   Architecture            Code architecture
echo   Interface               UI/UX review
echo   Performance             Performance optimization
echo   ErrorHandling           Error handling and resilience
echo   Testing                 Test coverage and quality
echo.
echo Examples:
echo   Run-FullReview.bat                      Run all reviews
echo   Run-FullReview.bat -SkipBeads           Generate reports only
echo   Run-FullReview.bat -ReviewTypes "Security,Performance"
echo   Run-FullReview.bat -OpenReports         Run all and open reports
echo.
echo For more help, see: REVIEW_SYSTEM.md

:end
echo.

#Requires -Version 5.1

param([switch]$Force)

# IMPORTANT: Do NOT set ErrorActionPreference = Stop here.
# Stop mode causes the script to terminate silently on non-terminating errors,
# which means the Read-Host in the finally block is never reached and the window
# vanishes immediately.
$ErrorActionPreference = 'Continue'
$Script:ExitCode = 0

$ScriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectFile = Join-Path $ScriptDir 'src\BitChord.WinUI\BitChord.WinUI.csproj'
$ObjDir      = Join-Path $ScriptDir 'src\BitChord.WinUI\obj'

function Write-Step([string]$Text) {
    Write-Host ''
    Write-Host ('  >> ' + $Text) -ForegroundColor Cyan
}
function Write-Ok([string]$Text) {
    Write-Host ('  OK  ' + $Text) -ForegroundColor Green
}
function Write-Warn([string]$Text) {
    Write-Host ('  !!  ' + $Text) -ForegroundColor Yellow
}
function Write-Fail([string]$Text) {
    Write-Host ''
    Write-Host ('  ERR ' + $Text) -ForegroundColor Red
}

# ── Everything inside try/finally so Read-Host is ALWAYS reached ──────────────
try {

    Clear-Host
    Write-Host ''
    Write-Host '  BitChord WinUI 3 -- Launch Script' -ForegroundColor Magenta
    Write-Host '  ==================================' -ForegroundColor DarkGray
    Write-Host ''

    # 1. Check .NET SDK
    Write-Step 'Checking .NET SDK...'
    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnetCmd) {
        Write-Warn '.NET SDK not found on PATH.'
        Write-Warn 'Download from https://dot.net/download'
        Write-Warn 'The build step will fail until the SDK is installed.'
    } else {
        $ver = (& dotnet --version 2>&1)
        Write-Ok ('.NET SDK ' + $ver + '  (' + $dotnetCmd.Source + ')')
    }

    # 2. Locate project
    Write-Step 'Locating project...'
    if (-not (Test-Path $ProjectFile)) {
        Write-Fail ('Project file not found: ' + $ProjectFile)
        $Script:ExitCode = 1
        return
    }
    Write-Ok ('Found: ' + $ProjectFile)

    # 3. Restore NuGet packages (skipped if obj/ exists, unless -Force is passed)
    $needsRestore = (-not (Test-Path $ObjDir)) -or $Force.IsPresent
    if ($needsRestore) {
        Write-Step 'Restoring NuGet packages...'
        & dotnet restore $ProjectFile -r win-x64
        if ($LASTEXITCODE -ne 0) {
            Write-Fail ('Restore failed (exit ' + $LASTEXITCODE + ')')
            $Script:ExitCode = $LASTEXITCODE
            return
        }
        Write-Ok 'Restore complete.'
    } else {
        Write-Ok 'obj/ found -- skipping restore. (Pass -Force to restore anyway.)'
    }

    # 4. Build
    Write-Step 'Building BitChord.WinUI  (win-x64  Debug)...'
    & dotnet build $ProjectFile -r win-x64 --no-restore --configuration Debug
    $buildCode = $LASTEXITCODE
    if ($buildCode -ne 0) {
        Write-Fail ('Build failed (exit ' + $buildCode + '). Scroll up for compiler errors.')
        $Script:ExitCode = $buildCode
        return
    }
    Write-Ok 'Build succeeded.'

    # 5. Run the app -- blocks until the window is closed
    Write-Step 'Launching BitChord...'
    Write-Host '  Close the app window to return here.' -ForegroundColor DarkGray
    Write-Host ''
    & dotnet run --project $ProjectFile -r win-x64 --no-build --configuration Debug
    $Script:ExitCode = $LASTEXITCODE
    Write-Host ''
    if ($Script:ExitCode -eq 0) {
        Write-Ok 'App exited cleanly (code 0).'
    } else {
        Write-Warn ('App exited with code ' + $Script:ExitCode + '.')
    }

} catch {
    # Catch-all -- display the error instead of dying silently.
    Write-Host ''
    Write-Host ('  UNEXPECTED ERROR: ' + $_) -ForegroundColor Red
    $Script:ExitCode = 1
} finally {
    # This block is ALWAYS executed, even after return, throw, or Ctrl+C.
    # Read-Host blocks until Enter is pressed -- the window will not close
    # prematurely under any circumstances.
    Write-Host ''
    Write-Host '  ------------------------------------------' -ForegroundColor DarkGray
    Read-Host '  Press Enter to close this window'
}

exit $Script:ExitCode

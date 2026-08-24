# Test runner for the react/ controls (DataGrid, Log, Scope), the counterpart to
# ../run-tests.sh for the C# suite.
#
# Usage: .\run-tests.ps1 [control] [nameSubstr]
#   .\run-tests.ps1                  # every react/ suite
#   .\run-tests.ps1 scope            # one folder: grid | log | scope | core
#   .\run-tests.ps1 scope affine     # only tests whose name contains "affine"
#   .\run-tests.ps1 grid -Watch      # jest watch mode
#   .\run-tests.ps1 -Types           # tsc --noEmit
#
# The library deliberately has no package.json, jest config, or tsconfig of its own: the
# suites are jest ports run by whichever app submodules this repo (see README.md).
# So this script walks UP from itself to find the nearest host with react-scripts
# installed and drives that. Nothing here knows which host it is.
#
# example/ is the one exception - a standalone vite app with its own package.json, for
# seeing the controls run without a host. It is not part of the library and this script
# does not touch it (jest ignores node_modules, so its deps never join the suites).

[CmdletBinding()]
param(
    [ValidateSet('all', 'grid', 'log', 'scope', 'core')]
    [string]$Control = 'all',

    # passed to jest as -t, matching test NAMES (run-tests.sh's methodSubstr)
    [string]$NameSubstr = '',

    [switch]$Watch,
    [switch]$Types
)

# NOT 'Stop': in Windows PowerShell 5.1 a native exe writing to stderr surfaces as a
# NativeCommandError, and 'Stop' would abort on jest's first progress line (jest writes
# its whole report to stderr). Exit codes are checked explicitly instead.
$ErrorActionPreference = 'Continue'

function Find-HostRoot {
    $dir = $PSScriptRoot
    while ($dir) {
        if (Test-Path (Join-Path $dir 'node_modules\.bin\react-scripts.cmd')) { return $dir }
        $parent = Split-Path $dir -Parent
        if (-not $parent -or $parent -eq $dir) { break }
        $dir = $parent
    }
    return $null
}

$hostRoot = Find-HostRoot
if (-not $hostRoot) {
    throw ("No host app with react-scripts found above $PSScriptRoot. " +
           "Install dependencies in the app that submodules this repo, then re-run.")
}

# jest's positional arg is a testPathPattern matched against the full path, so the
# library's location within the host becomes the pattern - which keeps "all" scoped to
# react/ instead of sweeping up the host's own tests.
$libPath = $PSScriptRoot.Substring($hostRoot.Length).TrimStart('\', '/').Replace('\', '/')
$pathPattern = if ($Control -eq 'all') { "$libPath/" } else { "$libPath/$Control/" }

Write-Host "host:    $hostRoot" -ForegroundColor DarkGray
Write-Host "library: $libPath" -ForegroundColor DarkGray

# Push/Pop, not Set-Location: a script's location change persists into the caller's
# session, so Set-Location here would leave the shell in the host dir and a second
# `.\run-tests.ps1` from react/ would not resolve.
Push-Location $hostRoot
try {
    if ($Types) {
        # the host's tsconfig is the only one there is, so this covers the host's own TS too
        Write-Host 'tsc --noEmit (via the host tsconfig)' -ForegroundColor Cyan
        npx tsc --noEmit
        if ($LASTEXITCODE -ne 0) { throw 'typecheck failed' }
        Write-Host 'typecheck clean' -ForegroundColor Green
        return
    }

    # @(...) is required: PowerShell unwraps a single-element array on return, and splatting
    # a bare string enumerates its CHARACTERS - jest would receive /s|r|c|\\|.../ and quietly
    # run every suite while claiming to be filtered.
    [string[]]$jestArgs = @($pathPattern)
    if ($NameSubstr) { $jestArgs += @('-t', $NameSubstr) }

    $label = if ($Control -eq 'all') { 'all react/ suites' } else { "react/$Control" }
    if ($NameSubstr) { $label += " matching '$NameSubstr'" }

    if ($Watch) {
        Write-Host "jest watch - $label" -ForegroundColor Cyan
        $env:CI = ''
        npx react-scripts test @jestArgs
    }
    else {
        Write-Host "jest - $label" -ForegroundColor Cyan
        $env:CI = 'true'
        npx react-scripts test --watchAll=false @jestArgs
        if ($LASTEXITCODE -ne 0) { throw 'tests failed' }
    }
}
finally {
    Pop-Location
}

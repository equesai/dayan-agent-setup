# Dayan Agent -- is the app really installed? (owner, 2026-09-26: "a test to
# detect the actual installation afterwards").
#
# Run from Dayan's guided chat flow, in PowerShell, after the member installed
# the app from its maker's own page:
#   irm <dayan>/api/device-setup/<code>/check.ps1 | iex
# It looks for the app the member picked (OpenCode, Claude Code or Codex),
# says what it found, and tells Dayan -- found or not, and the version,
# nothing else. It changes nothing on the computer and needs no key.
# Windows PowerShell 5.1 as it ships with Windows 10 and 11.
& {
    $api = '__DAYAN_API__'
    $code = '__DAYAN_CODE__'
    $app = '__DAYAN_APP__'
    $names = @{ opencode = 'OpenCode'; claude = 'Claude Code'; codex = 'Codex' }
    $name = $names[$app]
    if (-not $name) { $name = 'OpenCode' }

    # Folders on the PATH: this window's, and the one Windows gives new windows
    # (an installer that just ran changed the latter only). DAYAN_TEST_PATH_ONLY:
    # a test run on a PC that has the apps already looks at this window's only.
    $folders = @()
    $paths = @($env:Path)
    if (-not $env:DAYAN_TEST_PATH_ONLY) {
        $paths += [Environment]::GetEnvironmentVariable('Path', 'User'), [Environment]::GetEnvironmentVariable('Path', 'Machine')
    }
    foreach ($part in $paths) {
        if ($part) { $folders += ($part -split ';') | Where-Object { $_ } }
    }

    function Find-Program([string[]] $files, [string[]] $places) {
        foreach ($folder in $folders) {
            foreach ($file in $files) {
                $candidate = Join-Path ([Environment]::ExpandEnvironmentVariables($folder)) $file
                if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
            }
        }
        foreach ($place in $places) {
            if ($place -and (Test-Path -LiteralPath $place -PathType Leaf)) { return $place }
        }
        return $null
    }

    function Get-VersionOf([string] $program) {
        try {
            $out = & $program --version 2>$null | Out-String
            $match = [regex]::Match($out, '\d+\.\d+(\.\d+)?([-+][0-9A-Za-z.]+)?')
            if ($match.Success) { return $match.Value }
        } catch { }
        return ''
    }

    $where = $null
    $version = ''
    $local = $env:LOCALAPPDATA
    switch ($app) {
        'claude' {
            $where = Find-Program @('claude.exe', 'claude.cmd') @(
                "$env:USERPROFILE\.local\bin\claude.exe", "$env:APPDATA\npm\claude.cmd",
                "$local\Microsoft\WinGet\Links\claude.exe")
            if ($where) { $version = Get-VersionOf $where }
        }
        'codex' {
            $where = Find-Program @('codex.exe', 'codex.cmd') @(
                "$local\Programs\OpenAI\Codex\bin\codex.exe", "$env:APPDATA\npm\codex.cmd",
                "$local\Microsoft\WinGet\Links\codex.exe")
            if ($where) { $version = Get-VersionOf $where }
        }
        default {
            # The desktop app (its per-user installer), then the terminal app.
            $desktop = @()
            foreach ($root in @('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
                                'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall')) {
                Get-ChildItem $root -ErrorAction SilentlyContinue | ForEach-Object {
                    $entry = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
                    if ($entry -and $entry.DisplayName -like 'OpenCode*') {
                        if ($entry.InstallLocation) { $desktop += (Join-Path $entry.InstallLocation 'OpenCode.exe') }
                        if ($entry.DisplayIcon) { $desktop += ($entry.DisplayIcon -split ',')[0].Trim('"') }
                    }
                }
            }
            $desktop += "$local\Programs\@opencode-aidesktop\OpenCode.exe", "$local\Programs\@opencodedesktop\OpenCode.exe",
                        "$local\Programs\OpenCode\OpenCode.exe"
            foreach ($candidate in $desktop) {
                if ($candidate -like '*OpenCode.exe' -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
                    $where = $candidate
                    $version = [string](Get-Item -LiteralPath $candidate).VersionInfo.ProductVersion
                    break
                }
            }
            if (-not $where) {
                $where = Find-Program @('opencode.exe', 'opencode.cmd') @("$env:USERPROFILE\.opencode\bin\opencode.exe")
                if ($where) { $version = Get-VersionOf $where }
            }
        }
    }

    Write-Host ''
    Write-Host "Dayan Agent -- looking for $name on this computer..." -ForegroundColor Yellow
    $found = [bool]$where
    if ($found) {
        $shown = if ($version) { "$name $version" } else { $name }
        Write-Host "  $shown is installed  ($where)" -ForegroundColor Green
    } else {
        Write-Host "  $name isn't installed on this computer yet." -ForegroundColor Red
        Write-Host '  Install it from its page (Dayan has the button), then run this line again.'
    }

    # Tell Dayan: found or not, and the version -- nothing else.
    $clean = ($version -replace '[^0-9A-Za-z.+_-]', '')
    if ($clean.Length -gt 40) { $clean = $clean.Substring(0, 40) }
    $body = '{"app":"' + $app + '","found":' + $(if ($found) { 'true' } else { 'false' }) + ',"version":"' + $clean + '"}'
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072
        $null = Invoke-RestMethod -Method Post -Uri "$api/api/device-setup/$code/check" -ContentType 'application/json' `
                                  -Body $body -TimeoutSec 30 -UseBasicParsing
        Write-Host 'Done -- go back to Dayan and press "Show the result".'
    } catch {
        Write-Host "Dayan couldn't be told ($($_.Exception.Message)) -- check your internet and run this line again." -ForegroundColor Red
    }
    Write-Host ''
}

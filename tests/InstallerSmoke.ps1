$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$setup = Join-Path $project 'installer\CodexMonitor-0.1.0-Setup.exe'
$target = [System.IO.Path]::GetFullPath((Join-Path $project 'build\smoke-install'))
$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Codex Monitor\Codex Monitor.lnk'
if (-not (Test-Path -LiteralPath $setup)) { throw 'Installer was not built.' }
if (Test-Path -LiteralPath $target) { throw 'Test install directory already exists.' }
if (Test-Path -LiteralPath $shortcut) { throw 'Codex Monitor Start Menu shortcut already exists; skipping isolated test.' }

$installed = $false
try {
    $result = Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=$target") -Wait -PassThru -WindowStyle Hidden
    if ($result.ExitCode -ne 0) { throw "Installer exited with code $($result.ExitCode)." }
    $installed = $true
    if (-not (Test-Path -LiteralPath (Join-Path $target 'CodexMonitor.exe'))) { throw 'Application was not installed.' }
    if (-not (Test-Path -LiteralPath $shortcut)) { throw 'Start Menu shortcut was not created.' }
    Write-Output 'Install and Start Menu shortcut checks passed.'
}
finally {
    $uninstaller = Join-Path $target 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller) {
        $result = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -Wait -PassThru -WindowStyle Hidden
        if ($result.ExitCode -ne 0) { throw "Uninstaller exited with code $($result.ExitCode)." }
        if (Test-Path -LiteralPath (Join-Path $target 'CodexMonitor.exe')) { throw 'Application remained after uninstall.' }
        if (Test-Path -LiteralPath $shortcut) { throw 'Start Menu shortcut remained after uninstall.' }
        Write-Output 'Uninstall checks passed.'
    }
    elseif ($installed) { throw 'Uninstaller was not created.' }
}

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$compilerCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw 'Inno Setup 6 is missing. Install it with: winget install --id JRSoftware.InnoSetup -e'
}

Push-Location $project
try {
    dotnet publish (Join-Path $project 'src\CodexMonitor.Desktop\CodexMonitor.Desktop.csproj') -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeSourceRevisionInInformationalVersion=false -o (Join-Path $project 'build\publish')
    if ($LASTEXITCODE -ne 0) { throw 'App publishing failed.' }
    & $compiler (Join-Path $project 'packaging\CodexMonitor.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
    Write-Output (Join-Path $project 'installer\CodexMonitor-0.5.1-Setup.exe')
}
finally {
    Pop-Location
}

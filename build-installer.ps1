$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
$compilerCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw 'Brak Inno Setup 6. Zainstaluj: winget install --id JRSoftware.InnoSetup -e'
}

Push-Location $project
try {
    dotnet publish CodexMonitor.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o (Join-Path $project 'build\publish')
    if ($LASTEXITCODE -ne 0) { throw 'Publikacja aplikacji nie powiodla sie.' }
    & $compiler (Join-Path $project 'CodexMonitor.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Budowanie instalatora nie powiodlo sie.' }
    Write-Output (Join-Path $project 'installer\CodexMonitor-0.1.1-Setup.exe')
}
finally {
    Pop-Location
}

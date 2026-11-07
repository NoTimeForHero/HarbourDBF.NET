param(
    [string]$Hbmk2 = 'hbmk2',
    [string]$Compiler = 'bcc'
)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & $Hbmk2 "-comp=$Compiler" main.hbp
    if ($LASTEXITCODE -ne 0) { throw "hbmk2 failed with exit code $LASTEXITCODE" }
    if (-not (Test-Path -LiteralPath 'hbdbf.dll' -PathType Leaf)) { throw 'hbmk2 did not produce hbdbf.dll' }
} finally {
    Pop-Location
}

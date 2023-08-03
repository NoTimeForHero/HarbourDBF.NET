$oldLocation = Get-Location
# Переходим в папку где лежит PS1 скрипт
Set-Location -Path $PSScriptRoot

$library="hbdbf.dll"

if (Test-Path $library) {
    Write-Host "Removing old library: $library"
    Remove-Item $library
}

Write-Host "Building path: $pwd"
& C:\Harbour3\bin\hbmk2 main.hbp

if (-Not(Test-Path $library)) {
    Write-Error "Build failed!"
    exit 1
}

Set-Location -Path $oldLocation
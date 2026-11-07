$ErrorActionPreference = 'Stop'
$env:GOOS = 'linux'
$env:GOARCH = 'amd64'
$env:CGO_ENABLED = '0'
Push-Location $PSScriptRoot
try {
    go build -trimpath -ldflags="-s -w" -o MyMirror-Linux-x86_64 .
    if ($LASTEXITCODE -ne 0) { throw 'Échec de la compilation Linux.' }
} finally { Pop-Location }

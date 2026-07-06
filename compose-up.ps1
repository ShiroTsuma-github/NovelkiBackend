$ErrorActionPreference = "Stop"

Set-Location -LiteralPath $PSScriptRoot

docker compose build
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

docker compose up -d
exit $LASTEXITCODE

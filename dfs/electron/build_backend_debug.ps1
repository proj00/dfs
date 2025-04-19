dotnet publish "./../node/node.csproj" -c Debug -r win-x64 --self-contained $false -o ./assets
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

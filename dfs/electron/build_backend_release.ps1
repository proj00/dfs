$opts = @(
  '-c', 'Release',
  '-r', 'win-x64',
  '--self-contained', 'true',
  '/p:PublishSingleFile=true',
  '/p:IncludeNativeLibrariesForSelfExtract=true',
  '-o', './assets'
)

dotnet publish "./../node/node.csproj" @opts
if ($LASTEXITCODE -ne 0) {
  Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
  exit $LASTEXITCODE
}

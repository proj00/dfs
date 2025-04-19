$opts = @(
  '-c', 'Release',
  '-r', 'win-x64',
  '--self-contained', 'true',
  '/p:PublishSingleFile=true',
  '/p:PublishTrimmed=true',
  '/p:IncludeNativeLibrariesForSelfExtract=true',
  '-o', './assets'
)

dotnet publish "./../node/node.csproj" @opts

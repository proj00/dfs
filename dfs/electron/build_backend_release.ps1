dotnet publish "./../node/node.csproj" -c Release -r win-x64 --self-contained $true -o ./assets /p:PublishSingleFile=true /p:PublishTrimmed=true

dotnet publish -c debug --runtime win-x64 --self-contained true src\FUICompiler.csproj -o ../FUI/FUI/Compiler/FUICompiler -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true
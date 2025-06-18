# NuGet Deno esbuild

## Build

```shell
dotnet build -c Release
```

## Pack

```shell
dotnet pack -c Release
```

## Publish

```shell
dotnet nuget push bin/Release/DenoWrapper.0.0.1-alpha.1.nupkg --api-key <your-api-key> --source https://api.nuget.org/v3/index.json
```

## Private source

```shell
dotnet nuget add source "C:\Data\Private\MyGitHub\DenoWrapper\bin\Release" --name LocalPackages
```

## Install

```shell
dotnet add package DenoWrapper
```

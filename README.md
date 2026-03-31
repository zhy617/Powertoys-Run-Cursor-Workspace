# 编译
```shell
# at repository's root
dotnet build CursorWorkspaces.sln -c Release
```

## X64
```shell
dotnet build CursorWorkspaces.sln -c Release -p:Platform=x64
```
输出 DLL 在 bin\<平台>\Release\ 下（例如 x64\Release 或 ARM64\Release）。
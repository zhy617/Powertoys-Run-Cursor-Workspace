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

# 打包
## Win
将所有的 release 放入 dist
```shell
# 仅打包（需已先 Release 构建好）
.\scripts\pack-dist.ps1 -Version 1.0.0

# 先构建再打包
.\scripts\pack-dist.ps1 -Version 1.1.0 -Build
```

生成 SHA256
```shell
Get-ChildItem .\dist\* | ForEach-Object {
  $h = Get-FileHash $_.FullName -Algorithm SHA256
  "$($h.Hash)  $($_.Name)"
} | Set-Content .\dist\SHA256SUMS.txt
```


# Release
## Win
```shell
$VERSION = "v1.1.0"
gh release create $VERSION `
  --title "$VERSION" `
  --notes-file .\scripts\RELEASE_NOTES.md `
  .\dist\*
```
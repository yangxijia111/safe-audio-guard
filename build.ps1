$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '未找到 dotnet。请先安装 .NET 10 SDK：https://dotnet.microsoft.com/download/dotnet/10.0'
}

Write-Host '>>> 先运行纯逻辑自检（不修改系统音量）'
dotnet run --project ./SafeAudioGuard.PolicyTests/SafeAudioGuard.PolicyTests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw '策略测试未通过' }

Write-Host '>>> 编译为 Windows x64 自包含单文件 EXE'
dotnet publish ./SafeAudioGuard/SafeAudioGuard.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None -p:DebugSymbols=false `
  -o ./dist
if ($LASTEXITCODE -ne 0) { throw '发布失败，请将编译错误交给 Codex' }
Write-Host '发布成功：' (Join-Path $PSScriptRoot 'dist/SafeAudioGuard.exe')
Write-Host '首次启动前，请把 EXE 移到固定文件夹。'

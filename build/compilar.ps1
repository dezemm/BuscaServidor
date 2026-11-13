$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if (Test-Path -LiteralPath (Join-Path $taskRoot 'icone.png')) {
    & (Join-Path $PSScriptRoot 'converter-icone.ps1')
}
$sdkPath = Join-Path $env:LOCALAPPDATA 'BuscaServidorSDK\dotnet.exe'
if (-not (Test-Path -LiteralPath $sdkPath)) {
    $sdkPath = (Get-Command dotnet -ErrorAction Stop).Source
}
& $sdkPath publish (Join-Path $taskRoot 'src\BuscaServidor.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o (Join-Path $taskRoot 'Aplicativo')
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação.' }

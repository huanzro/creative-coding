param(
    [string]$BuildDirectory = (Join-Path $PSScriptRoot '../Builds'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../Builds/Release')
)
$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($destination) | Out-Null
$normal = Join-Path $destination 'Echoes-of-Light-Windows-x64.zip'
$debug = Join-Path $destination 'Echoes-of-Light-Debug-Windows-x64.zip'
foreach ($item in @(@('Windows', $normal), @('WindowsDebug', $debug))) {
    $variant = Join-Path $BuildDirectory $item[0]
    if (!(Test-Path -LiteralPath (Join-Path $variant 'Echoes of Light.exe'))) {
        throw "Unity Windows build is missing: $variant"
    }
    if (Test-Path -LiteralPath $item[1]) { Remove-Item -LiteralPath $item[1] }
    [IO.Compression.ZipFile]::CreateFromDirectory($variant, $item[1], [IO.Compression.CompressionLevel]::Optimal, $false)
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$setup = Join-Path $destination 'Echoes-of-Light-Setup-Windows-x64.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /reference:System.Windows.Forms.dll `
    /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
    "/resource:$normal,Echoes.Windows.zip" "/out:$setup" (Join-Path $PSScriptRoot 'WindowsInstaller.cs')
if ($LASTEXITCODE -ne 0) { throw 'Could not build the Windows installer.' }
$checksums = foreach ($artifact in @($setup, $normal, $debug)) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $artifact
    '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), [IO.Path]::GetFileName($artifact)
}
[IO.File]::WriteAllLines((Join-Path $destination 'SHA256SUMS.txt'), [string[]]$checksums)
Get-ChildItem -LiteralPath $destination -File | Select-Object Name, Length

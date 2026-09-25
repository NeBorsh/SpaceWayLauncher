$ErrorActionPreference = 'Stop'

[Console]::OutputEncoding = [Text.Encoding]::UTF8

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'

[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = $props.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

if (-not $version) {
    throw 'Version not found in Directory.Build.props'
}

Write-Host "Version: $version"

function Publish-Launcher([string]$rid, [string]$output) {
    if (Test-Path $output) {
        Remove-Item $output -Recurse -Force
    }

    foreach ($project in 'SpaceWay.Launcher', 'SpaceWay.Loader') {
        Write-Host "Publishing $project ($rid)"

        dotnet publish (Join-Path $root "src\$project") `
            -c Release -r $rid --self-contained true -o $output

        if ($LASTEXITCODE -ne 0) {
            throw "Publishing $project ($rid) failed"
        }
    }
}

$windows = Join-Path $artifacts 'win-x64'
$linux = Join-Path $artifacts 'linux-x64'

Publish-Launcher 'win-x64' $windows
Publish-Launcher 'linux-x64' $linux

$iscc = @("${env:ProgramFiles(x86)}", $env:ProgramFiles) |
    Where-Object { $_ } |
    ForEach-Object { Join-Path $_ 'Inno Setup *\ISCC.exe' } |
    ForEach-Object { Get-Item $_ -ErrorAction SilentlyContinue } |
    Sort-Object -Property FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $iscc) {
    throw 'Inno Setup not found. Install it with: winget install JRSoftware.InnoSetup'
}

Write-Host "Compiler: $iscc"

& $iscc "/DAppVersion=$version" (Join-Path $PSScriptRoot 'SpaceWayLauncher.iss')

if ($LASTEXITCODE -ne 0) {
    throw 'Installer build failed'
}

$setup = Join-Path $artifacts "SpaceWayLauncher-$version-setup.exe"
$portable = Join-Path $artifacts "SpaceWayLauncher-$version-win-x64-portable.zip"
$tarball = Join-Path $artifacts "SpaceWayLauncher-$version-linux-x64.tar.gz"

Write-Host 'Packing portable build'

$staging = Join-Path $artifacts 'portable'
$stagedApp = Join-Path $staging 'SpaceWayLauncher'

if (Test-Path $staging) {
    Remove-Item $staging -Recurse -Force
}

Copy-Item $windows $stagedApp -Recurse
New-Item (Join-Path $stagedApp 'portable') -ItemType File | Out-Null

if (Test-Path $portable) {
    Remove-Item $portable -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stagedApp, $portable, 'Optimal', $true)
Remove-Item $staging -Recurse -Force

Write-Host 'Packing Linux build'

dotnet run (Join-Path $PSScriptRoot 'pack-tar.cs') -- $linux $tarball 'SpaceWayLauncher'

if ($LASTEXITCODE -ne 0) {
    throw 'Packing the Linux build failed'
}

$files = $setup, $portable, $tarball
$sums = $files | ForEach-Object {
    $hash = (Get-FileHash $_ -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $(Split-Path $_ -Leaf)"
}

$sumsPath = Join-Path $artifacts 'SHA256SUMS'
[IO.File]::WriteAllText($sumsPath, (($sums -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))

Write-Host ''
Write-Host 'Release files:'

foreach ($file in $files + $sumsPath) {
    $size = [math]::Round((Get-Item $file).Length / 1MB, 1)
    Write-Host "  $(Split-Path $file -Leaf) ($size MB)"
}

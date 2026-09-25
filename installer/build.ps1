$ErrorActionPreference = 'Stop'

[Console]::OutputEncoding = [Text.Encoding]::UTF8

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$output = Join-Path $artifacts 'win-x64'

[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = $props.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

if (-not $version) {
    throw 'Version not found in Directory.Build.props'
}

Write-Host "Version: $version"

if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}

foreach ($project in 'SpaceWay.Launcher', 'SpaceWay.Loader') {
    Write-Host "Publishing $project"

    dotnet publish (Join-Path $root "src\$project") `
        -c Release -r win-x64 --self-contained true -o $output

    if ($LASTEXITCODE -ne 0) {
        throw "Publishing $project failed"
    }
}

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
$size = [math]::Round((Get-Item $setup).Length / 1MB)

Write-Host "Done: $setup ($size MB)"

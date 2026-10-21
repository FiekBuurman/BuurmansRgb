<#
Builds artifacts\installer\out\BuurmansRgb-setup.exe:
  1. publishes the engine and the settings app self-contained (no .NET runtime needed),
  2. fetches the OpenRGB MSI and the PawnIO setup, and checks their SHA-256,
  3. fetches the Inno Setup compiler from NuGet when -Iscc is not given,
  4. compiles BuurmansRgb.iss,
  5. writes artifacts\installer\BuurmansRgb-test.wsb, a Windows Sandbox test (see docs/ontwikkelaars.md).
Everything it downloads or builds stays under artifacts\installer, which git ignores.
#>
[CmdletBinding()]
param(
    [string]$Version = '1.0.0',
    [string]$Iscc
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$root = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $root 'artifacts\installer'
$appDir = Join-Path $artifacts 'app'
$depsDir = Join-Path $artifacts 'deps'
$toolsDir = Join-Path $artifacts 'tools'

$dependencies = @(
    @{
        Name = 'OpenRGB_1.1_Windows_64_8e16de2.msi'
        Url = 'https://codeberg.org/OpenRGB/OpenRGB/releases/download/release_1.1/OpenRGB_1.1_Windows_64_8e16de2.msi'
        Sha256 = 'A4CE48A5E1532750AD4F5248424D42F7F2C710775CEE88DB070CCDB63C549CAA'
    }
    @{
        Name = 'PawnIO_setup.exe'
        Url = 'https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe'
        Sha256 = '1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032'
    }
)

$innoSetupVersion = '7.1.0'

function Test-Sha256([string]$Path, [string]$Expected) {
    (Test-Path $Path) -and (Get-FileHash $Path -Algorithm SHA256).Hash -eq $Expected
}

function Get-Dependency([hashtable]$Dependency) {
    $target = Join-Path $depsDir $Dependency.Name
    if (Test-Sha256 $target $Dependency.Sha256) {
        return
    }

    Write-Host "Downloading $($Dependency.Name)"
    $partial = "$target.partial"
    Invoke-WebRequest $Dependency.Url -OutFile $partial
    if (-not (Test-Sha256 $partial $Dependency.Sha256)) {
        Remove-Item $partial
        throw "SHA-256 of $($Dependency.Name) does not match; refusing to package it."
    }

    Move-Item $partial $target -Force
}

function Get-InnoSetupCompiler {
    $dir = Join-Path $toolsDir "innosetup-$innoSetupVersion"
    $compiler = Join-Path $dir 'tools\ISCC.exe'
    if (Test-Path $compiler) {
        return $compiler
    }

    Write-Host "Downloading Inno Setup $innoSetupVersion from NuGet"
    $package = Join-Path $toolsDir "innosetup-$innoSetupVersion.zip"
    Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/tools.innosetup/$innoSetupVersion/tools.innosetup.$innoSetupVersion.nupkg" -OutFile $package
    Expand-Archive $package $dir -Force
    Remove-Item $package
    return $compiler
}

function Invoke-Native([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE"
    }
}

New-Item -ItemType Directory -Force $depsDir, $toolsDir | Out-Null

Remove-Item $appDir -Recurse -Force -ErrorAction SilentlyContinue
foreach ($project in 'BuurmansRgb.Engine', 'BuurmansRgb.UI') {
    Invoke-Native dotnet @('publish', (Join-Path $root "src\$project"), '-c', 'Release', '-r', 'win-x64', '--self-contained', '-o', $appDir, "-p:Version=$Version")
}

$dependencies | ForEach-Object { Get-Dependency $_ }

if (-not $Iscc) {
    $Iscc = Get-InnoSetupCompiler
}

Invoke-Native $Iscc @('/Qp', "/DAppVersion=$Version", "/DArtifactsDir=$artifacts", (Join-Path $PSScriptRoot 'BuurmansRgb.iss'))

# Windows Sandbox needs absolute host paths, so the .wsb is written here and not kept in git.
$sandboxConfig = @"
<Configuration>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$(Join-Path $artifacts 'out')</HostFolder>
      <SandboxFolder>C:\Setup</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$(Join-Path $PSScriptRoot 'sandbox')</HostFolder>
      <SandboxFolder>C:\Test</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$(New-Item -ItemType Directory -Force (Join-Path $artifacts 'sandbox-results'))</HostFolder>
      <SandboxFolder>C:\Results</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -ExecutionPolicy Bypass -File C:\Test\test-install.ps1</Command>
  </LogonCommand>
</Configuration>
"@
Set-Content (Join-Path $artifacts 'BuurmansRgb-test.wsb') $sandboxConfig -Encoding utf8

$setup = Get-Item (Join-Path $artifacts 'out\BuurmansRgb-setup.exe')
Write-Host ("Built {0} ({1:N1} MB)" -f $setup.FullName, ($setup.Length / 1MB))

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "LoLIdleDesktop.csproj"
$publishDir = Join-Path $root "artifacts\publish"
$installerDir = Join-Path $root "artifacts\installer"
$redistDir = Join-Path $PSScriptRoot "redist"
$webView2Bootstrapper = Join-Path $redistDir "MicrosoftEdgeWebView2Setup.exe"
$issFile = Join-Path $PSScriptRoot "LoLIdleDesktop.iss"

# Lê a versão diretamente do csproj para manter instalador e aplicativo alinhados.
[xml]$csproj = Get-Content $project
$version = @($csproj.Project.PropertyGroup.Version) |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1

if (-not $version) {
    throw "Não foi possível ler a versão em LoLIdleDesktop.csproj."
}

$version4 = if ($version -match '^\d+\.\d+\.\d+$') { "$version.0" } else { $version }

Write-Host "Versão detectada: $version"

if (-not $SkipPublish) {
    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }

    Write-Host "Publicando aplicativo para $Runtime..."
    & dotnet publish $project -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true --nologo -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "Falha no dotnet publish (código $LASTEXITCODE)."
    }
}

if (-not (Test-Path (Join-Path $publishDir "LoL Idle.exe"))) {
    throw "Executável não encontrado em $publishDir. Rode sem -SkipPublish ou publique o projeto primeiro."
}

New-Item -ItemType Directory -Force -Path $redistDir, $installerDir | Out-Null

if (-not (Test-Path $webView2Bootstrapper)) {
    Write-Host "Baixando o Microsoft Edge WebView2 Evergreen Bootstrapper..."
    Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile $webView2Bootstrapper
}

$iscc = @(
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    $command = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($command) { $iscc = $command.Source }
}

if (-not $iscc) {
    throw "Inno Setup 6 não encontrado. Instale com: winget install JRSoftware.InnoSetup -e"
}

Write-Host "Compilando instalador com Inno Setup..."
& $iscc "/DMyAppVersion=$version" "/DMyAppVersion4=$version4" "/DSourceDir=$publishDir" "/O$installerDir" $issFile
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao compilar o instalador (código $LASTEXITCODE)."
}

$setup = Get-ChildItem $installerDir -Filter "LoLIdleDesktop-Setup-*-x64.exe" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $setup) {
    throw "O instalador não foi gerado em $installerDir."
}

Write-Host ""
Write-Host "Instalador gerado com sucesso:"
Write-Host "  $($setup.FullName)"
Write-Host "  $([Math]::Round($setup.Length / 1MB, 2)) MB"

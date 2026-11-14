# Remove installers antigos: mantem apenas as DUAS versoes mais recentes
# da pasta artifacts/installer.
#
# Uso:
#   .\installer\prune-installers.ps1            # dry-run (so mostra o que apagaria)
#   .\installer\prune-installers.ps1 -Execute   # apaga de verdade
#   .\installer\prune-installers.ps1 -Folder "C:\caminho\para\pasta" -Execute
param(
    [switch]$Execute,
    [string]$Folder
)

$folderParam = if ($Folder) { $Folder } else { Join-Path $PSScriptRoot "..\artifacts\installer" }
$folder = (Resolve-Path $folderParam -ErrorAction Stop).Path

$files = Get-ChildItem -Path $folder -Filter "LoLIdleDesktop-Setup-*-x64.exe"
if (-not $files) { "Nenhum instalador encontrado em $folder"; exit 0 }

$parsed = foreach ($f in $files) {
    if ($f.BaseName -match 'LoLIdleDesktop-Setup-(?<v>\d+\.\d+\.\d+)-x64$') {
        [pscustomobject]@{ File = $f; Version = [version]$Matches.v }
    }
}

if (-not $parsed) { "Nenhum arquivo com o padrao LoLIdleDesktop-Setup-<versao>-x64.exe encontrado"; exit 0 }

$keepVersions = $parsed | Sort-Object Version -Descending | Select-Object -First 2 | ForEach-Object Version
$remove = $parsed | Where-Object { $keepVersions -notcontains $_.Version }

"Pasta: $folder"
"Mantendo: " + (($parsed | Where-Object { $keepVersions -contains $_.Version } | Sort-Object Version -Descending | ForEach-Object { $_.File.Name }) -join ', ')

if (-not $remove) { "Nada a apagar."; exit 0 }

foreach ($item in ($remove | Sort-Object Version -Descending)) {
    if ($Execute) {
        Remove-Item $item.File.FullName -Force
        "Apagado: $($item.File.Name)"
    } else {
        "Apagaria (dry-run): $($item.File.Name)"
    }
}

if (-not $Execute) { "" ; "Dry-run. Use -Execute para apagar de verdade." }

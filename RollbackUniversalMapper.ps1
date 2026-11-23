param([string]$GameBin = '')
$ErrorActionPreference = 'Stop'
if (!$GameBin -or !(Test-Path -LiteralPath (Join-Path $GameBin 'Subnautica2-Win64-Shipping.exe') -PathType Leaf)) {
    Add-Type -AssemblyName System.Windows.Forms
    $picker = New-Object System.Windows.Forms.OpenFileDialog
    $picker.Title = 'Select Subnautica2-Win64-Shipping.exe to restore the previous helper and mapping'
    $picker.Filter = 'Subnautica 2 executable|Subnautica2-Win64-Shipping.exe'
    try {
        if ($picker.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { throw 'Canceled; nothing changed.' }
        $GameBin = Split-Path -Parent $picker.FileName
    } finally { $picker.Dispose() }
}
if (Get-Process -Name 'TurboTad.Controls','Subnautica2-Win64-Shipping' -ErrorAction SilentlyContinue) { throw 'Close TurboTad Controls and Subnautica 2 before rollback.' }
$backupRoot = Join-Path $GameBin 'TurboTad_Backups'
$backups = @(Get-ChildItem -LiteralPath $backupRoot -Directory -Filter 'Before_UIM_Stage1_*' -ErrorAction SilentlyContinue | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'backup-complete.txt') -PathType Leaf } | Sort-Object Name -Descending)
if ($backups.Count -eq 0) { throw 'No Universal Input Mapper backup was found.' }
$backup = $backups[0].FullName
$oldExe = Join-Path $backup 'TurboTad.Controls.exe'
$oldMapping = Join-Path $backup 'mapping.xml'
if (!(Test-Path -LiteralPath (Join-Path $backup 'helper.sha256') -PathType Leaf) -or
    (Get-FileHash -LiteralPath $oldExe -Algorithm SHA256).Hash -ne (Get-Content -LiteralPath (Join-Path $backup 'helper.sha256') -Raw).Trim()) { throw 'Helper backup hash does not verify.' }
$hadMapping = Test-Path -LiteralPath $oldMapping -PathType Leaf
if ($hadMapping) {
    if ((Get-FileHash -LiteralPath $oldMapping -Algorithm SHA256).Hash -ne (Get-Content -LiteralPath (Join-Path $backup 'mapping.sha256') -Raw).Trim()) { throw 'Mapping backup hash does not verify.' }
} elseif (!(Test-Path -LiteralPath (Join-Path $backup 'mapping-was-absent.txt') -PathType Leaf)) { throw 'Mapping backup state is unknown.' }
$root = Join-Path $GameBin 'ue4ss\Mods\TurboTad'
$target = Join-Path $root 'TurboTad.Controls.exe'
$mapping = Join-Path $root 'mapping.xml'
# Keep the current mapper and any newly assigned bindings before restoring the pre-install pair.
$checkpoint = Join-Path $backupRoot ('Before_UIM_Rollback_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
New-Item -ItemType Directory -Path $checkpoint -Force | Out-Null
Copy-Item -LiteralPath $target -Destination (Join-Path $checkpoint 'TurboTad.Controls.exe')
$hasCurrentMapping = Test-Path -LiteralPath $mapping -PathType Leaf
if ($hasCurrentMapping) { Copy-Item -LiteralPath $mapping -Destination (Join-Path $checkpoint 'mapping.xml') }
try {
    Copy-Item -LiteralPath $oldExe -Destination $target -Force
    if ($hadMapping) { Copy-Item -LiteralPath $oldMapping -Destination $mapping -Force }
    elseif (Test-Path -LiteralPath $mapping) { Remove-Item -LiteralPath $mapping -Force }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $oldExe -Algorithm SHA256).Hash) { throw 'Restored helper hash mismatch.' }
    if ($hadMapping -and (Get-FileHash -LiteralPath $mapping -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $oldMapping -Algorithm SHA256).Hash) { throw 'Restored mapping hash mismatch.' }
} catch {
    Copy-Item -LiteralPath (Join-Path $checkpoint 'TurboTad.Controls.exe') -Destination $target -Force
    if ($hasCurrentMapping) { Copy-Item -LiteralPath (Join-Path $checkpoint 'mapping.xml') -Destination $mapping -Force }
    elseif (Test-Path -LiteralPath $mapping) { Remove-Item -LiteralPath $mapping -Force }
    throw
}
Write-Host 'Previous TurboTad helper and pre-install mapping restored.' -ForegroundColor Green
Write-Host ('New mapper bindings preserved at: ' + $checkpoint)

param([string]$RollbackScript = (Join-Path $PSScriptRoot '..\RollbackUniversalMapper.ps1'))
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('TurboTad_UIM_RollbackTest_' + [guid]::NewGuid().ToString('N'))
try {
    $bin = Join-Path $testRoot 'Win64'
    $mod = Join-Path $bin 'ue4ss\Mods\TurboTad'
    $backups = Join-Path $bin 'TurboTad_Backups'
    $valid = Join-Path $backups 'Before_UIM_Stage1_001'
    $partial = Join-Path $backups 'Before_UIM_Stage1_999'
    New-Item -ItemType Directory -Path $mod,$valid,$partial -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $bin 'Subnautica2-Win64-Shipping.exe') -Value 'test fixture' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $mod 'TurboTad.Controls.exe') -Value 'new helper' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $mod 'mapping.xml') -Value 'new bindings' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $valid 'TurboTad.Controls.exe') -Value 'old helper' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $valid 'mapping.xml') -Value 'old bindings' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $valid 'helper.sha256') -Value ((Get-FileHash -LiteralPath (Join-Path $valid 'TurboTad.Controls.exe') -Algorithm SHA256).Hash) -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $valid 'mapping.sha256') -Value ((Get-FileHash -LiteralPath (Join-Path $valid 'mapping.xml') -Algorithm SHA256).Hash) -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $valid 'backup-complete.txt') -Value 'verified' -Encoding ASCII
    & $RollbackScript -GameBin $bin
    if ((Get-Content -LiteralPath (Join-Path $mod 'TurboTad.Controls.exe') -Raw).Trim() -ne 'old helper') { throw 'Valid backup was not restored.' }
    if ((Get-Content -LiteralPath (Join-Path $mod 'mapping.xml') -Raw).Trim() -ne 'old bindings') { throw 'Pre-install bindings were not restored.' }
    $checkpoints = @(Get-ChildItem -LiteralPath $backups -Directory -Filter 'Before_UIM_Rollback_*')
    if ($checkpoints.Count -ne 1 -or (Get-Content -LiteralPath (Join-Path $checkpoints[0].FullName 'mapping.xml') -Raw).Trim() -ne 'new bindings') { throw 'Current bindings were not preserved.' }
    Write-Host 'Rollback test passed: incomplete backup skipped; helper/mapping restored; newer bindings preserved.'
}
finally { if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force } }

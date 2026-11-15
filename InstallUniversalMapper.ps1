param([string]$GameBin = '')
$ErrorActionPreference = 'Stop'
$stage = $null
$backupDir = $null
$target = $null
$replacementStarted = $false
try {
    if ([string]::IsNullOrWhiteSpace($GameBin)) {
        $candidates = @('D:\Steam\steamapps\common\Subnautica2\Subnautica2\Binaries\Win64',
            'C:\Program Files (x86)\Steam\steamapps\common\Subnautica2\Subnautica2\Binaries\Win64',
            'C:\Steam\steamapps\common\Subnautica2\Subnautica2\Binaries\Win64')
        $GameBin = $candidates | Where-Object {
            Test-Path -LiteralPath (Join-Path $_ 'Subnautica2-Win64-Shipping.exe') -PathType Leaf
        } | Select-Object -First 1
    }
    if (!$GameBin -or !(Test-Path -LiteralPath (Join-Path $GameBin 'Subnautica2-Win64-Shipping.exe') -PathType Leaf)) {
        Add-Type -AssemblyName System.Windows.Forms
        $picker = New-Object System.Windows.Forms.OpenFileDialog
        $picker.Title = 'Choose Subnautica2-Win64-Shipping.exe in the game Win64 folder'
        $picker.Filter = 'Subnautica 2 executable|Subnautica2-Win64-Shipping.exe'
        try {
            if ($picker.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { throw 'Canceled; no installed files changed.' }
            $GameBin = Split-Path -Parent $picker.FileName
        } finally { $picker.Dispose() }
    }
    if (Get-Process -Name 'TurboTad.Controls','Subnautica2-Win64-Shipping' -ErrorAction SilentlyContinue) {
        throw 'Close TurboTad Controls and Subnautica 2 before installing.'
    }
    $root = Join-Path $GameBin 'ue4ss\Mods\TurboTad'
    $target = Join-Path $root 'TurboTad.Controls.exe'
    $core = Join-Path $root 'Scripts\core.lua'
    if (!(Test-Path -LiteralPath $target -PathType Leaf) -or !(Test-Path -LiteralPath $core -PathType Leaf)) {
        throw 'Install the working TurboTad joystick version first. This is a helper-only test patch.'
    }
    if (!(Select-String -LiteralPath $core -SimpleMatch 'T6D7' -Quiet)) { throw 'Unrecognized installed TurboTad protocol.' }
    $exeText = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($target))
    if (!$exeText.Contains('BoostAudioPicker') -or !$exeText.Contains('RateSlider')) { throw 'Expected TurboTad GUI with WAV support was not found.' }
    $oldHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    $stage = Join-Path $env:TEMP ('TurboTad_UIM_Build_' + [guid]::NewGuid().ToString('N'))
    # Compilation and all five test suites occur before any installed file changes.
    & (Join-Path $PSScriptRoot 'BuildAndVerify.ps1') -OutputDirectory $stage
    $compiled = Join-Path $stage 'TurboTad.Controls.exe'
    if (!(Test-Path -LiteralPath $compiled -PathType Leaf)) { throw 'No verified helper was produced.' }
    if (Get-Process -Name 'TurboTad.Controls','Subnautica2-Win64-Shipping' -ErrorAction SilentlyContinue) { throw 'Game/helper started during validation; close them and retry.' }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $oldHash) { throw 'Installed helper changed during validation; retry from current baseline.' }
    $backupDir = Join-Path (Join-Path $GameBin 'TurboTad_Backups') ('Before_UIM_Stage1_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff'))
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    Copy-Item -LiteralPath $target -Destination (Join-Path $backupDir 'TurboTad.Controls.exe')
    if ((Get-FileHash -LiteralPath (Join-Path $backupDir 'TurboTad.Controls.exe') -Algorithm SHA256).Hash -ne $oldHash) { throw 'Helper backup failed verification.' }
    Set-Content -LiteralPath (Join-Path $backupDir 'helper.sha256') -Value $oldHash -Encoding ASCII
    $mapping = Join-Path $root 'mapping.xml'
    if (Test-Path -LiteralPath $mapping -PathType Leaf) {
        $mappingHash = (Get-FileHash -LiteralPath $mapping -Algorithm SHA256).Hash
        Copy-Item -LiteralPath $mapping -Destination (Join-Path $backupDir 'mapping.xml')
        if ((Get-FileHash -LiteralPath (Join-Path $backupDir 'mapping.xml') -Algorithm SHA256).Hash -ne $mappingHash) { throw 'Mapping backup failed verification.' }
        Set-Content -LiteralPath (Join-Path $backupDir 'mapping.sha256') -Value $mappingHash -Encoding ASCII
    } else { Set-Content -LiteralPath (Join-Path $backupDir 'mapping-was-absent.txt') -Value 'absent' -Encoding ASCII }
    Set-Content -LiteralPath (Join-Path $backupDir 'backup-complete.txt') -Value 'verified' -Encoding ASCII
    $replacementStarted = $true
    Copy-Item -LiteralPath $compiled -Destination $target -Force
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $compiled -Algorithm SHA256).Hash) { throw 'Installed helper hash did not verify.' }
    Write-Host 'TurboTad Device Discovery TEST helper installed.' -ForegroundColor Green
    Write-Host ('Rollback backup: ' + $backupDir)
    Write-Host 'INPUT MAPPING: choose Analog or Buttons on each row; your existing mappings are retained.'
    Write-Host 'DISCOVER CONTROLS: release controls, start scanning, move each axis, squeeze triggers and press buttons.'
    Write-Host 'Discovered inputs are listed first; unscanned supported inputs remain selectable.'
    Write-Host 'Discovery inventory: separate device-discovery.xml; existing mapping.xml is preserved.'
    Write-Host 'This installer checks the build, not live hardware behavior. Test the helper in-game before releasing.'
}
catch {
    $message = $_.Exception.Message
    if ($replacementStarted -and $backupDir) {
        try {
            $previous = Join-Path $backupDir 'TurboTad.Controls.exe'
            Copy-Item -LiteralPath $previous -Destination $target -Force
            if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $previous -Algorithm SHA256).Hash) { throw 'Rollback hash mismatch.' }
            Write-Host 'Original helper restored after failed installation.' -ForegroundColor Yellow
        } catch { Write-Host ('Automatic rollback failed: ' + $_.Exception.Message + '. Backup: ' + $backupDir) -ForegroundColor Red }
    }
    throw ('INSTALL FAILED: ' + $message)
}
finally {
    if ($stage -and (Test-Path -LiteralPath $stage)) { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
}

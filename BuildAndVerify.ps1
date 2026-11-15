param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$sourceRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'Microsoft .NET Framework 4.x x64 C# compiler not found. Install the .NET Framework developer tools or run on a compatible Windows PC.'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $env:TEMP ('TurboTadSourceVerify_' + [guid]::NewGuid().ToString('N'))
}
New-Item -Path $OutputDirectory -ItemType Directory -Force | Out-Null
$dest = (Resolve-Path -LiteralPath $OutputDirectory).Path
$exe = Join-Path $dest 'TurboTad.Controls.exe'
$assets = Join-Path $sourceRoot 'Assets'
$css = Join-Path $sourceRoot 'Source'
$refs = @('/reference:System.dll','/reference:System.Core.dll','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Xml.dll')
$ui = @( (Join-Path $css 'Controls.cs'), (Join-Path $css 'BoostAudioPicker.cs'), (Join-Path $css 'RateSlider.cs'), (Join-Path $css 'InputMapper.cs'), (Join-Path $css 'UimControls.cs'), (Join-Path $css 'InputBindingDialog.cs') )
foreach ($f in $ui) { if (!(Test-Path -LiteralPath $f -PathType Leaf)) { throw ('Source missing: ' + $f) } }
$icon = Join-Path $assets 'TurboTad.ico'
$logo = Join-Path $assets 'TurboTadLogo.png'
foreach ($f in @($icon,$logo)) { if (!(Test-Path -LiteralPath $f -PathType Leaf)) { throw ('Asset missing: ' + $f) } }

$guiArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+','/debug-',('/win32icon:' + $icon),('/out:' + $exe)) + $refs + $ui
& $compiler @guiArgs
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'GUI compilation failed' }

$audioExe = Join-Path $dest 'AudioSelfTest.exe'
$audioArgs = @('/nologo','/target:exe','/platform:x64',('/out:' + $audioExe)) + $refs + @((Join-Path $css 'BoostAudioPicker.cs'),(Join-Path $css 'AudioSelfTest.cs'))
& $compiler @audioArgs
if ($LASTEXITCODE -ne 0) { throw 'Audio test compilation failed' }
& $audioExe
if ($LASTEXITCODE -ne 0) { throw 'Audio self-test failed' }

$uiExe = Join-Path $dest 'UiSelfTest.exe'
$uiArgs = @('/nologo','/target:exe','/platform:x64',('/out:' + $uiExe)) + $refs + @((Join-Path $css 'UiSelfTest.cs'))
& $compiler @uiArgs
if ($LASTEXITCODE -ne 0) { throw 'GUI self-test compilation failed' }
$isolated = Join-Path $dest ('IsolatedModRoot_' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $isolated 'Assets') -Force | Out-Null
Copy-Item -LiteralPath $icon -Destination (Join-Path $isolated 'Assets\TurboTad.ico')
Copy-Item -LiteralPath $logo -Destination (Join-Path $isolated 'Assets\TurboTadLogo.png')
& $uiExe $exe $isolated
if ($LASTEXITCODE -ne 0) { throw 'GUI self-test failed' }
$mapperExe = Join-Path $dest 'InputMapperSelfTest.exe'
$mapperArgs = @('/nologo','/target:exe','/platform:x64',('/out:' + $mapperExe)) + $refs + @((Join-Path $css 'InputMapper.cs'),(Join-Path $css 'InputMapperSelfTest.cs'))
& $compiler @mapperArgs
if ($LASTEXITCODE -ne 0) { throw 'Input mapper test compilation failed' }
& $mapperExe
if ($LASTEXITCODE -ne 0) { throw 'Input mapper logic test failed' }
$mapperUiExe = Join-Path $dest 'InputMapperUiSelfTest.exe'
$mapperUiArgs = @('/nologo','/target:exe','/platform:x64',('/out:' + $mapperUiExe)) + $refs + @((Join-Path $css 'InputMapperUiSelfTest.cs'))
& $compiler @mapperUiArgs
if ($LASTEXITCODE -ne 0) { throw 'Mapper GUI test compilation failed' }
& $mapperUiExe $exe (Join-Path $dest ('IsolatedMapperRoot_' + [guid]::NewGuid().ToString('N')))
if ($LASTEXITCODE -ne 0) { throw 'Mapper GUI migration/Start/Stop test failed' }
& (Join-Path $css 'UimRollbackSelfTest.ps1') -RollbackScript (Join-Path $sourceRoot 'RollbackUniversalMapper.ps1')
Write-Host ('SOURCE BUILD AND TESTS PASSED. Output: ' + $exe) -ForegroundColor Green
Write-Host ('Built helper SHA-256: ' + (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash)
Write-Host 'This verifies compilation and self-tests, NOT equivalence to the previously gameplay-tested exe.'
Write-Host 'The installed game/mod is unchanged. A separate gameplay regression is necessary for the built exe.'

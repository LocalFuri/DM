param(
  [string]$ProjectRoot = 'C:\Unity\DM'
)
$ErrorActionPreference = 'Stop'
$progression = Join-Path $ProjectRoot 'Assets\Editor\Progression.json'
$checkpointFile = Join-Path $PSScriptRoot 'Checkpoint_OrnamentStrictDOS_20261123.json'
if (-not (Test-Path -LiteralPath $progression)) { throw "Progression.json not found: $progression" }
if (-not (Test-Path -LiteralPath $checkpointFile)) { throw "Checkpoint not found: $checkpointFile" }
$existing = Get-Content -LiteralPath $progression -Raw -Encoding UTF8 | ConvertFrom-Json
$checkpoint = Get-Content -LiteralPath $checkpointFile -Raw -Encoding UTF8 | ConvertFrom-Json
$key = 'checkpoint_strict_dos_ornament_legacy_deletion_20261123'
if ($existing.PSObject.Properties[$key]) { $existing.$key = $checkpoint }
else { Add-Member -InputObject $existing -NotePropertyName $key -NotePropertyValue $checkpoint }
# Stop before overwriting malformed source or object; preserve existing data.
if ($null -eq $existing -or $existing -is [System.Array]) { throw 'Unexpected progression root structure' }
$json = ConvertTo-Json -InputObject $existing -Depth 100
# Strict encoding: UTF-8 without BOM. Write atomically and retain an automatic backup.
$backup = "$progression.bak-20261123"
Copy-Item -LiteralPath $progression -Destination $backup -Force
$temp = "$progression.tmp-20261123"
[System.IO.File]::WriteAllText($temp, $json + [Environment]::NewLine, (New-Object System.Text.UTF8Encoding($false)))
$null = Get-Content -LiteralPath $temp -Raw -Encoding UTF8 | ConvertFrom-Json
Move-Item -LiteralPath $temp -Destination $progression -Force
Write-Host "Updated: $progression" -ForegroundColor Cyan
Write-Host "Preserved earlier entries. Checkpoint key: $key" -ForegroundColor Cyan

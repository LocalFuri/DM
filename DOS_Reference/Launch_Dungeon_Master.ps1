$ErrorActionPreference = 'Stop'

# Dungeon Master via D-Fend Reloaded, preserving its screenshots frontend.
# Reliable startup timing: 2500ms after activation, 900ms after each menu,
# 1800ms before Resume. This replaces the unsuccessful fast experiment.
# DOSBox CPU cycles are NOT changed: this only speeds up launcher delays.
# Keep an independent backup of the previously installed version before replacing it.
$dfend = 'C:\Program Files (x86)\D-Fend Reloaded\DFend.exe'
$profile = 'Dungeon Master - Unity Reference'
if (!(Test-Path -LiteralPath $dfend)) { throw "D-Fend Reloaded not found: $dfend" }

# The user should close other DOSBox games before launching so only our new
# game instance can receive the startup keystrokes.
$existing = @(Get-Process -Name 'DOSBox' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DMKeys {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extraInfo);
    public static void Press(byte vk, byte scan, int holdMs) {
        keybd_event(vk, scan, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(holdMs);
        keybd_event(vk, scan, 2, UIntPtr.Zero);
    }
}
'@

# This is the same profile launch used by D-Fend's desktop shortcut.
Start-Process -FilePath $dfend -ArgumentList ('"' + $profile + '"') | Out-Null

$shell = New-Object -ComObject WScript.Shell
$game = $null
$activated = $false
for ($i = 0; $i -lt 240; $i++) {
    Start-Sleep -Milliseconds 100
    $candidates = @(Get-Process -Name 'DOSBox' -ErrorAction SilentlyContinue |
        Where-Object { $existing -notcontains $_.Id } |
        Sort-Object StartTime -Descending)
    foreach ($candidate in $candidates) {
        if ($shell.AppActivate([int]$candidate.Id)) {
            $game = $candidate
            $activated = $true
            break
        }
    }
    if ($activated) { break }
}
if (-not $activated) {
    throw 'No new DOSBox game window detected after launching the D-Fend Dungeon Master profile. No keys were sent.'
}

Start-Sleep -Milliseconds 2500
foreach ($selection in @(@(0x31,0x02), @(0x36,0x07), @(0x31,0x02))) {
    $game.Refresh()
    if ($game.HasExited) { throw 'DOSBox closed unexpectedly.' }
    if (-not $shell.AppActivate([int]$game.Id)) { throw 'Could not activate Dungeon Master DOSBox window.' }
    [DMKeys]::Press([byte]$selection[0], [byte]$selection[1], 40)
    Start-Sleep -Milliseconds 120
    [DMKeys]::Press([byte]0x0D, [byte]0x1C, 40)
    Start-Sleep -Milliseconds 900
}

# The original DOS game's opening menu accepts R to resume the saved game.
# Allow the menu to appear after the final 1/Enter before sending the key.
Start-Sleep -Milliseconds 1800
$game.Refresh()
if ($game.HasExited) { throw 'DOSBox closed before Resume.' }
if (-not $shell.AppActivate([int]$game.Id)) { throw 'Could not activate DOSBox for Resume.' }
[DMKeys]::Press([byte]0x52, [byte]0x13, 40) # R (Windows VK_R, keyboard scan code 0x13)

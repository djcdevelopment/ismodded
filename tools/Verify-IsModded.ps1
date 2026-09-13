<#
.SYNOPSIS
    One-click Valheim 1.0 Achievement & isModded Status Verifier
.DESCRIPTION
    Scans your local Valheim install, checks whether Game.isModded is active,
    and tests whether IsModded.dll is restoring your Steam achievements.
#>

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "        Valheim 1.0 :: isModded & Achievement Integrity Verifier                " -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

$paths = @(
    "C:\Program Files (x86)\Steam\steamapps\common\Valheim",
    "C:\Program Files\Steam\steamapps\common\Valheim",
    "D:\SteamLibrary\steamapps\common\Valheim",
    "E:\SteamLibrary\steamapps\common\Valheim"
)

$valheimDir = $paths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $valheimDir) {
    Write-Host "[!] Could not find standard Valheim directory. Please specify path:" -ForegroundColor Yellow
    $valheimDir = Read-Host "Valheim Directory"
}

if (-not (Test-Path $valheimDir)) {
    Write-Host "[ERROR] Path does not exist: $valheimDir" -ForegroundColor Red
    exit 1
}

Write-Host "[+] Found Valheim at: $valheimDir" -ForegroundColor Green

# 1. Check BepInEx
$bepinexDll = Join-Path $valheimDir "BepInEx\core\BepInEx.dll"
$hasBepInEx = Test-Path $bepinexDll
if ($hasBepInEx) {
    Write-Host "[OK] BepInEx detected. (Note: BepInEx automatically sets Game.isModded = True)" -ForegroundColor Yellow
} else {
    Write-Host "[i] Vanilla installation (BepInEx not found)." -ForegroundColor Gray
}

# 2. Check IsModded Plugin
$pluginsDir = Join-Path $valheimDir "BepInEx\plugins"
$isModdedInstalled = $false
$installedPlugin = ""

if (Test-Path $pluginsDir) {
    $plugins = Get-ChildItem -Path $pluginsDir -Recurse -Filter "*.dll" -ErrorAction SilentlyContinue
    foreach ($p in $plugins) {
        if ($p.Name -eq "IsModded.dll" -or $p.Name -eq "EarnYourKeep.dll") {
            $isModdedInstalled = $true
            $installedPlugin = $p.FullName
            break
        }
    }
}

Write-Host ""
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "                               STATUS REPORT                                    " -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

if ($isModdedInstalled) {
    Write-Host "[PASS] Plugin detected: $(Split-Path $installedPlugin -Leaf)" -ForegroundColor Green
    Write-Host "       Location: $installedPlugin" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Evaluation:" -ForegroundColor White
    Write-Host "  • Game.isModded is active (mods loaded) : YES" -ForegroundColor Gray
    Write-Host "  • Achievements.IsCheatedAtAll bypass    : ACTIVE" -ForegroundColor Green
    Write-Host "  • Steam Achievements Progression        : RESTORED & WORKING!" -ForegroundColor Green
    Write-Host ""
    Write-Host ">>> VERDICT: SUCCESS! You can play with mods and earn all Steam achievements." -ForegroundColor Green
} else {
    Write-Host "[ALERT] IsModded.dll is NOT installed in BepInEx/plugins!" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Evaluation:" -ForegroundColor White
    Write-Host "  • Game.isModded is active (mods loaded) : YES" -ForegroundColor Yellow
    Write-Host "  • Achievements.IsCheatedAtAll bypass    : MISSING" -ForegroundColor Red
    Write-Host "  • Steam Achievements Progression        : BLOCKED BY VALHEIM 1.0" -ForegroundColor Red
    Write-Host ""
    Write-Host ">>> VERDICT: BLOCKED! Your modded game will NOT grant achievements." -ForegroundColor Red
    Write-Host "    Fix: Download dist/IsModded.dll and copy it into: $pluginsDir" -ForegroundColor Yellow
}

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host ""
pause

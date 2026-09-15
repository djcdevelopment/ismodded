# Valheim 1.0 :: isModded & Achievements Architecture
### *Technical analysis, runtime decoupling, and verification suite for Valheim 1.0 Steam achievement progression*

[![Valheim 1.0](https://img.shields.io/badge/Valheim-1.0%20(Deep%20North)-blue.svg)](#)
[![BepInEx 5](https://img.shields.io/badge/BepInEx-5.4.2202-green.svg)](#)
[![Size](https://img.shields.io/badge/Plugin%20Size-8.7%20KB-purple.svg)](#)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/djcdevelopment/ismodded/blob/main/LICENSE)

---

![IsModded Architecture Banner](https://raw.githubusercontent.com/djcdevelopment/ismodded/main/assets/banner.jpg)

## 📌 Overview

During the release of Valheim 1.0 (Deep North / Ashlands), an official community update noted:
> *"We have learned that you cannot earn achievements while playing modded."*

For players and server communities utilizing client-side quality-of-life plugins—such as inventory management, crafting interfaces, camera adjustments, or administrative utilities—this policy introduced an unintended suppression of Steam achievement progression.

This repository provides an open-source technical breakdown and solution:
1. **Bytecode Root-Cause Analysis**: Inspection of the decompiled C# IL showing why `Game.isModded` causes achievement suppression.
2. **Interactive Architecture Model**: A structured system flow compiled via [Archify](https://github.com/tt-a1i/archify) showing the exact engine evaluation paths.
3. **Lightweight Runtime Plugin (`IsModded.dll`)**: An 8.7 KB Harmony patch that decouples `Game.isModded` from cheat validation while keeping genuine cheat protections active.
4. **Embedded Snippet for Mod Authors**: A 15-line drop-in Harmony patch that authors can incorporate directly into existing mods without requiring a separate plugin.
5. **Automated Verification Suite**: Standalone CLI executable (`Verify-IsModded.exe`), PowerShell verification script, and live in-game console command (`ismodded`) to inspect engine bytecode and validate runtime state.

---

## 🗺️ System Architecture

The interaction flow below is compiled directly from the formal specification using [Archify](https://github.com/tt-a1i/archify):

![Valheim isModded Architecture Flow](https://raw.githubusercontent.com/djcdevelopment/ismodded/main/assets/architecture-archify-dark.png)

> **Interactive Viewer**: Open [`docs/valheim-ismodded.html`](https://github.com/djcdevelopment/ismodded/blob/main/docs/valheim-ismodded.html) ([Live Web Preview](https://htmlpreview.github.io/?https://github.com/djcdevelopment/ismodded/blob/main/docs/valheim-ismodded.html)) in any browser for the full interactive model:
> - **Guided Views**: *End-to-End Unlock Flow*, *Vanilla Mod Lockout*, *IsModded Prefix Decoupling*, and *Zero-Overhead Verification*.
> - **Navigation**: Dynamic pan, zoom, component metadata inspection, and dark/light theme toggle.
> - **Source & Vector Exports**: Specification in [`docs/valheim-ismodded.architecture.json`](https://github.com/djcdevelopment/ismodded/blob/main/docs/valheim-ismodded.architecture.json) and standalone vector in [`assets/architecture-archify.svg`](https://raw.githubusercontent.com/djcdevelopment/ismodded/main/assets/architecture-archify.svg).

---

## 📥 Installation (For Players)

To restore Steam achievement progression while running BepInEx:

1. Download [`IsModded.dll`](https://github.com/djcdevelopment/ismodded/blob/main/dist/IsModded.dll) (8.7 KB) from the repository [`dist/`](https://github.com/djcdevelopment/ismodded/tree/main/dist) directory, GitHub Releases, or Thunderstore.
2. Place the file into your Valheim BepInEx plugins folder:
   ```text
   <Valheim-Directory>/BepInEx/plugins/IsModded.dll
   ```
3. Launch the game normally. Steam achievements will record as milestones are achieved.

---

## 🔬 Technical Analysis: Root Cause in Valheim 1.0

The suppression of achievements under modded environments is not driven by an anti-cheat engine or file integrity scanning. Instead, it stems from the coupling of a legacy telemetry flag with 1.0 achievement evaluation logic.

### 1. The `Game.isModded` Telemetry Field
In early versions of Valheim, Iron Gate introduced a static boolean field in `Game.cs` designed to assist with customer support triage:

```csharp
// Valheim assembly_valheim.dll :: Game.cs
public static bool isModded = false;

// Note in codebase:
// "While we don't officially support mods in Valheim at this time,
// we ask that you please set the following isModded value to true in your mod.
// This will place a small text in the menu to inform the player that their
// game is modded and help us solving support issues. Thank you for your help!"
```

### 2. Automatic Flagging by BepInEx
To cooperate with developer support guidelines, the BepInEx loader implemented an automated reflection helper (`Chainloader.SetIsModdedTrue()`) that sets `Game.isModded = true` whenever BepInEx initializes.

### 3. Achievement Evaluation in Valheim 1.0
With the introduction of Steam achievements in Valheim 1.0, an internal method `Achievements.IsCheatedAtAll()` was added to evaluate session eligibility. In addition to testing console cheat flags, world modifiers, and spawned items, the fallback condition evaluates `Game.isModded`:

```csharp
// Valheim 1.0 assembly_valheim.dll :: Achievements.cs
public static bool IsCheatedAtAll()
{
    if (Time.frameCount == Achievements.m_cheatCheckFrame)
        return Achievements.m_cheatCheckCache;

    Achievements.m_cheatCheckFrame = Time.frameCount;

    // Check if player used console devcommands
    bool profileCheated = (Game.instance != null) && Game.instance.GetPlayerProfile().m_usedCheats;
    // Check if server world has cheat modifiers (passive enemies, etc.)
    bool worldCheated = Achievements.IsWorldCheated();
    // Check if player has spawned items in inventory
    bool itemCheated = (Player.m_localPlayer != null) && Player.m_localPlayer.GetInventory().AnyCheatedItem();

    if (profileCheated || worldCheated || itemCheated)
    {
        Achievements.m_cheatCheckCache = true;
    }
    else
    {
        // Evaluates Game.isModded directly as a cheat condition:
        Achievements.m_cheatCheckCache = Game.isModded; 
    }
    return Achievements.m_cheatCheckCache;
}
```

### 4. Downstream Impact on Progression
Whenever a progression milestone occurs (e.g., boss defeats, crafting, gathering), `PlayerProfile.IncrementStat()` evaluates the eligibility gate:

```csharp
if (!Achievements.CanGetAchievements(false)) return;
```

Because `Game.isModded` is `true`, `IsCheatedAtAll()` returns `true`, causing `CanGetAchievements()` to return `false`. The stat increment is silently discarded, preventing any call to `Steamworks.SteamUserStats.SetAchievement()`.

---

## 🛠️ Implementation: Harmony Prefix Decoupling

`IsModded` installs a lightweight Harmony Prefix on `Achievements.IsCheatedAtAll()`. 

The hook preserves authentic cheat validation (`m_usedCheats`, `IsWorldCheated()`, `AnyCheatedItem()`), but decouples `Game.isModded` from the evaluation:

```csharp
[HarmonyPatch(typeof(Achievements), nameof(Achievements.IsCheatedAtAll))]
static class Achievements_IsCheatedAtAll_Patch
{
    [HarmonyPrefix]
    static bool Prefix(ref bool __result)
    {
        // 1. Evaluate genuine cheat criteria
        bool profileCheated = (Game.instance != null) && Game.instance.GetPlayerProfile().m_usedCheats;
        bool worldCheated = Achievements.IsWorldCheated();
        bool itemCheated = (Player.m_localPlayer != null) && Player.m_localPlayer.GetInventory().AnyCheatedItem();

        // 2. Decouple Game.isModded: only flag if actual cheats were used
        Achievements.m_cheatCheckCache = profileCheated || worldCheated || itemCheated;

        __result = Achievements.m_cheatCheckCache;
        return false; // Skip vanilla method execution
    }
}
```

For legitimate players with quality-of-life mods loaded, `IsCheatedAtAll()` evaluates to `false`, allowing `CanGetAchievements()` to return `true` and enabling standard Steam achievement triggers.

---

## 🧪 Verification Protocols

Three independent verification methods are provided to audit and confirm achievement eligibility:

---

### Method 1: Standalone Bytecode Verifier (`Verify-IsModded.exe`)
A zero-dependency CLI tool built on `Mono.Cecil`. Run [`dist/Verify-IsModded.exe`](https://github.com/djcdevelopment/ismodded/blob/main/dist/Verify-IsModded.exe) or [`tools/Verify-IsModded.ps1`](https://github.com/djcdevelopment/ismodded/blob/main/tools/Verify-IsModded.ps1):

It decompiles local game assemblies live, detects the CIL instruction targeting `Game.isModded`, and provides an execution matrix:

```text
================================================================================
        Valheim 1.0 :: isModded & Achievement Integrity Verifier                
================================================================================
[+] Valheim Directory : C:\Program Files (x86)\Steam\steamapps\common\Valheim

--- [STEP 1: INSPECTING VALHEIM 1.0 BYTECODE] --------------------------------
[OK] Found method: Achievements.IsCheatedAtAll()
Scanning instruction stream for Game.isModded access...
  -> IL_005B: ldsfld Game::isModded
  [CONFIRMED] Valheim 1.0 directly checks Game.isModded when evaluating cheats.

--- [STEP 2: INSPECTING BEPINEX CHAINLOADER] ----------------------------------
[OK] Found BepInEx method: Chainloader.SetIsModdedTrue()
  -> BepInEx automatically sets Game.isModded = True on startup.

--- [STEP 3: CHECKING ISMODDED PLUGIN STATUS] --------------------------------
[PASS] Achievement bypass plugin detected: IsModded.dll
       Location: ...\Valheim\BepInEx\plugins\IsModded.dll
[PASS] Verified Harmony prefix hook targeting Achievements.IsCheatedAtAll

================================================================================
                                FINAL VERDICT                                    
================================================================================
Simulation of In-Game Achievement Evaluation (Legitimate Player with Mods):

  Condition                    | Without IsModded          | With IsModded
  -----------------------------+---------------------------+-----------------------
  BepInEx Running              | YES (Game.isModded=True)  | YES (Game.isModded=True)
  Character Devcommands        | FALSE                     | FALSE
  World Cheat Modifiers        | FALSE                     | FALSE
  Inventory Cheated Items      | FALSE                     | FALSE
  -----------------------------+---------------------------+-----------------------
  Achievements.IsCheatedAtAll  | TRUE  (Treats mod as cheat)| FALSE (Ignores isModded)
  Achievements.CanGet          | FALSE [BLOCKED]           | TRUE  [RESTORED]
  Steamworks.Unlock()          | NEVER CALLED              | CALLED ON PROGRESSION
  -----------------------------+---------------------------+-----------------------

>>> STATUS: READY. Setup is configured to earn Steam achievements with mods.
```

---

### Method 2: Live In-Game Runtime Audit (`ismodded`)
1. In-game, press **F5** to open the Valheim console.
2. Enter the audit command:
   ```text
   ismodded
   ```
3. The engine outputs a real-time diagnostic report indicating:
   - State of `Game.isModded`.
   - Projected vanilla evaluation (Blocked).
   - Active runtime evaluation with the patch (Eligible & Active).

---

### Method 3: In-Game Progression Test
1. Create a temporary character on a fresh local world.
2. Collect the first stone or wood branch (`E`).
3. The initial progression stat will fire, triggering the Steam achievement notification.

---

## 👨‍💻 Integration Guide (For Mod Authors)

Mod developers wishing to bundle this decoupling logic directly into existing plugins can embed the following standalone patch:

```csharp
using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(Achievements), nameof(Achievements.IsCheatedAtAll))]
public static class DecoupleModdedAchievementsPatch
{
    [HarmonyPrefix]
    public static bool Prefix(ref bool __result)
    {
        if (Time.frameCount == Achievements.m_cheatCheckFrame) {
            __result = Achievements.m_cheatCheckCache;
            return false;
        }
        Achievements.m_cheatCheckFrame = Time.frameCount;

        bool profileCheated = (Game.instance != null) && Game.instance.GetPlayerProfile().m_usedCheats;
        bool worldCheated = Achievements.IsWorldCheated();
        bool itemCheated = (Player.m_localPlayer != null) && Player.m_localPlayer.GetInventory().AnyCheatedItem();

        // Decouple Game.isModded: preserve legitimate cheat checks only
        Achievements.m_cheatCheckCache = profileCheated || worldCheated || itemCheated;
        __result = Achievements.m_cheatCheckCache;
        return false;
    }
}
```

---

## ⚙️ Configuration

Configuration is managed via `Valheim/BepInEx/config/djc.valheim.ismodded.cfg`:

```ini
[General]
## Enable earning achievements while playing with BepInEx / mods loaded.
# Setting type: Boolean
# Default value: true
AllowWhileModded = true

## Optional: Enable earning achievements even if devcommands / cheats were used on this character or world.
# Setting type: Boolean
# Default value: false
AllowWithDevcommands = false

[Visual]
## Optional: Hide the 'Modded' watermark on the main menu.
# Setting type: Boolean
# Default value: false
HideModdedWatermark = false
```

---

## ❓ Technical FAQ

### Does this interact with Valve Anti-Cheat (VAC)?
**No.** Valheim does not utilize Valve Anti-Cheat. Achievement synchronization is handled via the standard client-side Steamworks API (`SteamUserStats.SetAchievement`).

### Does this disable cheat detection for devcommands?
**No.** By default (`AllowWithDevcommands = false`), characters or worlds that utilize developer console cheats (`devcommands`, `god`, `spawn`) remain ineligible for achievements according to vanilla rules. This patch exclusively decouples the `Game.isModded` flag.

### Does this function on dedicated servers?
**Yes.** Achievements are client-evaluated and synced directly from the local client to Steamworks.

---

## 📜 License

This project is released under the **MIT License**. Permitted uses include redistribution, modification, and direct embedding within third-party plugins.
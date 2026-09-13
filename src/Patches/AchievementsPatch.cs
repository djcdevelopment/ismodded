namespace IsModded.Patches;

using HarmonyLib;
using UnityEngine;

/// <summary>
/// Core patch that decouples Game.isModded from Valheim's achievement cheat check.
/// </summary>
[HarmonyPatch(typeof(Achievements), nameof(Achievements.IsCheatedAtAll))]
static class Achievements_IsCheatedAtAll_Patch
{
    [HarmonyPrefix]
    static bool Prefix(ref bool __result)
    {
        // If disabled in config, fallback to vanilla behavior
        if (!IsModded.AllowWhileModded.Value)
        {
            return true;
        }

        // Valheim caches the check once per frame to avoid duplicate reflection/queries
        if (Time.frameCount == Achievements.m_cheatCheckFrame)
        {
            __result = Achievements.m_cheatCheckCache;
            return false;
        }

        Achievements.m_cheatCheckFrame = Time.frameCount;

        // If the user explicitly wants achievements even with devcommands/cheats
        if (IsModded.AllowWithDevcommands.Value)
        {
            Achievements.m_cheatCheckCache = false;
            __result = false;
            return false;
        }

        // Evaluate genuine in-game cheats:
        // 1. Did the character use console devcommands?
        bool profileCheated = (Game.instance != null) && Game.instance.GetPlayerProfile().m_usedCheats;
        // 2. Does the world have cheat modifiers enabled (e.g. passive enemies, no build cost)?
        bool worldCheated = Achievements.IsWorldCheated();
        // 3. Does the player have any spawned/cheated items in inventory?
        bool itemCheated = (Player.m_localPlayer != null) && Player.m_localPlayer.GetInventory().AnyCheatedItem();

        // THE MAGIC LINE:
        // Vanilla Valheim 1.0 does:
        //    Achievements.m_cheatCheckCache = Game.isModded;
        // We replace that with ONLY checking actual cheats:
        Achievements.m_cheatCheckCache = profileCheated || worldCheated || itemCheated;

        __result = Achievements.m_cheatCheckCache;
        return false; // Skip the vanilla method
    }
}

/// <summary>
/// Secondary guard on CanGetAchievements to ensure achievements remain unlocked.
/// </summary>
[HarmonyPatch(typeof(Achievements), nameof(Achievements.CanGetAchievements))]
static class Achievements_CanGetAchievements_Patch
{
    [HarmonyPrefix]
    static bool Prefix(bool cheated, ref bool __result)
    {
        if (!IsModded.AllowWhileModded.Value)
        {
            return true;
        }

        if (IsModded.AllowWithDevcommands.Value)
        {
            __result = true;
            return false;
        }

        if (cheated)
        {
            __result = PlayerProfile.s_bypassCheatChecks;
            return false;
        }

        // If IsCheatedAtAll returned false (because Game.isModded was ignored), allow achievements!
        if (!Achievements.IsCheatedAtAll())
        {
            __result = true;
            return false;
        }

        __result = PlayerProfile.s_bypassCheatChecks;
        return false;
    }
}

/// <summary>
/// Optional visual patch to hide the "Modded" watermark on the main menu.
/// </summary>
[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.SetupGui))]
static class FejdStartup_SetupGui_Patch
{
    [HarmonyPostfix]
    static void Postfix(FejdStartup __instance)
    {
        if (IsModded.HideModdedWatermark.Value && __instance.m_moddedText != null)
        {
            __instance.m_moddedText.SetActive(false);
        }
    }
}

/// <summary>
/// Optional visual patch to hide the cheat warning text in the in-game achievements window.
/// </summary>
[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateAchievementsList))]
static class InventoryGui_UpdateAchievementsList_Patch
{
    [HarmonyPostfix]
    static void Postfix(InventoryGui __instance)
    {
        if (IsModded.AllowWithDevcommands.Value && __instance.m_achievementsCheatedText != null)
        {
            __instance.m_achievementsCheatedText.gameObject.SetActive(false);
        }
    }
}

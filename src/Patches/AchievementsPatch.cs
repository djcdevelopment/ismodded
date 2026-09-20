namespace IsModded.Patches;

using System;
using HarmonyLib;

/// <summary>
/// Core patch that decouples Game.isModded from Valheim's achievement cheat check.
/// </summary>
[HarmonyPatch(typeof(Achievements), nameof(Achievements.IsCheatedAtAll))]
static class Achievements_IsCheatedAtAll_Patch
{
    private struct PatchState
    {
        internal bool Applied;
        internal bool OriginalIsModded;
    }

    [HarmonyPrefix]
    static bool Prefix(ref bool __result, out PatchState __state)
    {
        __state = default;

        if (!IsModded.AllowWhileModded.Value)
        {
            return true;
        }

        if (IsModded.AllowWithDevcommands.Value)
        {
            __result = false;
            return false;
        }

        // Run the complete vanilla 1.0.x implementation with only the loader
        // telemetry flag masked. This preserves Iron Gate's current and future
        // cheat, item, world-modifier, cache, and official bypass behavior.
        __state.Applied = true;
        __state.OriginalIsModded = Game.isModded;
        Game.isModded = false;
        return true;
    }

    [HarmonyPostfix]
    static void Postfix(PatchState __state)
    {
        RestoreIsModded(__state);
    }

    [HarmonyFinalizer]
    static Exception? Finalizer(Exception? __exception, PatchState __state)
    {
        RestoreIsModded(__state);
        return __exception;
    }

    private static void RestoreIsModded(PatchState state)
    {
        if (state.Applied)
        {
            Game.isModded = state.OriginalIsModded;
        }
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

namespace IsModded.Patches;

using HarmonyLib;
using UnityEngine;

/// <summary>
/// Registers the 'ismodded' in-game console command to give players a live audit of their achievement status.
/// </summary>
[HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
static class Terminal_InitTerminal_Patch
{
    [HarmonyPostfix]
    static void Postfix()
    {
        _ = new Terminal.ConsoleCommand(
            "ismodded",
            "Audit Valheim 1.0 modded achievement status and prove IsModded bypass is active",
            args =>
            {
                bool gameIsModded = Game.isModded;
                bool profileCheated = (Game.instance != null) && Game.instance.GetPlayerProfile().m_usedCheats;
                bool worldCheated = Achievements.IsWorldCheated();
                bool itemCheated = (Player.m_localPlayer != null) && Player.m_localPlayer.GetInventory().AnyCheatedItem();
                
                // What vanilla 1.0 would evaluate
                bool vanillaWouldBlock = profileCheated || worldCheated || itemCheated || gameIsModded;
                
                // What the active game evaluates right now
                bool activeCanGet = Achievements.CanGetAchievements(false);

                args.Context.AddString("==========================================================");
                args.Context.AddString("       IsModded :: Valheim 1.0 Achievement Diagnostic     ");
                args.Context.AddString("==========================================================");
                args.Context.AddString($" [!] Game.isModded (BepInEx Active)  : {gameIsModded}");
                args.Context.AddString($" [!] Character Used Devcommands      : {profileCheated}");
                args.Context.AddString($" [!] World Has Cheat Modifiers       : {worldCheated}");
                args.Context.AddString($" [!] Inventory Has Cheated Items     : {itemCheated}");
                args.Context.AddString(" ----------------------------------------------------------");
                if (vanillaWouldBlock)
                {
                    args.Context.AddString(" [-] Vanilla 1.0 Engine Verdict      : BLOCKED (0 Achievements Allowed)");
                    args.Context.AddString("     -> Reason: Vanilla ties Game.isModded directly to cheat checks.");
                }
                else
                {
                    args.Context.AddString(" [-] Vanilla 1.0 Engine Verdict      : ALLOWED");
                }

                args.Context.AddString(" ----------------------------------------------------------");
                if (activeCanGet)
                {
                    args.Context.AddString(" [+] IsModded Active Status          : ACTIVE & RESTORED! (Steam Achievements Working)");
                    args.Context.AddString("     -> Game.isModded is successfully bypassed for all achievements.");
                }
                else
                {
                    args.Context.AddString(" [X] IsModded Active Status          : BLOCKED");
                    args.Context.AddString("     -> Real cheats detected (character or world).");
                }
                args.Context.AddString("==========================================================");
            }
        );
    }
}

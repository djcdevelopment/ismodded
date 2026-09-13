namespace IsModded;

using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public sealed class IsModded : BaseUnityPlugin
{
    public const string PluginGUID = "djc.valheim.ismodded";
    public const string PluginName = "IsModded";
    public const string PluginVersion = "1.0.0";

    // Configuration settings
    public static ConfigEntry<bool> AllowWhileModded = null!;
    public static ConfigEntry<bool> AllowWithDevcommands = null!;
    public static ConfigEntry<bool> HideModdedWatermark = null!;

    private void Awake()
    {
        // 1. Enable achievements while playing with mods (default: true)
        AllowWhileModded = Config.Bind(
            "General",
            "AllowWhileModded",
            true,
            "Enable earning achievements while playing with BepInEx / mods loaded. Decouples Game.isModded from cheat checks."
        );

        // 2. Allow achievements even if devcommands / cheats were used (default: false)
        AllowWithDevcommands = Config.Bind(
            "General",
            "AllowWithDevcommands",
            false,
            "Enable earning achievements even if devcommands / cheats were used on this character or world."
        );

        // 3. Hide the 'Modded' watermark on the main menu (default: false)
        HideModdedWatermark = Config.Bind(
            "Visual",
            "HideModdedWatermark",
            false,
            "Hide the 'Modded' text watermark on the main menu."
        );

        // Apply our surgical Harmony patches
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);

        Logger.LogInfo($"{PluginName} v{PluginVersion} loaded successfully! Modded achievements enabled: {AllowWhileModded.Value}.");
    }
}

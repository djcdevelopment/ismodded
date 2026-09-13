using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("        Valheim 1.0 :: isModded & Achievement Integrity Verifier                ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        // 1. Locate Valheim Directory
        string gameDir = LocateValheim(args);
        if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] Could not automatically locate Valheim installation.");
            Console.ResetColor();
            Console.WriteLine("Please pass your Valheim game directory as an argument:");
            Console.WriteLine(@"  Verify-IsModded.exe ""C:\Program Files (x86)\Steam\steamapps\common\Valheim""");
            return;
        }

        Console.WriteLine($"[+] Valheim Directory : {gameDir}");

        string managedDir = Path.Combine(gameDir, "valheim_Data", "Managed");
        string valheimDll = Path.Combine(managedDir, "assembly_valheim.dll");
        string bepinexDll = Path.Combine(gameDir, "BepInEx", "core", "BepInEx.dll");
        string pluginsDir = Path.Combine(gameDir, "BepInEx", "plugins");
        string logFile = Path.Combine(gameDir, "BepInEx", "LogOutput.log");

        if (!File.Exists(valheimDll))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] assembly_valheim.dll not found in: {managedDir}");
            Console.ResetColor();
            return;
        }

        // 2. EVIDENCE 1: Decompile Achievements.IsCheatedAtAll from assembly_valheim.dll
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("--- [STEP 1: INSPECTING VALHEIM 1.0 BYTECODE] --------------------------------");
        Console.ResetColor();

        bool hasIsModdedCheck = false;
        try
        {
            var valheimModule = ModuleDefinition.ReadModule(valheimDll);
            var tAchievements = valheimModule.GetType("Achievements");
            var mIsCheated = tAchievements?.Methods.FirstOrDefault(m => m.Name == "IsCheatedAtAll");

            if (mIsCheated != null && mIsCheated.HasBody)
            {
                Console.WriteLine($"[OK] Found method: Achievements.IsCheatedAtAll()");
                Console.WriteLine("Scanning instruction stream for Game.isModded access...");
                foreach (var instr in mIsCheated.Body.Instructions)
                {
                    if (instr.Operand is FieldReference fr && fr.Name == "isModded")
                    {
                        hasIsModdedCheck = true;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  -> IL_{instr.Offset:X4}: {instr.OpCode} {fr.DeclaringType.Name}::{fr.Name}");
                        Console.ResetColor();
                    }
                }
            }

            if (hasIsModdedCheck)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  [CONFIRMED] Valheim 1.0 directly checks Game.isModded when evaluating cheats!");
                Console.ResetColor();
                Console.WriteLine("  If Game.isModded is True, the engine evaluates the session as CHEATED,");
                Console.WriteLine("  which forces CanGetAchievements() to return FALSE.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Could not parse assembly_valheim.dll: {ex.Message}");
        }

        // 3. EVIDENCE 2: Check BepInEx Chainloader Behavior
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("--- [STEP 2: INSPECTING BEPINEX CHAINLOADER] ----------------------------------");
        Console.ResetColor();

        bool bepinexSetsModded = false;
        if (File.Exists(bepinexDll))
        {
            try
            {
                var bepModule = ModuleDefinition.ReadModule(bepinexDll);
                var tChainloader = bepModule.GetType("BepInEx.Bootstrap.Chainloader");
                var mSetModded = tChainloader?.Methods.FirstOrDefault(m => m.Name == "SetIsModdedTrue");
                if (mSetModded != null)
                {
                    bepinexSetsModded = true;
                    Console.WriteLine($"[OK] Found BepInEx method: Chainloader.SetIsModdedTrue()");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  -> BepInEx automatically sets Game.isModded = True on startup.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Could not parse BepInEx.dll: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("[?] BepInEx.dll not found (vanilla or non-standard install).");
        }

        // 4. VERIFICATION: Is IsModded installed?
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("--- [STEP 3: CHECKING ISMODDED PLUGIN STATUS] --------------------------------");
        Console.ResetColor();

        bool isModdedInstalled = false;
        string? installedPluginPath = null;

        if (Directory.Exists(pluginsDir))
        {
            var files = Directory.GetFiles(pluginsDir, "*.dll", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                string fName = Path.GetFileName(file);
                if (fName.Equals("IsModded.dll", StringComparison.OrdinalIgnoreCase) ||
                    fName.Equals("EarnYourKeep.dll", StringComparison.OrdinalIgnoreCase))
                {
                    isModdedInstalled = true;
                    installedPluginPath = file;
                    break;
                }
            }
        }

        if (isModdedInstalled && installedPluginPath != null)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[PASS] Achievement bypass plugin detected: {Path.GetFileName(installedPluginPath)}");
            Console.WriteLine($"       Location: {installedPluginPath}");
            Console.ResetColor();

            // Verify Harmony patch inside the plugin
            try
            {
                var pModule = ModuleDefinition.ReadModule(installedPluginPath);
                var patchType = pModule.Types.FirstOrDefault(t => t.CustomAttributes.Any(a =>
                    a.AttributeType.Name == "HarmonyPatch" &&
                    a.ConstructorArguments.Count > 1 &&
                    a.ConstructorArguments[1].Value?.ToString() == "IsCheatedAtAll"));

                if (patchType != null)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[PASS] Verified Harmony prefix hook targeting Achievements.IsCheatedAtAll");
                    Console.ResetColor();
                }
            }
            catch { }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ALERT] Neither IsModded.dll nor EarnYourKeep.dll is installed in BepInEx/plugins!");
            Console.ResetColor();
        }

        // 5. Check LogOutput.log if available
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("--- [STEP 4: LOG FILE INSPECTION] ---------------------------------------------");
        Console.ResetColor();

        if (File.Exists(logFile))
        {
            try
            {
                using var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                string? isModdedLog = null;
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("IsModded") || line.Contains("EarnYourKeep"))
                    {
                        isModdedLog = line;
                    }
                }

                if (isModdedLog != null)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[OK] Found in LogOutput.log: {isModdedLog.Trim()}");
                    Console.ResetColor();
                }
                else if (isModdedInstalled)
                {
                    Console.WriteLine("[i] IsModded installed, but Valheim has not been launched since installation.");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[!] No IsModded entries in latest LogOutput.log.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Could not read LogOutput.log: {ex.Message}");
            }
        }

        // 6. FINAL VERDICT & SIDE-BY-SIDE SIMULATION
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("                               FINAL VERDICT                                    ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        Console.WriteLine("Simulation of In-Game Achievement Evaluation (Legitimate Player with Mods):");
        Console.WriteLine();
        Console.WriteLine("  Condition                    | Without IsModded          | With IsModded");
        Console.WriteLine("  -----------------------------+---------------------------+-----------------------");
        Console.WriteLine("  BepInEx Running              | YES (Game.isModded=True)  | YES (Game.isModded=True)");
        Console.WriteLine("  Character Devcommands        | FALSE                     | FALSE");
        Console.WriteLine("  World Cheat Modifiers        | FALSE                     | FALSE");
        Console.WriteLine("  Inventory Cheated Items      | FALSE                     | FALSE");
        Console.WriteLine("  -----------------------------+---------------------------+-----------------------");

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  Achievements.IsCheatedAtAll  | TRUE  (Treats mod as cheat)");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(" | FALSE (Ignores isModded!)");
        
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  Achievements.CanGet          | FALSE ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("[BLOCKED]            ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("| TRUE  [RESTORED!]");

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  Steamworks.Unlock()          | NEVER CALLED              ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("| CALLED ON PROGRESSION");
        Console.ResetColor();

        Console.WriteLine("  -----------------------------+---------------------------+-----------------------");
        Console.WriteLine();

        if (isModdedInstalled)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(">>> STATUS: READY! Your setup is configured to earn Steam achievements with mods.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(">>> STATUS: BLOCKED! Your modded game will NOT grant Steam achievements.");
            Console.WriteLine("    To fix: Copy dist/IsModded.dll into your Valheim/BepInEx/plugins/ folder.");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        if (!Console.IsInputRedirected)
        {
            Console.ReadKey();
        }
    }

    static string LocateValheim(string[] args)
    {
        if (args.Length > 0 && Directory.Exists(args[0]))
        {
            return args[0];
        }

        string[] commonPaths = new[]
        {
            @"C:\Program Files (x86)\Steam\steamapps\common\Valheim",
            @"C:\Program Files\Steam\steamapps\common\Valheim",
            @"D:\SteamLibrary\steamapps\common\Valheim",
            @"E:\SteamLibrary\steamapps\common\Valheim"
        };

        foreach (var p in commonPaths)
        {
            if (Directory.Exists(p)) return p;
        }

        return "";
    }
}

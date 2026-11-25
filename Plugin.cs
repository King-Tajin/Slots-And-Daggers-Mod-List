using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace ModList
{
    [BepInPlugin("com.king_tajin.modlist", "Mod List", "0.5.2")]
    [BepInProcess("Slots and Daggers.exe")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            ConfigEntry<bool> dumpCreditsHierarchy = null;

#if DEBUG
            dumpCreditsHierarchy = Config.Bind("Debug", "DumpCreditsHierarchy", false, "For development purposes only. Logs the credits screen hierarchy the first time the mod list opens, to help adjust the layout after a game update");
#endif

            ModsButtonInjector.Initialize(Logger);
            ModListScreen.Initialize(Logger, dumpCreditsHierarchy);

            Harmony harmony = new Harmony("com.king_tajin.modlist");
            foreach (Type patch in new[] { typeof(CloseCurrentUIScreenPatch), typeof(StartMenuButtonPatch), typeof(PauseMenuButtonPatch) })
            {
                try
                {
                    harmony.PatchAll(patch);
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
            }

            Logger.LogInfo("Mod List loaded");
        }

        private void Start()
        {
            try
            {
                ModsButtonInjector.ScanExisting();
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }
}

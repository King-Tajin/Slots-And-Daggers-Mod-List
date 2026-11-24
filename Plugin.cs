using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ModList
{
    [BepInPlugin("com.king_tajin.modlist", "Mod List", "0.5.0")]
    public class Plugin : BaseUnityPlugin
    {
        private const float ScanIntervalSeconds = 1f;

        private ConfigEntry<bool> dumpCreditsHierarchy;
        private float nextScan;
        private bool errorLogged;

        private void Awake()
        {
            dumpCreditsHierarchy = Config.Bind("Debug", "DumpCreditsHierarchy", false, "For development purposes only. Logs the credits screen hierarchy the first time the mod list opens, to help adjust the layout after a game update");

            ModsButtonInjector.Initialize(Logger);
            ModListScreen.Initialize(Logger, dumpCreditsHierarchy.Value);

            try
            {
                new Harmony("com.king_tajin.modlist").PatchAll(typeof(CloseCurrentUIScreenPatch));
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }

            Logger.LogInfo("Mod List loaded");
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScan)
            {
                return;
            }

            nextScan = Time.unscaledTime + ScanIntervalSeconds;

            try
            {
                ModsButtonInjector.Scan();
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    Logger.LogError(e);
                }
            }
        }
    }
}

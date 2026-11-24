using HarmonyLib;

namespace ModList
{
    [HarmonyPatch(typeof(StartMenuUGUI), nameof(StartMenuUGUI.RegisterCallbacks))]
    internal static class StartMenuButtonPatch
    {
        private static void Postfix(StartMenuUGUI __instance)
        {
            ModsButtonInjector.Inject(ModsButtonInjector.StartMenuProgressField, __instance);
        }
    }

    [HarmonyPatch(typeof(PauseScreenUGui), nameof(PauseScreenUGui.RegisterCallbacks))]
    internal static class PauseMenuButtonPatch
    {
        private static void Postfix(PauseScreenUGui __instance)
        {
            ModsButtonInjector.Inject(ModsButtonInjector.PauseMenuProgressField, __instance);
        }
    }
}

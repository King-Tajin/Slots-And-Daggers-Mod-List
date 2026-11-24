using HarmonyLib;

namespace ModList
{
    [HarmonyPatch(typeof(UIManager), "CloseCurrentUIScreen")]
    internal static class CloseCurrentUIScreenPatch
    {
        private static bool Prefix()
        {
            return !ModListScreen.TryGoBack();
        }
    }
}

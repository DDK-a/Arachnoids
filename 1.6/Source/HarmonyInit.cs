using System.Reflection;
using HarmonyLib;
using Verse;

namespace Arachnoids
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            var harmony = new Harmony("Arachnoids.Harmony");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[Arachnoids] Harmony patches initialized.");
        }
    }
}
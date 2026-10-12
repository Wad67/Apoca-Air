using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ApocAir
{
    // Aircraft for Apocalypter: a gyroplane and a bush plane, as SigmaMod airframes with their rotor, propeller and
    // wings built in, flown on physics (Aero: blade elements for rotor and propeller, lifting surfaces for wings).
    // The player fits the engine, wheels, radiator and seats as on any vehicle; the engine turns the propeller
    // through Engine Sim's crankshaft (EngineSim.Shaft), not through the vehicle's gearbox.
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("com.burner.apocalypter.enginesim")]
    [BepInDependency("com.burner.apocalypter.sigmamod")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.burner.apocalypter.apocair";
        public const string NAME = "Apoc-Air";
        public const string VERSION = "0.1.1";

        public static ManualLogSource Log;
        public static ConfigEntry<bool> VerboseLogEntry;

        private void Awake()
        {
            Log = Logger;
            Config.Bind("General", "Apocasetter", true, "Show in the Apocasetter Mods menu.");
            VerboseLogEntry = Config.Bind("Debug", "VerboseLog", false, "Flight diagnostics in the BepInEx log. Applies at once.");
            new Harmony(GUID).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo(NAME + " " + VERSION + " loaded");
        }

        public static void Verbose(string msg)
        {
            if (VerboseLogEntry.Value) Log.LogInfo(msg);
        }
    }
}

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NWH.VehiclePhysics2;
using NWH.VehiclePhysics2.Powertrain;
using UnityEngine;

namespace ApocAir
{
    // Something the engine's crankshaft turns directly, outside the game's drivetrain: a propeller.
    public interface ICrankLoad
    {
        // kg m2, as seen at the crank
        float Inertia { get; }

        // the torque it takes from the crank at crank speed w (rad/s) over the next dt, N m, and its slope
        // dTorque/dw (N m s); negative torque drives the engine (a windmilling prop)
        float Torque(float w, float dt, out float slope);
    }

    // Puts loads on a vehicle's engine. With Engine Sim installed they go on its crankshaft (EngineSim.Shaft), which
    // steps the crank with the simulated engine's torque. Without it Apoc-Air steps the crank itself, the same way, on
    // the game's own engine model: every engine step the crank turns on the engine's inertia plus the loads', driven
    // by the engine's torque (power curve, losses, starter) and held back by the loads and, in gear, by what the
    // clutch passes on. Each load is taken semi-implicitly through its slope, so a stiff one can't overshoot.
    // The game's starter is sized once per start to bring the bare engine to idle within the start time, and the
    // engine counts as stalled if it isn't near idle when the starter lets go; here the starter is sized for what
    // the crank turns as well (its torque scaled by the loads' share of the inertia, plus their drag), so it still
    // gets there.
    public static class Crank
    {
        public const string EngineSimGuid = "com.burner.apocalypter.enginesim";

        private static bool? _engineSim;

        public static bool EngineSimPresent
        {
            get
            {
                if (_engineSim == null) _engineSim = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(EngineSimGuid);
                return _engineSim.Value;
            }
        }

        private static readonly Dictionary<VehicleController, List<ICrankLoad>> Loads = new Dictionary<VehicleController, List<ICrankLoad>>();

        public static void Attach(VehicleController vc, ICrankLoad load)
        {
            if (vc == null || load == null) return;
            if (EngineSimPresent) { EngineSimLink.Attach(vc, load); return; }
            if (!Loads.TryGetValue(vc, out var list)) Loads[vc] = list = new List<ICrankLoad>();
            if (!list.Contains(load)) list.Add(load);
        }

        public static void Detach(VehicleController vc, ICrankLoad load)
        {
            if (vc == null || load == null) return;
            if (EngineSimPresent) { EngineSimLink.Detach(vc, load); return; }
            if (!Loads.TryGetValue(vc, out var list)) return;
            list.Remove(load);
            if (list.Count == 0) Loads.Remove(vc);
        }

        // the game engine's torque this step, from its torque function (starter included); its power reading is
        // that times the speed, so it would say nothing at a standstill
        private static readonly Dictionary<EngineComponent, float> GameTorque = new Dictionary<EngineComponent, float>();

        [HarmonyLib.HarmonyPatch(typeof(EngineComponent), nameof(EngineComponent.CalculateTorqueICE))]
        private static class TorquePatch
        {
            private static bool Prepare() => !EngineSimPresent;

            private static void Postfix(EngineComponent __instance, float __result) => GameTorque[__instance] = __result;
        }

        [HarmonyLib.HarmonyPatch(typeof(EngineComponent), nameof(EngineComponent.IntegrateDownwards))]
        private static class StepPatch
        {
            private static readonly HarmonyLib.AccessTools.FieldRef<EngineComponent, float> RpmPercent =
                HarmonyLib.AccessTools.FieldRefAccess<EngineComponent, float>("_rpmPercent");
            private static readonly HarmonyLib.AccessTools.FieldRef<EngineComponent, float> StarterTorque =
                HarmonyLib.AccessTools.FieldRefAccess<EngineComponent, float>("_starterTorque");

            private static bool Prepare() => !EngineSimPresent;

            private static void Prefix(EngineComponent __instance, out float __state) => __state = __instance.outputAngularVelocity;

            private static void Postfix(EngineComponent __instance, float dt, float __state)
            {
                var vc = __instance.vehicleController;
                if (dt <= 0f || vc == null || !Loads.TryGetValue(vc, out var list) || list.Count == 0) return;
                var clutch = vc.powertrain.clutch;
                var gearbox = vc.powertrain.transmission;
                bool inGear = clutch != null && gearbox != null && ReferenceEquals(__instance.Output, clutch)
                              && gearbox.Gear != 0 && ReferenceEquals(clutch.Output, gearbox);
                GameTorque.TryGetValue(__instance, out float engineTorque);
                float engineInertia = Mathf.Max(1e-4f, __instance.inertia);
                float inertia = engineInertia, torque = engineTorque - (inGear ? clutch.outputTorque : 0f), slope = 0f, drag = 0f;
                float w0 = Mathf.Max(0f, __state);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var load = list[i];
                    if (load == null || (load is Object o && o == null)) { list.RemoveAt(i); continue; }
                    inertia += Mathf.Max(0f, load.Inertia);
                    float q = load.Torque(w0, dt, out float s);
                    torque -= q;
                    drag += Mathf.Max(0f, q);
                    slope += Mathf.Max(0f, s);
                }
                // a starter that turns the loads too
                if (__instance.starterActive && StarterTorque(__instance) > 0f)
                    torque += StarterTorque(__instance) * (inertia - engineInertia) / engineInertia + drag;
                float limit = __instance.revLimiterRPM * (Mathf.PI / 30f);
                float w = __state + dt * torque / (inertia + dt * slope);
                __instance.outputAngularVelocity = Mathf.Clamp(w, 0f, limit * 1.05f);
                RpmPercent(__instance) = Mathf.Clamp01(__instance.outputAngularVelocity / limit);
            }
        }
    }

    // Everything that names Engine Sim's types, so the CLR only needs Engine Sim when this is used (it is installed).
    internal static class EngineSimLink
    {
        private static readonly Dictionary<ICrankLoad, Bridge> Bridges = new Dictionary<ICrankLoad, Bridge>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Attach(VehicleController vc, ICrankLoad load)
        {
            if (!Bridges.TryGetValue(load, out var b)) Bridges[load] = b = new Bridge(load);
            EngineSim.Shaft.Attach(vc, b);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Detach(VehicleController vc, ICrankLoad load)
        {
            if (!Bridges.TryGetValue(load, out var b)) return;
            EngineSim.Shaft.Detach(vc, b);
            Bridges.Remove(load);
        }

        private sealed class Bridge : EngineSim.IShaftLoad
        {
            private readonly ICrankLoad _load;
            public Bridge(ICrankLoad load) { _load = load; }
            public float Inertia => _load.Inertia;
            public float Torque(float w, float dt, out float slope) => _load.Torque(w, dt, out slope);
        }
    }
}

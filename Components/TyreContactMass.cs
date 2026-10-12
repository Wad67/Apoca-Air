using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NWH.WheelController3D;
using UnityEngine;

namespace ApocAir
{
    // Gives NWH's tyres the mass they actually push against. Each step an NWH tyre works out the grip that would stop
    // its contact patch sliding (and, below 0.12 m/s, pull it back to where it stopped) within that one step, as if
    // the patch carried its share of the whole vehicle's mass. But a sideways push at the ground also turns the body,
    // and the mass that resists it there is 1 / (1/m + (r x d) . I^-1 (r x d)) for the patch at r from the centre of
    // mass, pushed along d. For a car, heavy in roll for its height, that is most of its mass. For a light, tall,
    // narrow aircraft it is a quarter of it: every correction overshoots four times and the tyres flip their grip
    // from side to side each step, rocking the airframe. Using the mass that is really there keeps the step stable.
    // Add "ApocAir.TyreContactMass" to the vehicle's root; it applies to the wheels under it.
    public sealed class TyreContactMass : MonoBehaviour
    {
        private static readonly HashSet<WheelController> Wheels = new HashSet<WheelController>();

        private void OnEnable()
        {
            foreach (var w in GetComponentsInChildren<WheelController>(true)) Wheels.Add(w);
        }

        private void OnDisable()
        {
            foreach (var w in GetComponentsInChildren<WheelController>(true)) Wheels.Remove(w);
        }

        // the smaller of the masses resisting the patch sideways and along the wheel
        public static float Mass(Rigidbody rb, WheelController w)
        {
            if (rb == null) return 1f;
            if (Wheels.Count == 0 || !Wheels.Contains(w)) return rb.mass;
            Vector3 r = w.HitPoint + w.transform.up * (w.forceApplicationPointDistance * w.spring.maxLength) - rb.worldCenterOfMass;
            return Mathf.Min(Along(rb, r, w.transform.right), Along(rb, r, w.transform.forward));
        }

        private static float Along(Rigidbody rb, Vector3 r, Vector3 d)
        {
            Quaternion q = rb.rotation * rb.inertiaTensorRotation;
            Vector3 c = Quaternion.Inverse(q) * Vector3.Cross(r, d);   // in the principal axes
            Vector3 i = rb.inertiaTensor;
            float k = 1f / rb.mass + c.x * c.x / Mathf.Max(1e-3f, i.x) + c.y * c.y / Mathf.Max(1e-3f, i.y) + c.z * c.z / Mathf.Max(1e-3f, i.z);
            return 1f / k;
        }

        [HarmonyPatch(typeof(WheelController), "UpdateFriction")]
        private static class Patch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
            {
                var getMass = AccessTools.PropertyGetter(typeof(Rigidbody), nameof(Rigidbody.mass));
                var with = AccessTools.Method(typeof(TyreContactMass), nameof(Mass));
                int n = 0;
                foreach (var c in code)
                {
                    if (c.Calls(getMass))
                    {
                        n++;
                        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0);
                        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, with);
                    }
                    else yield return c;
                }
                if (n != 1) Plugin.Log.LogWarning($"WheelController.UpdateFriction: expected one read of the vehicle's mass, found {n}; tyre contact mass not applied");
            }
        }
    }
}

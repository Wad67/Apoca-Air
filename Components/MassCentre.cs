using UnityEngine;

namespace ApocAir
{
    // Puts the aircraft's centre of mass where its mass is. Unity places it at the middle of the colliders, which
    // for an open frame is the keel; an aircraft's balance (where the rotor's or wing's force acts relative to it)
    // decides whether it flies. Add "ApocAir.MassCentre" to any object at the centre of mass.
    // Unity also works the rotational inertia out from the colliders as if the mass were spread through them; an open
    // frame's few thin colliders give far too little (a 300 kg gyro on one keel box: 6 kg m2 in roll, where the pilot,
    // engine and tanks out to the sides make it about ten times that), and a body that light in roll shakes itself
    // apart on its springs. "inertia" gives it instead: kg m2 about the aircraft's x (pitch), y (yaw) and z (roll);
    // 0 keeps Unity's.
    public sealed class MassCentre : MonoBehaviour
    {
        public Vector3 inertia;

        private void Start() => Apply();

        private void OnEnable() => Apply();

        private void Apply()
        {
            var rb = GetComponentInParent<Rigidbody>();
            if (rb == null) return;
            rb.centerOfMass = rb.transform.InverseTransformPoint(transform.position);
            if (inertia.x > 0f && inertia.y > 0f && inertia.z > 0f)
            {
                rb.inertiaTensor = inertia;
                rb.inertiaTensorRotation = Quaternion.identity;
            }
        }
    }
}

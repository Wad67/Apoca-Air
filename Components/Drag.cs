using UnityEngine;

namespace ApocAir
{
    // Drag of everything that doesn't lift: the frame, the pilot, the engine, the wheels. Given as the drag area
    // (drag coefficient times area, m2) seen along each of the object's axes, acting at the object's position.
    // From a template's .json (add "ApocAir.Drag" to an object at the body's centre of area):
    //   dragArea            [side (x), vertical (y), frontal (z)] m2
    public sealed class Drag : MonoBehaviour
    {
        public Vector3 dragArea = new Vector3(1.2f, 1.5f, 0.7f);

        public Vector3 LastForce { get; private set; }

        private Rigidbody _rb;

        private void OnEnable() => _rb = GetComponentInParent<Rigidbody>();

        private void FixedUpdate()
        {
            if (_rb == null) return;
            Vector3 p = transform.position;
            Vector3 v = transform.InverseTransformDirection(_rb.GetPointVelocity(p));
            float speed = v.magnitude;
            if (speed < 0.1f) { LastForce = Vector3.zero; return; }
            float half = 0.5f * ApocAir.Air.Density(p) * speed;
            var local = new Vector3(-half * dragArea.x * v.x, -half * dragArea.y * v.y, -half * dragArea.z * v.z);
            LastForce = transform.TransformDirection(local);
            _rb.AddForceAtPosition(LastForce, p);
        }
    }
}

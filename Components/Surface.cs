using ApocAir.Aero;
using UnityEngine;

namespace ApocAir
{
    // A lifting surface: a wing, a tailplane, a fin. It meets the air at the velocity of its own point of the
    // aircraft (so a turning aircraft's tail sees a sideways flow and damps the turn) and makes lift and drag at
    // that angle, all the way round (Airfoil), with the lift slope a finite span gets and its induced drag.
    // A control surface (rudder, elevator, aileron) on its trailing edge shifts the angle by its effectiveness
    // times its deflection.
    //
    // From a template's .json (add "ApocAir.Surface" to an object at the surface's centre of pressure, turned so its
    // forward is the chord, leading edge first):
    //   area                m2
    //   aspectRatio         span^2 / area
    //   vertical            true for a fin (lift along the object's right), false for a wing (along its up)
    //   camber              zero-lift angle, deg (negative for a cambered wing; 0 for a symmetric tail)
    //   stall               deg
    //   control             -1..1, the control's position
    //   controlAngle        deg at full control; positive pushes the surface's lift side (up, or right for a fin)
    //   controlEffect       angle change per angle of deflection: about 0.5 for a control of 30-40 % chord
    //   propwash, span      a propeller whose slipstream the surface may sit in, and the surface's span (m, along
    //                       its up for a fin, its right for a wing): the part of the span inside the slipstream meets
    //                       air that much faster, which is where a pusher's rudder gets its bite at low speed
    public sealed class Surface : MonoBehaviour
    {
        public float area = 0.3f, aspectRatio = 1.5f;
        public bool vertical;
        public float camber, stall = 18f;
        public float control, controlAngle = 25f, controlEffect = 0.5f;
        public float cd0 = 0.012f;
        public Propeller propwash;
        public float span = 0.5f;

        public Vector3 LastForce { get; private set; }

        private readonly Airfoil _foil = new Airfoil();
        private Rigidbody _rb;
        private const float Oswald = 0.8f;

        private void OnEnable() => _rb = GetComponentInParent<Rigidbody>();

        private void FixedUpdate()
        {
            if (_rb == null) return;
            Vector3 p = transform.position, f = transform.forward, n = vertical ? transform.right : transform.up;
            Vector3 w = -_rb.GetPointVelocity(p);
            if (propwash != null && propwash.SlipstreamSpeed > 0f)
            {
                // share of the span inside the slipstream's cylinder behind the prop
                Vector3 axis = propwash.transform.forward, o = propwash.transform.position, along = vertical ? transform.up : transform.right;
                float r = propwash.SlipstreamRadius;
                int inside = 0;
                const int samples = 9;
                for (int i = 0; i < samples; i++)
                {
                    Vector3 q = p + along * (span * ((i + 0.5f) / samples - 0.5f)) - o;
                    float back = -Vector3.Dot(q, axis);
                    if (back > 0f && (q + axis * back).sqrMagnitude < r * r) inside++;
                }
                w -= axis * (propwash.SlipstreamSpeed * inside / samples);
            }
            // the flow in the surface's section plane (spanwise flow makes no lift)
            float wf = Vector3.Dot(w, f), wn = Vector3.Dot(w, n), q2 = wf * wf + wn * wn;
            if (q2 < 0.01f) { LastForce = Vector3.zero; return; }
            float alpha = Mathf.Atan2(wn, -wf) + Mathf.Clamp(control, -1f, 1f) * controlAngle * controlEffect * Mathf.Deg2Rad;

            // finite span: lift slope a0 / (1 + a0 / (pi e AR))
            const float a0 = 2f * Mathf.PI * 0.95f;
            float ar = Mathf.Max(0.1f, aspectRatio);
            _foil.LiftSlope = a0 / (1f + a0 / (Mathf.PI * Oswald * ar));
            _foil.ZeroLiftAngle = camber * Airfoil.Deg;
            _foil.StallAngle = stall * Airfoil.Deg;
            _foil.Cd0 = cd0;
            _foil.At(alpha, out double cl, out double cdp);
            float cd = (float)cdp + (float)(cl * cl) / (Mathf.PI * Oswald * ar);

            float rho = ApocAir.Air.Density(p);
            float qs = 0.5f * rho * q2 * area;
            float speed = Mathf.Sqrt(q2);
            // drag along the flow, lift across it towards the surface's lift side when alpha > 0
            float df = wf / speed, dn = wn / speed;
            float ff = qs * ((float)cl * dn + cd * df);
            float fn = qs * (-(float)cl * df + cd * dn);
            LastForce = f * ff + n * fn;
            _rb.AddForceAtPosition(LastForce, p);
        }
    }
}

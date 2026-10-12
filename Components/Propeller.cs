using ApocAir.Aero;
using NWH.VehiclePhysics2;
using UnityEngine;

namespace ApocAir
{
    // A fixed-pitch propeller on the engine's crankshaft. Thrust is along this object's forward (+Z), at its
    // position. The engine turns it through its crankshaft (Crank): every engine step it takes the torque the
    // air puts on it at that speed and airspeed (PropellerModel), so the engine settles where the prop's torque
    // matches its own. Unpowered in flight it windmills and turns the engine over. With no engine fitted it turns
    // on its own bearing, on the air's torque alone.
    //
    // From a template's .json (add "ApocAir.Propeller" to an object at the prop):
    //   radius, hubRadius   m
    //   blades, chord       chord at 3/4 radius, m; taper = tip chord / root chord
    //   pitch               geometric pitch, m (how far a turn would screw it forward)
    //   ratio               crank turns per prop turn (a reduction drive), 1 = on the crank
    //   inertia             the prop's own, kg m2
    //   sense               +1 turns clockwise seen from behind, -1 anticlockwise
    //   visual              the prop's mesh object, spun about the thrust axis
    public sealed class Propeller : MonoBehaviour, ICrankLoad
    {
        public float radius = 0.459f, hubRadius = 0.06f;
        public int blades = 2;
        public float chord = 0.09f, taper = 0.7f, pitch = 0.9f;
        public float ratio = 1f;
        public float inertia = 0.15f;
        public int sense = 1;
        public Transform visual;

        // read-outs
        public float Rpm => _omega * 30f / Mathf.PI;
        public float Thrust => _thrust;
        // what the last physics step put on the airframe, N
        public float AppliedThrust { get; private set; }
        public Vector3 LastReaction { get; private set; }
        // how much faster than the airspeed the prop's slipstream leaves (momentum theory: the far wake of a disc
        // putting thrust T into the air through area A), m/s, and the slipstream's radius just behind the prop
        public float SlipstreamSpeed { get; private set; }
        public float SlipstreamRadius => 0.85f * radius;

        private readonly PropellerModel _model = new PropellerModel();
        private Rigidbody _rb;
        private VehicleController _vc;
        private float _omega, _thrust, _torque, _axial, _rho = 1.225f;
        private float _lastCrankStep = -1f;
        private string _key;

        private void OnEnable()
        {
            _rb = GetComponentInParent<Rigidbody>();
            _vc = GetComponentInParent<VehicleController>();
            Crank.Attach(_vc, this);
        }

        private void OnDisable() => Crank.Detach(_vc, this);

        private void Configure()
        {
            string key = $"{radius}|{hubRadius}|{blades}|{chord}|{taper}|{pitch}";
            if (key == _key) return;
            _key = key;
            _model.Radius = radius; _model.HubRadius = hubRadius; _model.Blades = blades;
            _model.Chord = chord; _model.Taper = taper; _model.Pitch = pitch;
            _model.Tabulate();
        }

        public float Inertia => inertia / Mathf.Max(0.01f, ratio * ratio);

        // the engine's step: torque at the crank for crank speed w
        public float Torque(float w, float dt, out float slope)
        {
            Configure();
            _lastCrankStep = Time.fixedTime;
            float r = Mathf.Max(0.01f, ratio);
            _omega = w / r;
            Air(_omega, out _thrust, out _torque);
            // slope of the prop's torque with its speed, seen at the crank
            Air(_omega + 1f, out _, out float q1);
            slope = Mathf.Max(0f, (q1 - _torque)) / (r * r);
            return _torque / r;
        }

        private void Air(float omega, out float thrust, out float torque)
        {
            _model.Lookup(_axial, Mathf.Max(0f, omega), _rho, out double t, out double q);
            thrust = (float)t; torque = (float)q;
        }

        private void FixedUpdate()
        {
            if (_rb == null) return;
            Configure();
            Vector3 axis = transform.forward;
            _axial = Vector3.Dot(_rb.GetPointVelocity(transform.position), axis);
            _rho = ApocAir.Air.Density(transform.position);
            // no engine stepping it (none fitted): a free prop on its bearing
            if (Time.fixedTime - _lastCrankStep > 2.5f * Time.fixedDeltaTime)
            {
                Air(_omega, out _thrust, out _torque);
                _omega = Mathf.Max(0f, _omega - _torque / Mathf.Max(1e-3f, inertia) * Time.fixedDeltaTime);
                Air(_omega, out _thrust, out _torque);
            }
            _rb.AddForceAtPosition(axis * _thrust, transform.position);
            AppliedThrust = _thrust;
            float disc = Mathf.PI * radius * radius, v = Mathf.Max(0f, _axial);
            SlipstreamSpeed = _thrust > 0f ? Mathf.Sqrt(v * v + 2f * _thrust / (_rho * disc)) - v : 0f;
            // the engine's mount takes the reaction of the torque that turns the prop. The prop spins about
            // -sense * axis (Unity's rotation sense), so the airframe is twisted about +sense * axis
            LastReaction = axis * (sense * _torque);
            _rb.AddTorque(LastReaction);
        }

        private void Update()
        {
            if (visual != null && _omega > 0f)
                visual.RotateAround(visual.position, transform.forward, -sense * _omega * Mathf.Rad2Deg * Time.deltaTime);
        }
    }
}

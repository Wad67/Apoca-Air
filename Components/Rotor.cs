using ApocAir.Aero;
using UnityEngine;

namespace ApocAir
{
    // A gyroplane's rotor: free to turn on its bearing, driven only by the air (RotorModel: blade elements around
    // the disc, teetering blades). Air coming up through the disc keeps it turning; its thrust and drag act at the
    // hub, along the tip-path plane. The stick tilts the rotor head, and with it the disc, which is how a gyroplane
    // is steered: the force tilts with the disc and its line moves relative to the centre of mass.
    //
    // From a template's .json (add "ApocAir.Rotor" to any object):
    //   head                the rotor head object: tilted by the stick about its own pivot; the disc's frame is its
    //                       up (the shaft) and forward
    //   spin                the blades' object, turned about the shaft
    //   radius, rootCutout  m: tip, and where the aerofoil starts
    //   blades, chord       m
    //   pitch, twist        deg: blade pitch at 3/4 radius, tip less root
    //   sense               +1 turns clockwise seen from above, -1 anticlockwise
    //   bladeMass           kg each, for the rotor's inertia (a uniform blade: m R^2 / 3)
    //   bearingFriction     N m
    //   tiltForward, tiltBack, tiltSide   deg of head tilt at full stick
    //   stickPitch, stickRoll             the stick, -1..1 (forward, right positive); the head follows at stickRate
    //                                     (full travel per second), as fast as a hand moves it
    //
    // Starting it: a stopped two-blade rotor gets almost no turning torque from air across it, so it is started by
    // hand, as on small gyroplanes: shove a blade as it comes round, then taxi with the stick back and the
    // air coming up through the tilted disc winds it up. A hand can only push a blade moving slower than itself.
    // The disc holds its own orientation in space (each blade is a gyroscope, free on the teeter hinge) and follows
    // the shaft, when the airframe or the stick moves it, after the flapping's time constant 16 / (Lock number x rotor
    // speed), no further than the teeter stops. The force tilts with the disc, so an airframe rolling or pitching under
    // the rotor meets a torque against it: what makes a gyroplane hang steady under its rotor.
    // Stick: stickPitch / stickRoll from keys, or mousePitch / mouseRoll when useMouse is set.
    //
    // Stabiliser (stabilise): an autopilot servo on the stick, the way real ones work (it moves the controls, it
    // adds no forces). In the air the pilot's pitch input asks for a pitch rate (stabPitchRate deg/s at full) and
    // letting go holds the attitude; roll input asks for a bank angle (stabBank deg at full) and centred levels the
    // wings. It steers the head by attitude error and rate (stabPitchGain / stabPitchDamp, stabRollGain /
    // stabRollDamp: full stick per rad and per rad/s). On the ground pitch goes straight through and it only damps roll.
    // It trims out the steady roll (the prop's torque, the rotor's sideways tilt) by slowly learning the stick offset
    // that holds the wings level (stabRollTrim: stick per rad of bank error per second, no more than stabTrimLimit),
    // and works the rudders on top of the pedals: against sideslip (stabSlip, rudder per rad) and yaw rate
    // (stabYawDamp, rudder per rad/s).
    //   pedals              -1..1, the pilot's rudder pedals (right positive); rudders: the surfaces they move
    //
    //   stick, stickAngle   the cockpit stick, tilted about its own origin with the stick's position, deg at full
    //   handPush            set true to give the rotor one shove (the .json's input sets it; the rotor clears it)
    //   handImpulse         N s at the tip per shove
    //   handSpeed           m/s: the fastest tip a hand can still push
    public sealed class Rotor : MonoBehaviour
    {
        public Transform head, spin, stick;
        public float stickAngle = 15f;
        public float radius = 2.7f, rootCutout = 0.3f;
        public int blades = 2;
        public float chord = 0.2f, pitch = 2.5f, twist = 0f;
        public int sense = -1;
        public float bladeMass = 7f, bearingFriction = 0.5f;
        public float tiltForward = 12f, tiltBack = 12f, tiltSide = 8f;
        public float stickPitch, stickRoll, stickRate = 2f;
        public bool useMouse;
        public float mousePitch, mouseRoll;
        public bool stabilise = true;
        public float stabPitchRate = 20f, stabBank = 30f;
        public float stabPitchGain = 2f, stabPitchDamp = 1.2f, stabRollGain = 1.2f, stabRollDamp = 0.5f;
        public float stabRollTrim = 0.4f, stabTrimLimit = 0.6f, stabSlip = 2f, stabYawDamp = 0.6f;
        public float pedals;
        public Surface[] rudders = new Surface[0];
        public int radialStations = 10, azimuthSteps = 24;
        public bool handPush;
        public float handImpulse = 15f, handSpeed = 10f;

        // read-outs
        public float Rpm => _omega * 30f / Mathf.PI;
        public float RotorThrust { get; private set; }
        public Vector3 LastForce { get; private set; }
        public Vector3 LastPoint { get; private set; }
        public bool Airborne { get; private set; }
        public float RollTrim { get; private set; }

        private readonly RotorModel _model = new RotorModel();
        private Rigidbody _rb;
        private Quaternion _headRest, _stickRest;
        private float _omega, _pitch, _roll, _pitchHold, _rollTrim;
        private NWH.WheelController3D.WheelController[] _wheels;
        private Vector3 _disc;   // the disc's normal, world

        private void Awake()
        {
            if (head != null) _headRest = head.localRotation;
            if (stick != null) _stickRest = stick.localRotation;
        }

        private void OnEnable()
        {
            _rb = GetComponentInParent<Rigidbody>();
            _wheels = _rb != null ? _rb.GetComponentsInChildren<NWH.WheelController3D.WheelController>() : new NWH.WheelController3D.WheelController[0];
        }

        private float RotorInertia => Mathf.Max(0.1f, blades * bladeMass * radius * radius / 3f);

        private void Configure()
        {
            _model.Radius = radius; _model.RootCutout = rootCutout; _model.Blades = blades; _model.Chord = chord;
            _model.Pitch = pitch * Airfoil.Deg; _model.Twist = twist * Airfoil.Deg; _model.Sense = sense >= 0 ? 1 : -1;
            _model.RadialStations = Mathf.Max(2, radialStations); _model.AzimuthSteps = Mathf.Max(4, azimuthSteps);
        }

        private void FixedUpdate()
        {
            if (_rb == null || head == null) return;
            float dt = Time.fixedDeltaTime;
            Configure();

            // a shove by hand, if the tip is slow enough to catch
            if (handPush)
            {
                handPush = false;
                float tip = Mathf.Max(0.1f, radius), most = handSpeed / tip;
                if (_omega < most) _omega = Mathf.Min(most, _omega + handImpulse * tip / RotorInertia);
            }

            // the pilot's stick, and the stabiliser between it and the head
            float pilotPitch = Mathf.Clamp(useMouse ? mousePitch : stickPitch, -1f, 1f);
            float pilotRoll = Mathf.Clamp(useMouse ? mouseRoll : stickRoll, -1f, 1f);
            bool grounded = false;
            foreach (var wc in _wheels) if (wc != null && wc.IsGrounded) { grounded = true; break; }
            Airborne = !grounded;
            Transform body = _rb.transform;
            float noseUp = Mathf.Asin(Mathf.Clamp(body.forward.y, -1f, 1f));
            float bankRight = Mathf.Asin(Mathf.Clamp(-body.right.y, -1f, 1f));
            Vector3 w = body.InverseTransformDirection(_rb.angularVelocity);
            float noseUpRate = -w.x, rollRightRate = -w.z;
            float wantPitch = pilotPitch, wantRoll = pilotRoll, rate = stickRate;
            if (stabilise && !grounded)
            {
                _pitchHold = Mathf.Clamp(_pitchHold - pilotPitch * stabPitchRate * Mathf.Deg2Rad * dt, -25f * Mathf.Deg2Rad, 30f * Mathf.Deg2Rad);
                float bank = pilotRoll * stabBank * Mathf.Deg2Rad;
                wantPitch = Mathf.Clamp(-stabPitchGain * (_pitchHold - noseUp) + stabPitchDamp * noseUpRate, -1f, 1f);
                _rollTrim = Mathf.Clamp(_rollTrim + stabRollTrim * (bank - bankRight) * dt, -stabTrimLimit, stabTrimLimit);
                wantRoll = Mathf.Clamp(_rollTrim + stabRollGain * (bank - bankRight) - stabRollDamp * rollRightRate, -1f, 1f);
                rate = 6f;   // a servo moves the stick faster than a hand
            }
            else
            {
                _pitchHold = noseUp;
                // on the ground the stabiliser only damps roll (the ground sets the bank), as a pilot holds the disc
                // level on the take-off run
                if (stabilise) { wantRoll = Mathf.Clamp(pilotRoll - stabRollDamp * rollRightRate, -1f, 1f); rate = 6f; }
            }
            if (!stabilise) _rollTrim = 0f;

            // pedals to the rudders, with the stabiliser's yaw damper on top in the air
            float rudder = Mathf.Clamp(pedals, -1f, 1f);
            Vector3 vb = body.InverseTransformDirection(_rb.velocity);
            if (stabilise && !grounded && vb.z > 5f)
                rudder = Mathf.Clamp(rudder + stabSlip * Mathf.Atan2(vb.x, vb.z) - stabYawDamp * w.y, -1f, 1f);
            foreach (var s in rudders) if (s != null) s.control = rudder;
            RollTrim = _rollTrim;

            // the stick moves the head
            _pitch = Mathf.MoveTowards(_pitch, wantPitch, rate * dt);
            _roll = Mathf.MoveTowards(_roll, wantRoll, rate * dt);
            float tilt = _pitch >= 0f ? _pitch * tiltForward : _pitch * tiltBack;
            head.localRotation = Quaternion.AngleAxis(tilt, Vector3.right) * Quaternion.AngleAxis(-_roll * tiltSide, Vector3.forward) * _headRest;
            // the stick in the cockpit: forward is the top going forward, right the top going right
            if (stick != null)
                stick.localRotation = Quaternion.AngleAxis(_pitch * stickAngle, Vector3.right) * Quaternion.AngleAxis(-_roll * stickAngle, Vector3.forward) * _stickRest;

            // disc frame: x forward, y right, z up the shaft (right-handed in these numbers)
            Vector3 hub = head.position, fx = head.forward, fy = head.right, fz = head.up;
            Vector3 air = -_rb.GetPointVelocity(hub);
            var wind = new Vec3(Vector3.Dot(air, fx), Vector3.Dot(air, fy), Vector3.Dot(air, fz));
            var r = _model.Solve(wind, _omega, ApocAir.Air.Density(hub));
            Vector3 force = fx * (float)r.Force.X + fy * (float)r.Force.Y + fz * (float)r.Force.Z;

            // the disc follows the shaft with the flapping's lag; Lock number: air's lift on a blade over its inertia
            if (_disc == Vector3.zero) _disc = fz;
            float bladeInertia = Mathf.Max(0.01f, bladeMass * radius * radius / 3f);
            float lockNumber = ApocAir.Air.Density(hub) * 5.73f * chord * Mathf.Pow(radius, 4) / bladeInertia;
            float tau = _omega > 0.1f ? Mathf.Clamp(16f / (lockNumber * _omega), dt, 5f) : 5f;
            _disc = Vector3.Slerp(_disc, fz, Mathf.Clamp01(dt / tau)).normalized;
            float stops = (float)_model.MaxFlap * Mathf.Rad2Deg;
            if (Vector3.Angle(_disc, fz) > stops)
                _disc = Vector3.RotateTowards(fz, _disc, stops * Mathf.Deg2Rad, 0f).normalized;
            force = Quaternion.FromToRotation(fz, _disc) * force;
            _rb.AddForceAtPosition(force, hub);
            LastForce = force;
            LastPoint = hub;
            RotorThrust = (float)r.Thrust;

            // the rotor turns on the air's torque less its bearing; the bearing's drag is all the airframe feels
            float inertia = RotorInertia;
            float friction = _omega > 0f ? bearingFriction : 0f;
            _omega = Mathf.Max(0f, _omega + ((float)r.Torque - friction) / inertia * dt);
            _rb.AddTorque(fz * (_model.Sense * friction));
        }

        private void Update()
        {
            if (spin != null && head != null && _omega > 0f)
                spin.RotateAround(head.position, head.up, _model.Sense * _omega * Mathf.Rad2Deg * Time.deltaTime);
        }
    }
}

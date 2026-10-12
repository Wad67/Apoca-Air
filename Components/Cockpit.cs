using HutongGames.PlayMaker;
using NWH.VehiclePhysics2;
using UnityEngine;

namespace ApocAir
{
    // A plain on-screen panel while the player sits in the aircraft: the controls (as the template's .json maps them,
    // so the text lives next to the mapping) and the numbers a pilot reads off the panel. Temporary, until there are
    // gauges. Add "ApocAir.Cockpit" to any object of the aircraft; "controls" is the text, one line per control,
    // separated by "|".
    public sealed class Cockpit : MonoBehaviour
    {
        public string controls = "";

        private Rigidbody _rb;
        private VehicleController _vc;
        private Rotor _rotor;
        private Propeller _prop;
        private FsmGameObject _player;
        private GUIStyle _style;
        private float _nextLog;

        private void OnEnable()
        {
            _rb = GetComponentInParent<Rigidbody>();
            _vc = GetComponentInParent<VehicleController>();
            var root = _rb != null ? _rb.transform : transform.root;
            _rotor = root.GetComponentInChildren<Rotor>();
            _prop = root.GetComponentInChildren<Propeller>();
        }

        // the player sits under the vehicle (the Drive FSM parents them to its seat)
        private bool Seated()
        {
            if (_player == null) _player = FsmVariables.GlobalVariables.FindFsmGameObject("Player");
            var p = _player != null ? _player.Value : null;
            return p != null && _rb != null && p.transform.IsChildOf(_rb.transform);
        }

        // with VerboseLog, every 2 s: what the airframe is doing and what holds it
        private void FixedUpdate()
        {
            if (_rb == null || !Plugin.VerboseLogEntry.Value) return;
            // switched on while flying: start gathering contacts now
            if (_rb.GetComponent<ContactProbe>() == null) _rb.gameObject.AddComponent<ContactProbe>();
            LateFixedLog();
            if (Time.time < _nextLog) return;
            _nextLog = Time.time + 2f;
            var sb = new System.Text.StringBuilder();
            Vector3 v = _rb.transform.InverseTransformDirection(_rb.velocity);
            Vector3 wl = _rb.transform.InverseTransformDirection(_rb.angularVelocity);
            sb.Append($"[cockpit] {_rb.name} seated {Seated()}: w local (pitch x, yaw y, roll z) {wl.x:0.00},{wl.y:0.00},{wl.z:0.00} | v local {v.x:0.00},{v.y:0.00},{v.z:0.00} m/s, w {_rb.angularVelocity.magnitude:0.00} rad/s, mass {_rb.mass:0}, ")
              .Append($"com {_rb.centerOfMass}, inertia {_rb.inertiaTensor}, kinematic {_rb.isKinematic}, constraints {_rb.constraints}, sleeping {_rb.IsSleeping()}");
            if (_prop != null) sb.Append($" | prop {_prop.Rpm:0} rpm, thrust {_prop.Thrust:0} N, applied {_prop.AppliedThrust:0} N");
            if (_rotor != null) sb.Append($" | rotor {_rotor.Rpm:0} rpm, {_rotor.RotorThrust:0} N");
            foreach (var w in _rb.GetComponentsInChildren<NWH.WheelController3D.WheelController>())
                sb.Append($" | {w.name}: grounded {w.IsGrounded}, motor {w.MotorTorque:0}, brake {w.BrakeTorque:0}, slip {w.LongitudinalSlip:0.00}, spin {w.wheel.angularVelocity:0.0}");
            foreach (var s in _rb.GetComponentsInChildren<Surface>()) sb.Append($" | {s.name} {L(s.LastForce)} N");
            foreach (var d in _rb.GetComponentsInChildren<Drag>()) sb.Append($" | {d.name} {L(d.LastForce)} N");
            if (_rotor != null) sb.Append($" | rotor force {L(_rotor.LastForce)} N");
            var probe = _rb.GetComponent<ContactProbe>();
            if (probe != null) sb.Append(" | contacts: ").Append(probe.Take());
            // every collider that is part of the airframe's body
            sb.Append(" | solid colliders:");
            foreach (var c in _rb.GetComponentsInChildren<Collider>())
                if (c.enabled && !c.isTrigger && c.attachedRigidbody == _rb)
                    sb.Append($" {c.name}(layer {c.gameObject.layer}, bottom {c.bounds.min.y - _rb.position.y:0.00})");
            Plugin.Log.LogInfo(sb.ToString());
        }

        private string L(Vector3 world)
        {
            Vector3 v = _rb.transform.InverseTransformDirection(world);
            return $"({v.x:0},{v.y:0},{v.z:0})";
        }

        private float _nextRoll;
        private static readonly System.Reflection.FieldInfo SuspensionF = HarmonyLib.AccessTools.Field(typeof(NWH.WheelController3D.WheelController), "_suspensionForce");
        private static readonly System.Reflection.FieldInfo FrictionF = HarmonyLib.AccessTools.Field(typeof(NWH.WheelController3D.WheelController), "_frictionForce");

        // torque about the centre of mass in the airframe's axes: x pitch, y yaw, z roll
        private Vector3 M(Vector3 force, Vector3 point) =>
            _rb.transform.InverseTransformDirection(Vector3.Cross(point - _rb.worldCenterOfMass, force));

        private static string T(Vector3 m) => $"({m.x:0},{m.y:0},{m.z:0})";

        // with VerboseLog, while it rolls or pitches fast, 10 times a second: every source's torque about the centre of mass
        private void LateFixedLog()
        {
            Vector3 wl = _rb.transform.InverseTransformDirection(_rb.angularVelocity);
            if ((Mathf.Abs(wl.z) < 0.5f && Mathf.Abs(wl.x) < 0.5f) || Time.time < _nextRoll) return;
            _nextRoll = Time.time + 0.1f;
            var sb = new System.Text.StringBuilder();
            Vector3 v = _rb.transform.InverseTransformDirection(_rb.velocity);
            sb.Append($"[roll] t {Time.time:0.00} w (p,y,r) {wl.x:0.00},{wl.y:0.00},{wl.z:0.00} v {v.x:0.0},{v.y:0.0},{v.z:0.0} | torque (pitch,yaw,roll) N m:");
            if (_rotor != null) sb.Append($" rotor {T(M(_rotor.LastForce, _rotor.LastPoint))} at {_rotor.Rpm:0} rpm;");
            if (_prop != null) sb.Append($" prop {T(M(_prop.transform.forward * _prop.AppliedThrust, _prop.transform.position) + _rb.transform.InverseTransformDirection(_prop.LastReaction))};");
            foreach (var s in _rb.GetComponentsInChildren<Surface>()) sb.Append($" {s.name} {T(M(s.LastForce, s.transform.position))};");
            foreach (var d in _rb.GetComponentsInChildren<Drag>()) sb.Append($" {d.name} {T(M(d.LastForce, d.transform.position))};");
            foreach (var w in _rb.GetComponentsInChildren<NWH.WheelController3D.WheelController>())
            {
                var sus = SuspensionF != null ? (Vector3)SuspensionF.GetValue(w) : Vector3.zero;
                var fr = FrictionF != null ? (Vector3)FrictionF.GetValue(w) : Vector3.zero;
                Vector3 fp = w.HitPoint + w.transform.up * (w.forceApplicationPointDistance * w.spring.maxLength);
                sb.Append($" {w.name} susp {T(M(sus, w.transform.position))} tyre {T(M(fr, fp))} load {w.Load:0};");
            }
            var probe = _rb.GetComponent<ContactProbe>();
            if (probe != null) sb.Append($" contacts {T(_rb.transform.InverseTransformDirection(probe.LastTorque))} {probe.LastNames}");
            Plugin.Log.LogInfo(sb.ToString());
        }

        private void OnGUI()
        {
            if (_rb == null || !Seated()) return;
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = new Color(1f, 0.95f, 0.8f) } };

            float speed = _rb.velocity.magnitude;
            var sb = new System.Text.StringBuilder();
            sb.Append($"Airspeed {speed:0.0} m/s ({speed * 3.6f:0} km/h)   climb {_rb.velocity.y:+0.0;-0.0} m/s\n");
            sb.Append($"Height {Air.Height(_rb.position):0} m   air {Air.Temperature:0} C, {Air.Density(_rb.position):0.000} kg/m3\n");
            if (_vc != null)
                sb.Append($"Engine {_vc.powertrain.engine.OutputRPM:0} rpm   throttle {_vc.input.Throttle * 100f:0} %\n");
            if (_prop != null) sb.Append($"Prop {_prop.Rpm:0} rpm   thrust {_prop.Thrust:0} N\n");
            if (_rotor != null)
            {
                // how much of the aircraft's weight the rotor carries: lift is the rotor's force straight up
                float weight = _rb.mass * -Physics.gravity.y;
                float lift = Mathf.Max(0f, _rotor.LastForce.y);
                sb.Append($"Rotor {_rotor.Rpm:0} rpm   lift {lift / Mathf.Max(1f, weight) * 100f:0} % of weight\n");
                sb.Append($"Stick {(_rotor.useMouse ? "mouse" : "keys")}   stabiliser {(_rotor.stabilise ? (_rotor.Airborne ? "holding" : "on (ground)") : "off")}   roll trim {_rotor.RollTrim * 100f:+0;-0} %\n");
            }
            sb.Append('\n');
            foreach (var line in controls.Split('|')) sb.Append(line.Trim()).Append('\n');

            var text = new GUIContent(sb.ToString());
            float w = 380f, h = _style.CalcHeight(text, w);
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(10f, 10f, w + 16f, h + 12f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(18f, 16f, w, h), text, _style);
        }
    }

    // what the airframe's body touches, gathered between log lines (VerboseLog diagnostics)
    public sealed class ContactProbe : MonoBehaviour
    {
        private readonly System.Collections.Generic.Dictionary<string, float> _seen = new System.Collections.Generic.Dictionary<string, float>();

        // torque about the centre of mass from the last physics step's contacts, and what touched
        public Vector3 LastTorque => Time.fixedTime - _stamp <= Time.fixedDeltaTime * 1.5f ? _torque : Vector3.zero;
        public string LastNames => _names.ToString();
        private Vector3 _torque;
        private float _stamp = -1f;
        private readonly System.Text.StringBuilder _names = new System.Text.StringBuilder();
        private Rigidbody _rb;

        private void OnCollisionStay(Collision c)
        {
            if (c.contactCount == 0) return;
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (Time.fixedTime != _stamp) { _stamp = Time.fixedTime; _torque = Vector3.zero; _names.Length = 0; }
            Vector3 at = Vector3.zero;
            for (int i = 0; i < c.contactCount; i++) at += c.GetContact(i).point;
            at /= c.contactCount;
            // the impulse acts on this body; as a force over the step
            if (_rb != null) _torque += Vector3.Cross(at - _rb.worldCenterOfMass, c.impulse / Time.fixedDeltaTime);
            _names.Append(c.GetContact(0).thisCollider.name).Append('>').Append(c.collider.name).Append(' ');
            var cp = c.GetContact(0);
            string key = $"{cp.thisCollider.name}->{c.collider.name}(layer {c.collider.gameObject.layer})";
            _seen.TryGetValue(key, out float imp);
            _seen[key] = imp + c.impulse.magnitude;
        }

        public string Take()
        {
            if (_seen.Count == 0) return "none";
            var sb = new System.Text.StringBuilder();
            foreach (var kv in _seen) sb.Append(kv.Key).Append($" {kv.Value:0} N s; ");
            _seen.Clear();
            return sb.ToString();
        }
    }
}

using System;

namespace ApocAir.Aero
{
    // A free (unpowered in flight) rotor on a teetering or freely flapping hub: a gyroplane's lifting rotor.
    //
    // Blade elements around the disc (radial stations x azimuth steps), each meeting the air at the speed of its
    // spin plus the aircraft's flight across the disc, and at the angle set by the blade pitch plus the inflow: the
    // air coming up through the tilted-back disc, less what the rotor's own thrust pushes down (uniform inflow from
    // Glauert's momentum theory, good from hover to fast flight). Lift and drag from the section (Airfoil, all angles,
    // so the reverse-flow region near the retreating root is handled).
    //
    // Flapping: the blades are free to flap about the hub (a teeter hinge for two blades), so no flapping moment
    // reaches the hub. The first-harmonic flapping (the tip-path plane tilting back by a1 and sideways by b1) is
    // solved so the aerodynamic flap moment has no once-per-rev part, which is the condition at a centre hinge.
    // That flapping changes each element's angle of attack and tilts its force, so the hub force follows the tip-path
    // plane: what a real teetering rotor does, and why the stick tilts the rotor head rather than the blades.
    //
    // Torque about the shaft is aerodynamic only: positive speeds the rotor up. In autorotation the inner disc,
    // with air coming up through it at a large inflow angle, drives, the outer disc drags, and the rotor settles at
    // the speed where they balance. Nothing here makes it autorotate; it does or doesn't from the numbers.
    //
    // Rotor frame: z along the shaft, up through the disc; x, y in the plane. The rotor turns about +z (Sense +1,
    // anticlockwise from above) or -z.
    public sealed class RotorModel
    {
        public int Blades = 2;
        public double Radius = 4.2;              // m
        public double RootCutout = 0.6;          // m from the hub where the aerofoil starts
        public double Chord = 0.22;              // m
        public double Pitch = 2.5 * Airfoil.Deg; // rad at 3/4 radius
        public double Twist = 0;                 // rad, tip less root (linear)
        public int Sense = 1;
        public Airfoil Airfoil = new Airfoil { LiftSlope = 5.73, ZeroLiftAngle = -1.0 * Airfoil.Deg, StallAngle = 13 * Airfoil.Deg, Cd0 = 0.009, DragRise = 0.3 };
        public int RadialStations = 10;
        public int AzimuthSteps = 24;
        public double MaxFlap = 10 * Airfoil.Deg; // rad, where the hub's stops limit the teeter
        public double FlapFrom = 0;              // rad/s: below it the blades are held unflapped (0: free at any speed)

        public struct Result
        {
            public Vec3 Force;              // N on the hub, rotor frame
            public double Torque;           // N m about the shaft, in the turning sense: + speeds the rotor up
            public double Thrust;           // N along the shaft
            public double Inflow;           // m/s induced down through the disc
            public double A1, B1;           // rad, tip-path tilt back and sideways
        }

        // warm start: the last call's inflow and flapping
        private double _vi, _a1, _b1;

        public double DiscArea => Math.PI * Radius * Radius;

        // forces at the hub for the air's velocity relative to the hub (rotor frame, m/s: the aircraft's velocity
        // through the air, negated) and spin omega (rad/s), air density rho
        public Result Solve(Vec3 wind, double omega, double rho)
        {
            double vi = _vi, a1 = _a1, b1 = _b1;
            bool flapping = omega > FlapFrom;
            if (!flapping) { a1 = 0; b1 = 0; }
            double vip = Math.Sqrt(wind.X * wind.X + wind.Y * wind.Y);
            Elements(wind, omega, rho, vi, a1, b1, out var r0);
            for (int it = 0; it < 30; it++)
            {
                double ma = 0, mb = 0;
                if (flapping)
                {
                    // Newton on the once-per-rev flap moment (cos, sin parts) over the tilt (a1, b1)
                    const double d = 0.004;
                    Elements(wind, omega, rho, vi, a1 + d, b1, out var ra);
                    Elements(wind, omega, rho, vi, a1, b1 + d, out var rb);
                    double j11 = (ra.Mc - r0.Mc) / d, j12 = (rb.Mc - r0.Mc) / d;
                    double j21 = (ra.Ms - r0.Ms) / d, j22 = (rb.Ms - r0.Ms) / d;
                    double det = j11 * j22 - j12 * j21;
                    if (Math.Abs(det) > 1e-12)
                    {
                        ma = -(j22 * r0.Mc - j12 * r0.Ms) / det;
                        mb = -(-j21 * r0.Mc + j11 * r0.Ms) / det;
                    }
                    a1 = Clamp(a1 + ma, MaxFlap);
                    b1 = Clamp(b1 + mb, MaxFlap);
                }
                // uniform inflow: T = 2 rho A vi sqrt(Vip^2 + (Vz - vi)^2)
                double through = wind.Z - vi;
                double speed = Math.Sqrt(vip * vip + through * through);
                double viNew = speed > 0.5 ? r0.Thrust / (2 * rho * DiscArea * speed)
                                           : Math.Sign(r0.Thrust) * Math.Sqrt(Math.Abs(r0.Thrust) / (2 * rho * DiscArea));
                double dv = viNew - vi;
                vi += 0.5 * dv;
                Elements(wind, omega, rho, vi, a1, b1, out r0);
                if (Math.Abs(dv) < 1e-3 && Math.Abs(ma) < 1e-5 && Math.Abs(mb) < 1e-5) break;
            }
            _vi = vi; _a1 = a1; _b1 = b1;
            return new Result { Force = r0.Force, Torque = r0.Torque, Thrust = r0.Thrust, Inflow = vi, A1 = a1, B1 = b1 };
        }

        private struct Sum { public Vec3 Force; public double Torque, Thrust, Mc, Ms; }

        private void Elements(Vec3 wind, double omega, double rho, double vi, double a1, double b1, out Sum sum)
        {
            sum = default(Sum);
            double fx = 0, fy = 0, fz = 0;
            double dr = (Radius - RootCutout) / RadialStations;
            double up = wind.Z - vi;   // air up through the disc
            int n = AzimuthSteps;
            for (int k = 0; k < n; k++)
            {
                double psi = 2 * Math.PI * k / n;
                double cs = Math.Cos(psi), sn = Math.Sin(psi);
                // blade along er, moving along t
                double erx = cs, ery = sn;
                double tx = -Sense * sn, ty = Sense * cs;
                double beta = -a1 * cs - b1 * sn;
                double betaDot = Sense * omega * (a1 * sn - b1 * cs);
                double windT = wind.X * tx + wind.Y * ty;
                double windR = wind.X * erx + wind.Y * ery;
                double moment = 0;
                for (int i = 0; i < RadialStations; i++)
                {
                    double r = RootCutout + (i + 0.5) * dr;
                    double ut = omega * r - windT;                    // air meeting the leading edge
                    double un = up - beta * windR - r * betaDot;      // air up through the flapped blade
                    double w2 = ut * ut + un * un;
                    if (w2 < 1e-9) continue;
                    double phi = Math.Atan2(un, ut);
                    double theta = Pitch + Twist * (r / Radius - 0.75);
                    Airfoil.At(theta + phi, out double cl, out double cd);
                    double q = 0.5 * rho * w2 * Chord * dr;
                    double sphi = Math.Sin(phi), cphi = Math.Cos(phi);
                    double ft = q * (cl * sphi - cd * cphi);           // along the blade's motion
                    double fn = q * (cl * cphi + cd * sphi);           // normal to the flapped blade
                    // normal of the flapped blade: z - beta er
                    fx += ft * tx - fn * beta * erx;
                    fy += ft * ty - fn * beta * ery;
                    fz += fn;
                    sum.Torque += ft * r;
                    moment += fn * r;
                }
                sum.Mc += moment * cs;
                sum.Ms += moment * sn;
            }
            double scale = (double)Blades / n;
            sum.Force = new Vec3(fx * scale, fy * scale, fz * scale);
            sum.Torque *= scale;
            sum.Thrust = fz * scale;
            sum.Mc *= scale;
            sum.Ms *= scale;
        }

        private static double Clamp(double v, double m) => Math.Max(-m, Math.Min(m, v));
    }
}

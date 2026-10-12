using System;

namespace ApocAir.Aero
{
    // A fixed-pitch propeller by blade element momentum theory: the disc cut into annuli, each annulus's blade
    // sections giving lift and drag at the angle the air meets them, and the air's axial and swirl speed through the
    // annulus being what that thrust and torque put into it (momentum), with Prandtl's tip loss. Iterated to agree.
    //
    // With no Reynolds-number effects the result scales exactly with the air's speed over the blade: thrust and
    // torque at axial speed V and spin w depend only on the inflow angle chi = atan(V / (w R)) and grow with
    // V^2 + (w R)^2. So the model is solved once over chi (Tabulate) and looked up after, which is cheap enough for
    // the engine's every step and smooth from standing still (chi 0) to a stopped prop windmilling (chi 90 deg)
    // and blown backwards (chi < 0).
    public sealed class PropellerModel
    {
        public double Radius = 0.9;          // m
        public double HubRadius = 0.12;      // m, where the blades start
        public int Blades = 2;
        public double Chord = 0.13;          // m at 3/4 radius
        public double Taper = 0.6;           // tip chord / root chord, linear between
        public double Pitch = 1.07;          // m: geometric pitch, the distance a turn would screw it forward
        public Airfoil Airfoil = new Airfoil { LiftSlope = 5.7, ZeroLiftAngle = -3.5 * Airfoil.Deg, Cd0 = 0.012 };
        public int Stations = 16;

        private double[] _tChi, _ct, _cq;    // thrust and torque / (rho (V^2 + (wR)^2) R^2 [, R])
        private const int TableSize = 181;   // chi from -90 to 90 deg, 1 deg apart

        private double ChordAt(double r)
        {
            // linear from the hub to the tip, through Chord at 3/4 radius
            double x = (r - HubRadius) / (Radius - HubRadius), x75 = (0.75 * Radius - HubRadius) / (Radius - HubRadius);
            double root = Chord / (1 + (Taper - 1) * x75);
            return root * (1 + (Taper - 1) * x);
        }

        // thrust (N, along the axis the air leaves by) and torque it takes (N m) at axial speed v (m/s, the
        // aircraft's speed along the prop axis, forwards positive) and spin omega (rad/s, >= 0), air density rho
        public void Solve(double v, double omega, double rho, out double thrust, out double torque)
        {
            thrust = 0; torque = 0;
            double dr = (Radius - HubRadius) / Stations;
            for (int i = 0; i < Stations; i++)
            {
                double r = HubRadius + (i + 0.5) * dr;
                double c = ChordAt(r);
                double beta = Math.Atan2(Pitch, 2 * Math.PI * r);   // blade angle of a constant-pitch prop
                double vi = 0, ai = 0;                              // induced axial speed, swirl factor
                double dT = 0, dQ = 0;
                for (int it = 0; it < 60; it++)
                {
                    double ua = v + vi, ut = omega * r * (1 - ai);
                    double w2 = ua * ua + ut * ut;
                    if (w2 < 1e-9) break;
                    double phi = Math.Atan2(ua, ut);
                    Airfoil.At(beta - phi, out double cl, out double cd);
                    double q = 0.5 * rho * w2 * c * Blades * dr;
                    dT = q * (cl * Math.Cos(phi) - cd * Math.Sin(phi));
                    dQ = q * (cl * Math.Sin(phi) + cd * Math.Cos(phi)) * r;
                    // tip loss
                    double sphi = Math.Max(1e-3, Math.Abs(Math.Sin(phi)));
                    double f = 2 / Math.PI * Math.Acos(Math.Min(1, Math.Exp(-Blades * (Radius - r) / (2 * r * sphi))));
                    f = Math.Max(0.05, f);
                    // axial momentum: dT = 4 pi r rho F (v + vi) vi dr, solved for vi (the windmill-brake side capped)
                    double k = dT / (4 * Math.PI * r * rho * f * dr);
                    double disc = v * v / 4 + k;
                    // (v + vi) vi = k: the root that is 0 at no load; with none (a windmill slowing the air past
                    // half its speed, where momentum theory fails) held at the edge
                    double viNew = disc >= 0 ? -v / 2 + Math.Sqrt(disc) : -v / 2;
                    // swirl momentum: dQ = 4 pi r^3 rho F (v + vi) omega a' dr
                    double axial = Math.Max(0.5, Math.Abs(v + vi));
                    double aiNew = omega > 1e-3 ? dQ / (4 * Math.PI * r * r * r * rho * f * axial * omega * dr) : 0;
                    aiNew = Math.Max(-0.3, Math.Min(0.3, aiNew));
                    double dv = viNew - vi;
                    vi += 0.5 * dv;
                    ai += 0.5 * (aiNew - ai);
                    if (Math.Abs(dv) < 1e-5) break;
                }
                thrust += dT;
                torque += dQ;
            }
        }

        // solve once over the inflow angle; Thrust / Torque look it up
        public void Tabulate()
        {
            _tChi = new double[TableSize]; _ct = new double[TableSize]; _cq = new double[TableSize];
            const double wRef = 100, rho = 1.0;
            for (int i = 0; i < TableSize; i++)
            {
                double chi = (i - 90) * Airfoil.Deg;
                _tChi[i] = chi;
                double v = wRef * Math.Sin(chi), omega = wRef * Math.Cos(chi) / Radius;
                Solve(v, omega, rho, out double t, out double q);
                double norm = rho * wRef * wRef * Radius * Radius;
                _ct[i] = t / norm;
                _cq[i] = q / (norm * Radius);
            }
        }

        // thrust and torque from the table at axial speed v and spin omega (>= 0)
        public void Lookup(double v, double omega, double rho, out double thrust, out double torque)
        {
            if (_ct == null) Tabulate();
            double ut = Math.Max(0, omega) * Radius;
            double w2 = v * v + ut * ut;
            if (w2 < 1e-12) { thrust = 0; torque = 0; return; }
            double chi = Math.Atan2(v, ut);   // -90 .. 90 deg
            double x = chi / Airfoil.Deg + 90;
            int i = Math.Max(0, Math.Min(TableSize - 2, (int)Math.Floor(x)));
            double t = Math.Max(0, Math.Min(1, x - i));
            double norm = rho * w2 * Radius * Radius;
            thrust = norm * (_ct[i] + (_ct[i + 1] - _ct[i]) * t);
            torque = norm * Radius * (_cq[i] + (_cq[i + 1] - _cq[i]) * t);
        }
    }
}

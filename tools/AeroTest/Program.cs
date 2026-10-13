using System;
using ApocAir.Aero;

// Checks the aero core against real aircraft, before anything flies in the game.
//   dotnet run -c Release [-- prop | rotor | all]
static class Program
{
    const double G = 9.81;

    static int Main(string[] a)
    {
        string what = a.Length > 0 ? a[0] : "all";
        double rho = Atmosphere.Density(0, 15);
        Console.WriteLine($"air: {rho:0.000} kg/m3 at sea level, 15 C; {Atmosphere.Density(1500, 5):0.000} at 1500 m, 5 C");
        if (what == "all" || what == "airfoil") Airfoils();
        if (what == "all" || what == "prop") Props(rho);
        if (what == "all" || what == "rotor") Rotor(rho);
        if (what == "all" || what == "gyro") Gyro(rho);
        if (what == "time") Time(rho);
        if (what == "cases") Cases(rho);
        if (what == "spinup") SpinUp(rho);
        if (what == "liftoff") LiftOff(rho);
        if (what == "prop2")
            foreach (var (c, pitch) in new[] { (0.12, 1.0), (0.12, 0.9), (0.10, 0.9), (0.12, 1.1) })
            {
                var pr = new PropellerModel { Radius = 0.64, HubRadius = 0.08, Blades = 2, Chord = c, Taper = 0.7, Pitch = pitch };
                PropReport($"apocagyro pusher 1.28 m, chord {c}", pr, rho, new double[] { 2500, 3000, 3500, 4000 }, new double[] { 0, 25 });
                StaticRpm(pr, rho, 41);
                StaticRpm(pr, rho, 44);
            }
        return 0;
    }

    static void Airfoils()
    {
        var f = new Airfoil();
        Console.WriteLine("\nairfoil (defaults): alpha  cl  cd");
        foreach (double d in new[] { -180, -90, -20, -10, 0, 5, 10, 14, 16, 18, 20, 30, 45, 90, 135, 170 })
        {
            f.At(d * Airfoil.Deg, out double cl, out double cd);
            Console.WriteLine($"  {d,5}  {cl,6:0.000}  {cd,6:0.000}");
        }
    }

    static void PropReport(string name, PropellerModel p, double rho, double[] rpms, double[] speeds)
    {
        p.Tabulate();
        Console.WriteLine($"\n{name}: D {2 * p.Radius:0.00} m, {p.Blades} blades, pitch {p.Pitch:0.00} m");
        Console.WriteLine("   rpm   V m/s   thrust N   torque Nm   power kW   (table: thrust, torque)");
        foreach (double v in speeds)
            foreach (double rpm in rpms)
            {
                double w = rpm * Math.PI / 30;
                p.Solve(v, w, rho, out double t, out double q);
                p.Lookup(v, w, rho, out double tt, out double qt);
                Console.WriteLine($"  {rpm,5}  {v,5}  {t,9:0}  {q,9:0.0}  {q * w / 1000,8:0.0}   ({tt:0}, {qt:0.0})");
            }
        // windmilling: the prop stopped / unpowered at speed
        p.Lookup(40, 0, rho, out double t0, out double q0);
        Console.WriteLine($"  stopped at 40 m/s: thrust {t0:0} N, torque {q0:0.0} N m (negative turns it the way it runs)");
    }

    static void Props(double rho)
    {
        // a J-3 Cub style fixed-pitch prop: 72 x 42, Clark-Y-ish, on 65-75 kW. Static rpm about 2250-2350.
        var cub = new PropellerModel { Radius = 0.914, HubRadius = 0.12, Blades = 2, Chord = 0.13, Taper = 0.6, Pitch = 1.067 };
        PropReport("Cub-type prop", cub, rho, new double[] { 2000, 2300, 2600 }, new double[] { 0, 20, 40 });
        StaticRpm(cub, rho, 65);

        // a gyroplane pusher: 68 in, 3 blades, ground-adjusted coarse, behind a Rotax 914 (84 kW, 2.43:1, ~2400 prop
        // rpm). Static thrust of such set-ups is quoted at about 2.5 - 3 kN.
        var pusher = new PropellerModel { Radius = 0.864, HubRadius = 0.1, Blades = 3, Chord = 0.11, Taper = 0.6, Pitch = 1.05 };
        PropReport("gyro pusher prop", pusher, rho, new double[] { 2000, 2400 }, new double[] { 0, 30 });
        StaticRpm(pusher, rho, 84);
    }

    // where an engine of constant power settles on the prop standing still
    static void StaticRpm(PropellerModel p, double rho, double kw)
    {
        double lo = 300, hi = 9000;
        for (int i = 0; i < 60; i++)
        {
            double mid = (lo + hi) / 2, w = mid * Math.PI / 30;
            p.Lookup(0, w, rho, out _, out double q);
            if (q * w / 1000 > kw) hi = mid; else lo = mid;
        }
        double wf = lo * Math.PI / 30;
        p.Lookup(0, wf, rho, out double t, out _);
        Console.WriteLine($"  {kw} kW standing still: settles at {lo:0} rpm, {t:0} N ({t / G:0} kgf)");
    }

    // equilibrium rotor speed (aerodynamic torque 0) at airspeed v and disc angle (tilted back: air comes up through)
    static RotorModel.Result Autorotate(RotorModel r, double v, double discDeg, double rho, out double rpm)
    {
        double ang = discDeg * Airfoil.Deg;
        var wind = new Vec3(-v * Math.Cos(ang), 0, v * Math.Sin(ang));
        double lo = 3, hi = 120;
        RotorModel.Result res = default;
        for (int i = 0; i < 40; i++)
        {
            double mid = (lo + hi) / 2;
            res = r.Solve(wind, mid, rho);
            if (res.Torque > 0) lo = mid; else hi = mid;
        }
        rpm = lo * 30 / Math.PI;
        return r.Solve(wind, lo, rho);
    }

    // the in-game gyro (apocagyro.glb): 5.4 m two-blade rotor, 0.2 m chord, 0.92 m two-blade pusher on the crank,
    // about 300 kg flying with pilot and a small car engine (the 1.3 L v2: 41 kW)
    static void Gyro(double rho)
    {
        var prop = new PropellerModel { Radius = 0.459, HubRadius = 0.06, Blades = 2, Chord = 0.09, Taper = 0.7, Pitch = 0.9 };
        PropReport("apocagyro pusher", prop, rho, new double[] { 3000, 4000, 5000, 6000 }, new double[] { 0, 25 });
        StaticRpm(prop, rho, 41);
        StaticRpm(prop, rho, 66);
        var r = new RotorModel { Radius = 2.7, RootCutout = 0.3, Chord = 0.2 };
        Trim(r, 300 * G, rho, new double[] { 15, 20, 25, 30, 35, 40 });
    }

    // cost of one rotor solve per physics step, warm-started as in the game (the state changes a little each step)
    static void Time(double rho)
    {
        foreach (var (st, az) in new[] { (10, 24), (8, 16), (6, 12) })
        {
            var r = new RotorModel { Radius = 2.7, RootCutout = 0.3, Chord = 0.2, RadialStations = st, AzimuthSteps = az };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            RotorModel.Result res = default;
            int n = 2000;
            for (int i = 0; i < n; i++)
            {
                double v = 25 + 2 * Math.Sin(i * 0.01), ang = (8 + Math.Sin(i * 0.02)) * Airfoil.Deg;
                res = r.Solve(new Vec3(-v * Math.Cos(ang), 0.5 * Math.Sin(i * 0.03), v * Math.Sin(ang)), 63, rho);
            }
            Console.WriteLine($"  {st} x {az}: {sw.Elapsed.TotalMilliseconds / n:0.000} ms per solve; thrust {res.Thrust:0} N, torque {res.Torque:0.0} N m");
        }
    }

    // spin-up on the ground: the rotor's air torque at low rotor speed for taxi speed and disc angle (tilted back)
    static void SpinUp(double rho)
    {
        foreach (var (flapFrom, maxFlap) in new[] { (3.0, 0.35), (0.0, 0.35), (0.0, 0.17), (3.0, 0.17) })
            SpinUp(rho, flapFrom, maxFlap);
    }

    static void SpinUp(double rho, double flapFrom, double maxFlap)
    {
        Console.WriteLine($"\n[flapping from {flapFrom} rad/s, stops {maxFlap / Airfoil.Deg:0} deg]");
        Console.WriteLine("\nrotor torque (N m, + speeds it up) on the ground: rpm across, taxi speed / disc back down");
        double[] rpms = { 0, 10, 20, 40, 60, 100, 150, 200, 300, 400 };
        Console.Write("              "); foreach (var n in rpms) Console.Write($"{n,8}"); Console.WriteLine();
        foreach (double v in new double[] { 5, 10 })
            foreach (double disc in new double[] { 10, 15, 20 })
            {
                Console.Write($"  {v,3} m/s {disc,3}deg");
                foreach (var n in rpms)
                {
                    var r = new RotorModel { Radius = 2.7, RootCutout = 0.3, Chord = 0.2, FlapFrom = flapFrom, MaxFlap = maxFlap };
                    double ang = disc * Airfoil.Deg;
                    var res = r.Solve(new Vec3(-v * Math.Cos(ang), 0, v * Math.Sin(ang)), n * Math.PI / 30, rho);
                    Console.Write($"{res.Torque,8:0.0}");
                }
                Console.WriteLine();
            }
    }

    // the in-game gyro's rotor at take-off speeds: lift (N, weight 2943) and torque (N m) for rotor rpm, disc back
    static void LiftOff(double rho)
    {
        double[] rpms = { 100, 150, 200, 250, 300, 400, 500, 600 };
        foreach (double v in new double[] { 15, 20, 28 })
        {
            Console.WriteLine($"\n{v} m/s: lift / torque by rotor rpm");
            Console.Write("        "); foreach (var n in rpms) Console.Write($"{n,13}"); Console.WriteLine();
            foreach (double disc in new double[] { 5, 10, 15 })
            {
                Console.Write($"  {disc,3}deg");
                foreach (var n in rpms)
                {
                    var r = new RotorModel { Radius = 2.7, RootCutout = 0.3, Chord = 0.2 };
                    double a = disc * Airfoil.Deg;
                    var res = r.Solve(new Vec3(-v * Math.Cos(a), 0, v * Math.Sin(a)), n * Math.PI / 30, rho);
                    double lift = res.Force.Z * Math.Cos(a) + res.Force.X * Math.Sin(a);
                    Console.Write($"{lift,8:0}/{res.Torque,4:0}");
                }
                Console.WriteLine();
            }
        }
    }

    // iterations and cost per solve in the states a gyro spends its time in, warm-started step after step
    static void Cases(double rho)
    {
        var cases = new (string, double, double, double)[] {
            ("parked, stopped rotor", 0, 0, 0), ("parked, rotor 120 rpm", 0, 0, 120), ("taxi 5 m/s, 40 rpm", 5, 10, 40),
            ("taxi 10 m/s, 200 rpm", 10, 15, 200), ("cruise 30 m/s, 600 rpm", 30, 8, 600) };
        foreach (var (st, az, cap) in new[] { (10, 24, 30), (8, 16, 4) })
        {
        Console.WriteLine($" {st} x {az}, at most {cap} iterations a step:");
        foreach (var (name, v, disc, rpm) in cases)
        {
            var r = new RotorModel { Radius = 2.7, RootCutout = 0.3, Chord = 0.2, RadialStations = st, AzimuthSteps = az, MaxIterations = cap };
            RotorModel.Result last = default;
            double a = disc * Airfoil.Deg; int total = 0, n = 500;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < n; i++)
            {
                double jitter = 0.02 * Math.Sin(i * 0.7);
                last = r.Solve(new Vec3(-v * Math.Cos(a) + jitter, jitter, v * Math.Sin(a) + jitter), rpm * Math.PI / 30, rho);
                total += r.LastIterations;
            }
            Console.WriteLine($"  {name,-24} {total / (double)n,5:0.0} iterations, {sw.Elapsed.TotalMilliseconds / n:0.000} ms per solve; thrust {last.Thrust:0} N, torque {last.Torque:0.0} N m, flap {last.A1 / Airfoil.Deg:0.0}/{last.B1 / Airfoil.Deg:0.0} deg");
        }
        }
    }

    static void Trim(RotorModel r, double weight, double rho, double[] speeds)
    {
        Console.WriteLine($"\nrotor D {2 * r.Radius} m, chord {r.Chord} m, pitch {r.Pitch / Airfoil.Deg:0.0} deg, weight {weight:0} N; trim (lift = weight):");
        foreach (double v in speeds)
        {
            double lo = 0, hi = 30, rpm = 0;
            RotorModel.Result res = default;
            for (int i = 0; i < 30; i++)
            {
                double mid = (lo + hi) / 2;
                res = Autorotate(r, v, mid, rho, out rpm);
                double ang = mid * Airfoil.Deg;
                double lift = res.Force.Z * Math.Cos(ang) + res.Force.X * Math.Sin(ang);
                if (lift > weight) hi = mid; else lo = mid;
            }
            double a = lo * Airfoil.Deg;
            double drag = -(res.Force.X * Math.Cos(a) - res.Force.Z * Math.Sin(a));
            Console.WriteLine($"    {v} m/s ({v * 3.6:0} km/h): disc {lo:0.0} deg, rotor {rpm:0} rpm, rotor drag {drag:0} N, flap back {res.A1 / Airfoil.Deg:0.0} deg");
        }
    }

    static void Rotor(double rho)
    {
        // Magni M16 class: 8.4 m two-blade teetering rotor, 0.22 m chord, ~450 kg all up; rotor ~330 - 380 rpm in
        // cruise.
        var r = new RotorModel();
        double weight = 450 * G;
        Console.WriteLine($"\ngyro rotor: D {2 * r.Radius} m, {r.Blades} blades, chord {r.Chord} m, pitch {r.Pitch / Airfoil.Deg:0.0} deg; weight {weight:0} N");
        Console.WriteLine("  V m/s  disc deg   rotor rpm   thrust N   H N (drag)   flap back deg  side deg   inflow m/s");
        foreach (double v in new double[] { 15, 25, 35, 45 })
            foreach (double disc in new double[] { 4, 8, 12, 16 })
            {
                var res = Autorotate(r, v, disc, rho, out double rpm);
                // drag on the aircraft: the hub force against the flight path (flight is along +x, -wind)
                double ang = disc * Airfoil.Deg;
                double lift = res.Force.Z * Math.Cos(ang) + res.Force.X * Math.Sin(ang);
                double drag = -(res.Force.X * Math.Cos(ang) - res.Force.Z * Math.Sin(ang));
                Console.WriteLine($"  {v,5}  {disc,6}   {rpm,8:0}   {res.Thrust,8:0}   {drag,8:0}      {res.A1 / Airfoil.Deg,6:0.0}     {res.B1 / Airfoil.Deg,6:0.0}    {res.Inflow,6:0.00}   lift {lift:0}, L/D {lift / Math.Max(1, drag):0.0}");
            }
        // the disc angle that carries the weight at each speed
        Console.WriteLine("  trim (lift = weight):");
        foreach (double v in new double[] { 15, 20, 25, 30, 35, 40, 45 })
        {
            double lo = 0, hi = 30, rpm = 0;
            RotorModel.Result res = default;
            for (int i = 0; i < 30; i++)
            {
                double mid = (lo + hi) / 2;
                res = Autorotate(r, v, mid, rho, out rpm);
                double ang = mid * Airfoil.Deg;
                double lift = res.Force.Z * Math.Cos(ang) + res.Force.X * Math.Sin(ang);
                if (lift > weight) hi = mid; else lo = mid;
            }
            double a = lo * Airfoil.Deg;
            double drag = -(res.Force.X * Math.Cos(a) - res.Force.Z * Math.Sin(a));
            Console.WriteLine($"    {v} m/s ({v * 3.6:0} km/h): disc {lo:0.0} deg, rotor {rpm:0} rpm, rotor drag {drag:0} N, power to fly it {drag * v / 1000:0.0} kW");
        }
    }
}

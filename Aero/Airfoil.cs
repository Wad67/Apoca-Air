using System;

namespace ApocAir.Aero
{
    // A wing section's lift and drag coefficients at any angle of attack, all the way round (a rotor's retreating
    // blade sees the air from behind). Attached flow: lift rises with the lift slope from the zero-lift angle, profile
    // drag grows with angle. Past the stall the flow separates and the section behaves as a flat plate (normal force
    // ~ CdMax sin a), which is also what it does at any large angle. The two are blended over a few degrees, so lift
    // falls away past the stall instead of switching off.
    public sealed class Airfoil
    {
        public double LiftSlope = 5.7;               // per radian: 2 pi less a little for a real section
        public double ZeroLiftAngle = 0;             // rad; negative for a cambered section
        public double StallAngle = 14 * Deg;         // rad above the zero-lift angle where the flow separates
        public double StallWidth = 4 * Deg;          // rad over which it goes from attached to separated
        public double Cd0 = 0.010;                   // profile drag at zero lift
        public double DragRise = 0.4;                // profile drag growth, per rad^2 of angle from zero lift
        public double CdMax = 1.98;                  // flat plate broadside

        public const double Deg = Math.PI / 180;

        // lift and drag coefficients at angle of attack a (rad, any value)
        public void At(double alpha, out double cl, out double cd)
        {
            double a = Wrap(alpha);
            double rel = Wrap(a - ZeroLiftAngle);

            // separated: a flat plate (normal force CdMax sin a, resolved into lift and drag)
            double clPlate = CdMax * Math.Sin(a) * Math.Cos(a);
            double cdPlate = Cd0 + CdMax * Math.Sin(a) * Math.Sin(a);

            double ar = Math.Abs(rel);
            double w;   // 0 attached .. 1 separated
            if (ar <= StallAngle) w = 0;
            else if (ar >= StallAngle + StallWidth) w = 1;
            else { double t = (ar - StallAngle) / StallWidth; w = t * t * (3 - 2 * t); }
            if (w >= 1) { cl = clPlate; cd = cdPlate; return; }

            double clAtt = LiftSlope * rel;
            double cdAtt = Cd0 + DragRise * rel * rel;
            cl = clAtt + (clPlate - clAtt) * w;
            cd = cdAtt + (cdPlate - cdAtt) * w;
        }

        public static double Wrap(double a)
        {
            if (a > -Math.PI && a <= Math.PI) return a;   // the usual case, no division
            a %= 2 * Math.PI;
            if (a > Math.PI) a -= 2 * Math.PI;
            else if (a <= -Math.PI) a += 2 * Math.PI;
            return a;
        }
    }
}

using System;

namespace ApocAir.Aero
{
    // Air density from height and the air's own temperature. Pressure follows the standard atmosphere's
    // troposphere (it falls about 1.2 % per 100 m); the temperature is what the game says it is (GlobalTemperature)
    // rather than the standard's, so a hot day thins the air.
    public static class Atmosphere
    {
        public const double GasConstant = 287.05;       // J/(kg K), dry air
        public const double SeaLevelPressure = 101325;  // Pa

        public static double Pressure(double height)
        {
            double h = Math.Max(-500, Math.Min(height, 11000));
            return SeaLevelPressure * Math.Pow(1 - 2.25577e-5 * h, 5.25588);
        }

        // kg/m3 at a height above sea level (m) and air temperature (degrees C)
        public static double Density(double height, double temperatureC) =>
            Pressure(height) / (GasConstant * (Math.Max(-60, temperatureC) + 273.15));
    }
}

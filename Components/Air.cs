using ApocAir.Aero;
using HutongGames.PlayMaker;
using UnityEngine;

namespace ApocAir
{
    // The air where an aircraft is: density from its height and the game's air temperature. The air itself is
    // still (the game has no wind), so the air's speed over anything is the aircraft's own velocity there, negated.
    public static class Air
    {
        private static FsmFloat _temperature;

        // degrees C: the game's GlobalTemperature, 15 until the game has one
        public static float Temperature
        {
            get
            {
                if (_temperature == null) _temperature = FsmVariables.GlobalVariables.FindFsmFloat("GlobalTemperature");
                return _temperature != null ? _temperature.Value : 15f;
            }
        }

        // height above the world's origin: NWH's floating origin moves the whole world (height too) while a car is
        // driven, and keeps how far
        public static float Height(Vector3 position)
        {
            var fo = NWH.Common.FloatingOrigin.FloatingOrigin.Instance;
            return position.y + (fo != null ? fo.TotalOffset.y : 0f);
        }

        public static float Density(Vector3 position) => (float)Atmosphere.Density(Height(position), Temperature);
    }
}

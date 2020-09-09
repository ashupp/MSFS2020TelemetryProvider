using System.Runtime.InteropServices;
using Math = System.Math;

namespace SimFeedback.telemetry
{
    # region Simconnect Enums ans Structs

    enum DEFINITIONS
    {
        FlightStatus
    }

    internal enum DATA_REQUESTS
    {
        FLIGHT_STATUS,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    struct FlightStatusStruct
    {
        public float xAccel;
        public float yAccel;
        public float zAccel;
        public float xVelocity;
        public float yVelocity;
        public float zVelocity;
        public float Pitch;
        public float Bank;
        public float Yaw;
        public float MagneticHeading;
        public float GroundAltitude;
        public float GroundSpeed;
        public float IndicatedAirSpeed;
        public float AirSpeedTrue;
        public float VerticalSpeed;

        public float WindVelocity;
        public float WindDirection;

        public float RPM;

        public float AngleOfAttack;
        public float AngleOfSideslip;
    }


    #endregion

    public struct TelemetryData
    {
        #region For SimFeedback Available Values - Just for UDP Transport 
        public float Pitch { get; set; }
        public float Yaw { get; set; }
        public float Roll { get; set; }
        public float Heave { get; set; }
        public float Sway { get; set; }
        public float Surge { get; set; }
        public float Speed { get; set; }
        public float RollSpeed { get; set; }
        public float YawSpeed { get; set; }
        public float PitchSpeed { get; set; }
        public float AirSpeedTrue { get; set; }
        public float RPM { get; set; }
        public float AngleOfAttack { get; set; }
        public float AngleOfAttackSmoothed { get; set; }
        public float AngleOfSideslip { get; set; }
        #endregion
    }
}
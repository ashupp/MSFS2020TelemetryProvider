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
        //public float SimTime;
        public int SimRate;

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
        #region Private members
        private float _pitch;
        private float _roll;
        private float _yaw;
        private float _rpm;
        private float _angleOfAttack;
        private float _angleOfSideslip;
        #endregion

        #region For SimFeedback Available Values

        public float Pitch
        {
            get => ConvertRadiansToDegrees((float)Math.Sin(_pitch));
            set => _pitch = value;
        }

        public float Yaw
        {
            get => ConvertRadiansToDegrees(_yaw / 1000);
            set => _yaw = value;
        }

        public float Roll
        {
            get => ConvertRadiansToDegrees((float)(Math.Cos(_pitch) * Math.Sin(_roll)));
            set => _roll = value;
        }

        public float Heave { get; set; }
        public float Sway { get; set; }
        public float Surge { get; set; }
        public float Speed { get; set; }
        public float RollSpeed { get; set; }
        public float YawSpeed { get; set; }
        public float PitchSpeed { get; set; }
        public float AirSpeedTrue { get; set; }

        public float RPM
        {
            get => _rpm / 100 * 16;
            set => _rpm = value;
        }

        public float AngleOfAttack
        {
            get => (float)Math.Sin(_angleOfAttack / 180 * 3.14159265359) * Math.Min(1, AirSpeedTrue / 30);
            set => _angleOfAttack = value;
        }

        public float AngleOfSideslip
        {
            get => (float)Math.Sin(_angleOfSideslip / 180 * 3.14159265359) * Math.Min(1, AirSpeedTrue / 30);
            set => _angleOfSideslip = value;
        }

        #endregion

        #region Conversion calculations
        private static float ConvertRadiansToDegrees(float radians)
        {
            var degrees = (float)(180 / Math.PI) * radians;
            return degrees;
        }

        private static float ConvertAccel(float accel)
        {
            return (float) (accel / 9.80665);
        }

        private float LoopAngle(float angle, float minMag)
        {

            float absAngle = Math.Abs(angle);

            if (absAngle <= minMag)
            {
                return angle;
            }

            float direction = angle / absAngle;

            //(180.0f * 1) - 135 = 45
            //(180.0f *-1) - -135 = -45
            float loopedAngle = (180.0f * direction) - angle;

            return loopedAngle;
        }

        #endregion
    }
}
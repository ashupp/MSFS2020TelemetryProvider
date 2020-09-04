using System.Runtime.InteropServices;
using Math = System.Math;

namespace SimFeedback.telemetry
{

    enum DEFINITIONS
    {
        AircraftData,
        FlightStatus
    }

    internal enum DATA_REQUESTS
    {
        NONE,
        SUBSCRIBE_GENERIC,
        AIRCRAFT_DATA,
        FLIGHT_STATUS,
        ENVIRONMENT_DATA,
        FLIGHT_PLAN
    }

    internal enum EVENTS
    {
        CONNECTED,
        MESSAGE_RECEIVED,
        POSITION_CHANGED
    }


    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    struct AircraftDataStruct2
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Type;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Model;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Title;
        public double EstimatedCruiseSpeed;
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
        public float VerticalSpeed;

        public float WindVelocity;
        public float WindDirection;

        public float RPM;

        public float AngleOfAttack;
        public float AngleOfSideslip;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct AircraftDataStruct
    {
        /*public float xAccel;

        public float yAccel;

        public float zAccel;

        public float xVelocity;

        public float yVelocity;

        public float zVelocity;*/

        public float pitch;

        public float roll;

        /*public float yaw;

        public float Speed;

        public float RPM;*/
    }

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
            get => LoopAngle(ConvertRadiansToDegrees((float)(Math.Cos(_roll) * Math.Sin(_pitch))), 90);
            set => _pitch = value;
        }

        public float Yaw
        {
            get => ConvertRadiansToDegrees(_yaw / 1000);
            set => _yaw = value;
        }

        public float Roll
        {
            get => LoopAngle(ConvertRadiansToDegrees((float) (Math.Cos(_pitch) * Math.Sin(_roll))), 90);
            set => _roll = value;
        }

        public float Heave { get; set; }
        public float Sway { get; set; }
        public float Surge { get; set; }
        public float Speed { get; set; }
        public float RollSpeed { get; set; }
        public float YawSpeed { get; set; }
        public float PitchSpeed { get; set; }

        public float RPM
        {
            get => _rpm / 100;
            set => _rpm = value;
        }

        public float AngleOfAttack
        {
            get => ConvertRadiansToDegrees(_angleOfAttack);
            set => _angleOfAttack = value;
        }

        public float AngleOfSideslip
        {
            get => ConvertRadiansToDegrees(_angleOfSideslip);
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
using System;
using System.Numerics;

namespace IntelOrca.Biohazard.BioRand
{
    /// <summary>
    /// Represents a Quaternion in Euler angles (degrees).
    /// </summary>
    /// <param name="yaw"></param>
    /// <param name="pitch"></param>
    /// <param name="roll"></param>
    public struct EulerAngles(float yaw, float pitch, float roll)
    {
        public float Yaw = yaw;
        public float Pitch = pitch;
        public float Roll = roll;

        public EulerAngles(Vector3 v) : this(v.X, v.Y, v.Z)
        {
        }

        public EulerAngles(Quaternion q) : this(0, 0, 0)
        {
#if NET
            var yaw = MathF.Atan2(2.0f * (q.Y * q.W + q.X * q.Z), 1.0f - 2.0f * (q.X * q.X + q.Y * q.Y));
            var pitch = MathF.Asin(2.0f * (q.X * q.W - q.Y * q.Z));
            var roll = MathF.Atan2(2.0f * (q.X * q.Y + q.Z * q.W), 1.0f - 2.0f * (q.X * q.X + q.Z * q.Z));
#else
            var yaw = (float)Math.Atan2(2.0f * (q.Y * q.W + q.X * q.Z), 1.0f - 2.0f * (q.X * q.X + q.Y * q.Y));
            var pitch = (float)Math.Asin(2.0f * (q.X * q.W - q.Y * q.Z));
            var roll = (float)Math.Atan2(2.0f * (q.X * q.Y + q.Z * q.W), 1.0f - 2.0f * (q.X * q.X + q.Z * q.Z));
#endif

            Yaw = RadToDeg(yaw);
            Pitch = RadToDeg(pitch);
            Roll = RadToDeg(roll);
        }

        public readonly Vector3 ToVector3() => new Vector3(Yaw, Pitch, Roll);

        public readonly Quaternion ToQuaternion()
        {
            return Quaternion.CreateFromYawPitchRoll(DegToRad(Yaw), DegToRad(Pitch), DegToRad(Roll));
        }

        public override readonly string ToString() => $"<{Yaw}, {Pitch}, {Roll}>";

        static float DegToRad(float degrees) => degrees * (float)(Math.PI / 180.0);
        static float RadToDeg(float radians) => radians * (float)(180 / Math.PI);
    }
}

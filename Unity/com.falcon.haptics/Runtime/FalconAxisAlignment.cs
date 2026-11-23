using System;
using System.Numerics;

namespace Falcon.Haptics
{
    // Observed device directions. Aligned +Z means toward the user, before Workspace's Z inversion.
    public readonly struct FalconAxisAlignment
    {
        public readonly Vector3 Right, Up, TowardsUser;
        public FalconAxisAlignment(Vector3 right, Vector3 up, Vector3 towardsUser)
        {
            if (!Unit(right) || !Unit(up) || !Unit(towardsUser) || Math.Abs(Vector3.Dot(right, up)) > .0001f ||
                Math.Abs(Vector3.Dot(right, towardsUser)) > .0001f || Math.Abs(Vector3.Dot(up, towardsUser)) > .0001f)
                throw new ArgumentException("Les trois directions doivent former une base orthonormale.");
            Right = right; Up = up; TowardsUser = towardsUser;
        }
        public static FalconAxisAlignment Identity => new FalconAxisAlignment(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        public Matrix4x4 Matrix => new Matrix4x4(
            Right.X, Up.X, TowardsUser.X, 0, Right.Y, Up.Y, TowardsUser.Y, 0,
            Right.Z, Up.Z, TowardsUser.Z, 0, 0, 0, 0, 1);
        public static bool TryFromGestures(Vector3 rightMotion, Vector3 upMotion, Vector3 towardsMotion,
            out FalconAxisAlignment result, out string error)
        {
            result = default;
            if (!Movement(rightMotion) || !Movement(upMotion) || !Movement(towardsMotion))
            { error = "Geste trop court ou invalide : déplacer doucement d’au moins 5 mm dans chaque direction."; return false; }
            var right = Vector3.Normalize(rightMotion); var measuredUp = Vector3.Normalize(upMotion);
            var towards = Vector3.Normalize(towardsMotion);
            if (Math.Abs(Vector3.Dot(right, measuredUp)) > .35f)
            { error = "Les gestes droite et haut sont trop proches. Recommencer avec deux directions distinctes."; return false; }
            var up = Vector3.Normalize(measuredUp - right * Vector3.Dot(right, measuredUp));
            var cross = Vector3.Normalize(Vector3.Cross(right, up));
            float depth = Vector3.Dot(cross, towards);
            if (Math.Abs(depth) < .85f)
            { error = "Le geste vers vous doit être distinct des gestes droite et haut. Recommencer ce réglage."; return false; }
            if (depth < 0) cross = -cross;
            result = new FalconAxisAlignment(right, up, cross);
            error = null; return true;
        }
        static bool Unit(Vector3 value) => FalconWorkspaceMap.IsFinite(value) && Math.Abs(value.LengthSquared() - 1) <= .0001f;
        static bool Movement(Vector3 value) => FalconWorkspaceMap.IsFinite(value) &&
            !float.IsInfinity(value.LengthSquared()) && value.LengthSquared() >= .005f * .005f;
    }
}

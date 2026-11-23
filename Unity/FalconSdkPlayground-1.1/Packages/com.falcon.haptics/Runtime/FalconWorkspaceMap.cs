using System;
using System.Numerics;

namespace Falcon.Haptics
{
    // An application-defined box, never a claim about the Falcon's reachable geometry.
    public readonly struct FalconPhysicalBounds
    {
        public readonly Vector3 MinimumMetres, MaximumMetres;
        public FalconPhysicalBounds(Vector3 minimumMetres, Vector3 maximumMetres)
        {
            if (!FalconWorkspaceMap.IsFinite(minimumMetres) || !FalconWorkspaceMap.IsFinite(maximumMetres) ||
                minimumMetres.X >= maximumMetres.X || minimumMetres.Y >= maximumMetres.Y ||
                minimumMetres.Z >= maximumMetres.Z)
                throw new ArgumentException("Les limites physiques doivent être finies et strictement ordonnées sur chaque axe.");
            MinimumMetres = minimumMetres; MaximumMetres = maximumMetres;
        }
        public bool Contains(Vector3 position) => FalconWorkspaceMap.IsFinite(position) &&
            position.X >= MinimumMetres.X && position.X <= MaximumMetres.X &&
            position.Y >= MinimumMetres.Y && position.Y <= MaximumMetres.Y &&
            position.Z >= MinimumMetres.Z && position.Z <= MaximumMetres.Z;
        public Vector3 Clamp(Vector3 position)
        {
            if (!FalconWorkspaceMap.IsFinite(position)) throw new ArgumentException("Position invalide.");
            return Vector3.Min(MaximumMetres, Vector3.Max(MinimumMetres, position));
        }
    }

    // System.Numerics uses row vectors. frameToWorld may include rotation, scale and shear.
    public readonly struct FalconWorkspaceMap
    {
        readonly Matrix4x4 deviceToWorld, worldToDevice;
        public bool IsValid { get; }

        public FalconWorkspaceMap(Vector3 deviceOriginMetres, Vector3 unitsPerMetre, bool invertZ,
            Matrix4x4 frameToWorld, FalconAxisAlignment? alignment = null)
        {
            if (!IsFinite(deviceOriginMetres) || !IsFinite(unitsPerMetre) ||
                unitsPerMetre.X <= 0 || unitsPerMetre.Y <= 0 || unitsPerMetre.Z <= 0)
                throw new ArgumentException("L’origine doit être finie et les échelles strictement positives.");
            if (!IsFinite(frameToWorld) || frameToWorld.M14 != 0 || frameToWorld.M24 != 0 ||
                frameToWorld.M34 != 0 || frameToWorld.M44 != 1)
                throw new ArgumentException("Le repère doit être une transformation affine finie.");
            var signedScale = unitsPerMetre;
            if (invertZ) signedScale.Z = -signedScale.Z;
            var axes = alignment ?? FalconAxisAlignment.Identity;
            _ = new FalconAxisAlignment(axes.Right, axes.Up, axes.TowardsUser);
            deviceToWorld = Matrix4x4.CreateTranslation(-deviceOriginMetres) * axes.Matrix *
                Matrix4x4.CreateScale(signedScale) * frameToWorld;
            if (!IsFinite(deviceToWorld) || !Matrix4x4.Invert(deviceToWorld, out worldToDevice) || !IsFinite(worldToDevice))
                throw new ArgumentException("Le mapping est singulier ou dépasse la précision disponible.");
            IsValid = true;
        }

        public Vector3 DeviceToWorldPosition(Vector3 metres) => Transform(metres, deviceToWorld, false);
        public Vector3 WorldToDevicePosition(Vector3 position) => Transform(position, worldToDevice, false);
        public Vector3 DeviceToWorldVelocity(Vector3 metresPerSecond) => Transform(metresPerSecond, deviceToWorld, true);
        public Vector3 WorldToDeviceVelocity(Vector3 unitsPerSecond) => Transform(unitsPerSecond, worldToDevice, true);

        // Forces conjugate to scene displacement have units J / Unity unit, not device newtons.
        // F_device = A^T F_world preserves F_device dot dx_device = F_world dot dx_world.
        public Vector3 WorldToDeviceForce(Vector3 sceneForce) => Transform(sceneForce, Matrix4x4.Transpose(deviceToWorld), true);
        public Vector3 DeviceToWorldForce(Vector3 newtons) => Transform(newtons, Matrix4x4.Transpose(worldToDevice), true);

        Vector3 Transform(Vector3 value, Matrix4x4 matrix, bool direction)
        {
            if (!IsValid) throw new InvalidOperationException("Mapping non initialisé.");
            if (!IsFinite(value)) throw new ArgumentException("Vecteur invalide.");
            var result = direction ? Vector3.TransformNormal(value, matrix) : Vector3.Transform(value, matrix);
            if (!IsFinite(result)) throw new ArgumentException("Conversion hors plage numérique.");
            return result;
        }

        public static bool IsFinite(Vector3 v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool IsFinite(Matrix4x4 m) =>
            Finite(m.M11) && Finite(m.M12) && Finite(m.M13) && Finite(m.M14) &&
            Finite(m.M21) && Finite(m.M22) && Finite(m.M23) && Finite(m.M24) &&
            Finite(m.M31) && Finite(m.M32) && Finite(m.M33) && Finite(m.M34) &&
            Finite(m.M41) && Finite(m.M42) && Finite(m.M43) && Finite(m.M44);
    }
}

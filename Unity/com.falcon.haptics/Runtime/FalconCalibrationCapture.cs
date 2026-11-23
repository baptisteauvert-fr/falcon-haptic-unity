using System;
using System.Numerics;

namespace Falcon.Haptics
{
    public readonly struct FalconCalibrationResult
    {
        public readonly Vector3 OriginMetres;
        public readonly FalconPhysicalBounds Bounds;
        public readonly int SampleCount;
        internal FalconCalibrationResult(Vector3 origin, FalconPhysicalBounds bounds, int count)
        { OriginMetres = origin; Bounds = bounds; SampleCount = count; }
    }

    public sealed class FalconCalibrationCapture
    {
        Vector3 minimum, maximum;
        public int SampleCount { get; private set; }
        public void AddSample(Vector3 positionMetres)
        {
            if (!FalconWorkspaceMap.IsFinite(positionMetres)) throw new ArgumentException("Position de calibration invalide.");
            if (SampleCount == 0) minimum = maximum = positionMetres;
            else { minimum = Vector3.Min(minimum, positionMetres); maximum = Vector3.Max(maximum, positionMetres); }
            SampleCount++;
        }

        // Span and margin are software quality criteria, not manufacturer specifications.
        public bool TryComplete(float minimumSpanMetres, float insetMetres,
            out FalconCalibrationResult result, out string error)
        {
            result = default;
            if (float.IsNaN(minimumSpanMetres) || float.IsInfinity(minimumSpanMetres) || minimumSpanMetres <= 0 ||
                float.IsNaN(insetMetres) || float.IsInfinity(insetMetres) || insetMetres < 0)
            { error = "Le déplacement minimal doit être positif et la marge positive ou nulle."; return false; }
            if (SampleCount < 10)
            { error = "Au moins dix lectures sont nécessaires."; return false; }
            var size = maximum - minimum;
            if (!FalconWorkspaceMap.IsFinite(size))
            { error = "L’étendue enregistrée dépasse la plage numérique."; return false; }
            if (size.X < minimumSpanMetres || size.Y < minimumSpanMetres || size.Z < minimumSpanMetres)
            { error = "Déplacez la poignée sur les trois axes avant de terminer."; return false; }
            var inset = new Vector3(insetMetres);
            var lower = minimum + inset;
            var upper = maximum - inset;
            if (!FalconWorkspaceMap.IsFinite(lower) || !FalconWorkspaceMap.IsFinite(upper) ||
                lower.X >= upper.X || lower.Y >= upper.Y || lower.Z >= upper.Z)
            { error = "La marge est trop grande pour les déplacements enregistrés."; return false; }
            var bounds = new FalconPhysicalBounds(lower, upper);
            result = new FalconCalibrationResult(minimum + size * .5f, bounds, SampleCount);
            error = null;
            return true;
        }
    }
}

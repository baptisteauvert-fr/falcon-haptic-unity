using System;
using UnityEngine;

namespace Falcon.Haptics
{
    [CreateAssetMenu(menuName = "Falcon/Calibration Profile", fileName = "Falcon Calibration")]
    public sealed class FalconCalibrationProfile : ScriptableObject
    {
        [SerializeField] Vector3 originMetres;
        [SerializeField] bool usePhysicalBounds;
        [SerializeField] Vector3 minimumMetres, maximumMetres;
        public Vector3 OriginMetres => originMetres;
        public bool UsePhysicalBounds => usePhysicalBounds;
        public Vector3 MinimumMetres => minimumMetres;
        public Vector3 MaximumMetres => maximumMetres;

        public void SetCalibration(Vector3 origin, bool useBounds, Vector3 minimum, Vector3 maximum)
        {
            if (!FalconWorkspaceMap.IsFinite(ToNumerics(origin))) throw new ArgumentException("Origine invalide.");
            if (useBounds && !new FalconPhysicalBounds(ToNumerics(minimum), ToNumerics(maximum)).Contains(ToNumerics(origin)))
                throw new ArgumentException("L’origine doit rester dans la boîte configurée.");
            originMetres = origin; usePhysicalBounds = useBounds;
            minimumMetres = minimum; maximumMetres = maximum;
        }
        static System.Numerics.Vector3 ToNumerics(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
    }
}

using System;
using UnityEngine;
using NVector = System.Numerics.Vector3;
using NMatrix = System.Numerics.Matrix4x4;

namespace Falcon.Haptics
{
    [DisallowMultipleComponent, RequireComponent(typeof(FalconInput))]
    [AddComponentMenu("Input/Falcon Workspace")]
    public sealed class FalconWorkspace : MonoBehaviour
    {
        [SerializeField, Tooltip("Origine dans le repère brut du Falcon, en mètres.")]
        Vector3 deviceOriginMetres;
        [SerializeField, Tooltip("Unités locales Unity par mètre physique, avant le Transform de cet objet.")]
        Vector3 unitsPerMetre = new Vector3(10, 10, 10);
        [SerializeField] bool invertZ = true;
        [SerializeField, Tooltip("Boîte logicielle définie par l’utilisateur, pas une limite mécanique certifiée.")]
        bool usePhysicalBounds;
        [SerializeField] Vector3 minimumMetres, maximumMetres;
        [SerializeField] bool clampCursorToBounds = true;
        [SerializeField, Tooltip("Profil à charger/enregistrer explicitement avec les boutons de l’inspecteur.")]
        FalconCalibrationProfile calibrationProfile = null;
        [SerializeField, Min(.000001f)] float minimumCaptureSpanMetres = .005f;
        [SerializeField, Min(0)] float captureInsetMetres = .001f;

        FalconInput input;
        FalconWorkspaceMap mapping;
        FalconPhysicalBounds? bounds;
        FalconCalibrationCapture capture;
        long lastCapturedSequence = -1;
        bool hasConfiguration, valid, attached;
        Vector3 previousOrigin, previousScale, previousMinimum, previousMaximum;
        bool previousInvert, previousBounds, previousClamp;
        Matrix4x4 previousFrame;
        string captureStatus = "Aucune capture.";

        public bool IsValid { get { RefreshMapping(); return valid; } }
        public string Status { get; private set; } = "Mapping non initialisé.";
        public string CaptureStatus => captureStatus;
        public bool IsCapturing => capture != null;
        public int CapturedSamples => capture?.SampleCount ?? 0;
        public Vector3 OriginMetres => deviceOriginMetres;
        public bool UsePhysicalBounds => usePhysicalBounds;
        public Vector3 MinimumMetres => minimumMetres;
        public Vector3 MaximumMetres => maximumMetres;
        public Vector3 MappedPositionWorld { get; private set; }
        public bool IsInsidePhysicalBounds { get; private set; }
        public FalconCalibrationProfile CalibrationProfile => calibrationProfile;

        void OnEnable()
        {
            input = GetComponent<FalconInput>();
            input.AttachWorkspace(this);
            attached = true;
            hasConfiguration = false;
            RefreshMapping();
        }
        void OnDisable()
        {
            attached = false;
            CancelCapture();
            if (input != null) input.DetachWorkspace(this);
            IsInsidePhysicalBounds = false;
        }

        void LateUpdate()
        {
            RefreshMapping();
            if (input == null || !input.TryGetSample(out var sample, out long sequence))
            {
                IsInsidePhysicalBounds = false;
                if (capture != null) { CancelCapture(); captureStatus = "Capture annulée : lecture indisponible ou périmée."; }
                return;
            }
            var raw = new Vector3((float)sample.X, (float)sample.Y, (float)sample.Z);
            if (capture != null && sequence != lastCapturedSequence)
            {
                capture.AddSample(ToNumerics(raw));
                lastCapturedSequence = sequence;
            }
            if (TryMapPosition(raw, out var position)) MappedPositionWorld = position;
        }

        public bool TryRecenter()
        {
            if (!attached || input == null || !input.TryGetSample(out var sample, out _))
            { captureStatus = "Recentrage impossible : aucune lecture récente."; return false; }
            var origin = new Vector3((float)sample.X, (float)sample.Y, (float)sample.Z);
            if (usePhysicalBounds)
            {
                try
                {
                    if (!new FalconPhysicalBounds(ToNumerics(minimumMetres), ToNumerics(maximumMetres)).Contains(ToNumerics(origin)))
                    { captureStatus = "Recentrage refusé hors de la boîte configurée."; return false; }
                }
                catch (ArgumentException ex) { captureStatus = ex.Message; return false; }
            }
            CancelCapture();
            input.DisarmEffect();
            deviceOriginMetres = origin;
            RefreshMapping();
            captureStatus = "Origine recentrée ; forces désarmées.";
            return true;
        }

        public bool BeginCapture()
        {
            if (!attached || input == null || !input.TryGetSample(out _, out _))
            { captureStatus = "Capture impossible : aucune lecture récente."; return false; }
            input.DisarmEffect();
            capture = new FalconCalibrationCapture();
            lastCapturedSequence = -1;
            captureStatus = "Déplacez doucement la poignée sur les trois axes, sans forcer les butées.";
            return true;
        }
        public void CancelCapture() { capture = null; lastCapturedSequence = -1; captureStatus = "Capture annulée."; }
        public bool CompleteCapture()
        {
            if (capture == null) return false;
            if (!capture.TryComplete(minimumCaptureSpanMetres, captureInsetMetres, out var result, out var error))
            { captureStatus = error; return false; }
            SetCalibration(ToUnity(result.OriginMetres), true, ToUnity(result.Bounds.MinimumMetres), ToUnity(result.Bounds.MaximumMetres));
            capture = null;
            captureStatus = $"Calibration calculée depuis {result.SampleCount} lectures. Vérifiez la boîte et enregistrez un profil.";
            return true;
        }

        public void SetCalibration(Vector3 origin, bool useBounds, Vector3 minimum, Vector3 maximum)
        {
            if (!FalconWorkspaceMap.IsFinite(ToNumerics(origin))) throw new ArgumentException("Origine invalide.");
            if (useBounds)
            {
                var check = new FalconPhysicalBounds(ToNumerics(minimum), ToNumerics(maximum));
                if (!check.Contains(ToNumerics(origin))) throw new ArgumentException("L’origine doit rester dans la boîte configurée.");
            }
            if (attached) input?.DisarmEffect();
            deviceOriginMetres = origin; usePhysicalBounds = useBounds;
            minimumMetres = minimum; maximumMetres = maximum;
            RefreshMapping();
        }
        public void SetScale(Vector3 scale, bool flipZ)
        {
            _ = new FalconWorkspaceMap(ToNumerics(deviceOriginMetres), ToNumerics(scale), flipZ, ToNumerics(transform.localToWorldMatrix));
            if (attached) input?.DisarmEffect();
            unitsPerMetre = scale; invertZ = flipZ; RefreshMapping();
        }
        public void LoadProfile(FalconCalibrationProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            SetCalibration(profile.OriginMetres, profile.UsePhysicalBounds, profile.MinimumMetres, profile.MaximumMetres);
            calibrationProfile = profile;
        }
        public void CopyCalibrationTo(FalconCalibrationProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            RefreshMapping();
            if (!valid) throw new InvalidOperationException(Status);
            profile.SetCalibration(deviceOriginMetres, usePhysicalBounds, minimumMetres, maximumMetres);
        }

        public bool TryGetMapping(out FalconWorkspaceMap result)
        { RefreshMapping(); result = mapping; return valid; }

        public bool TryWorldToDevicePosition(Vector3 worldPosition, out Vector3 deviceMetres)
        {
            RefreshMapping(); deviceMetres = default;
            if (!valid) return false;
            try { deviceMetres = ToUnity(mapping.WorldToDevicePosition(ToNumerics(worldPosition))); return true; }
            catch (ArgumentException) { return false; }
        }

        // sceneForce is conjugate to Unity displacement (J / unit), not a raw newton vector.
        public bool TryWorldToDeviceForce(Vector3 sceneForce, out Vector3 deviceNewtons)
        {
            RefreshMapping(); deviceNewtons = default;
            if (!valid) return false;
            try { deviceNewtons = ToUnity(mapping.WorldToDeviceForce(ToNumerics(sceneForce))); return true; }
            catch (ArgumentException) { return false; }
        }

        public bool TryMapPosition(Vector3 deviceMetres, out Vector3 worldPosition)
        {
            RefreshMapping(); worldPosition = default; IsInsidePhysicalBounds = false;
            if (!valid || !FalconWorkspaceMap.IsFinite(ToNumerics(deviceMetres))) return false;
            var raw = ToNumerics(deviceMetres);
            IsInsidePhysicalBounds = !bounds.HasValue || bounds.Value.Contains(raw);
            if (bounds.HasValue && clampCursorToBounds) raw = bounds.Value.Clamp(raw);
            try
            {
                worldPosition = ToUnity(mapping.DeviceToWorldPosition(raw));
                Status = IsInsidePhysicalBounds ? "Mapping configuré." : "Hors zone logicielle : forces désarmées.";
                return true;
            }
            catch (ArgumentException)
            {
                Status = "Position hors plage numérique du mapping.";
                if (attached) input?.DisarmEffect();
                return false;
            }
        }

        internal bool CanArm(Transform cursor)
        {
            RefreshMapping();
            if (!valid || capture != null || !CanUseCursor(cursor) || input == null || !input.TryGetSample(out var sample, out _)) return false;
            var raw = new NVector((float)sample.X, (float)sample.Y, (float)sample.Z);
            return FalconWorkspaceMap.IsFinite(raw) && (!bounds.HasValue || bounds.Value.Contains(raw));
        }
        internal bool CanUseCursor(Transform cursor) => cursor == null ||
            (cursor != transform && !transform.IsChildOf(cursor));

        void RefreshMapping()
        {
            var frame = transform.localToWorldMatrix;
            if (hasConfiguration && previousOrigin.Equals(deviceOriginMetres) && previousScale.Equals(unitsPerMetre) &&
                previousMinimum.Equals(minimumMetres) && previousMaximum.Equals(maximumMetres) &&
                previousInvert == invertZ && previousBounds == usePhysicalBounds && previousClamp == clampCursorToBounds && previousFrame.Equals(frame)) return;
            previousOrigin = deviceOriginMetres; previousScale = unitsPerMetre;
            previousMinimum = minimumMetres; previousMaximum = maximumMetres;
            previousInvert = invertZ; previousBounds = usePhysicalBounds; previousClamp = clampCursorToBounds; previousFrame = frame;
            hasConfiguration = true;
            if (attached) input?.DisarmEffect();
            if (capture != null) CancelCapture();
            valid = false; bounds = null;
            try
            {
                mapping = new FalconWorkspaceMap(ToNumerics(deviceOriginMetres), ToNumerics(unitsPerMetre), invertZ, ToNumerics(frame));
                if (usePhysicalBounds)
                {
                    bounds = new FalconPhysicalBounds(ToNumerics(minimumMetres), ToNumerics(maximumMetres));
                    if (!bounds.Value.Contains(ToNumerics(deviceOriginMetres))) throw new ArgumentException("L’origine doit rester dans la boîte configurée.");
                }
                valid = true; Status = "Mapping configuré.";
            }
            catch (ArgumentException ex) { mapping = default; bounds = null; Status = ex.Message; }
            if (attached) input?.SetWorkspaceBounds(valid ? bounds : null);
        }

        void OnDrawGizmosSelected()
        {
            try
            {
                var map = new FalconWorkspaceMap(ToNumerics(deviceOriginMetres), ToNumerics(unitsPerMetre), invertZ, ToNumerics(transform.localToWorldMatrix));
                var origin = map.DeviceToWorldPosition(ToNumerics(deviceOriginMetres));
                var axes = new[] { NVector.UnitX, NVector.UnitY, NVector.UnitZ };
                var colors = new[] { Color.red, Color.green, Color.blue };
                for (int i = 0; i < 3; i++)
                { Gizmos.color = colors[i]; Gizmos.DrawLine(ToUnity(origin), ToUnity(origin + map.DeviceToWorldVelocity(axes[i] * .01f))); }
                if (!usePhysicalBounds) return;
                _ = new FalconPhysicalBounds(ToNumerics(minimumMetres), ToNumerics(maximumMetres));
                var old = Gizmos.matrix;
                Gizmos.matrix = transform.localToWorldMatrix;
                var centre = ((minimumMetres + maximumMetres) * .5f - deviceOriginMetres);
                centre = Vector3.Scale(centre, unitsPerMetre);
                if (invertZ) centre.z = -centre.z;
                Gizmos.color = new Color(1, .7f, .1f, .8f);
                Gizmos.DrawWireCube(centre, Vector3.Scale(maximumMetres - minimumMetres, unitsPerMetre));
                Gizmos.matrix = old;
            }
            catch (ArgumentException) { }
        }

        static NVector ToNumerics(Vector3 v) => new NVector(v.x, v.y, v.z);
        static Vector3 ToUnity(NVector v) => new Vector3(v.X, v.Y, v.Z);
        static NMatrix ToNumerics(Matrix4x4 m) => new NMatrix(
            m.m00, m.m10, m.m20, m.m30, m.m01, m.m11, m.m21, m.m31,
            m.m02, m.m12, m.m22, m.m32, m.m03, m.m13, m.m23, m.m33);
    }
}

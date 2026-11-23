using System;
using UnityEngine;
using NVector = System.Numerics.Vector3;

namespace Falcon.Haptics
{
    public enum FalconSurfaceFace { PositiveZ, NegativeZ, PositiveX, NegativeX, PositiveY, NegativeY }

    [DefaultExecutionOrder(100), DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Input/Falcon Surface")]
    public sealed class FalconSurface : MonoBehaviour
    {
        [SerializeField] FalconWorkspace workspace = null;
        [SerializeField] FalconSurfaceFace face;
        [SerializeField, Min(0), Tooltip("Rigidité dans le repère physique en N/m.")] float stiffness = 40;
        [SerializeField, Min(0), Tooltip("Amortissement en N·s/m.")] float damping = 1;
        [SerializeField, Range(.01f, 1.2f)] float maximumForceNewtons = .6f;
        [SerializeField, Min(0)] float toolRadiusMetres = .002f;
        [SerializeField, Min(.000001f)] float edgeFadeMetres = .002f;
        [SerializeField, Min(.000001f)] float maximumPenetrationMetres = .01f;
        [SerializeField, Min(.000001f)] float riseRateNewtonsPerSecond = 10;
        [SerializeField, Min(0), Tooltip("Coefficient de friction dynamique, sans unité. Zéro conserve la surface sans friction.")]
        float frictionCoefficient = 0;
        [SerializeField, Min(.000001f), Tooltip("Vitesse tangentielle de régularisation en m/s. La friction tend vers zéro à l’arrêt.")]
        float frictionSpeedMetresPerSecond = .005f;

        FalconInput input;
        FalconPlaneSurface? geometry;
        public string Status { get; private set; } = "Assignez un Falcon Workspace.";
        public bool InContact => input != null && input.SurfaceContact;
        public FalconWorkspace Workspace => workspace;
        public float FrictionCoefficient => frictionCoefficient;
        public float FrictionSpeedMetresPerSecond => frictionSpeedMetresPerSecond;
        public float Stiffness => stiffness;
        public float Damping => damping;
        public float MaximumForceNewtons => maximumForceNewtons;
        public void SetMaterial(float stiffnessNewtonsPerMetre, float dampingNewtonsSecondsPerMetre, float forceLimitNewtons)
        {
            if (float.IsNaN(stiffnessNewtonsPerMetre) || float.IsInfinity(stiffnessNewtonsPerMetre) || stiffnessNewtonsPerMetre < 0 ||
                float.IsNaN(dampingNewtonsSecondsPerMetre) || float.IsInfinity(dampingNewtonsSecondsPerMetre) || dampingNewtonsSecondsPerMetre < 0 ||
                float.IsNaN(forceLimitNewtons) || float.IsInfinity(forceLimitNewtons) || forceLimitNewtons <= 0 || forceLimitNewtons > 1.2f)
                throw new ArgumentOutOfRangeException(nameof(forceLimitNewtons), "Matériau invalide (plafond maximal logiciel : 1,2 N).");
            if (stiffness == stiffnessNewtonsPerMetre && damping == dampingNewtonsSecondsPerMetre && maximumForceNewtons == forceLimitNewtons) return;
            input?.DisarmEffect();
            stiffness = stiffnessNewtonsPerMetre; damping = dampingNewtonsSecondsPerMetre; maximumForceNewtons = forceLimitNewtons;
            if (isActiveAndEnabled) Publish();
        }

        public void SetFriction(float coefficient, float regularizationSpeed = .005f)
        {
            if (float.IsNaN(coefficient) || float.IsInfinity(coefficient) || coefficient < 0 ||
                float.IsNaN(regularizationSpeed) || float.IsInfinity(regularizationSpeed) || regularizationSpeed <= 0)
                throw new ArgumentOutOfRangeException(nameof(coefficient), "Friction ou vitesse de régularisation invalide.");
            if (frictionCoefficient == coefficient && frictionSpeedMetresPerSecond == regularizationSpeed) return;
            input?.DisarmEffect();
            frictionCoefficient = coefficient; frictionSpeedMetresPerSecond = regularizationSpeed;
            if (isActiveAndEnabled) Publish();
        }

        void OnEnable() => Publish();
        void LateUpdate() => Publish();
        void OnDisable() { if (input != null) input.DetachSurface(this); input = null; geometry = null; }

        public void Configure(FalconWorkspace target, FalconSurfaceFace selectedFace = FalconSurfaceFace.PositiveZ)
        {
            if (input != null) input.DetachSurface(this);
            input = null; workspace = target; face = selectedFace;
            if (isActiveAndEnabled) Publish();
        }
        public bool TryArm(int intensity = 30) => Publish() && input.TryArmSurface(intensity);
        public void Disarm() => input?.DisarmEffect();
        public bool TryGetGeometry(out FalconPlaneSurface data)
        { bool ready = Publish(); data = geometry ?? default; return ready; }

        // Only this main-thread method touches colliders, transforms and workspace objects.
        internal bool Publish()
        {
            if (!isActiveAndEnabled) return false;
            var target = workspace != null && workspace.isActiveAndEnabled ? workspace.GetComponent<FalconInput>() : null;
            if (input != target)
            { if (input != null) input.DetachSurface(this); input = null; }
            if (target == null) return Fail("Assignez un Falcon Workspace actif.");
            if (input == null)
            {
                if (!target.AttachSurface(this)) return Fail("Une seule surface par Falcon est prise en charge dans cette préversion.");
                input = target;
            }
            var box = GetComponent<BoxCollider>();
            if (!box.enabled || box.attachedRigidbody != null)
                return Fail("La surface exige un BoxCollider actif sans Rigidbody, même cinématique.");
            if (!workspace.TryGetMapping(out var map)) return Fail(workspace.Status);
            try
            {
                if (!Enum.IsDefined(typeof(FalconSurfaceFace), face)) return Fail("Face inconnue.");
                if (!Finite(box.size) || box.size.x <= 0 || box.size.y <= 0 || box.size.z <= 0 || !Finite(box.center))
                    return Fail("Dimensions du collider invalides.");
                FaceVectors(box, out var centre, out var u, out var v, out var normal);
                var worldCentre = box.transform.TransformPoint(centre);
                var origin = map.WorldToDevicePosition(ToNumerics(worldCentre));
                var halfU = map.WorldToDevicePosition(ToNumerics(box.transform.TransformPoint(centre + u))) - origin;
                var halfV = map.WorldToDevicePosition(ToNumerics(box.transform.TransformPoint(centre + v))) - origin;
                var outward = map.WorldToDevicePosition(ToNumerics(box.transform.TransformPoint(centre + normal))) - origin;
                var data = new FalconPlaneSurface(origin, halfU, halfV, outward, stiffness, damping,
                    maximumForceNewtons, toolRadiusMetres, edgeFadeMetres, maximumPenetrationMetres, riseRateNewtonsPerSecond,
                    frictionCoefficient, frictionSpeedMetresPerSecond);
                input.SetSurfaceData(this, data);
                geometry = data;
                Status = "Face prête · activer puis relâcher et maintenir le bouton 1, devant la face.";
                return true;
            }
            catch (ArgumentException ex) { return Fail(ex.Message); }
        }

        bool Fail(string message) { geometry = null; input?.SetSurfaceData(this, null); Status = message; return false; }
        void FaceVectors(BoxCollider box, out Vector3 centre, out Vector3 u, out Vector3 v, out Vector3 normal)
        {
            var half = box.size * .5f;
            switch (face)
            {
                case FalconSurfaceFace.PositiveZ: case FalconSurfaceFace.NegativeZ:
                    normal = face == FalconSurfaceFace.PositiveZ ? Vector3.forward : Vector3.back;
                    centre = box.center + normal * half.z; u = Vector3.right * half.x; v = Vector3.up * half.y; break;
                case FalconSurfaceFace.PositiveX: case FalconSurfaceFace.NegativeX:
                    normal = face == FalconSurfaceFace.PositiveX ? Vector3.right : Vector3.left;
                    centre = box.center + normal * half.x; u = Vector3.up * half.y; v = Vector3.forward * half.z; break;
                default:
                    normal = face == FalconSurfaceFace.PositiveY ? Vector3.up : Vector3.down;
                    centre = box.center + normal * half.y; u = Vector3.right * half.x; v = Vector3.forward * half.z; break;
            }
        }
        void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            FaceVectors(box, out var centre, out var u, out var v, out var normal);
            var previous = Gizmos.matrix;
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(centre - u - v, centre + u - v); Gizmos.DrawLine(centre + u - v, centre + u + v);
            Gizmos.DrawLine(centre + u + v, centre - u + v); Gizmos.DrawLine(centre - u + v, centre - u - v);
            Gizmos.DrawLine(centre, centre + normal * .1f);
            Gizmos.matrix = previous;
        }
        static bool Finite(Vector3 v) => FalconWorkspaceMap.IsFinite(ToNumerics(v));
        static NVector ToNumerics(Vector3 v) => new NVector(v.x, v.y, v.z);
    }
}

using System;
using System.Numerics;

namespace Falcon.Haptics
{
    // Immutable finite face in device metres. U and V are half-edges of a parallelogram.
    public readonly struct FalconPlaneSurface : IEquatable<FalconPlaneSurface>
    {
        public readonly Vector3 Centre, HalfU, HalfV, Normal;
        public readonly float Stiffness, Damping, MaximumForce, ToolRadius, EdgeFade, MaximumPenetration, SlewRate;
        public readonly float FrictionCoefficient, FrictionSpeed;
        public bool IsValid { get; }
        public FalconPlaneSurface(Vector3 centre, Vector3 halfU, Vector3 halfV, Vector3 outward,
            float stiffness = 40, float damping = 1, float maximumForce = .6f,
            float toolRadius = .002f, float edgeFade = .002f, float maximumPenetration = .01f, float slewRate = 10,
            float frictionCoefficient = 0, float frictionSpeed = .005f)
        {
            if (!FalconWorkspaceMap.IsFinite(centre) || !FalconWorkspaceMap.IsFinite(halfU) ||
                !FalconWorkspaceMap.IsFinite(halfV) || !FalconWorkspaceMap.IsFinite(outward))
                throw new ArgumentException("Géométrie de surface non finie.");
            var cross = Vector3.Cross(halfU, halfV);
            float gram = halfU.LengthSquared() * halfV.LengthSquared() - MathF.Pow(Vector3.Dot(halfU, halfV), 2);
            if (!Finite(gram) || !(gram > 0)) throw new ArgumentException("Face numériquement dégénérée.");
            if (!(cross.LengthSquared() > 1e-16f) || !Finite(cross.LengthSquared()))
                throw new ArgumentException("Face de surface dégénérée.");
            var normal = Vector3.Normalize(cross);
            float orientation = Vector3.Dot(normal, outward);
            if (!Finite(orientation) || Math.Abs(orientation) < 1e-8f)
                throw new ArgumentException("Normale extérieure invalide.");
            if (orientation < 0) normal = -normal;
            if (!Finite(stiffness) || stiffness < 0 || !Finite(damping) || damping < 0 ||
                !Finite(maximumForce) || maximumForce <= 0 || maximumForce > 1.2f ||
                !Finite(toolRadius) || toolRadius < 0 || !Finite(edgeFade) || edgeFade <= 0 ||
                !Finite(maximumPenetration) || maximumPenetration <= 0 || !Finite(slewRate) || slewRate <= 0)
                throw new ArgumentException("Paramètres de surface invalides ; le plafond doit rester entre 0 et 1,2 N.");
            if (!Finite(frictionCoefficient) || frictionCoefficient < 0 || !Finite(frictionSpeed) || frictionSpeed <= 0)
                throw new ArgumentException("Le coefficient de friction doit être positif ou nul et sa vitesse de régularisation strictement positive.");
            Centre = centre; HalfU = halfU; HalfV = halfV; Normal = normal;
            Stiffness = stiffness; Damping = damping; MaximumForce = maximumForce;
            ToolRadius = toolRadius; EdgeFade = edgeFade; MaximumPenetration = maximumPenetration; SlewRate = slewRate;
            FrictionCoefficient = frictionCoefficient; FrictionSpeed = frictionSpeed;
            IsValid = true;
        }

        public float SignedDistance(Vector3 position) => Vector3.Dot(position - Centre, Normal);
        public float FootprintGain(Vector3 position)
        {
            var offset = position - Centre;
            float uu = Vector3.Dot(HalfU, HalfU), uv = Vector3.Dot(HalfU, HalfV), vv = Vector3.Dot(HalfV, HalfV);
            float determinant = uu * vv - uv * uv;
            if (!(determinant > 0)) throw new InvalidOperationException("Face numériquement dégénérée.");
            float pu = Vector3.Dot(offset, HalfU), pv = Vector3.Dot(offset, HalfV);
            float u = (pu * vv - pv * uv) / determinant, v = (pv * uu - pu * uv) / determinant;
            if (Math.Abs(u) >= 1 || Math.Abs(v) >= 1) return 0;
            float area = Vector3.Cross(HalfU, HalfV).Length();
            float edgeDistance = Math.Min((1 - Math.Abs(u)) * area / HalfV.Length(), (1 - Math.Abs(v)) * area / HalfU.Length());
            return Math.Clamp(edgeDistance / EdgeFade, 0, 1);
        }
        public bool Equals(FalconPlaneSurface other) => IsValid == other.IsValid && Centre == other.Centre &&
            HalfU == other.HalfU && HalfV == other.HalfV && Normal == other.Normal && Stiffness == other.Stiffness &&
            Damping == other.Damping && MaximumForce == other.MaximumForce && ToolRadius == other.ToolRadius &&
            EdgeFade == other.EdgeFade && MaximumPenetration == other.MaximumPenetration && SlewRate == other.SlewRate &&
            FrictionCoefficient == other.FrictionCoefficient && FrictionSpeed == other.FrictionSpeed;
        public override bool Equals(object obj) => obj is FalconPlaneSurface other && Equals(other);
        public override int GetHashCode() => Centre.GetHashCode() ^ HalfU.GetHashCode() ^ HalfV.GetHashCode();
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class FalconSurfaceContact
    {
        readonly FalconPlaneSurface surface;
        Vector3 previous, velocity;
        float age;
        public bool InContact { get; private set; }
        public float PenetrationMetres { get; private set; }
        public float NormalForceNewtons { get; private set; }
        public Vector3 TangentialForceNewtons { get; private set; }
        public FalconSurfaceContact(FalconPlaneSurface surface, FalconSample initial)
        {
            if (!surface.IsValid) throw new ArgumentException("Surface non initialisée.");
            this.surface = surface;
            previous = Position(initial);
            if (!FalconWorkspaceMap.IsFinite(previous) || surface.SignedDistance(previous) < surface.ToolRadius)
                throw new InvalidOperationException("Placez l’outil devant la face avant d’activer la surface.");
        }
        public Vector3 Step(FalconSample sample, float seconds, int intensity)
        {
            var position = Position(sample);
            var delta = position - previous;
            if (!(seconds > 0 && seconds <= .045f) || !FalconWorkspaceMap.IsFinite(position) ||
                !(delta.LengthSquared() <= .03f * .03f))
                throw new InvalidOperationException("Lecture de contact irrégulière.");
            previous = position;
            var measuredVelocity = delta / seconds;
            if (!FalconWorkspaceMap.IsFinite(measuredVelocity) || float.IsInfinity(measuredVelocity.LengthSquared()))
                throw new InvalidOperationException("Vitesse de contact invalide.");
            velocity = Vector3.Lerp(velocity, measuredVelocity, seconds / (.025f + seconds));
            float footprint = surface.FootprintGain(position);
            PenetrationMetres = Math.Max(0, surface.ToolRadius - surface.SignedDistance(position));
            InContact = footprint > 0 && PenetrationMetres > 0;
            if (InContact && PenetrationMetres > surface.MaximumPenetration)
                throw new InvalidOperationException("Pénétration maximale dépassée : surface désarmée.");
            float gain = Math.Clamp(intensity, 0, 100) / 100f;
            age = Math.Min(.2f, age + seconds);
            float magnitude = InContact ? Math.Max(0, surface.Stiffness * PenetrationMetres -
                surface.Damping * Vector3.Dot(velocity, surface.Normal)) * footprint : 0;
            magnitude = Math.Min(surface.MaximumForce * gain, magnitude * gain * age / .2f);
            float previousNormal = NormalForceNewtons;
            float maximumRise = surface.SlewRate * seconds;
            // Leaving contact and intensity reductions must not retain a push in free space.
            NormalForceNewtons = !InContact || magnitude < previousNormal ? magnitude :
                Math.Min(magnitude, previousNormal + maximumRise);

            var tangentVelocity = measuredVelocity - surface.Normal * Vector3.Dot(measuredVelocity, surface.Normal);
            float speed = tangentVelocity.Length();
            if (!InContact || NormalForceNewtons <= 0 || surface.FrictionCoefficient == 0 || speed == 0)
                TangentialForceNewtons = Vector3.Zero;
            else
            {
                var opposition = -tangentVelocity / speed;
                // Scale the regularization to avoid overflow when a caller chooses a huge finite speed.
                float largest = Math.Max(speed, surface.FrictionSpeed);
                float a = speed / largest, b = surface.FrictionSpeed / largest;
                float regularization = a / MathF.Sqrt(a * a + b * b);
                float cap = surface.MaximumForce * gain;
                float remaining = MathF.Sqrt(Math.Max(0, cap * cap - NormalForceNewtons * NormalForceNewtons));
                float desired = Math.Min(remaining, surface.FrictionCoefficient * (NormalForceNewtons * regularization));
                // Reuse only the dissipative projection of the previous command: a reversal cannot retain an assisting force.
                float previousOpposition = Math.Max(0, Vector3.Dot(TangentialForceNewtons, opposition));
                float riseBudget = Math.Max(0, maximumRise - Math.Max(0, NormalForceNewtons - previousNormal));
                float friction = Math.Min(desired, previousOpposition + riseBudget);
                TangentialForceNewtons = opposition * friction;
                if (Vector3.Dot(TangentialForceNewtons, measuredVelocity) > 0)
                    TangentialForceNewtons = Vector3.Zero; // Guard projection roundoff near a purely normal motion.
            }
            var force = surface.Normal * NormalForceNewtons + TangentialForceNewtons;
            if (!FalconWorkspaceMap.IsFinite(force)) throw new InvalidOperationException("Force de surface invalide.");
            float finalCap = surface.MaximumForce * gain, length = force.Length();
            if (length > finalCap)
            {
                float ratio = finalCap / length;
                NormalForceNewtons *= ratio; TangentialForceNewtons *= ratio; force *= ratio;
            }
            return force;
        }
        static Vector3 Position(FalconSample sample) => new Vector3((float)sample.X, (float)sample.Y, (float)sample.Z);
    }
}

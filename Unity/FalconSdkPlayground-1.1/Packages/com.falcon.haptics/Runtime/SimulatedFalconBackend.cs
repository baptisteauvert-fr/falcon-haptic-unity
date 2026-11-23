using System;
using System.Numerics;

namespace Falcon.Haptics
{
    // Records commanded force; it does not simulate the mechanics of the handle.
    public sealed class SimulatedFalconBackend : IFalconBackend
    {
        readonly object gate = new object();
        FalconSample sample;
        Vector3 force;
        bool available = true, connected, enabled;
        float limit;

        public string SdkVersion => "Simulation (sans matériel)";
        public Vector3 CommandedForce { get { lock (gate) return force; } }
        public void SetAvailable(bool value)
        {
            lock (gate)
            {
                available = value;
                if (!value) { enabled = false; force = Vector3.Zero; }
            }
        }

        public void SetSample(FalconSample value)
        {
            if (!Finite(value.X) || !Finite(value.Y) || !Finite(value.Z))
                throw new ArgumentException("Position simulée invalide.", nameof(value));
            lock (gate) sample = value;
        }

        public void Connect()
        {
            lock (gate)
            {
                if (!available) throw new InvalidOperationException("Falcon simulé déconnecté.");
                connected = true;
                enabled = false;
                force = Vector3.Zero;
            }
        }

        public FalconSample Read() { lock (gate) { EnsureConnected(); return sample; } }
        public void BeginHaptics(HapticMode mode)
        {
            lock (gate)
            {
                EnsureConnected();
                if (!Enum.IsDefined(typeof(HapticMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
                limit = HapticEffect.ForceLimit(mode);
                force = Vector3.Zero;
                enabled = true;
            }
        }

        public void ApplyHaptics(Vector3 value)
        {
            lock (gate)
            {
                EnsureConnected();
                if (!enabled || !(value.LengthSquared() <= limit * limit + .000001f))
                    throw new InvalidOperationException("Commande simulée hors limites.");
                force = value;
            }
        }

        public void EndHaptics() { lock (gate) { force = Vector3.Zero; enabled = false; } }
        public void Dispose() { lock (gate) { force = Vector3.Zero; enabled = connected = false; } }
        void EnsureConnected()
        {
            if (!connected || !available) throw new InvalidOperationException("Falcon simulé déconnecté.");
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

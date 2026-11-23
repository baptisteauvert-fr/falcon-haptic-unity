using System;
using UnityEngine;

namespace Falcon.Haptics
{
    public enum FalconBackendMode { Hardware, Simulation }

    [DisallowMultipleComponent]
    [AddComponentMenu("Input/Novint Falcon")]
    public sealed class FalconInput : MonoBehaviour
    {
        [SerializeField] FalconBackendMode backend = FalconBackendMode.Hardware;
        [SerializeField, Tooltip("Chemin absolu vers dhd64.dll. Vide = détection du SDK installé.")]
        string dhdLibraryPath = null;
        [SerializeField, Tooltip("Objet facultatif à déplacer dans le repère local Unity.")]
        Transform cursor = null;
        [SerializeField, Min(0)] float unityUnitsPerMetre = 10;
        [SerializeField, Tooltip("Position brute en mètres utilisée en simulation.")]
        Vector3 simulationPositionMetres;
        [SerializeField, Range(0, 15), Tooltip("Masque des quatre boutons ; 1 = bouton d’autorisation.")]
        int simulationButtons;
        [SerializeField] bool simulationAvailable = true;

        FalconSession session;
        SimulatedFalconBackend simulator;
        FalconWorkspace workspace;
        FalconPhysicalBounds? workspaceBounds;
        FalconSurface surface;
        bool paused;
        public bool Connected { get; private set; }
        public Vector3 PositionMetres { get; private set; }
        public uint Buttons { get; private set; }
        public string Status { get; private set; } = "Arrêté";
        public bool EffectArmed => session?.State.EffectArmed == true;
        public bool EffectActive => session?.State.EffectActive == true;
        public bool SurfaceContact => session?.State.SurfaceContact == true;
        public float SurfaceFrictionPeakNewtons => session?.State.SurfaceFrictionPeakNewtons ?? 0;
        public string EffectError => session?.State.EffectError ?? string.Empty;
        public FalconConnectionState ConnectionState => session?.State.Connection ?? FalconConnectionState.Stopped;
        public long SampleSequence => session?.State.Sequence ?? 0;
        public double SampleAgeSeconds => session?.State.SampleAgeSeconds ?? double.PositiveInfinity;
        public double MeanIntervalMilliseconds => session?.State.MeanIntervalMilliseconds ?? 0;
        public double MaximumIntervalMilliseconds => session?.State.MaximumIntervalMilliseconds ?? 0;
        public bool IsSimulation => simulator != null;
        public Vector3 SimulatedForceNewtons
        {
            get
            {
                var force = simulator?.CommandedForce ?? System.Numerics.Vector3.Zero;
                return new Vector3(force.X, force.Y, force.Z);
            }
        }

        public bool IsButtonPressed(int index) =>
            Connected && index >= 0 && index < 4 && (Buttons & (1u << index)) != 0;

        public bool TryArmEffect(HapticMode mode, int intensity = 30) =>
            isActiveAndEnabled && Connected && !paused && Application.isFocused &&
            (workspace == null || workspace.CanArm(cursor)) &&
            session != null && session.TryArmEffect(mode, intensity);

        public void SetEffectIntensity(int intensity) => session?.SetIntensity(intensity);
        public void DisarmEffect() => session?.Disarm();
        public bool TryArmSurface(int intensity = 30) => isActiveAndEnabled && Connected && !paused &&
            Application.isFocused && workspace != null && workspace.CanArm(cursor) && surface != null &&
            surface.Publish() && session != null && session.TryArmSurface(intensity);
        internal bool AttachSurface(FalconSurface value)
        {
            if (surface != null && surface != value && surface.isActiveAndEnabled) return false;
            surface = value; DisarmEffect(); return true;
        }
        internal void DetachSurface(FalconSurface value)
        {
            if (surface != value) return;
            surface = null; session?.SetSurface(null);
        }
        internal void SetSurfaceData(FalconSurface owner, FalconPlaneSurface? data)
        { if (surface == owner) session?.SetSurface(data); }

        public bool TryGetSample(out FalconSample sample, out long sequence)
        {
            var state = session?.State;
            if (state.HasValue && state.Value.Connected)
            { sample = state.Value.Sample.Value; sequence = state.Value.Sequence; return true; }
            sample = default; sequence = 0; return false;
        }

        internal void AttachWorkspace(FalconWorkspace value) { workspace = value; DisarmEffect(); }
        internal void DetachWorkspace(FalconWorkspace value)
        {
            if (workspace != value) return;
            workspace = null;
            SetWorkspaceBounds(null);
        }
        internal void SetWorkspaceBounds(FalconPhysicalBounds? value)
        { workspaceBounds = value; session?.SetPhysicalBounds(value); }

        public void SetSimulationSample(Vector3 positionMetres, uint buttons)
        {
            if (!Finite(positionMetres.x) || !Finite(positionMetres.y) || !Finite(positionMetres.z))
                throw new ArgumentException("Position simulée invalide.", nameof(positionMetres));
            simulationPositionMetres = positionMetres;
            simulationButtons = (int)(buttons & 15u);
        }

        public void SetSimulationAvailable(bool available) => simulationAvailable = available;
        public void StartSession()
        {
            if (isActiveAndEnabled && Application.isPlaying) OnEnable();
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        void OnApplicationFocus(bool focused) { if (!focused) DisarmEffect(); }
        void OnApplicationPause(bool pause) { paused = pause; if (pause) DisarmEffect(); }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (session != null && session.IsRunning)
            {
                Status = session.Stopping ? "Arrêt du thread précédent en attente…" : session.State.Message;
                return;
            }
            paused = false;
            simulator = null;
            if (backend == FalconBackendMode.Simulation)
            {
                simulator = new SimulatedFalconBackend();
                PublishSimulation();
                var selected = simulator;
                session = new FalconSession(() => selected);
                session.SetPhysicalBounds(workspaceBounds);
                return;
            }
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (!string.IsNullOrWhiteSpace(dhdLibraryPath) && !FalconSdk.IsLibrary(dhdLibraryPath))
            {
                Status = "Le chemin configuré doit pointer vers dhd64.dll.";
                return;
            }
            string configured = string.IsNullOrWhiteSpace(dhdLibraryPath) ? null : dhdLibraryPath;
            session = new FalconSession(() => new FalconDevice(configured ?? FalconSdk.Find() ??
                throw new InvalidOperationException("Installez Force Dimension DHD x64.")));
            session.SetPhysicalBounds(workspaceBounds);
#else
            Status = "Le Falcon requiert Windows x64. Le mode Simulation reste disponible.";
#endif
        }

        void PublishSimulation()
        {
            if (simulator == null) return;
            simulator.SetAvailable(simulationAvailable);
            simulator.SetSample(new FalconSample(simulationPositionMetres.x, simulationPositionMetres.y,
                simulationPositionMetres.z, (uint)(simulationButtons & 15)));
        }

        void Update()
        {
            if (session == null) return;
            if (session.Stopping)
            {
                ClearInput();
                Status = "Arrêt du thread précédent en attente…";
                if (!session.IsRunning) { session = null; OnEnable(); }
                return;
            }
            PublishSimulation();
            if (!paused && Application.isFocused) session.UpdateHeartbeat();
            else session.Disarm();

            var state = session.State;
            Connected = state.Connected;
            Status = state.Connection == FalconConnectionState.Stale ? "Lecture Falcon périmée." : state.Message;
            Buttons = Connected ? state.Sample.Value.Buttons : 0;
            if (Connected)
            {
                var sample = state.Sample.Value;
                PositionMetres = new Vector3((float)sample.X, (float)sample.Y, (float)sample.Z);
                if (workspace != null)
                {
                    if (workspace.TryMapPosition(PositionMetres, out var mapped) && workspace.CanUseCursor(cursor))
                    { if (cursor != null) cursor.position = mapped; }
                    else
                    {
                        session.Disarm();
                        Status = !workspace.CanUseCursor(cursor) ? "Le curseur doit être distinct du repère de calibration et de ses parents." : workspace.Status;
                    }
                }
                else if (cursor != null)
                    cursor.localPosition = new Vector3(PositionMetres.x, PositionMetres.y, -PositionMetres.z) * unityUnitsPerMetre;
            }
            else { PositionMetres = Vector3.zero; session.Disarm(); }
        }

        void ClearInput() { Connected = false; PositionMetres = Vector3.zero; Buttons = 0; }
        void StopSession()
        {
            ClearInput();
            if (session == null) { Status = "Arrêté"; return; }
            bool stopped = session.Stop();
            var state = session.State;
            Status = !stopped ? "Arrêt non confirmé : le thread conserve la session Falcon." :
                !string.IsNullOrEmpty(state.EffectError) ? state.Message : "Arrêté";
            if (!stopped) Debug.LogError(Status, this);
        }
        void OnDisable() => StopSession();
        void OnDestroy() => StopSession();
        void OnApplicationQuit() => StopSession();
    }
}

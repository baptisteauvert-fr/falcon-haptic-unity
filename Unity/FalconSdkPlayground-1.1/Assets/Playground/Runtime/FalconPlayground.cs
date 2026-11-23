using System;
using System.IO;
using Falcon.Haptics;
using UnityEngine;

namespace Falcon.Playground
{
    public sealed partial class FalconPlayground : MonoBehaviour
    {
        [SerializeField] FalconInput simulation = null, hardware = null;
        [SerializeField] FalconSurface surface = null;
        [SerializeField] FalconCalibrationProfile exampleCalibration = null;
        [SerializeField] Transform cursor = null;
        [SerializeField, Tooltip("Vide : dossier de données de l’application. Utilisé aussi par les tests isolés.")]
        string storageDirectoryOverride = null;
        readonly string[] tabs = { "Entrées", "Six effets", "Surface", "Calibration", "Diagnostic" };
        readonly string[] modeNames = { "Eau", "Paroi X", "Résistance", "Ressort", "Gravier", "Moteur" };
        readonly string[] modeHints = {
            "Déplacer sur les trois axes : le freinage augmente avec la vitesse.",
            "Après la prise, diminuer X brut d’au moins 4 mm. Cette paroi historique est indépendante du collider.",
            "Déplacer lentement : résistance opposée au mouvement.",
            "La position à la prise devient l’ancre. Écarter doucement la poignée.",
            "Déplacer la poignée pour faire varier le frottement.",
            "Vibration moteur même à l’arrêt, avec un rappel léger."
        };
        readonly bool[] modesSeen = new bool[6];
        FalconInput current;
        Vector3 position = new Vector3(0, 0, .006f), minimum, maximum;
        Vector3 baseScale = Vector3.one * 10;
        bool[] buttons = new bool[4];
        bool available = true, autoMotion, invertZ = true, initialized;
        int tab, selectedMode, intensity = 20;
        int faceIndex;
        uint buttonsSeen;
        float uniformScale = 10, motionStarted;
        string message = "Prêt en simulation. Aucun effet n’est armé au démarrage.";
        Vector2 scroll;

        public FalconInput ActiveInput => current;
        public Transform Cursor => cursor;
        public FalconSurface Surface => surface;
        public FalconWorkspace Workspace => current != null ? current.GetComponent<FalconWorkspace>() : null;
        public bool HardwareSelected => current == hardware;
        public bool HardwareRigActive => hardware != null && hardware.gameObject.activeInHierarchy;
        public string LastMessage => message;
        public string StorageDirectory => string.IsNullOrWhiteSpace(storageDirectoryOverride) ? Application.persistentDataPath : storageDirectoryOverride;
        public string CalibrationPath => Path.Combine(StorageDirectory, HardwareSelected ? "falcon-calibration-v2-hardware.json" : "falcon-calibration-v2-simulation.json");

        void Start()
        {
            current = simulation;
            Workspace.SetAxisAlignment(FalconAxisAlignment.Identity);
            simulationDirectionsConfirmed = true;
            ResetObservations();
            surface.Configure(Workspace, FalconSurfaceFace.NegativeZ);
            faceIndex = (int)FalconSurfaceFace.NegativeZ;
        }

        void Update()
        {
            if (current == null) return;
            if (current == simulation)
            {
                if (autoMotion)
                {
                    float t = Time.unscaledTime - motionStarted;
                    position = new Vector3(.015f * Mathf.Sin(t), .015f * Mathf.Sin(t * 1.3f), .006f + .007f * Mathf.Sin(t * .7f));
                }
                uint mask = 0;
                for (int i = 0; i < buttons.Length; i++) if (buttons[i]) mask |= 1u << i;
                current.SetSimulationAvailable(available);
                current.SetSimulationSample(position, mask);
            }
            if (!current.TryGetSample(out var sample, out _)) return;
            var raw = new Vector3((float)sample.X, (float)sample.Y, (float)sample.Z);
            if (!initialized) { minimum = maximum = raw; initialized = true; }
            minimum = Vector3.Min(minimum, raw); maximum = Vector3.Max(maximum, raw);
            buttonsSeen |= sample.Buttons;
            if (tab == 1 && current.EffectActive) modesSeen[selectedMode] = true;
        }

        void OnDisable() { if (current != null) current.DisarmEffect(); }

        public bool SelectBackend(bool useHardware)
        {
            var next = useHardware ? hardware : simulation;
            if (next == null) return false;
            if (current == next && next.gameObject.activeSelf) return true;
            StopAllForces();
            if (Workspace != null) Workspace.CancelCapture();
            if (current != null)
            {
                current.gameObject.SetActive(false);
                if (current.ConnectionState == FalconConnectionState.Stopping)
                { message = "Arrêt non confirmé : aucun autre périphérique n’est démarré. Consultez Diagnostic."; return false; }
            }
            current = next;
            current.gameObject.SetActive(true);
            baseScale = Workspace.UnitsPerMetre; invertZ = Workspace.InvertZ; uniformScale = baseScale.x;
            ResetDirectionWizard();
            surface.Configure(Workspace, (FalconSurfaceFace)faceIndex);
            ResetObservations();
            message = useHardware ? "Falcon réel sélectionné. Lecture seule ; initialiser et calibrer avant d’armer." : "Simulation sélectionnée. Les forces restent désarmées.";
            return true;
        }

        public void StopAllForces()
        {
            simulation.DisarmEffect(); hardware.DisarmEffect(); surface.Disarm();
            buttons[0] = false;
            message = "Arrêt demandé. Relâchez le bouton avant de réactiver un essai.";
        }

        public bool RestartReading()
        {
            if (current == null) return false;
            StopAllForces();
            current.enabled = false;
            if (current.ConnectionState == FalconConnectionState.Stopping)
            { message = "Le thread n’a pas confirmé son arrêt ; relance refusée."; return false; }
            current.enabled = true;
            message = "Lecture relancée, sans armement.";
            return true;
        }

        public void SetSimulation(Vector3 metres, uint mask)
        {
            if (float.IsNaN(metres.x) || float.IsNaN(metres.y) || float.IsNaN(metres.z) ||
                float.IsInfinity(metres.x) || float.IsInfinity(metres.y) || float.IsInfinity(metres.z))
                throw new ArgumentException("Position invalide.");
            position = metres;
            autoMotion = false;
            for (int i = 0; i < 4; i++) buttons[i] = (mask & (1u << i)) != 0;
        }
        public void SetSimulationAvailability(bool value)
        {
            if (available == value) return;
            StopAllForces(); available = value;
            if (current == simulation) current.SetSimulationAvailable(value);
        }

        public bool ArmMode(HapticMode mode)
        {
            if (current == null) return false;
            if (!DirectionsConfirmed) { message = "Réglez d’abord les directions de la poignée."; page = 1; return false; }
            selectedMode = (int)mode;
            bool result = current.TryArmEffect(mode, intensity);
            message = result ? "Effet préparé. Relâcher puis maintenir le bouton 1." : "Armement refusé : vérifier lecture, limites, capture et focus de la vue Game.";
            return result;
        }
        public bool ArmSurface()
        {
            if (!DirectionsConfirmed) { message = "Réglez d’abord les directions de la poignée."; page = 1; return false; }
            bool result = current != null && current.TryArmSurface(intensity);
            message = result ? "Surface préparée. Relâcher puis maintenir le bouton 1 devant la face." : "Surface refusée : vérifier lecture, Workspace et géométrie.";
            return result;
        }

        public void ResetSimulation()
        {
            StopAllForces();
            autoMotion = false; available = true;
            position = new Vector3(0, 0, .006f);
            for (int i = 0; i < 4; i++) buttons[i] = false;
            if (current == simulation)
            {
                Workspace.SetCalibration(Vector3.zero, false, Vector3.zero, Vector3.zero);
                Workspace.SetAxisAlignment(FalconAxisAlignment.Identity);
                simulationDirectionsConfirmed = true;
                Workspace.transform.localRotation = Quaternion.identity;
                uniformScale = 10; baseScale = Vector3.one * 10; invertZ = true;
                Workspace.SetScale(baseScale, true);
                surface.transform.SetPositionAndRotation(new Vector3(0, 0, .01f), Quaternion.identity);
                SelectFace(FalconSurfaceFace.NegativeZ);
            }
            message = "Simulation réinitialisée : outil devant la face, forces désarmées.";
        }

        public void SelectFace(FalconSurfaceFace face)
        {
            StopAllForces();
            faceIndex = (int)face;
            bool x = face == FalconSurfaceFace.PositiveX || face == FalconSurfaceFace.NegativeX;
            bool y = face == FalconSurfaceFace.PositiveY || face == FalconSurfaceFace.NegativeY;
            surface.transform.localScale = x ? new Vector3(.02f, .6f, .6f) : y ? new Vector3(.6f, .02f, .6f) : new Vector3(.6f, .6f, .02f);
            surface.Configure(Workspace, face);
        }

        public bool PlaceSurfaceAhead()
        {
            StopAllForces();
            if (current == null || !current.TryGetSample(out var sample, out _) || !Workspace.TryGetMapping(out var map) ||
                !surface.TryGetGeometry(out var data)) { message = "Lecture et géométrie récentes nécessaires."; return false; }
            var raw = new System.Numerics.Vector3((float)sample.X, (float)sample.Y, (float)sample.Z);
            var desired = map.DeviceToWorldPosition(raw - data.Normal * .006f);
            var previous = map.DeviceToWorldPosition(data.Centre);
            surface.transform.position += new Vector3(desired.X - previous.X, desired.Y - previous.Y, desired.Z - previous.Z);
            surface.TryGetGeometry(out _);
            message = "Face placée à 6 mm devant l’outil. Aucun déplacement matériel n’a été commandé.";
            return true;
        }

        public bool SaveCalibration()
        {
            try
            {
                if (!Workspace.IsValid) throw new InvalidOperationException(Workspace.Status);
                var alignment = Workspace.AxisAlignment;
                var data = new CalibrationData { version = 2, origin = Workspace.OriginMetres, useBounds = Workspace.UsePhysicalBounds,
                    minimum = Workspace.MinimumMetres, maximum = Workspace.MaximumMetres, hasAlignment = Workspace.HasAxisAlignment,
                    right = U(alignment.Right), up = U(alignment.Up), towards = U(alignment.TowardsUser), scale = Workspace.UnitsPerMetre,
                    invert = Workspace.InvertZ, rotation = Workspace.transform.localRotation };
                Directory.CreateDirectory(StorageDirectory);
                File.WriteAllText(CalibrationPath, JsonUtility.ToJson(data, true));
                message = "Calibration enregistrée : " + CalibrationPath;
                return true;
            }
            catch (Exception ex) { message = ex.Message; return false; }
        }
        public bool LoadCalibration()
        {
            try
            {
                string path = CalibrationPath;
                if (!File.Exists(path)) path = Path.Combine(StorageDirectory, HardwareSelected ? "falcon-calibration-hardware.json" : "falcon-calibration-simulation.json");
                var data = JsonUtility.FromJson<CalibrationData>(File.ReadAllText(path));
                if (data == null || (data.version != 1 && data.version != 2)) throw new InvalidOperationException("Format de calibration inconnu.");
                var axes = data.version == 2 && data.hasAlignment ? new FalconAxisAlignment(N(data.right), N(data.up), N(data.towards)) : FalconAxisAlignment.Identity;
                var restoredScale = data.version == 2 ? data.scale : Workspace.UnitsPerMetre;
                bool restoredInvert = data.version == 2 ? data.invert : Workspace.InvertZ;
                _ = new FalconWorkspaceMap(N(data.origin), N(restoredScale), restoredInvert, System.Numerics.Matrix4x4.Identity, axes);
                if (data.useBounds && !new FalconPhysicalBounds(N(data.minimum), N(data.maximum)).Contains(N(data.origin))) throw new ArgumentException("Origine hors limites.");
                if (data.version == 2 && (float.IsNaN(data.rotation.x) || float.IsNaN(data.rotation.y) || float.IsNaN(data.rotation.z) || float.IsNaN(data.rotation.w) ||
                    float.IsInfinity(data.rotation.x) || float.IsInfinity(data.rotation.y) || float.IsInfinity(data.rotation.z) || float.IsInfinity(data.rotation.w) ||
                    Mathf.Abs(Quaternion.Dot(data.rotation, data.rotation) - 1) > .001f)) throw new ArgumentException("Orientation sauvegardée invalide.");
                Workspace.SetCalibration(data.origin, data.useBounds, data.minimum, data.maximum);
                if (data.version == 2 && data.hasAlignment) Workspace.SetAxisAlignment(axes); else Workspace.ClearAxisAlignment();
                Workspace.SetScale(restoredScale, restoredInvert);
                if (data.version == 2) Workspace.transform.localRotation = data.rotation;
                baseScale = restoredScale; uniformScale = baseScale.x; invertZ = restoredInvert;
                if (HardwareSelected) hardwareDirectionsConfirmed = false;
                message = data.version == 1 ? "Ancien fichier chargé : réglez les directions avant tout effort." : "Réglages rechargés. Vérifiez les mouvements puis confirmez les directions.";
                return true;
            }
            catch (Exception ex) { message = ex.Message; return false; }
        }
        public bool ExportDiagnostic()
        {
            try
            {
                var report = new DiagnosticData { version = Application.version, unity = Application.unityVersion,
                    timestampUtc = DateTime.UtcNow.ToString("o"), backend = HardwareSelected ? "Hardware" : "Simulation",
                    state = current.ConnectionState.ToString(), message = current.Status, error = current.EffectError,
                    rawMetres = current.PositionMetres, buttons = current.Buttons, sequence = current.SampleSequence,
                    meanMilliseconds = current.MeanIntervalMilliseconds, maxMilliseconds = current.MaximumIntervalMilliseconds,
                    armed = current.EffectArmed, active = current.EffectActive, contact = current.SurfaceContact,
                    frictionPeakNewtons = current.SurfaceFrictionPeakNewtons };
                Directory.CreateDirectory(StorageDirectory);
                string path = Path.Combine(StorageDirectory, "falcon-diagnostic-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                message = "Diagnostic exporté : " + path;
                return true;
            }
            catch (Exception ex) { message = ex.Message; return false; }
        }

        void ResetObservations()
        {
            initialized = false; buttonsSeen = 0;
            for (int i = 0; i < modesSeen.Length; i++) modesSeen[i] = false;
        }

        [Serializable] sealed class CalibrationData
        { public int version; public Vector3 origin, minimum, maximum, right, up, towards, scale; public bool useBounds, hasAlignment, invert; public Quaternion rotation; }
        [Serializable] sealed class DiagnosticData
        {
            public string version, unity, timestampUtc, backend, state, message, error;
            public Vector3 rawMetres; public uint buttons; public long sequence;
            public double meanMilliseconds, maxMilliseconds; public bool armed, active, contact; public float frictionPeakNewtons;
        }
    }
}


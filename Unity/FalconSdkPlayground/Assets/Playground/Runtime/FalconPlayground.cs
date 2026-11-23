using System;
using System.IO;
using Falcon.Haptics;
using UnityEngine;

namespace Falcon.Playground
{
    public sealed class FalconPlayground : MonoBehaviour
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
        float uniformScale = 10, yaw, motionStarted;
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
        public string CalibrationPath => Path.Combine(StorageDirectory, HardwareSelected ? "falcon-calibration-hardware.json" : "falcon-calibration-simulation.json");

        void Start()
        {
            current = simulation;
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
            Workspace.SetScale(baseScale, invertZ);
            surface.Configure(Workspace, (FalconSurfaceFace)faceIndex);
            ResetObservations();
            message = useHardware ? "Falcon réel sélectionné. Lecture seule ; initialiser et calibrer avant d’armer." : "Simulation sélectionnée. Les forces restent désarmées.";
            return true;
        }

        public void StopAllForces()
        {
            simulation.DisarmEffect(); hardware.DisarmEffect(); surface.Disarm();
            buttons[0] = false;
            message = "Forces désarmées. Relâcher puis réarmer explicitement pour reprendre.";
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
            selectedMode = (int)mode;
            bool result = current.TryArmEffect(mode, intensity);
            message = result ? "Effet préparé. Relâcher puis maintenir le bouton 1." : "Armement refusé : vérifier lecture, limites, capture et focus de la vue Game.";
            return result;
        }
        public bool ArmSurface()
        {
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
                Workspace.transform.localRotation = Quaternion.identity;
                uniformScale = 10; baseScale = Vector3.one * 10; invertZ = true; yaw = 0;
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
                var data = new CalibrationData { version = 1, origin = Workspace.OriginMetres, useBounds = Workspace.UsePhysicalBounds,
                    minimum = Workspace.MinimumMetres, maximum = Workspace.MaximumMetres };
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
                var data = JsonUtility.FromJson<CalibrationData>(File.ReadAllText(CalibrationPath));
                if (data == null || data.version != 1) throw new InvalidOperationException("Format de calibration inconnu.");
                Workspace.SetCalibration(data.origin, data.useBounds, data.minimum, data.maximum);
                message = "Calibration rechargée ; forces désarmées.";
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

        void OnGUI()
        {
            if (current == null) return;
            float width = Mathf.Clamp(Screen.width * .44f, 380, 640);
            GUILayout.BeginArea(new Rect(12, 12, width, Screen.height - 24), GUI.skin.box);
            GUILayout.Label("FALCON SDK · BANC D’ESSAI", GUI.skin.box);
            GUILayout.Label((HardwareSelected ? "FALCON RÉEL" : "SIMULATION") + "  |  " + current.ConnectionState +
                (current.EffectActive ? "  |  EFFORT ACTIF" : current.EffectArmed ? "  |  ARMÉ" : "  |  DÉSARMÉ"));
            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1, .3f, .25f);
            if (GUILayout.Button("COUPER TOUTES LES FORCES", GUILayout.Height(32))) StopAllForces();
            GUI.backgroundColor = previousColor;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Simulation")) SelectBackend(false);
            if (GUILayout.Button("Connecter le Falcon réel")) SelectBackend(true);
            GUILayout.EndHorizontal();
            GUILayout.Label(message);
            GUILayout.Label($"Intensité : {intensity} % · l’armement reste explicite");
            int selectedIntensity = Mathf.RoundToInt(GUILayout.HorizontalSlider(intensity, 0, 100));
            if (selectedIntensity != intensity) { intensity = selectedIntensity; current.SetEffectIntensity(intensity); }
            int selectedTab = GUILayout.Toolbar(tab, tabs);
            if (selectedTab != tab) { StopAllForces(); tab = selectedTab; }
            scroll = GUILayout.BeginScrollView(scroll);
            try
            {
                switch (tab)
                {
                    case 0: DrawInputs(); break;
                    case 1: DrawEffects(); break;
                    case 2: DrawSurface(); break;
                    case 3: DrawCalibration(); break;
                    default: DrawDiagnostics(); break;
                }
                if (current == simulation) DrawSimulation();
            }
            catch (Exception ex) { GUILayout.Label(ex.Message); }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(width + 30, 16, Mathf.Max(200, Screen.width - width - 48), 100), GUI.skin.box);
            GUILayout.Label("Sphère : position mesurée · flèche : normale de contact");
            GUILayout.Label($"Position brute : {(current.PositionMetres * 1000):F1} mm");
            GUILayout.Label(current == simulation ? $"Commande simulée : {current.SimulatedForceNewtons:F4} N" : "La force physique n’est pas mesurée par ce banc.");
            GUILayout.Label($"Contact : {current.SurfaceContact} · pic friction : {current.SurfaceFrictionPeakNewtons:F4} N");
            GUILayout.EndArea();
        }

        void DrawInputs()
        {
            GUILayout.Label("LECTURE ET BOUTONS", GUI.skin.box);
            GUILayout.Label(current.Status);
            GUILayout.Label($"Position physique : {(current.PositionMetres * 1000):F2} mm");
            GUILayout.Label($"Position scène : {Workspace.MappedPositionWorld:F3}");
            GUILayout.Label("Boutons : " + ButtonSummary(current.Buttons));
            GUILayout.Label("Boutons déjà observés : " + ButtonSummary(buttonsSeen));
            var extent = (maximum - minimum) * 1000;
            GUILayout.Label($"Étendue observée : X {extent.x:F1} / Y {extent.y:F1} / Z {extent.z:F1} mm");
            GUILayout.Label("Bouger chaque axe et actionner les quatre boutons. La lecture ne demande aucun armement.");
            if (GUILayout.Button("Réinitialiser les observations")) ResetObservations();
        }

        void DrawEffects()
        {
            GUILayout.Label("SIX EFFETS HISTORIQUES", GUI.skin.box);
            int selected = GUILayout.SelectionGrid(selectedMode, modeNames, 3);
            if (selected != selectedMode) { StopAllForces(); selectedMode = selected; }
            GUILayout.Label(modeHints[selectedMode]);
            if (GUILayout.Button("Préparer cet effet")) ArmMode((HapticMode)selectedMode);
            GUILayout.Label("Relâcher puis maintenir le bouton 1. Le relâchement coupe l’effort.");
            for (int i = 0; i < modesSeen.Length; i++)
                GUILayout.Label((modesSeen[i] ? "✓ " : "○ ") + modeNames[i] + (modesSeen[i] ? " · activation observée" : ""));
            GUILayout.Label("Ces repères suivent le parcours d’essai ; ils ne certifient pas la force physique.");
        }

        void DrawSurface()
        {
            GUILayout.Label("SURFACE ET FRICTION", GUI.skin.box);
            bool enabled = GUILayout.Toggle(surface.enabled, "Activer la géométrie de surface");
            if (enabled != surface.enabled) { StopAllForces(); surface.enabled = enabled; }
            int selected = GUILayout.SelectionGrid(faceIndex, new[] { "+Z", "−Z", "+X", "−X", "+Y", "−Y" }, 6);
            if (selected != faceIndex) SelectFace((FalconSurfaceFace)selected);
            if (GUILayout.Button("Placer la face 6 mm devant l’outil")) PlaceSurfaceAhead();
            GUILayout.Label($"Friction dynamique μ : {surface.FrictionCoefficient:F2}");
            float mu = GUILayout.HorizontalSlider(surface.FrictionCoefficient, 0, 1);
            if (Mathf.Abs(mu - surface.FrictionCoefficient) > .0001f) surface.SetFriction(mu);
            if (GUILayout.Button("Préparer le contact de surface")) ArmSurface();
            GUILayout.Label(surface.Status);
            GUILayout.Label("Commencer devant la face, maintenir le bouton puis avancer. Balayer X/Y pour la friction.");
            GUILayout.Label("Une face à la fois. Le curseur peut pénétrer : il n’y a pas encore de proxy contraint ni d’adhérence statique.");
#if UNITY_EDITOR
            if (GUILayout.Button("Sélectionner la surface dans l’Inspecteur")) UnityEditor.Selection.activeGameObject = surface.gameObject;
#endif
            GUILayout.Label("Dans l’Inspecteur : rigidité, amortissement, plafond, rayon, bord et pénétration maximale. Chaque changement désarme.");
        }

        void DrawCalibration()
        {
            GUILayout.Label("CALIBRATION ET MAPPING", GUI.skin.box);
            GUILayout.Label(Workspace.Status);
            GUILayout.Label($"Origine : {(Workspace.OriginMetres * 1000):F1} mm · lectures capturées : {Workspace.CapturedSamples}");
            if (GUILayout.Button("Recentrer ici")) { Workspace.TryRecenter(); message = Workspace.CaptureStatus; }
            if (!Workspace.IsCapturing)
            {
                if (GUILayout.Button("Commencer la capture des trois axes")) { Workspace.BeginCapture(); message = Workspace.CaptureStatus; }
            }
            else
            {
                GUILayout.Label(Workspace.CaptureStatus);
                if (GUILayout.Button("Terminer et appliquer")) { Workspace.CompleteCapture(); message = Workspace.CaptureStatus; autoMotion = false; }
                if (GUILayout.Button("Annuler la capture")) Workspace.CancelCapture();
            }
            GUILayout.Label("Capturer sans forcer les butées. La boîte est logicielle ; ses coins ne sont pas garantis accessibles.");
            GUILayout.Label($"Échelle : {uniformScale:F1} unités / mètre");
            float scale = GUILayout.HorizontalSlider(uniformScale, 2, 30);
            bool flip = GUILayout.Toggle(invertZ, "Inverser Z");
            if (Mathf.Abs(scale - uniformScale) > .001f || flip != invertZ)
            { uniformScale = scale; invertZ = flip; baseScale = Vector3.one * scale; Workspace.SetScale(baseScale, flip); }
            GUILayout.Label($"Orientation du repère : {yaw:F0}° autour de Y");
            float angle = GUILayout.HorizontalSlider(yaw, -180, 180);
            if (Mathf.Abs(angle - yaw) > .1f) { yaw = angle; Workspace.transform.localRotation = Quaternion.Euler(0, yaw, 0); Workspace.TryGetMapping(out _); }
            if (GUILayout.Button("Retirer les limites logicielles")) Workspace.SetCalibration(Workspace.OriginMetres, false, Vector3.zero, Vector3.zero);
            if (GUILayout.Button("Charger le profil exemple (origine zéro, sans limites)")) Workspace.LoadProfile(exampleCalibration);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Enregistrer")) SaveCalibration();
            if (GUILayout.Button("Recharger")) LoadCalibration();
            GUILayout.EndHorizontal();
            GUILayout.Label("Sauvegardes distinctes pour simulation et matériel. Aucune calibration simulée n’est chargée automatiquement sur le Falcon.");
            GUILayout.Label(CalibrationPath);
#if UNITY_EDITOR
            if (GUILayout.Button("Sélectionner le Workspace dans l’Inspecteur")) UnityEditor.Selection.activeGameObject = Workspace.gameObject;
#endif
        }

        void DrawDiagnostics()
        {
            GUILayout.Label("DIAGNOSTIC ET CYCLE DE VIE", GUI.skin.box);
            GUILayout.Label($"État : {current.ConnectionState} · échantillon {current.SampleSequence}");
            GUILayout.Label($"Âge logiciel : {current.SampleAgeSeconds * 1000:F1} ms");
            GUILayout.Label($"Intervalles moyen / max : {current.MeanIntervalMilliseconds:F2} / {current.MaximumIntervalMilliseconds:F2} ms");
            GUILayout.Label("Ce compteur ne valide pas une cadence de 1 kHz.");
            GUILayout.Label("Dernière erreur : " + (string.IsNullOrEmpty(current.EffectError) ? "aucune" : current.EffectError));
            if (GUILayout.Button("Relancer la lecture (désarme)")) RestartReading();
            if (GUILayout.Button("Exporter le diagnostic JSON")) ExportDiagnostic();
            GUILayout.Label("À vérifier : relâchement du bouton 1, perte de focus, pause de Play, sortie de Play, recompilation et déconnexion.");
            GUILayout.Label("Disponible : six effets, une face statique, friction dynamique, calibration et mapping. Non implémenté : mélange d’effets, adhérence statique, objets mobiles et contacts multiples.");
            if (HardwareSelected)
                GUILayout.Label("Prérequis : Windows x64, SDK DHD et pilote WinUSB installés. Arrêter Falcon Diagnostics avant de connecter. Après initialisation, commencer en lecture puis à faible intensité.");
        }

        void DrawSimulation()
        {
            GUILayout.Space(12);
            GUILayout.Label("COMMANDES DU SIMULATEUR", GUI.skin.box);
            bool nextAvailable = GUILayout.Toggle(available, "Périphérique simulé disponible (décocher = déconnexion)");
            if (available != nextAvailable) SetSimulationAvailability(nextAvailable);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 4; i++) buttons[i] = GUILayout.Toggle(buttons[i], "Bouton " + (i + 1));
            GUILayout.EndHorizontal();
            bool nextAuto = GUILayout.Toggle(autoMotion, "Balayage automatique en simulation uniquement");
            if (nextAuto != autoMotion) { autoMotion = nextAuto; motionStarted = Time.unscaledTime; }
            if (!autoMotion)
            {
                position.x = AxisSlider("X", position.x); position.y = AxisSlider("Y", position.y); position.z = AxisSlider("Z", position.z);
            }
            if (GUILayout.Button("Réinitialiser la simulation / revenir devant la face")) ResetSimulation();
        }
        static float AxisSlider(string name, float value)
        { GUILayout.Label($"{name} brut : {value * 1000:F1} mm"); return GUILayout.HorizontalSlider(value, -.04f, .04f); }
        static string ButtonSummary(uint mask) => $"1:{((mask & 1) != 0 ? "●" : "○")}  2:{((mask & 2) != 0 ? "●" : "○")}  3:{((mask & 4) != 0 ? "●" : "○")}  4:{((mask & 8) != 0 ? "●" : "○")}";

        [Serializable] sealed class CalibrationData { public int version; public Vector3 origin, minimum, maximum; public bool useBounds; }
        [Serializable] sealed class DiagnosticData
        {
            public string version, unity, timestampUtc, backend, state, message, error;
            public Vector3 rawMetres; public uint buttons; public long sequence;
            public double meanMilliseconds, maxMilliseconds; public bool armed, active, contact; public float frictionPeakNewtons;
        }
    }
}

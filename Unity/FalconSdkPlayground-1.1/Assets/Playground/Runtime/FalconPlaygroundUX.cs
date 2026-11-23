using System;
using Falcon.Haptics;
using UnityEngine;

namespace Falcon.Playground
{
    public sealed partial class FalconPlayground
    {
        bool simulationDirectionsConfirmed = true, hardwareDirectionsConfirmed;
        int page, gestureStep, experiment;
        bool recordingGesture, advanced, showCapture, moreEffects;
        Vector3 gestureStart;
        readonly Vector3[] gestures = new Vector3[3];
        readonly string[] gestureNames = { "vers votre droite", "vers le haut", "vers vous" };
        readonly string[] experiments = { "Surface + friction", "Ressort", "Eau", "Résistance", "Gravier", "Moteur", "Paroi du pilote" };
        readonly HapticMode[] experimentModes = { HapticMode.Spring, HapticMode.Spring, HapticMode.Water, HapticMode.Resistance,
            HapticMode.Gravel, HapticMode.EngineLever, HapticMode.Brick };
        GUISkin readableSkin;
        Texture2D panelTexture;
        GUIStyle titleStyle, noteStyle, cardStyle;
        public bool DirectionsConfirmed => Workspace != null && Workspace.HasAxisAlignment && (HardwareSelected ? hardwareDirectionsConfirmed : simulationDirectionsConfirmed);
        public int DirectionStep => gestureStep;

        public void ResetDirectionWizard()
        { recordingGesture = false; gestureStep = 0; for (int i = 0; i < 3; i++) gestures[i] = Vector3.zero; }
        public bool BeginDirectionGesture()
        {
            StopAllForces(); autoMotion = false;
            if (gestureStep >= 3 || !TryRaw(out gestureStart)) { message = "Une lecture récente est nécessaire."; return false; }
            if (HardwareSelected) hardwareDirectionsConfirmed = false; else simulationDirectionsConfirmed = false;
            recordingGesture = true;
            message = "Déplacez doucement la poignée " + gestureNames[gestureStep] + ", puis validez le geste.";
            return true;
        }
        public bool EndDirectionGesture()
        {
            if (!recordingGesture || !TryRaw(out var end)) { message = "Commencez par le bouton Départ du geste."; return false; }
            var delta = end - gestureStart;
            if (delta.magnitude < .005f) { message = "Geste trop court. Continuez doucement dans la direction demandée (au moins 5 mm)."; return false; }
            gestures[gestureStep] = delta;
            if (gestureStep == 2)
            {
                if (!FalconAxisAlignment.TryFromGestures(N(gestures[0]), N(gestures[1]), N(gestures[2]), out var alignment, out var error))
                { recordingGesture = false; gestureStep = 0; message = error; return false; }
                Workspace.SetAxisAlignment(alignment);
                // The guided frame is screen-facing. Do not retain an old rotated or inverted frame.
                Workspace.transform.localRotation = Quaternion.identity;
                Workspace.SetScale(Vector3.one * uniformScale, true);
                invertZ = true; baseScale = Vector3.one * uniformScale;
            }
            recordingGesture = false; gestureStep++;
            if (HardwareSelected) hardwareDirectionsConfirmed = false; else simulationDirectionsConfirmed = false;
            message = gestureStep == 3 ? "Les directions sont calculées. Vérifiez les mouvements puis confirmez." : "Geste enregistré. Revenez dans une position confortable pour le suivant.";
            return true;
        }
        public bool ConfirmDirections()
        {
            StopAllForces();
            if (recordingGesture || (gestureStep > 0 && gestureStep < 3) || !Workspace.HasAxisAlignment || !Workspace.IsValid || !TryRaw(out _))
            { message = "Enregistrez d’abord les trois gestes ou rechargez un réglage de directions valide."; return false; }
            // Recentering must not fail because an old raw bounding box belongs to another setup.
            Workspace.SetCalibration(Workspace.OriginMetres, false, Vector3.zero, Vector3.zero);
            Workspace.transform.localRotation = Quaternion.identity;
            Workspace.SetScale(Workspace.UnitsPerMetre, true);
            if (!Workspace.TryRecenter()) { message = Workspace.CaptureStatus; return false; }
            if (HardwareSelected) hardwareDirectionsConfirmed = true; else simulationDirectionsConfirmed = true;
            bool saved = SaveCalibration();
            string saveError = message;
            page = 2;
            message = saved ? "Directions confirmées et centre enregistré. Vous pouvez choisir un essai." : "Directions confirmées pour cette session. Sauvegarde impossible : " + saveError;
            return true;
        }
        bool TryRaw(out Vector3 raw)
        {
            raw = default;
            if (current == null || !current.TryGetSample(out var sample, out _)) return false;
            raw = new Vector3((float)sample.X, (float)sample.Y, (float)sample.Z); return true;
        }
        public Vector3 AlignedPositionMetres
        {
            get
            {
                if (!TryRaw(out var raw)) return Vector3.zero;
                var alignment = Workspace.AxisAlignment; var relative = N(raw - Workspace.OriginMetres);
                return new Vector3(System.Numerics.Vector3.Dot(relative, alignment.Right), System.Numerics.Vector3.Dot(relative, alignment.Up),
                    System.Numerics.Vector3.Dot(relative, alignment.TowardsUser));
            }
        }

        void EnsureReadableSkin()
        {
            if (readableSkin != null) return;
            readableSkin = Instantiate(GUI.skin);
            readableSkin.label.fontSize = 17; readableSkin.label.wordWrap = true;
            readableSkin.label.normal.textColor = new Color(.9f, .94f, 1);
            readableSkin.button.fontSize = 17; readableSkin.button.wordWrap = true;
            readableSkin.button.padding = new RectOffset(12, 12, 10, 10);
            readableSkin.toggle.fontSize = 17; readableSkin.toggle.wordWrap = true;
            readableSkin.toggle.padding = new RectOffset(22, 4, 7, 7);
            panelTexture = new Texture2D(1, 1); panelTexture.SetPixel(0, 0, new Color(.06f, .085f, .12f)); panelTexture.Apply();
            cardStyle = new GUIStyle(readableSkin.box) { padding = new RectOffset(18, 18, 16, 16) };
            cardStyle.normal.background = panelTexture;
            titleStyle = new GUIStyle(readableSkin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
            noteStyle = new GUIStyle(readableSkin.label) { fontSize = 14 };
            noteStyle.normal.textColor = new Color(.65f, .75f, .87f);
        }
        void OnDestroy()
        { if (readableSkin != null) Destroy(readableSkin); if (panelTexture != null) Destroy(panelTexture); }

        void OnGUI()
        {
            if (current == null) return;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { StopAllForces(); Event.current.Use(); }
            EnsureReadableSkin(); var oldSkin = GUI.skin; GUI.skin = readableSkin;
            float width = Mathf.Min(450, Screen.width * .4f);
            var camera = Camera.main;
            if (camera != null)
            {
                float bottom = current == simulation ? 250 : 20;
                camera.rect = new Rect((width + 24) / Screen.width, bottom / Screen.height,
                    1 - (width + 24) / Screen.width, Mathf.Max(.1f, (Screen.height - bottom - 190) / Screen.height));
            }
            GUILayout.BeginArea(new Rect(12, 12, width, Screen.height - 24), cardStyle);
            GUILayout.Label("Falcon · prise en main", titleStyle);
            GUILayout.Label((HardwareSelected ? "Falcon réel" : "Simulation") + "  ·  " + FriendlyState(), noteStyle);
            var oldColor = GUI.backgroundColor; GUI.backgroundColor = new Color(.9f, .27f, .24f);
            if (GUILayout.Button("ARRÊTER LES FORCES  ·  Échap")) StopAllForces();
            GUI.backgroundColor = oldColor;
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            foreach (var item in new[] { "1 Appareil", "2 Directions", "3 Essayer" })
            {
                int index = item[0] - '1';
                if (GUILayout.Button((page == index ? "● " : "") + item))
                { NavigateTo(index); }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            scroll = GUILayout.BeginScrollView(scroll);
            if (page == 0) DevicePage(); else if (page == 1) DirectionsPage(); else TryPage();
            GUILayout.Space(12);
            GUILayout.Label(message, noteStyle);
            GUILayout.Space(12);
            advanced = GUILayout.Toggle(advanced, "Réglages avancés et diagnostic");
            if (advanced) AdvancedPage();
            GUILayout.EndScrollView(); GUILayout.EndArea();

            float x = width + 44;
            GUILayout.BeginArea(new Rect(x, 18, Screen.width - x - 20, 170), cardStyle);
            GUILayout.Label(current.EffectActive ? "Effort actif · relâcher coupe" : "Déplacement de la poignée", titleStyle);
            var aligned = AlignedPositionMetres * 1000;
            GUILayout.Label($"Droite / gauche   {aligned.x:F1} mm     ·     Haut / bas   {aligned.y:F1} mm");
            GUILayout.Label($"Vers vous / loin de vous   {aligned.z:F1} mm", noteStyle);
            if (!DirectionsConfirmed) GUILayout.Label("Les directions restent à régler avant d’activer un effort.");
            else GUILayout.Label(current.SurfaceContact ? "Contact avec la surface" : "Déplacement libre", noteStyle);
            GUILayout.EndArea();
            if (current == simulation)
            {
                GUILayout.BeginArea(new Rect(x, Mathf.Max(200, Screen.height - 240), Screen.width - x - 20, 220), cardStyle);
                GUILayout.Label("Déplacer le simulateur", titleStyle);
                SimulationDirections();
                GUILayout.EndArea();
            }
            GUI.skin = oldSkin;
        }
        string FriendlyState()
        {
            if (current.EffectActive) return "effort activé";
            if (current.EffectArmed) return "prêt · maintenez le bouton 1";
            switch (current.ConnectionState)
            {
                case FalconConnectionState.Ready: return "connecté · aucun effort";
                case FalconConnectionState.Connecting: return "connexion en cours";
                case FalconConnectionState.Stale: return "lecture trop ancienne";
                case FalconConnectionState.Faulted: return "connexion à vérifier";
                case FalconConnectionState.Stopping: return "arrêt en cours";
                default: return "lecture arrêtée";
            }
        }
        void NavigateTo(int destination)
        { StopAllForces(); page = destination; scroll = Vector2.zero; GUIUtility.ExitGUI(); }
        void DevicePage()
        {
            GUILayout.Label("Choisir votre appareil", titleStyle);
            GUILayout.Label("Sans Falcon, utilisez la simulation. Avec le Falcon, commencez par régler ses directions.");
            if (GUILayout.Button("Essayer sans appareil\nSimulation") && SelectBackend(false)) NavigateTo(2);
            if (GUILayout.Button("Utiliser mon Falcon\nConnexion en lecture seule") && SelectBackend(true)) NavigateTo(1);
            if (HardwareSelected && !current.Connected)
                GUILayout.Label("Branchez l’alimentation et l’USB. Le SDK DHD et son pilote doivent être installés. Arrêtez tout autre lecteur du Falcon.");
            if (current.Connected)
            {
                GUILayout.Label("La position est reçue. Aucun effort n’est activé automatiquement.");
                if (GUILayout.Button(DirectionsConfirmed ? "Continuer vers les essais" : "Régler les directions")) NavigateTo(DirectionsConfirmed ? 2 : 1);
            }
        }
        void DirectionsPage()
        {
            GUILayout.Label("Faire correspondre la poignée et l’écran", titleStyle);
            GUILayout.Label("Ce réglage se fait sans force. Il identifie les axes du pilote à partir de vos gestes, sans supposer que X signifie droite.");
            if (!current.Connected) { GUILayout.Label("Connectez d’abord l’appareil."); return; }
            if (gestureStep < 3)
            {
                GUILayout.Space(8);
                GUILayout.Label($"Geste {gestureStep + 1} sur 3 : {gestureNames[gestureStep]}", titleStyle);
                GUILayout.Label(recordingGesture ? "Déplacez doucement, sans forcer les butées, puis validez." : "Placez la poignée confortablement. Cliquez sur Départ, puis faites uniquement le geste demandé.");
                if (!recordingGesture)
                { if (GUILayout.Button("Départ du geste")) BeginDirectionGesture(); }
                else
                { if (GUILayout.Button("Valider ce geste")) EndDirectionGesture(); }
                GUILayout.Label("Un déplacement de 5 mm minimum suffit. Si les gestes se mélangent, le réglage est refusé.", noteStyle);
            }
            if (gestureStep == 3 || (gestureStep == 0 && !recordingGesture && Workspace.HasAxisAlignment))
            {
                GUILayout.Space(8);
                GUILayout.Label("Vérifier : droite → droite, haut → haut. La profondeur est indiquée en millimètres.");
                GUILayout.Label("Revenez dans une position confortable, puis confirmez et centrez.");
                if (GUILayout.Button("Les directions sont correctes · centrer") && ConfirmDirections()) GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("Recommencer les trois gestes")) { StopAllForces(); ResetDirectionWizard(); }
            if (GUILayout.Button("Recharger mes derniers réglages")) LoadCalibration();
        }
        void TryPage()
        {
            GUILayout.Label("Choisir un essai", titleStyle);
            if (!DirectionsConfirmed)
            {
                GUILayout.Label("Avant les forces, vérifiez la correspondance des mouvements.");
                if (GUILayout.Button("Régler les directions")) NavigateTo(1);
                return;
            }
            GUILayout.Label("Le bouton de la poignée garde le contrôle : relâcher coupe l’effort.");
            bool showAll = GUILayout.Toggle(moreEffects, "Afficher les autres effets");
            if (showAll != moreEffects) { moreEffects = showAll; if (!showAll && experiment > 2) { StopAllForces(); experiment = 0; } }
            int selected = GUILayout.SelectionGrid(experiment, moreEffects ? experiments : new[] { "Surface + friction", "Ressort", "Eau" }, 1);
            if (selected != experiment) { StopAllForces(); experiment = selected; surface.enabled = true; }
            GUILayout.Space(8);
            if (experiment == 0)
            {
                if (GUILayout.Button("Placer la surface devant la poignée")) { surface.enabled = true; PlaceSurfaceAhead(); }
                GUILayout.Label("Poussez ensuite doucement vers la surface. Glissez sur le côté pour sentir la friction.");
                bool friction = GUILayout.Toggle(surface.FrictionCoefficient > 0, "Frottement de surface");
                if (friction != (surface.FrictionCoefficient > 0)) { StopAllForces(); surface.SetFriction(friction ? .5f : 0); }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Souple")) surface.SetMaterial(40, 1, .6f);
                if (GUILayout.Button("Plus ferme")) surface.SetMaterial(80, 1.5f, .6f);
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label(modeHints[(int)experimentModes[experiment]]);
            GUILayout.Label($"Intensité : {intensity} %");
            int nextGain = Mathf.RoundToInt(GUILayout.HorizontalSlider(intensity, 0, 100));
            if (nextGain != intensity) { intensity = nextGain; current.SetEffectIntensity(intensity); }
            bool oldEnabled = GUI.enabled; GUI.enabled = oldEnabled && current.Connected && !Workspace.IsCapturing;
            if (GUILayout.Button("Activer cet essai"))
            { if (experiment == 0) ArmSurface(); else ArmMode(experimentModes[experiment]); }
            GUI.enabled = oldEnabled;
            GUILayout.Label(current.EffectArmed ? "Relâchez puis maintenez le bouton 1 pour l’effort." : "L’effort reste désactivé tant que vous n’avez pas activé l’essai.", noteStyle);
            if (GUILayout.Button("Centrer le curseur ici")) { StopAllForces(); Workspace.TryRecenter(); }
            if (!string.IsNullOrEmpty(current.EffectError)) GUILayout.Label("Essai arrêté : " + current.EffectError);
        }
        void SimulationDirections()
        {
            var axes = Workspace.AxisAlignment;
            var relative = N(position - Workspace.OriginMetres);
            var local = new Vector3(System.Numerics.Vector3.Dot(relative, axes.Right), System.Numerics.Vector3.Dot(relative, axes.Up), System.Numerics.Vector3.Dot(relative, axes.TowardsUser));
            GUILayout.Label("Gauche  ←  →  Droite", noteStyle); local.x = GUILayout.HorizontalSlider(local.x, -.04f, .04f);
            GUILayout.Label("Bas  ←  →  Haut", noteStyle); local.y = GUILayout.HorizontalSlider(local.y, -.04f, .04f);
            GUILayout.Label("Loin de vous  ←  →  Vers vous", noteStyle); local.z = GUILayout.HorizontalSlider(local.z, -.04f, .04f);
            position = Workspace.OriginMetres + U(axes.Right * local.x + axes.Up * local.y + axes.TowardsUser * local.z);
            buttons[0] = GUILayout.Toggle(buttons[0], "Maintenir le bouton 1 simulé");
        }
        void AdvancedPage()
        {
            GUILayout.Label("Les données brutes sont celles du pilote, pas les directions de l’écran.", noteStyle);
            GUILayout.Label($"Brut : {(current.PositionMetres * 1000):F1} mm", noteStyle);
            GUILayout.Label($"Lecture : {current.SampleSequence} · moyenne {current.MeanIntervalMilliseconds:F1} ms · max {current.MaximumIntervalMilliseconds:F1} ms", noteStyle);
            if (current == simulation)
            {
                bool nextAvailable = GUILayout.Toggle(available, "Appareil simulé disponible");
                if (available != nextAvailable) SetSimulationAvailability(nextAvailable);
                GUILayout.BeginHorizontal();
                for (int i = 1; i < 4; i++) buttons[i] = GUILayout.Toggle(buttons[i], "Bouton " + (i + 1));
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Réinitialiser le simulateur")) ResetSimulation();
            }
            showCapture = GUILayout.Toggle(showCapture, "Zone physique, échelle et sauvegarde");
            if (showCapture)
            {
                GUILayout.Label(Workspace.CaptureStatus, noteStyle);
                if (!Workspace.IsCapturing)
                { if (GUILayout.Button("Capturer la zone sur les trois axes")) Workspace.BeginCapture(); }
                else
                {
                    GUILayout.Label($"{Workspace.CapturedSamples} lectures enregistrées");
                    if (GUILayout.Button("Terminer la capture")) Workspace.CompleteCapture();
                    if (GUILayout.Button("Annuler la capture")) Workspace.CancelCapture();
                }
                GUILayout.Label("La boîte est une limite logicielle, pas la course mécanique du Falcon.", noteStyle);
                if (GUILayout.Button("Retirer les limites logicielles")) Workspace.SetCalibration(Workspace.OriginMetres, false, Vector3.zero, Vector3.zero);
                GUILayout.Label($"Taille d’affichage : {uniformScale:F1} unités / mètre", noteStyle);
                float scale = GUILayout.HorizontalSlider(uniformScale, 2, 30);
                if (Mathf.Abs(scale - uniformScale) > .001f)
                { uniformScale = scale; baseScale = Vector3.one * scale; Workspace.SetScale(baseScale, true); }
            }
            if (GUILayout.Button("Enregistrer mes réglages")) SaveCalibration();
            if (GUILayout.Button("Charger le profil exemple · directions à vérifier"))
            {
                StopAllForces(); Workspace.LoadProfile(exampleCalibration);
                if (HardwareSelected) hardwareDirectionsConfirmed = false; else simulationDirectionsConfirmed = false;
                ResetDirectionWizard(); NavigateTo(1);
            }
            if (GUILayout.Button("Exporter un diagnostic")) ExportDiagnostic();
            if (GUILayout.Button("Relancer la lecture")) RestartReading();
            if (experiment == 0)
            {
                int face = GUILayout.SelectionGrid(faceIndex, new[] { "+Z", "−Z", "+X", "−X", "+Y", "−Y" }, 3);
                if (face != faceIndex) SelectFace((FalconSurfaceFace)face);
                GUILayout.Label($"Friction : {surface.FrictionCoefficient:F2} · pic commandé : {current.SurfaceFrictionPeakNewtons:F3} N", noteStyle);
                float mu = GUILayout.HorizontalSlider(surface.FrictionCoefficient, 0, 1);
                if (Mathf.Abs(mu - surface.FrictionCoefficient) > .001f) surface.SetFriction(mu);
                GUILayout.Label($"Rigidité : {surface.Stiffness:F0} N/m · amortissement : {surface.Damping:F1}", noteStyle);
                float k = GUILayout.HorizontalSlider(surface.Stiffness, 0, 150);
                float d = GUILayout.HorizontalSlider(surface.Damping, 0, 5);
                if (Mathf.Abs(k - surface.Stiffness) > .01f || Mathf.Abs(d - surface.Damping) > .01f) surface.SetMaterial(k, d, surface.MaximumForceNewtons);
            }
            GUILayout.Label("Une face statique. Pas d’adhérence statique, de corps mobiles ou de contacts multiples.", noteStyle);
        }
        static System.Numerics.Vector3 N(Vector3 value) => new System.Numerics.Vector3(value.x, value.y, value.z);
        static Vector3 U(System.Numerics.Vector3 value) => new Vector3(value.X, value.Y, value.Z);
    }
}

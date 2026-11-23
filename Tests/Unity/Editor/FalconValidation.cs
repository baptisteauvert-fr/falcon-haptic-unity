using System;
using Falcon.Haptics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in an isolated Unity project referencing the local package, never in a user scene.
[InitializeOnLoad]
static class FalconValidation
{
    const string Prefix = "Falcon.Validation.";

    static FalconValidation() { EditorApplication.update += Tick; }

    public static void Run()
    {
        SessionState.SetBool(Prefix + "OriginalEnabled", EditorSettings.enterPlayModeOptionsEnabled);
        SessionState.SetInt(Prefix + "OriginalOptions", (int)EditorSettings.enterPlayModeOptions);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var input = new GameObject("Simulated Falcon validation").AddComponent<FalconInput>();
        var settings = new SerializedObject(input);
        settings.FindProperty("backend").enumValueIndex = (int)FalconBackendMode.Simulation;
        var parent = new GameObject("Workspace parent").transform;
        parent.localScale = new Vector3(.7f, 1.2f, 2);
        parent.rotation = Quaternion.Euler(10, 30, -15);
        input.transform.SetParent(parent, false);
        input.transform.localPosition = new Vector3(1, 2, 3);
        input.transform.localRotation = Quaternion.Euler(20, 15, 10);
        var cursor = new GameObject("Validation cursor").transform;
        cursor.SetParent(new GameObject("Cursor parent").transform, false);
        cursor.parent.localScale = new Vector3(2, 3, 4);
        settings.FindProperty("cursor").objectReferenceValue = cursor;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var workspace = input.gameObject.AddComponent<FalconWorkspace>();
        const string profilePath = "Assets/FalconValidationCalibration.asset";
        var profile = AssetDatabase.LoadAssetAtPath<FalconCalibrationProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<FalconCalibrationProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }
        profile.SetCalibration(new Vector3(.001f, .002f, .003f), true,
            new Vector3(-.04f, -.05f, -.06f), new Vector3(.04f, .05f, .06f));
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        workspace.LoadProfile(profile);
        workspace.SetScale(new Vector3(10, 20, 30), true);
        var wall = new GameObject("Validation surface");
        wall.transform.position = workspace.transform.position;
        wall.AddComponent<BoxCollider>().size = Vector3.one * .3f;
        wall.AddComponent<FalconSurface>().Configure(workspace);
        EditorSceneManager.SaveScene(input.gameObject.scene, "Assets/FalconValidation.unity");
        SessionState.SetInt(Prefix + "Cycle", 0);
        SessionState.SetBool(Prefix + "Running", true);
        BeginCycle();
    }

    static void BeginCycle()
    {
        int cycle = SessionState.GetInt(Prefix + "Cycle", 0);
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions =
            ((cycle & 1) != 0 ? EnterPlayModeOptions.DisableDomainReload : EnterPlayModeOptions.None) |
            ((cycle & 2) != 0 ? EnterPlayModeOptions.DisableSceneReload : EnterPlayModeOptions.None);
        SessionState.SetInt(Prefix + "Stage", 0);
        SessionState.SetFloat(Prefix + "Deadline", (float)EditorApplication.timeSinceStartup + 90);
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Prefix + "Running", false)) return;
        try
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Prefix + "Deadline", 0))
                throw new Exception("Play Mode validation timed out, stage " + SessionState.GetInt(Prefix + "Stage", 0));
            int stage = SessionState.GetInt(Prefix + "Stage", 0);
            if (stage == 4)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!FalconSession.StopAll() || FalconSession.ActiveSessionCount != 0)
                    throw new Exception("Session survived Play Mode exit.");
                int cycle = SessionState.GetInt(Prefix + "Cycle", 0);
                Debug.Log("FALCON PLAY PASS options=" + cycle);
                if (++cycle == 4) { Finish(0); return; }
                SessionState.SetInt(Prefix + "Cycle", cycle);
                BeginCycle();
                return;
            }
            if (!EditorApplication.isPlaying) return;
            var input = UnityEngine.Object.FindObjectOfType<FalconInput>();
            if (input == null) throw new Exception("Simulation component missing.");
            var workspace = input.GetComponent<FalconWorkspace>();
            if (workspace == null) throw new Exception("Workspace component missing.");
            if (stage == 0)
            {
                if (!input.Connected || input.SampleSequence < 4) return;
                if (!input.IsSimulation) throw new Exception("Hardware backend selected.");
                workspace.LoadProfile(AssetDatabase.LoadAssetAtPath<FalconCalibrationProfile>("Assets/FalconValidationCalibration.asset"));
                workspace.SetScale(new Vector3(10, 20, 30), true);
                var surface = GameObject.Find("Validation surface").GetComponent<FalconSurface>();
                if (!surface.TryGetGeometry(out _)) throw new Exception("Play surface export failed: " + surface.Status);
                var surfaceSession = (FalconSession)typeof(FalconInput).GetField("session", System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance).GetValue(input);
                if (!surfaceSession.TryArmSurface()) throw new Exception("Published surface cannot be prepared in Play.");
                surface.SetFriction(surface.FrictionCoefficient == 0 ? .5f : 0);
                if (surfaceSession.State.EffectArmed) throw new Exception("Friction change did not disarm the published surface.");
                surfaceSession.Disarm(); // No button press or force activation in this editor regression check.
                input.SetSimulationSample(new Vector3(.01f, .02f, .03f), 8);
                SessionState.SetInt(Prefix + "Stage", 1);
            }
            else if (stage == 1)
            {
                if (input.Buttons != 8 || Vector3.Distance(input.PositionMetres, new Vector3(.01f, .02f, .03f)) > .00001f) return;
                var expected = workspace.transform.TransformPoint(Vector3.Scale(input.PositionMetres - workspace.OriginMetres, new Vector3(10, 20, -30)));
                var cursor = GameObject.Find("Validation cursor").transform;
                if (Vector3.Distance(cursor.position, expected) > .00001f) throw new Exception("World mapping or cursor parent handling incorrect.");
                var session = (FalconSession)typeof(FalconInput).GetField("session", System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance).GetValue(input);
                if (!session.TryArmEffect(HapticMode.Spring)) throw new Exception("Cannot prepare simulated recenter test.");
                if (!workspace.TryRecenter() || session.State.EffectArmed) throw new Exception("Recenter did not disarm.");
                if (!workspace.TryMapPosition(input.PositionMetres, out var recentered) || Vector3.Distance(recentered, workspace.transform.position) > .00001f)
                    throw new Exception("Recenter origin incorrect.");
                if (!session.TryArmEffect(HapticMode.Spring)) throw new Exception("Cannot prepare simulated frame test.");
                workspace.transform.position += Vector3.right;
                workspace.TryGetMapping(out _);
                if (session.State.EffectArmed) throw new Exception("Frame change did not disarm.");
                workspace.LoadProfile(workspace.CalibrationProfile);
                if (workspace.OriginMetres != new Vector3(.001f, .002f, .003f)) throw new Exception("Saved profile did not reload.");
                if (!workspace.BeginCapture()) throw new Exception("Cannot start calibration capture.");
                SessionState.SetInt(Prefix + "CaptureStep", 0);
                SessionState.SetInt(Prefix + "Stage", 5);
            }
            else if (stage == 5)
            {
                int step = SessionState.GetInt(Prefix + "CaptureStep", 0);
                var position = new Vector3(-.02f + step * .0021f, -.03f + step * .0031f, -.04f + step * .0041f);
                input.SetSimulationSample(position, 0);
                if (Vector3.Distance(input.PositionMetres, position) > .000001f || workspace.CapturedSamples < step + 1) return;
                if (++step < 20) { SessionState.SetInt(Prefix + "CaptureStep", step); return; }
                if (!workspace.CompleteCapture() || !workspace.UsePhysicalBounds || workspace.IsCapturing)
                    throw new Exception("Capture could not complete: " + workspace.CaptureStatus);
                var saved = ScriptableObject.CreateInstance<FalconCalibrationProfile>();
                workspace.CopyCalibrationTo(saved);
                if (saved.OriginMetres != workspace.OriginMetres || !saved.UsePhysicalBounds) throw new Exception("Profile copy failed.");
                UnityEngine.Object.DestroyImmediate(saved);
                input.enabled = false;
                if (input.ConnectionState != FalconConnectionState.Stopped || FalconSession.ActiveSessionCount != 0)
                    throw new Exception("Disable did not stop the session.");
                input.enabled = true;
                SessionState.SetInt(Prefix + "Stage", 2);
            }
            else if (stage == 2)
            {
                if (!input.Connected || input.SampleSequence < 4) return;
                EditorApplication.isPaused = true;
                SessionState.SetInt(Prefix + "Stage", 3);
            }
            else if (stage == 3)
            {
                if (input.EffectArmed || input.EffectActive) throw new Exception("Pause retained armed output.");
                EditorApplication.isPaused = false;
                SessionState.SetInt(Prefix + "Stage", 4);
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception ex) { Debug.LogException(ex); Finish(1); }
    }

    static void Finish(int exitCode)
    {
        SessionState.SetBool(Prefix + "Running", false);
        FalconSession.StopAll();
        EditorApplication.isPaused = false;
        EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Prefix + "OriginalEnabled", false);
        EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Prefix + "OriginalOptions", 0);
        if (exitCode == 0) Debug.Log("FALCON UNITY VALIDATION PASS: four Play Mode configurations, no hardware.");
        EditorApplication.Exit(exitCode);
    }
}

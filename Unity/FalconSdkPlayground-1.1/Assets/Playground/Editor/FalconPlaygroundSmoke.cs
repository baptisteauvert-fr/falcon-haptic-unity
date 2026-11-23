using System;
using System.IO;
using Falcon.Haptics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Falcon.Playground.Editor
{
    [InitializeOnLoad]
    public static class FalconPlaygroundSmoke
    {
        const string Prefix = "Falcon.Playground.Smoke.";
        static FalconPlaygroundSmoke() { EditorApplication.update += Tick; }
        public static void Run()
        {
            FalconPlaygroundBuilder.ValidateScene();
            var panel = UnityEngine.Object.FindFirstObjectByType<FalconPlayground>();
            var settings = new SerializedObject(panel);
            settings.FindProperty("storageDirectoryOverride").stringValue = Path.GetFullPath("Temp/PlaygroundSmoke");
            settings.ApplyModifiedPropertiesWithoutUndo();
            SessionState.SetBool(Prefix + "Running", true);
            SessionState.SetInt(Prefix + "Stage", 0);
            SessionState.SetFloat(Prefix + "Deadline", (float)EditorApplication.timeSinceStartup + 120);
            EditorApplication.isPlaying = true;
        }
        static void Require(bool condition, string error) { if (!condition) throw new Exception(error); }
        static void Tick()
        {
            if (!SessionState.GetBool(Prefix + "Running", false)) return;
            try
            {
                Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(Prefix + "Deadline", 0), "Playground test timed out.");
                int stage = SessionState.GetInt(Prefix + "Stage", 0);
                if (stage == 8)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    Require(FalconSession.StopAll() && FalconSession.ActiveSessionCount == 0, "Worker survived Play exit.");
                    var stoppedPanel = UnityEngine.Object.FindFirstObjectByType<FalconPlayground>();
                    var so = new SerializedObject(stoppedPanel); so.FindProperty("storageDirectoryOverride").stringValue = "";
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.SaveScene(stoppedPanel.gameObject.scene);
                    SessionState.SetBool(Prefix + "Running", false);
                    Debug.Log("FALCON PLAYGROUND SMOKE PASS: inputs, calibration, JSON, faces, friction, reconnect and restart; no hardware.");
                    EditorApplication.Exit(0); return;
                }
                if (!EditorApplication.isPlaying) return;
                var panel = UnityEngine.Object.FindFirstObjectByType<FalconPlayground>();
                if (panel == null || panel.ActiveInput == null) return;
                var input = panel.ActiveInput;
                Require(!panel.HardwareSelected, "Hardware selected during simulation test.");
                var hardware = (FalconInput)new SerializedObject(panel).FindProperty("hardware").objectReferenceValue;
                Require(!hardware.gameObject.activeSelf, "Hardware object activated automatically.");
                if (stage == 0)
                {
                    if (!input.Connected || input.SampleSequence < 4) return;
                    Require(input.IsSimulation && !input.EffectArmed && !input.EffectActive, "Unsafe simulation startup.");
                    panel.SetSimulation(Vector3.zero, 0);
                    SessionState.SetInt(Prefix + "Stage", 9);
                }
                else if (stage >= 9 && stage <= 12)
                {
                    var points = new[] { Vector3.zero, new Vector3(0, .02f, 0), new Vector3(0, .02f, .02f), new Vector3(.02f, .02f, .02f) };
                    if (Vector3.Distance(input.PositionMetres, points[stage - 9]) > .000001f) return;
                    if (stage > 9) Require(panel.EndDirectionGesture(), "Gesture rejected: " + panel.LastMessage);
                    if (stage < 12)
                    {
                        Require(panel.BeginDirectionGesture(), "Gesture start rejected.");
                        panel.SetSimulation(points[stage - 8], 0);
                        SessionState.SetInt(Prefix + "Stage", stage + 1);
                        return;
                    }
                    Require(panel.DirectionStep == 3 && !panel.DirectionsConfirmed && !panel.ArmSurface(), "Unconfirmed alignment allowed force.");
                    Require(panel.ConfirmDirections(), "Directions could not be confirmed.");
                    Require(panel.Workspace.TryGetMapping(out var map), "Aligned map unavailable.");
                    var origin = panel.Workspace.OriginMetres;
                    var raw = new System.Numerics.Vector3(origin.x, origin.y, origin.z);
                    var right = map.DeviceToWorldPosition(raw + System.Numerics.Vector3.UnitY * .01f);
                    var up = map.DeviceToWorldPosition(raw + System.Numerics.Vector3.UnitZ * .01f);
                    Require(right.X > .09f && Math.Abs(right.Y) < .00001f && up.Y > .09f && Math.Abs(up.X) < .00001f, "Reported swapped-axis regression persists.");
                    var camera = Camera.main;
                    var centre = camera.WorldToScreenPoint(Vector3.zero);
                    Require(camera.WorldToScreenPoint(new Vector3(right.X, right.Y, right.Z)).x > centre.x &&
                        camera.WorldToScreenPoint(new Vector3(up.X, up.Y, up.Z)).y > centre.y, "Camera reverses semantic directions.");
                    var profile = ScriptableObject.CreateInstance<FalconCalibrationProfile>();
                    panel.Workspace.CopyCalibrationTo(profile); panel.Workspace.ClearAxisAlignment(); panel.Workspace.LoadProfile(profile);
                    Require(panel.Workspace.HasAxisAlignment && profile.HasAxisAlignment, "Profile lost alignment.");
                    UnityEngine.Object.Destroy(profile);
                    panel.Workspace.SetScale(Vector3.one * 12, true);
                    Require(panel.SaveCalibration(), "Aligned JSON save failed.");
                    panel.Workspace.ClearAxisAlignment(); panel.Workspace.SetScale(Vector3.one * 3, false);
                    Require(panel.LoadCalibration() && panel.Workspace.HasAxisAlignment && panel.Workspace.UnitsPerMetre.x == 12 &&
                        panel.Workspace.AxisAlignment.Right == System.Numerics.Vector3.UnitY && panel.Workspace.InvertZ, "JSON lost axes, scale or inversion.");
                    panel.ResetSimulation();
                    panel.SetSimulation(new Vector3(.01f, .02f, .006f), 15);
                    SessionState.SetInt(Prefix + "Stage", 1);
                }
                else if (stage == 1)
                {
                    if (input.Buttons != 15 || Vector3.Distance(input.PositionMetres, new Vector3(.01f, .02f, .006f)) > .000001f) return;
                    panel.StopAllForces();
                    Require(!input.EffectArmed, "Stop did not clear requests.");
                    Require(panel.Workspace.BeginCapture(), "Capture unavailable.");
                    SessionState.SetInt(Prefix + "CaptureStep", 0); SessionState.SetInt(Prefix + "Stage", 2);
                }
                else if (stage == 2)
                {
                    int step = SessionState.GetInt(Prefix + "CaptureStep", 0);
                    var point = new Vector3(-.02f + step * .002f, -.025f + step * .0025f, -.015f + step * .002f);
                    panel.SetSimulation(point, 0);
                    if (Vector3.Distance(input.PositionMetres, point) > .000001f || panel.Workspace.CapturedSamples < step + 1) return;
                    if (++step < 20) { SessionState.SetInt(Prefix + "CaptureStep", step); return; }
                    Require(panel.Workspace.CompleteCapture(), "Capture failed: " + panel.Workspace.CaptureStatus);
                    var origin = panel.Workspace.OriginMetres;
                    Require(panel.SaveCalibration(), "Could not save calibration JSON: " + panel.LastMessage);
                    Require(File.Exists(panel.CalibrationPath), "Calibration file missing.");
                    panel.Workspace.SetCalibration(Vector3.zero, false, Vector3.zero, Vector3.zero);
                    Require(panel.LoadCalibration() && panel.Workspace.OriginMetres == origin && panel.Workspace.UsePhysicalBounds,
                        "Calibration JSON roundtrip failed.");
                    Require(panel.ExportDiagnostic(), "Could not export diagnostic.");
                    Require(Directory.GetFiles(panel.StorageDirectory, "falcon-diagnostic-*.json").Length > 0, "Diagnostic JSON missing.");
                    panel.ResetSimulation();
                    SessionState.SetInt(Prefix + "Stage", 3);
                }
                else if (stage == 3)
                {
                    if (!input.Connected || Vector3.Distance(input.PositionMetres, new Vector3(0, 0, .006f)) > .000001f) return;
                    foreach (FalconSurfaceFace face in Enum.GetValues(typeof(FalconSurfaceFace)))
                    {
                        panel.SelectFace(face);
                        Require(panel.PlaceSurfaceAhead(), "Face placement failed.");
                        Require(panel.Surface.TryGetGeometry(out var data), "Face export failed.");
                        input.TryGetSample(out var sample, out _);
                        float distance = data.SignedDistance(new System.Numerics.Vector3((float)sample.X, (float)sample.Y, (float)sample.Z));
                        Require(Math.Abs(distance - .006f) < .00001f, "Face not six millimetres ahead.");
                    }
                    panel.ResetSimulation(); panel.Surface.SetFriction(0);
                    Require(panel.Surface.TryGetGeometry(out var smooth) && smooth.FrictionCoefficient == 0, "Friction off failed.");
                    panel.Surface.SetFriction(.5f);
                    Require(panel.Surface.TryGetGeometry(out var rough) && rough.FrictionCoefficient == .5f, "Friction on failed.");
                    panel.SetSimulationAvailability(false);
                    SessionState.SetInt(Prefix + "Stage", 4);
                }
                else if (stage == 4)
                {
                    if (input.ConnectionState != FalconConnectionState.Faulted) return;
                    Require(!input.EffectArmed && !input.EffectActive, "Disconnect retained output.");
                    panel.SetSimulationAvailability(true);
                    SessionState.SetInt(Prefix + "Stage", 5);
                }
                else if (stage == 5)
                {
                    if (!input.Connected) return;
                    Require(!input.EffectArmed && !input.EffectActive, "Reconnect rearmed output.");
                    Require(panel.RestartReading(), "Reading restart failed.");
                    SessionState.SetInt(Prefix + "Stage", 6);
                }
                else if (stage == 6)
                {
                    if (!input.Connected || input.SampleSequence < 4) return;
                    Require(!input.EffectArmed && panel.SelectBackend(false), "Simulator selection failed.");
                    Require(Enum.GetValues(typeof(HapticMode)).Length == 6, "Six modes not available.");
                    SessionState.SetInt(Prefix + "Stage", 7);
                }
                else if (stage == 7)
                {
                    panel.StopAllForces();
                    SessionState.SetInt(Prefix + "Stage", 8);
                    EditorApplication.isPlaying = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex); FalconSession.StopAll();
                SessionState.SetBool(Prefix + "Running", false);
                EditorApplication.Exit(1);
            }
        }
    }
}

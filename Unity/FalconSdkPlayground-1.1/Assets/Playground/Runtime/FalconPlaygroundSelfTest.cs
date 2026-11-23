using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Falcon.Playground
{
    // Opt-in check for the built application. It never selects hardware or arms effects.
    public sealed class FalconPlaygroundSelfTest : MonoBehaviour
    {
        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--playground-self-test") < 0) yield break;
            int resultIndex = Array.IndexOf(args, "--playground-result");
            string output = resultIndex >= 0 && resultIndex + 1 < args.Length ? args[resultIndex + 1] : null;
            yield return null;
            var panel = GetComponent<FalconPlayground>();
            float deadline = Time.realtimeSinceStartup + 20;
            while ((panel.ActiveInput == null || !panel.ActiveInput.Connected) && Time.realtimeSinceStartup < deadline) yield return null;
            if (panel.ActiveInput == null || !panel.ActiveInput.Connected || panel.HardwareSelected || panel.HardwareRigActive)
            { Finish(output, false, "Simulation startup failed."); yield break; }
            panel.SetSimulation(new Vector3(.01f, -.01f, .006f), 15);
            while ((panel.ActiveInput.Buttons != 15 || Vector3.Distance(panel.ActiveInput.PositionMetres, new Vector3(.01f, -.01f, .006f)) > .000001f) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            bool ok = panel.ActiveInput.Buttons == 15 && !panel.ActiveInput.EffectArmed && !panel.ActiveInput.EffectActive &&
                panel.Workspace.IsValid && panel.Surface.TryGetGeometry(out var geometry) && geometry.FrictionCoefficient == .5f;
            panel.StopAllForces();
            Finish(output, ok, ok ? "Simulation, inputs, mapping and surface references verified. No hardware or force activation." : "Player state validation failed.");
        }
        static void Finish(string path, bool success, string message)
        {
            var result = new Result { success = success, message = message, unity = Application.unityVersion };
            if (!string.IsNullOrWhiteSpace(path)) File.WriteAllText(path, JsonUtility.ToJson(result, true));
            Debug.Log("FALCON PLAYGROUND PLAYER " + (success ? "PASS" : "FAIL") + ": " + message);
            Application.Quit(success ? 0 : 1);
        }
        [Serializable] sealed class Result { public bool success; public string message, unity; }
    }
}

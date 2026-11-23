using Falcon.Haptics;
using UnityEngine;

[RequireComponent(typeof(FalconInput))]
public sealed class FalconMonitor : MonoBehaviour
{
    static readonly string[] Modes = { "Eau", "Brique", "Résistance", "Ressort", "Gravier", "Levier moteur" };
    FalconInput falcon;
    int mode, intensity = 30;

    void Awake() => falcon = GetComponent<FalconInput>();

    void OnDisable()
    {
        if (falcon != null)
            falcon.DisarmEffect();
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(16, 16, Mathf.Min(440, Screen.width - 32), 500), GUI.skin.box);
        GUILayout.Label(falcon.Connected ? "Novint Falcon connecté" : "Novint Falcon · " + falcon.Status);
        GUILayout.Label($"État : {falcon.ConnectionState} · échantillon {falcon.SampleSequence}");
        GUILayout.Label($"Intervalles : moyenne {falcon.MeanIntervalMilliseconds:F2} ms · max {falcon.MaximumIntervalMilliseconds:F2} ms");
        if (falcon.IsSimulation)
            GUILayout.Label($"SIMULATION · force calculée {falcon.SimulatedForceNewtons:F3} N");
        var workspace = GetComponent<FalconWorkspace>();
        if (workspace != null)
        {
            GUILayout.Label(workspace.Status);
            GUILayout.Label($"Position scène : {workspace.MappedPositionWorld:F3}");
        }
        if (falcon.Connected)
        {
            Vector3 mm = falcon.PositionMetres * 1000;
            GUILayout.Label($"Position (mm)   X {mm.x:F1}   Y {mm.y:F1}   Z {mm.z:F1}");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 4; i++)
                GUILayout.Label((falcon.IsButtonPressed(i) ? "● " : "○ ") + (i + 1));
            GUILayout.EndHorizontal();
        }

        int selected = GUILayout.SelectionGrid(mode, Modes, 3);
        if (selected != mode)
        {
            mode = selected;
            falcon.DisarmEffect();
        }
        GUILayout.Label($"Intensité · {intensity} %");
        int newIntensity = Mathf.RoundToInt(GUILayout.HorizontalSlider(intensity, 0, 100));
        if (newIntensity != intensity)
        {
            intensity = newIntensity;
            falcon.SetEffectIntensity(intensity);
        }

        bool previous = GUI.enabled;
        GUI.enabled = previous && falcon.Connected && !falcon.EffectArmed;
        if (GUILayout.Button("Activer le mode"))
            falcon.TryArmEffect((HapticMode)mode, intensity);
        GUI.enabled = previous && (falcon.EffectArmed || falcon.EffectActive);
        if (GUILayout.Button("Couper les forces"))
            falcon.DisarmEffect();
        GUI.enabled = previous;

        if (!string.IsNullOrEmpty(falcon.EffectError))
            GUILayout.Label(falcon.EffectError);
        else if (falcon.EffectArmed)
            GUILayout.Label("Maintenez le bouton 1 de la poignée. Relâchez-le pour couper la force.");
        GUILayout.EndArea();
    }
}

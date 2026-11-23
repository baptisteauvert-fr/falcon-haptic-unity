using Falcon.Haptics;
using UnityEngine;

public sealed class FalconSurfaceDemo : MonoBehaviour
{
    [SerializeField] FalconInput falcon = null;
    [SerializeField] FalconSurface surface = null;
    Vector3 positionMetres = new Vector3(0, 0, .006f);
    bool held;
    void Update()
    {
        if (falcon != null && falcon.IsSimulation)
            falcon.SetSimulationSample(positionMetres, held ? 1u : 0u);
    }
    void OnDisable() { if (surface != null) surface.Disarm(); }
    void OnGUI()
    {
        if (falcon == null || surface == null) return;
        GUILayout.BeginArea(new Rect(16, 16, Mathf.Min(470, Screen.width - 32), 560), GUI.skin.box);
        GUILayout.Label("Première surface Falcon · simulation");
        GUILayout.Label("1. Activer, bouton relâché et outil devant la face.");
        GUILayout.Label("2. Maintenir le bouton simulé, puis réduire Z vers 0 mm.");
        if (GUILayout.Button("Activer la surface à 30 %")) surface.TryArm(30);
        if (GUILayout.Button("Couper les forces")) { surface.Disarm(); held = false; }
        bool friction = surface.FrictionCoefficient > 0;
        bool selected = GUILayout.Toggle(friction, "Friction dynamique (μ = 0,5) · changer désarme");
        if (selected != friction) { surface.SetFriction(selected ? .5f : 0); held = false; }
        if (falcon.IsSimulation)
        {
            held = GUILayout.Toggle(held, "Maintenir le bouton 1 simulé");
            GUILayout.Label($"X : {positionMetres.x * 1000:F1} mm");
            positionMetres.x = GUILayout.HorizontalSlider(positionMetres.x, -.04f, .04f);
            GUILayout.Label($"Y : {positionMetres.y * 1000:F1} mm");
            positionMetres.y = GUILayout.HorizontalSlider(positionMetres.y, -.04f, .04f);
            GUILayout.Label($"Z : {positionMetres.z * 1000:F1} mm (face à 0, rayon 2 mm)");
            positionMetres.z = GUILayout.HorizontalSlider(positionMetres.z, -.006f, .006f);
            if (GUILayout.Button("Replacer devant la face"))
            { held = false; positionMetres = new Vector3(0, 0, .006f); surface.Disarm(); }
            GUILayout.Label($"Force calculée : {falcon.SimulatedForceNewtons:F3} N");
            GUILayout.Label($"Pic de friction sur cette prise : {falcon.SurfaceFrictionPeakNewtons:F4} N");
        }
        GUILayout.Label($"Contact : {(surface.InContact ? "oui" : "non")} · état : {falcon.ConnectionState}");
        GUILayout.Label(string.IsNullOrEmpty(falcon.EffectError) ? surface.Status : falcon.EffectError);
        GUILayout.Label("Le curseur montre la position mesurée, pas un proxy bloqué sur la face.");
        GUILayout.EndArea();
    }
}

using UnityEditor;
using UnityEngine;

namespace Falcon.Haptics.Editor
{
    [CustomEditor(typeof(FalconSurface))]
    public sealed class FalconSurfaceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var surface = (FalconSurface)target;
            EditorGUILayout.HelpBox("Une seule face du BoxCollider, à sens unique. Les autres faces et les arêtes ne produisent pas de contact. " +
                "La flèche cyan indique le côté de départ. Les réglages sont physiques (m, N/m, N·s/m), indépendants de l’échelle visuelle.", MessageType.Info);
            EditorGUILayout.HelpBox(surface.Status, MessageType.Info);
            EditorGUILayout.LabelField("Contact", surface.InContact ? "Oui" : "Non");
            EditorGUILayout.HelpBox("La friction dynamique est régularisée : elle tend vers zéro à l’arrêt et ne simule pas l’adhérence statique. " +
                "Le budget de force donne priorité au contact normal. Modifier la friction désarme l’interaction.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !surface.isActiveAndEnabled))
            {
                if (GUILayout.Button("Activer la surface à 30 %")) surface.TryArm(30);
                if (GUILayout.Button("Couper les forces")) surface.Disarm();
            }
            EditorGUILayout.HelpBox("Après activation, cliquez dans Game, relâchez puis maintenez le bouton 1 devant la face. " +
                "Toute modification de géométrie ou de matériau désarme l’effet. La stabilité physique reste à qualifier sur le Falcon.", MessageType.Info);
            if (Application.isPlaying) Repaint();
        }
    }
}

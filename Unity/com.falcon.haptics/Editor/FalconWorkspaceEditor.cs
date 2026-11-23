using System;
using UnityEditor;
using UnityEngine;

namespace Falcon.Haptics.Editor
{
    [CustomEditor(typeof(FalconWorkspace))]
    public sealed class FalconWorkspaceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var workspace = (FalconWorkspace)target;
            EditorGUILayout.HelpBox("Le Transform de cet objet définit l’origine et l’orientation dans la scène. " +
                "La boîte est une limite logicielle ; ses coins ne sont pas nécessairement accessibles au Falcon.", MessageType.Info);
            bool valid = workspace.IsValid;
            EditorGUILayout.HelpBox(workspace.Status, valid ? MessageType.Info : MessageType.Error);
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Dans la zone", workspace.IsInsidePhysicalBounds ? "Oui" : "Non / lecture indisponible");
                EditorGUILayout.LabelField("Lectures capturées", workspace.CapturedSamples.ToString());
                EditorGUILayout.HelpBox(workspace.CaptureStatus, MessageType.Info);
                using (new EditorGUI.DisabledScope(!workspace.isActiveAndEnabled))
                {
                    if (GUILayout.Button("Recentrer sur la position actuelle"))
                    {
                        Undo.RecordObject(workspace, "Recentrer le Falcon");
                        workspace.TryRecenter();
                    }
                    if (!workspace.IsCapturing)
                    {
                        if (GUILayout.Button("Commencer la capture de déplacement")) workspace.BeginCapture();
                    }
                    else
                    {
                        if (GUILayout.Button("Terminer et appliquer la calibration"))
                        {
                            Undo.RecordObject(workspace, "Calibrer le Falcon");
                            workspace.CompleteCapture();
                        }
                        if (GUILayout.Button("Annuler la capture")) workspace.CancelCapture();
                    }
                }
                Repaint();
            }
            else EditorGUILayout.HelpBox("Le recentrage et la capture nécessitent Play et une lecture récente. " +
                "Le mode Simulation permet de les essayer sans appareil.", MessageType.Info);

            try
            {
                using (new EditorGUI.DisabledScope(workspace.CalibrationProfile == null))
                {
                    if (GUILayout.Button("Charger le profil assigné"))
                    {
                        Undo.RecordObject(workspace, "Charger la calibration Falcon");
                        workspace.LoadProfile(workspace.CalibrationProfile);
                        EditorUtility.SetDirty(workspace);
                    }
                    if (GUILayout.Button("Enregistrer dans le profil assigné"))
                    {
                        Undo.RecordObject(workspace.CalibrationProfile, "Enregistrer la calibration Falcon");
                        workspace.CopyCalibrationTo(workspace.CalibrationProfile);
                        EditorUtility.SetDirty(workspace.CalibrationProfile);
                        AssetDatabase.SaveAssets();
                    }
                }
                if (GUILayout.Button("Enregistrer dans un nouveau profil…"))
                {
                    string path = EditorUtility.SaveFilePanelInProject("Profil de calibration Falcon", "Falcon Calibration", "asset", "Choisissez un emplacement pour la calibration.");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var profile = CreateInstance<FalconCalibrationProfile>();
                        try { workspace.CopyCalibrationTo(profile); AssetDatabase.CreateAsset(profile, path); }
                        catch { DestroyImmediate(profile); throw; }
                        Undo.RecordObject(workspace, "Assigner la calibration Falcon");
                        workspace.LoadProfile(profile);
                        EditorUtility.SetDirty(workspace);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
            catch (Exception ex) { EditorGUILayout.HelpBox(ex.Message, MessageType.Error); }
            EditorGUILayout.HelpBox("Les changements effectués dans Play sont temporaires. Enregistrez un profil pour conserver la calibration, " +
                "puis rechargez ce profil hors Play. Le chargement est explicite et ne modifie pas l’échelle ni le Transform de la scène.", MessageType.Info);
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace Falcon.Haptics.Editor
{
    [InitializeOnLoad]
    static class FalconEditorLifecycle
    {
        static FalconEditorLifecycle()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) Stop();
                if (state == PlayModeStateChange.EnteredPlayMode)
                    foreach (var input in Resources.FindObjectsOfTypeAll<FalconInput>())
                        if (input.gameObject.scene.IsValid() && input.gameObject.scene.isLoaded)
                            input.StartSession();
            };
            EditorApplication.pauseStateChanged += state =>
            {
                if (state == PauseState.Paused) FalconSession.DisarmAll();
            };
        }

        static void Stop()
        {
            if (!FalconSession.StopAll())
                Debug.LogError("Arrêt Falcon non confirmé : un appel natif peut être bloqué. " +
                    "La coupure matérielle n’est pas garantie ; ne réarmez pas l’appareil.");
        }
    }
}

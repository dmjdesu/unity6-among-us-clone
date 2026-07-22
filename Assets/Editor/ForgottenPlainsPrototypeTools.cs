using AmongUsClone;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AmongUsClone.Editor
{
    public static class ForgottenPlainsPrototypeTools
    {
        private const string PrototypeScenePath = "Assets/Game/Maps/ForgottenPlainsGamePrototype.unity";

        [MenuItem("Tools/Game/Open Forgotten Plains Prototype")]
        public static void OpenPrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(PrototypeScenePath);
            ForgottenPlainsGamePrototypeBootstrap.EnsurePrototypeScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Opened {PrototypeScenePath}");
        }

        [MenuItem("Tools/Game/Refresh Forgotten Plains Prototype Markers")]
        public static void RefreshPrototypeMarkers()
        {
            if (EditorSceneManager.GetActiveScene().path != PrototypeScenePath)
            {
                EditorSceneManager.OpenScene(PrototypeScenePath);
            }

            ForgottenPlainsGamePrototypeBootstrap.EnsurePrototypeScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Refreshed Forgotten Plains prototype markers.");
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace AmongUsClone.Editor
{
    public sealed class ForgottenPlainsMapWindow : EditorWindow
    {
        private TextAsset _definitionAsset;
        private int _seed = ForgottenPlainsMapGenerator.DefaultSeed;

        [MenuItem("Tools/Forgotten Plains/Open Map Generator")]
        public static void Open()
        {
            GetWindow<ForgottenPlainsMapWindow>("Forgotten Plains");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Large Prototype Generator", EditorStyles.boldLabel);
            _seed = EditorGUILayout.IntField("Seed", _seed);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Generate Layout", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Layout A"))
                {
                    ForgottenPlainsMapGenerator.GenerateSpecificLayout("A", _seed);
                }

                if (GUILayout.Button("Layout B"))
                {
                    ForgottenPlainsMapGenerator.GenerateSpecificLayout("B", _seed);
                }

                if (GUILayout.Button("Layout C"))
                {
                    ForgottenPlainsMapGenerator.GenerateSpecificLayout("C", _seed);
                }
            }

            if (GUILayout.Button("Generate Adopted Layout"))
            {
                ForgottenPlainsMapGenerator.GenerateAdoptedLargePrototype(_seed);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Definition", EditorStyles.boldLabel);
            _definitionAsset = (TextAsset)EditorGUILayout.ObjectField("Map Definition", _definitionAsset, typeof(TextAsset), false);

            using (new EditorGUI.DisabledScope(_definitionAsset == null))
            {
                if (GUILayout.Button("Generate Selected Definition"))
                {
                    ForgottenPlainsMapGenerator.GenerateFromDefinition(_definitionAsset, _seed);
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            if (GUILayout.Button("Validate Large Prototype"))
            {
                ForgottenPlainsMapGenerator.ValidateLargePrototypeBatch();
            }

            if (GUILayout.Button("Open Large Prototype Scene"))
            {
                ForgottenPlainsMapGenerator.OpenLargePrototypeScene();
            }
        }
    }
}

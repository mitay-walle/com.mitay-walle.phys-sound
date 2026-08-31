#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using UnityEditor;
using UnityEngine;

namespace PhysSound.Editor
{
    [CustomEditor(typeof(PhysSoundSubprofile))]
    internal sealed class PhysSoundSubprofileEditor : UnityEditor.Editor
    {
        private readonly PhysSoundInteractivePreview _preview = new();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            PhysSoundSettings settings = AssetDatabase.LoadAssetAtPath<PhysSoundSettings>(
                PhysSoundSettingsProvider.AssetPath);

            if (settings == null)
            {
                EditorGUILayout.HelpBox(
                    "Create the Phys Sound project settings before adding this subprofile.",
                    MessageType.Info);
                return;
            }

            SerializedObject serializedSettings = new SerializedObject(settings);
            SerializedProperty subprofiles = serializedSettings.FindProperty("_externalSubprofiles");

            if (Contains(subprofiles, target))
            {
                if (GUILayout.Button("Open Settings"))
                {
                    SettingsService.OpenProjectSettings(PhysSoundSettingsProvider.SettingsPath);
                }

                return;
            }

            if (GUILayout.Button("Add to Settings"))
            {
                Undo.RecordObject(settings, "Add Phys Sound Subprofile");
                int index = subprofiles.arraySize;
                subprofiles.InsertArrayElementAtIndex(index);
                subprofiles.GetArrayElementAtIndex(index).objectReferenceValue = target;
                serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        public override bool HasPreviewGUI()
        {
            return true;
        }

        public override GUIContent GetPreviewTitle()
        {
            return new GUIContent("Phys Sound Audio Markup");
        }

        public override void OnInteractivePreviewGUI(Rect rect, GUIStyle background)
        {
            _preview.Draw(rect, target);
        }

        private void OnDisable()
        {
            PhysSoundInteractivePreview.Stop();
        }

        private static bool Contains(SerializedProperty subprofiles, Object subprofile)
        {
            for (int i = 0; i < subprofiles.arraySize; i++)
            {
                if (subprofiles.GetArrayElementAtIndex(i).objectReferenceValue == subprofile)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif

#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PhysSound.Editor
{
    [CustomEditor(typeof(PhysSoundSubprofile))]
    internal sealed class PhysSoundSubprofileEditor : UnityEditor.Editor
    {
        private readonly PhysSoundInteractivePreview _preview = new();

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            VisualElement actions = new();
            actions.style.marginTop = 4f;
            root.Add(actions);

            PhysSoundSettings settings = AssetDatabase.LoadAssetAtPath<PhysSoundSettings>(
                PhysSoundSettingsProvider.AssetPath);

            if (settings == null)
            {
                actions.Add(new HelpBox(
                    "Create the Phys Sound project settings before adding this subprofile.",
                    HelpBoxMessageType.Info));
                return root;
            }

            SerializedObject serializedSettings = new SerializedObject(settings);
            SerializedProperty subprofiles = serializedSettings.FindProperty("_externalSubprofiles");

            void RefreshActions()
            {
                serializedSettings.Update();
                actions.Clear();
                if (Contains(subprofiles, target))
                {
                    actions.Add(CreateOpenSettingsButton(settings));
                    return;
                }

                Button addToSettings = new(() =>
                {
                    serializedSettings.Update();
                    if (!Contains(subprofiles, target))
                    {
                        Undo.RecordObject(settings, "Add Phys Sound Subprofile");
                        int index = subprofiles.arraySize;
                        subprofiles.InsertArrayElementAtIndex(index);
                        subprofiles.GetArrayElementAtIndex(index).objectReferenceValue = target;
                        serializedSettings.ApplyModifiedProperties();
                        EditorUtility.SetDirty(settings);
                        AssetDatabase.SaveAssets();
                    }

                    RefreshActions();
                })
                {
                    text = "Add to Settings"
                };
                actions.Add(addToSettings);
            }

            actions.TrackPropertyValue(subprofiles, _ => RefreshActions());
            RefreshActions();
            return root;
        }

        public override bool HasPreviewGUI()
        {
            return true;
        }

        public override GUIContent GetPreviewTitle()
        {
            return new GUIContent("Phys Sound Editor");
        }

        public override VisualElement CreatePreview(VisualElement inspectorPreviewWindow)
        {
            inspectorPreviewWindow.Clear();
            inspectorPreviewWindow.Add(_preview.CreateVisualElement(target));
            return inspectorPreviewWindow;
        }

        private void OnDisable()
        {
            _preview.Dispose();
        }

        private static Button CreateOpenSettingsButton(PhysSoundSettings settings)
        {
            return new Button(() =>
            {
                SettingsService.OpenProjectSettings(PhysSoundSettingsProvider.SettingsPath);
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            })
            {
                text = "Open Settings"
            };
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

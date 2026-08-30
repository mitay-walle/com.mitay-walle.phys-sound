using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PhysSound.Editor
{
    internal static class PhysSoundSettingsProvider
    {
        private const string SettingsPath = "Project/Phys Sound";
        private const string AssetPath = "Assets/Resources/PhysSound/PhysSoundSettings.asset";

#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
        private static PhysSoundSettings _settings;
        private static SerializedObject _serializedSettings;
#endif

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Phys Sound",
                guiHandler = Draw,
                keywords = new HashSet<string>
                {
                    "Phys Sound",
                    "Physics",
                    "Audio",
                    "Impact",
                    "Slide",
                    "Provides Contacts"
                }
            };
        }

        private static void Draw(string searchContext)
        {
#if !PHYS_SOUND_AUDIO
            EditorGUILayout.HelpBox(
                "Phys Sound requires the built-in Unity Audio module.",
                MessageType.Error);
#endif

#if !PHYS_SOUND_3D
            EditorGUILayout.HelpBox(
                "Phys Sound 2.0 currently requires the built-in Unity Physics 3D module.",
                MessageType.Error);
#endif

#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
            LoadSettings();

            if (_settings == null)
            {
                EditorGUILayout.HelpBox(
                    "No Phys Sound settings exist in this project. Creating them is an explicit project change.",
                    MessageType.Info);

                EditorGUILayout.LabelField("Asset Path", AssetPath);

                if (GUILayout.Button("Create Settings"))
                {
                    CreateSettings();
                }

                return;
            }

            EditorGUILayout.LabelField("Backing Asset", AssetPath);
            EditorGUILayout.HelpBox(
                $"Use \"{PhysSoundSettings.AnySurface}\" as a surface name in an interaction to define a wildcard fallback. " +
                $"Unmapped Physics Materials resolve to \"{PhysSoundSettings.DefaultSurface}\".",
                MessageType.None);

            _serializedSettings.Update();

            SerializedProperty property = _serializedSettings.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                {
                    EditorGUILayout.PropertyField(property, true);
                }

                enterChildren = false;
            }

            _serializedSettings.ApplyModifiedProperties();
#endif
        }

#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
        private static void LoadSettings()
        {
            if (_settings != null)
            {
                return;
            }

            _settings = AssetDatabase.LoadAssetAtPath<PhysSoundSettings>(AssetPath);
            _serializedSettings = _settings == null ? null : new SerializedObject(_settings);
        }

        private static void CreateSettings()
        {
            string directory = Path.GetDirectoryName(AssetPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AssetDatabase.Refresh();

            PhysSoundSettings settings = ScriptableObject.CreateInstance<PhysSoundSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();

            _settings = settings;
            _serializedSettings = new SerializedObject(settings);
        }
#endif
    }
}

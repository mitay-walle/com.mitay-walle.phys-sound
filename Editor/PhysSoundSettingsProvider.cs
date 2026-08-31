using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace PhysSound.Editor
{
    internal static class PhysSoundSettingsProvider
    {
        private const float EmitterInspectorMaxWidth = 520f;
        private const float EmitterInspectorLabelWidth = 150f;
        private const string DisablePhysics2DDefine = "PHYS_SOUND_DISABLE_2D";
        internal const string SettingsPath = "Project/Audio/Phys Sound";
        internal const string AssetPath = "Assets/Resources/PhysSound/PhysSoundSettings.asset";
        private const string EmitterPrefabPath = "Assets/Resources/PhysSound/PhysSoundAudioSource.prefab";
        private const string RepositoryReadmeUrl =
            "https://github.com/mitay-walle/com.mitay-walle.phys-sound/blob/master/README.md";

        private static readonly string[] VoicePoolPropertyNames =
        {
            "_maximumVoices",
            "_minimumImpactInterval",
            "_slideContactTimeout",
            "_slideFadeInSpeed",
            "_slideFadeOutSpeed",
            "_slidePitchSpeed",
            "_slidePositionSpeed"
        };

#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
        private static PhysSoundSettings _settings;
        private static SerializedObject _serializedSettings;
        private static AudioSource _emitterSource;
        private static UnityEditor.Editor _emitterEditor;
        private static bool _emitterInspectorExpanded;
#endif

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Phys Sound",
                guiHandler = Draw,
                titleBarGuiHandler = DrawTitleBar,
#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
                deactivateHandler = Deactivate,
#endif
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
                EditorGUILayout.LabelField("Emitter Prefab Path", EmitterPrefabPath);

                if (GUILayout.Button("Create Settings"))
                {
                    CreateSettings();
                }

                return;
            }

            EditorGUILayout.LabelField("Backing Asset", AssetPath);
            DrawPhysics2DDefine();
            EditorGUILayout.HelpBox(
                "Leave Surface B empty in an interaction to define a wildcard fallback. " +
                $"Unmapped Physics Materials resolve to \"{PhysSoundSettings.DefaultSurface}\".",
                MessageType.None);

            _serializedSettings.Update();
            DrawVoicePoolSettings();
            DrawDefaultInteraction();

            SerializedProperty property = _serializedSettings.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                if (property.propertyPath != "_emitterPrefab" &&
                    property.propertyPath != "_defaultInteraction" &&
                    !IsVoicePoolProperty(property.propertyPath))
                {
                    using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }
                }

                enterChildren = false;
            }

            _serializedSettings.ApplyModifiedProperties();
            DrawEmitterInspector();
#endif
        }

        private static void DrawTitleBar()
        {
            GUIContent infoIcon = EditorGUIUtility.IconContent("console.infoicon.sml");
            GUIContent documentationContent = new GUIContent("Documentation", infoIcon.image);

            if (GUILayout.Button(documentationContent, EditorStyles.miniButton, GUILayout.Width(120f)))
            {
                Application.OpenURL(RepositoryReadmeUrl);
            }
        }

#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
        private static void DrawPhysics2DDefine()
        {
            NamedBuildTarget buildTarget = NamedBuildTarget.FromBuildTargetGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup);

            string defineString = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            List<string> defines = new List<string>(defineString.Split(';'));

            bool disabled = defines.Contains(DisablePhysics2DDefine);
            EditorGUI.BeginChangeCheck();
            disabled = EditorGUILayout.Toggle(
                new GUIContent(
                    "Force Disable Physics 2D",
                    "Excludes Phys Sound 2D fields without disabling Unity Physics 2D for the project."),
                disabled);

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            if (disabled)
            {
                if (!defines.Contains(DisablePhysics2DDefine))
                {
                    defines.Add(DisablePhysics2DDefine);
                }
            }
            else
            {
                defines.RemoveAll(value => value == DisablePhysics2DDefine);
            }

            defines.RemoveAll(string.IsNullOrEmpty);
            PlayerSettings.SetScriptingDefineSymbols(buildTarget, defines.ToArray());
            GUIUtility.ExitGUI();
        }

        private static void DrawVoicePoolSettings()
        {
            for (int i = 0; i < VoicePoolPropertyNames.Length; i++)
            {
                EditorGUILayout.PropertyField(
                    _serializedSettings.FindProperty(VoicePoolPropertyNames[i]),
                    true);
            }

            EditorGUILayout.Space();
        }

        private static bool IsVoicePoolProperty(string propertyPath)
        {
            for (int i = 0; i < VoicePoolPropertyNames.Length; i++)
            {
                if (propertyPath == VoicePoolPropertyNames[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static void DrawDefaultInteraction()
        {
            SerializedProperty defaultInteraction = _serializedSettings.FindProperty("_defaultInteraction");
            defaultInteraction.isExpanded = EditorGUILayout.Foldout(
                defaultInteraction.isExpanded,
                "Default Interaction",
                true);

            if (!defaultInteraction.isExpanded)
            {
                return;
            }

            SerializedProperty property = defaultInteraction.Copy();
            SerializedProperty endProperty = property.GetEndProperty();
            bool enterChildren = true;

            EditorGUI.indentLevel++;

            while (property.NextVisible(enterChildren) && !SerializedProperty.EqualContents(property, endProperty))
            {
                if (property.depth == defaultInteraction.depth + 1 &&
                    property.name != "_surfaceA" &&
                    property.name != "_surfaceB")
                {
                    EditorGUILayout.PropertyField(property, true);
                }

                enterChildren = false;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        private static void DrawEmitterInspector()
        {
            SerializedProperty emitterProperty = _serializedSettings.FindProperty("_emitterPrefab");

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            AudioSource emitterSource;

            using (new EditorGUILayout.HorizontalScope())
            {
                emitterSource = EditorGUILayout.ObjectField(
                    "Emitter Prefab",
                    emitterProperty.objectReferenceValue,
                    typeof(AudioSource),
                    false) as AudioSource;

                if (emitterSource == null && GUILayout.Button("Create Prefab", GUILayout.Width(110f)))
                {
                    emitterSource = CreateEmitterPrefab();
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                emitterProperty.objectReferenceValue = emitterSource;
                _serializedSettings.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                DisposeEmitterEditor();
            }

            if (emitterSource == null)
            {
                EditorGUILayout.HelpBox("Assign an AudioSource emitter prefab to use Phys Sound.", MessageType.Error);
                return;
            }

            _emitterInspectorExpanded = EditorGUILayout.Foldout(
                _emitterInspectorExpanded,
                "Audio Source Inspector",
                true);

            if (!_emitterInspectorExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            UnityEditor.Editor.CreateCachedEditor(emitterSource, null, ref _emitterEditor);
            _emitterSource = emitterSource;

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = EmitterInspectorLabelWidth;
            try
            {
                using (new EditorGUILayout.VerticalScope(
                           EditorStyles.helpBox,
                           GUILayout.MaxWidth(EmitterInspectorMaxWidth)))
                {
                    _emitterEditor.OnInspectorGUI();
                }
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
                EditorGUI.indentLevel--;
            }
        }

        private static void DisposeEmitterEditor()
        {
            if (_emitterEditor != null)
            {
                Object.DestroyImmediate(_emitterEditor);
            }

            _emitterEditor = null;
            _emitterSource = null;
        }

        private static void Deactivate()
        {
            PhysSoundInteractivePreview.Stop();
            DisposeEmitterEditor();
        }

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
            SerializedObject serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty("_emitterPrefab").objectReferenceValue = CreateEmitterPrefab();
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();

            _settings = settings;
            _serializedSettings = new SerializedObject(settings);
        }

        private static AudioSource CreateEmitterPrefab()
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EmitterPrefabPath);

            if (existingPrefab != null)
            {
                AudioSource existingSource = existingPrefab.GetComponent<AudioSource>();

                if (existingSource == null)
                {
                    throw new InvalidDataException($"{EmitterPrefabPath} must contain an AudioSource on its root.");
                }

                return existingSource;
            }

            GameObject emitterObject = new GameObject("PhysSoundAudioSource");

            try
            {
                AudioSource source = emitterObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 1f;
                source.maxDistance = 40f;
                source.dopplerLevel = 0f;

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(emitterObject, EmitterPrefabPath);
                return prefab.GetComponent<AudioSource>();
            }
            finally
            {
                Object.DestroyImmediate(emitterObject);
            }
        }
#endif
    }
}

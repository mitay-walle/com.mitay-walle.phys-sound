#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace PhysSound.Editor
{
    internal sealed class PhysSoundInteractivePreview
    {
        private const float ToolbarHeight = 20f;
        private const float ClipRowHeight = 20f;
        private const float ControlsHeight = 89f;
        private const float Spacing = 3f;
        private const float HandleWidth = 6f;
        private const float MinimumRegionPixels = 3f;
        private const float MinimumVisibleFraction = 0.005f;
        private const float DetectionSliderExponent = 2f;
        private const float ImpactAxisExponent = 2f;
        private const float PreviewMinimumHeight = 300f;
        private const string ErrorIconString64 =
            "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAYUlEQVR4nGNgoBAw4pK4ZG7+H5mvd/IkVrWMhDSiA3SDmEjRjE0NEz7NuidOgDE+Q5gYKARMuGwnBGB6mKjiggE1gBHGIDUcYOmBCZ8iXNGIDJjQTSTFdhCgOClTnJkoBgDAnSwIFWRJXgAAAABJRU5ErkJggg==";

        private static readonly Color WaveformColor = new(0.55f, 0.78f, 1f, 0.9f);
        private static readonly Color RegionColor = new(0.2f, 0.65f, 1f, 0.2f);
        private static readonly Color SelectedRegionColor = new(1f, 0.65f, 0.15f, 0.3f);
        private static readonly Dictionary<AudioClip, float[]> WaveformCache = new();
        private static readonly Type AudioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        private static readonly MethodInfo PlayPreviewClipMethod = GetAudioUtilMethod(
            "PlayPreviewClip",
            typeof(AudioClip),
            typeof(int),
            typeof(bool));
        private static readonly MethodInfo StopAllPreviewClipsMethod = GetAudioUtilMethod("StopAllPreviewClips");
        private static readonly MethodInfo IsPreviewClipPlayingMethod = GetAudioUtilMethod("IsPreviewClipPlaying");
        private static readonly MethodInfo GetPreviewClipSamplePositionMethod =
            GetAudioUtilMethod("GetPreviewClipSamplePosition");
        private static readonly MethodInfo GetMinMaxDataMethod = GetAudioUtilMethod(
            "GetMinMaxData",
            typeof(AudioImporter));

        private readonly List<PreviewEntry> _entries = new();
        private string[] _interactionNames = Array.Empty<string>();
        private int _selectedInteraction;
        private int _selectedSurface;
        private string _selectedSurfaceName;
        private int _selectedRegion = -1;
        private int _selectedImpactRange;
        private int _selectedImpactSource;
        private int _dragImpactBoundary = -1;
        private bool _impactBoundaryUndoRecorded;
        private int _curveMode;
        private PreviewMode _mode;
        private DragMode _dragMode;
        private float _dragStartTime;
        private float _dragOriginalStart;
        private float _dragOriginalEnd;
        private float _viewStartNormalized;
        private float _viewEndNormalized = 1f;
        private bool _isPanning;
        private Vector2 _impactSourceScroll;
        private Vector2 _surfaceScroll;
        private Vector2 _mappingScroll;
        private string _newMappingSurfaceA;
        private string _newMappingSurfaceB;
        private string _newMappingSourcePath;
        private float _soundVolumeMinDb = -36f;
        private float _soundVolumeMaxDb;
        private float _pauseVolumeMinDb = -80f;
        private float _pauseVolumeMaxDb = -42f;
        private float _soundDurationMin = 0.025f;
        private float _soundDurationMax = 10f;
        private float _pauseDurationMin = 0.05f;
        private float _pauseDurationMax = 0.5f;

        private static AudioClip _playingClip;
        private static int _playingEndSample = -1;
        private static Texture2D _errorIcon;
        private static Sprite _errorSprite;
        private VisualElement _uiContent;
        private Object _uiOwner;
        private readonly GUIContent[] _tabContents = new GUIContent[6];
        private bool _validationDirty = true;
        private double _nextValidationTime;
        private Object _validatedOwner;

        internal void Draw(Rect rect, Object owner)
        {
            BuildEntries(owner);
            EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f, 1f));

            Rect toolbarRect = TakeTop(ref rect, ToolbarHeight);
            DrawToolbar(toolbarRect, owner);
            DrawModeContent(rect, owner);
        }

        internal VisualElement CreateVisualElement(Object owner)
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            Undo.undoRedoPerformed += HandleUndoRedo;
            _uiOwner = owner;
            BuildEntries(owner);
            VisualElement root = new() { name = "phys-sound-preview-root" };
            root.style.flexGrow = 1f;
            root.style.minHeight = PreviewMinimumHeight;
            root.style.width = Length.Percent(100f);
            root.style.backgroundColor = new StyleColor(new Color(0.13f, 0.13f, 0.13f, 1f));

            IMGUIContainer toolbar = null;
            toolbar = new IMGUIContainer(() =>
            {
                PreviewMode previousMode = _mode;
                DrawToolbar(new Rect(0f, 0f, toolbar.resolvedStyle.width, ToolbarHeight), _uiOwner);
                if (previousMode != _mode)
                {
                    toolbar.schedule.Execute(RebuildUIContent);
                }
            });
            toolbar.style.height = ToolbarHeight;
            toolbar.style.flexShrink = 0f;
            root.Add(toolbar);

            _uiContent = new VisualElement { name = "phys-sound-preview-content" };
            _uiContent.style.flexGrow = 1f;
            _uiContent.style.minHeight = PreviewMinimumHeight - ToolbarHeight;
            _uiContent.style.width = Length.Percent(100f);
            root.Add(_uiContent);
            RebuildUIContent();
            return root;
        }

        internal void Dispose()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            Stop();
            _uiContent = null;
            _uiOwner = null;
        }

        private void HandleUndoRedo()
        {
            InvalidateValidation();
            RebuildUIContent();
        }

        private void RebuildUIContent()
        {
            if (_uiContent == null || _uiOwner == null)
            {
                return;
            }

            BuildEntries(_uiOwner);
            _uiContent.Clear();
            if (_mode == PreviewMode.Surfaces)
            {
                _uiContent.Add(CreateMaterialsList(_uiOwner));
                return;
            }

            if (_mode == PreviewMode.InteractionMapping)
            {
                _uiContent.Add(CreateMappingTable(_uiOwner));
                return;
            }

            IMGUIContainer content = null;
            content = new IMGUIContainer(() =>
            {
                Rect rect = new(0f, 0f, content.resolvedStyle.width, content.resolvedStyle.height);
                EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f, 1f));
                DrawModeContent(rect, _uiOwner);
            });
            content.style.flexGrow = 1f;
            content.style.minHeight = PreviewMinimumHeight - ToolbarHeight;
            _uiContent.Add(content);
        }

        private VisualElement CreateMaterialsList(Object owner)
        {
            Dictionary<string, PhysSoundSurface> surfaces = GetSurfaces(owner);
            VisualElement root = new();
            root.style.flexDirection = FlexDirection.Row;
            root.style.flexGrow = 1f;
            root.style.paddingLeft = 2f;
            root.style.paddingRight = 2f;
            root.style.paddingTop = 2f;
            root.style.paddingBottom = 2f;
            if (surfaces == null)
            {
                root.Add(new HelpBox("Surface data is unavailable.", HelpBoxMessageType.Error));
                return root;
            }

            List<string> names = new(surfaces.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            int selectedIndex = names.FindIndex(name =>
                string.Equals(name, _selectedSurfaceName, StringComparison.Ordinal));
            if (selectedIndex >= 0)
            {
                _selectedSurface = selectedIndex;
            }

            _selectedSurface = Mathf.Clamp(_selectedSurface, 0, Mathf.Max(0, names.Count - 1));
            _selectedSurfaceName = names.Count > 0 ? names[_selectedSurface] : null;

            VisualElement listPanel = new();
            listPanel.style.width = 190f;
            listPanel.style.minWidth = 130f;
            listPanel.style.flexShrink = 0f;

            ListView list = new();
            list.itemsSource = names;
            list.fixedItemHeight = 22f;
            list.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            list.selectionType = SelectionType.Single;
            list.showBorder = true;
            list.showAlternatingRowBackgrounds = AlternatingRowBackground.None;
            list.makeItem = () => new Label();
            list.bindItem = (element, index) => ((Label)element).text = names[index];
            list.style.flexGrow = 1f;
            list.selectionChanged += selection =>
            {
                string selectedName = selection.OfType<string>().FirstOrDefault();
                int index = names.IndexOf(selectedName);
                if (index >= 0 && index != _selectedSurface)
                {
                    _selectedSurface = index;
                    _selectedSurfaceName = selectedName;
                    RebuildUIContent();
                }
            };
            if (names.Count > 0)
            {
                list.SetSelectionWithoutNotify(new[] { _selectedSurface });
            }

            VisualElement buttons = new();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.height = 24f;
            buttons.style.marginTop = 2f;
            Button add = new(() =>
            {
                Undo.RecordObject(owner, "Add Phys Sound Surface");
                string name = GetUniqueSurfaceName(surfaces);
                surfaces.Add(name, new PhysSoundSurface());
                _selectedSurfaceName = name;
                EditorUtility.SetDirty(owner);
                InvalidateValidation();
                RebuildUIContent();
            })
            {
                text = "+"
            };
            add.style.flexGrow = 1f;
            Button remove = new(() =>
            {
                if (names.Count == 0)
                {
                    return;
                }

                Undo.RecordObject(owner, "Remove Phys Sound Surface");
                int removedIndex = _selectedSurface;
                surfaces.Remove(names[removedIndex]);
                names.RemoveAt(removedIndex);
                _selectedSurface = Mathf.Clamp(_selectedSurface - 1, 0, Mathf.Max(0, surfaces.Count - 1));
                _selectedSurfaceName = names.Count > 0 ? names[_selectedSurface] : null;
                EditorUtility.SetDirty(owner);
                InvalidateValidation();
                RebuildUIContent();
            })
            {
                text = "−"
            };
            remove.style.flexGrow = 1f;
            remove.SetEnabled(names.Count > 0);
            buttons.Add(add);
            buttons.Add(remove);
            listPanel.Add(list);
            listPanel.Add(buttons);
            root.Add(listPanel);

            VisualElement details = new();
            details.style.flexGrow = 1f;
            details.style.marginLeft = 4f;
            if (names.Count == 0)
            {
                details.Add(new HelpBox(
                    "Custom surfaces are optional. Add one to map Physics Materials.",
                    HelpBoxMessageType.Info));
                root.Add(details);
                return root;
            }

            string currentName = names[_selectedSurface];
            TextField nameField = new("Name")
            {
                value = currentName,
                isDelayed = true
            };
            nameField.RegisterValueChangedCallback(evt =>
            {
                string editedName = evt.newValue;
                if (editedName != currentName && !surfaces.ContainsKey(editedName) &&
                    TryRenameSurface(owner, surfaces, currentName, editedName))
                {
                    _selectedSurfaceName = editedName;
                    InvalidateValidation();
                    RebuildUIContent();
                }
            });
            details.Add(nameField);

            SerializedObject serializedOwner = new(owner);
            serializedOwner.Update();
            string propertyPath = FindSurfacePropertyPath(serializedOwner, currentName);
            SerializedProperty materials = string.IsNullOrEmpty(propertyPath)
                ? null
                : serializedOwner.FindProperty($"{propertyPath}.value._materials");
#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
            SerializedProperty materials2D = string.IsNullOrEmpty(propertyPath)
                ? null
                : serializedOwner.FindProperty($"{propertyPath}.value._materials2D");
#endif
            if (materials == null)
            {
                details.Add(new HelpBox(
                    "Could not resolve this surface's serialized data.",
                    HelpBoxMessageType.Error));
                root.Add(details);
                return root;
            }

            ScrollView materialScroll = new();
            materialScroll.style.flexGrow = 1f;
            PropertyField materialsField = new(materials, "Physics Materials");
            materialsField.Bind(serializedOwner);
            materialsField.TrackPropertyValue(materials, _ => InvalidateValidation());
            materialScroll.Add(materialsField);
#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
            if (materials2D != null)
            {
                PropertyField materials2DField = new(materials2D, "Physics Materials 2D");
                materials2DField.Bind(serializedOwner);
                materials2DField.TrackPropertyValue(materials2D, _ => InvalidateValidation());
                materialScroll.Add(materials2DField);
            }
#endif
            details.Add(materialScroll);
            root.Add(details);
            return root;
        }

        private void DrawModeContent(Rect rect, Object owner)
        {

            if (_mode == PreviewMode.Surfaces)
            {
                DrawSurfaces(rect, owner);
                return;
            }

            if (_mode == PreviewMode.InteractionMapping)
            {
                DrawInteractionMapping(rect, owner);
                return;
            }

            if (_entries.Count == 0)
            {
                EditorGUI.LabelField(rect, "Add an interaction mapping to edit its audio.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _selectedInteraction = Mathf.Clamp(_selectedInteraction, 0, _entries.Count - 1);
            Rect interactionRect = TakeTop(ref rect, ToolbarHeight);
            DrawInteractionSelector(interactionRect, owner);
            PreviewEntry entry = _entries[_selectedInteraction];

            if (_mode == PreviewMode.ImpactRanges)
            {
                DrawImpactRanges(rect, owner, entry.Interaction);
                return;
            }

            if (_mode == PreviewMode.ImpactAudio)
            {
                DrawImpactAudio(rect, owner, entry.Interaction);
                return;
            }

            if (_mode == PreviewMode.Curves)
            {
                DrawCurves(rect, owner, entry);
                return;
            }

            Rect clipRect = TakeTop(ref rect, ClipRowHeight);
            AudioClip clip = DrawClipField(clipRect, owner, entry.Interaction);
            rect.yMin += Spacing;

            Rect controlsRect = new(rect.x, rect.yMax - ControlsHeight, rect.width, ControlsHeight);
            rect.yMax = controlsRect.y - Spacing;

            if (clip == null)
            {
                EditorGUI.HelpBox(rect, "Assign a source AudioClip, then drag directly on its waveform.", MessageType.Info);
                return;
            }

            List<PhysSoundAudioRegion> regions = GetRegions(entry.Interaction);
            DrawWaveform(rect, clip);
            DrawRegions(rect, clip, regions);
            HandleWaveformInput(rect, clip, owner, regions);
            DrawControls(controlsRect, owner, entry.Interaction, clip, regions, -1);
        }

        internal static void Stop()
        {
            StopAllPreviewClipsMethod?.Invoke(null, null);
            EditorApplication.update -= UpdatePlayback;
            _playingClip = null;
            _playingEndSample = -1;
        }

        private void BuildEntries(Object owner)
        {
            _entries.Clear();

            if (owner is PhysSoundSettings settings)
            {
                _entries.Add(new PreviewEntry("Default", settings.DefaultInteraction, "_defaultInteraction"));
                AddInteractions(settings.Interactions, owner, "_interactions");
            }
            else if (owner is PhysSoundSubprofile subprofile)
            {
                AddInteractions(subprofile.Interactions, owner, "_interactions");
            }

            _interactionNames = new string[_entries.Count];
            for (int i = 0; i < _entries.Count; i++)
            {
                _interactionNames[i] = _entries[i].Name;
            }
        }

        private void AddInteractions(
            Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> interactions,
            Object owner,
            string dictionaryPath)
        {
            if (interactions == null)
            {
                return;
            }

            SerializedObject serializedOwner = new(owner);
            List<KeyValuePair<PhysSoundInteractionKey, PhysSoundInteraction>> sortedInteractions = new(interactions);
            sortedInteractions.Sort((left, right) => string.Compare(
                GetMappingLabel(left.Key),
                GetMappingLabel(right.Key),
                StringComparison.OrdinalIgnoreCase));
            foreach ((PhysSoundInteractionKey key, PhysSoundInteraction interaction) in sortedInteractions)
            {
                if (interaction != null)
                {
                    _entries.Add(new PreviewEntry(
                        key.PreviewName,
                        interaction,
                        FindInteractionPropertyPath(serializedOwner, dictionaryPath, key)));
                }
            }
        }

        private static string FindInteractionPropertyPath(
            SerializedObject serializedOwner,
            string dictionaryPath,
            PhysSoundInteractionKey key)
        {
            SerializedProperty dictionary = serializedOwner.FindProperty(dictionaryPath);
            for (int i = 0; dictionary != null && i < dictionary.arraySize; i++)
            {
                string elementPath = $"{dictionaryPath}.Array.data[{i}]";
                SerializedProperty surfaceA = serializedOwner.FindProperty($"{elementPath}.key._surfaceA");
                SerializedProperty surfaceB = serializedOwner.FindProperty($"{elementPath}.key._surfaceB");
                if (surfaceA != null && surfaceB != null &&
                    string.Equals(surfaceA.stringValue, key.SurfaceA, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(surfaceB.stringValue, key.SurfaceB, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{elementPath}.value";
                }
            }

            return null;
        }

        private void DrawToolbar(Rect rect, Object owner)
        {
            EnsureValidation(owner);
            int selectedMode = GUI.Toolbar(rect, (int)_mode, _tabContents, EditorStyles.toolbarButton);
            if (selectedMode == (int)_mode)
            {
                return;
            }

            Stop();
            _mode = (PreviewMode)selectedMode;
            _selectedRegion = -1;
            _dragImpactBoundary = -1;
            _impactBoundaryUndoRecorded = false;
            _dragMode = DragMode.None;
            _impactSourceScroll = Vector2.zero;
            ResetView();
        }

        private void DrawInteractionSelector(Rect rect, Object owner)
        {
            Rect copyRect = new(rect.xMax - 94f, rect.y, 94f, rect.height);
            Rect nextRect = new(copyRect.x - Spacing - 22f, rect.y, 22f, rect.height);
            Rect previousRect = new(nextRect.x - Spacing - 22f, rect.y, 22f, rect.height);
            rect.xMax = previousRect.x - Spacing;
            int selectedInteraction = EditorGUI.Popup(
                rect,
                _selectedInteraction,
                _interactionNames,
                EditorStyles.toolbarPopup);

            if (selectedInteraction != _selectedInteraction)
            {
                SelectInteraction(selectedInteraction);
            }

            using (new EditorGUI.DisabledScope(_selectedInteraction <= 0))
            {
                if (GUI.Button(previousRect, "<"))
                {
                    SelectInteraction(_selectedInteraction - 1);
                }
            }

            using (new EditorGUI.DisabledScope(_selectedInteraction >= _entries.Count - 1))
            {
                if (GUI.Button(nextRect, ">"))
                {
                    SelectInteraction(_selectedInteraction + 1);
                }
            }

            DrawCopyFromButton(copyRect, owner, _entries[_selectedInteraction].PropertyPath);
        }

        private void SelectInteraction(int index)
        {
            Stop();
            _selectedInteraction = Mathf.Clamp(index, 0, _entries.Count - 1);
            _selectedRegion = -1;
            _dragImpactBoundary = -1;
            _impactBoundaryUndoRecorded = false;
            _dragMode = DragMode.None;
            _impactSourceScroll = Vector2.zero;
            ResetView();
            InvalidateValidation();
        }

        private void DrawCopyFromButton(Rect rect, Object owner, string targetPropertyPath)
        {
            int sourceCount = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!string.Equals(_entries[i].PropertyPath, targetPropertyPath, StringComparison.Ordinal))
                {
                    sourceCount++;
                }
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(targetPropertyPath) || sourceCount == 0))
            {
                GUIContent content = new(
                    "Copy From",
                    "Replace this entire Interaction with an independent copy of another Interaction.");
                if (!EditorGUI.DropdownButton(rect, content, FocusType.Keyboard))
                {
                    return;
                }

                GenericMenu menu = new();
                for (int i = 0; i < _entries.Count; i++)
                {
                    PreviewEntry source = _entries[i];
                    if (string.Equals(source.PropertyPath, targetPropertyPath, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string sourcePath = source.PropertyPath;
                    menu.AddItem(
                        new GUIContent(source.Name.Replace('/', '／')),
                        false,
                        () => CopyInteraction(owner, targetPropertyPath, sourcePath));
                }

                menu.DropDown(rect);
            }
        }

        private void CopyInteraction(Object owner, string targetPropertyPath, string sourcePropertyPath)
        {
            if (string.IsNullOrEmpty(targetPropertyPath) || string.IsNullOrEmpty(sourcePropertyPath) ||
                string.Equals(targetPropertyPath, sourcePropertyPath, StringComparison.Ordinal))
            {
                return;
            }

            SerializedObject serializedOwner = new(owner);
            serializedOwner.Update();
            SerializedProperty target = serializedOwner.FindProperty(targetPropertyPath);
            SerializedProperty source = serializedOwner.FindProperty(sourcePropertyPath);
            if (target == null || source == null)
            {
                return;
            }

            Undo.RecordObject(owner, "Copy Phys Sound Interaction");
            target.boxedValue = source.boxedValue;
            serializedOwner.ApplyModifiedProperties();
            EditorUtility.SetDirty(owner);
            Stop();
            InvalidateValidation();
            _uiContent?.schedule.Execute(RebuildUIContent);
        }

        private void ShowCopyFromDropdown(Button button, Object owner, string targetPropertyPath)
        {
            if (string.IsNullOrEmpty(targetPropertyPath))
            {
                return;
            }

            GenericDropdownMenu menu = new();
            for (int i = 0; i < _entries.Count; i++)
            {
                PreviewEntry source = _entries[i];
                if (string.Equals(source.PropertyPath, targetPropertyPath, StringComparison.Ordinal))
                {
                    continue;
                }

                string sourcePath = source.PropertyPath;
                menu.AddItem(
                    source.Name.Replace('/', '／'),
                    false,
                    () => CopyInteraction(owner, targetPropertyPath, sourcePath));
            }

            menu.DropDown(button.worldBound, button, true);
        }

        private void DrawSurfaces(Rect rect, Object owner)
        {
            Dictionary<string, PhysSoundSurface> surfaces = GetSurfaces(owner);
            if (surfaces == null)
            {
                EditorGUI.HelpBox(rect, "Surface data is unavailable.", MessageType.Error);
                return;
            }

            List<string> names = new(surfaces.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            int selectedIndex = names.FindIndex(name =>
                string.Equals(name, _selectedSurfaceName, StringComparison.Ordinal));
            if (selectedIndex >= 0)
            {
                _selectedSurface = selectedIndex;
            }

            Rect selectionRow = TakeTop(ref rect, ClipRowHeight);
            Rect addRect = new(selectionRow.xMax - 45f, selectionRow.y, 21f, selectionRow.height);
            Rect removeRect = new(addRect.xMax + Spacing, selectionRow.y, 21f, selectionRow.height);
            selectionRow.xMax = addRect.x - Spacing;

            if (names.Count > 0)
            {
                _selectedSurface = Mathf.Clamp(_selectedSurface, 0, names.Count - 1);
                _selectedSurface = EditorGUI.Popup(selectionRow, "Surface", _selectedSurface, names.ToArray());
                _selectedSurfaceName = names[_selectedSurface];
            }
            else
            {
                EditorGUI.LabelField(selectionRow, "Surface", "No custom surfaces");
            }

            if (GUI.Button(addRect, "+"))
            {
                Undo.RecordObject(owner, "Add Phys Sound Surface");
                string name = GetUniqueSurfaceName(surfaces);
                surfaces.Add(name, new PhysSoundSurface());
                _selectedSurfaceName = name;
                EditorUtility.SetDirty(owner);
                return;
            }

            using (new EditorGUI.DisabledScope(names.Count == 0))
            {
                if (GUI.Button(removeRect, "−"))
                {
                    Undo.RecordObject(owner, "Remove Phys Sound Surface");
                    int removedIndex = _selectedSurface;
                    surfaces.Remove(names[removedIndex]);
                    names.RemoveAt(removedIndex);
                    _selectedSurface = Mathf.Clamp(_selectedSurface - 1, 0, Mathf.Max(0, surfaces.Count - 1));
                    _selectedSurfaceName = names.Count > 0 ? names[_selectedSurface] : null;
                    EditorUtility.SetDirty(owner);
                    return;
                }
            }

            if (names.Count == 0)
            {
                EditorGUI.HelpBox(rect, "Custom surfaces are optional. Add one to map Physics Materials.", MessageType.Info);
                return;
            }

            string currentName = names[_selectedSurface];
            Rect nameRow = TakeTop(ref rect, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginChangeCheck();
            string editedName = EditorGUI.TextField(nameRow, "Name", currentName);
            if (EditorGUI.EndChangeCheck() && editedName != currentName && !surfaces.ContainsKey(editedName) &&
                TryRenameSurface(owner, surfaces, currentName, editedName))
            {
                _selectedSurfaceName = editedName;
                currentName = editedName;
            }

            SerializedObject serializedOwner = new(owner);
            serializedOwner.Update();
            string propertyPath = FindSurfacePropertyPath(serializedOwner, currentName);
            SerializedProperty materials = string.IsNullOrEmpty(propertyPath)
                ? null
                : serializedOwner.FindProperty($"{propertyPath}.value._materials");
#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
            SerializedProperty materials2D = string.IsNullOrEmpty(propertyPath)
                ? null
                : serializedOwner.FindProperty($"{propertyPath}.value._materials2D");
#endif
            if (materials == null)
            {
                EditorGUI.HelpBox(rect, "Could not resolve this surface's serialized data.", MessageType.Error);
                return;
            }

            float contentHeight = EditorGUI.GetPropertyHeight(materials, true);
#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
            if (materials2D != null)
            {
                contentHeight += Spacing + EditorGUI.GetPropertyHeight(materials2D, true);
            }
#endif
            Rect view = new(0f, 0f, Mathf.Max(0f, rect.width - 16f), contentHeight);
            _surfaceScroll = GUI.BeginScrollView(rect, _surfaceScroll, view);
            Rect materialsRect = new(view.x, view.y, view.width, EditorGUI.GetPropertyHeight(materials, true));
            EditorGUI.PropertyField(materialsRect, materials, new GUIContent("Physics Materials"), true);
#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
            if (materials2D != null)
            {
                Rect materials2DRect = new(
                    view.x,
                    materialsRect.yMax + Spacing,
                    view.width,
                    EditorGUI.GetPropertyHeight(materials2D, true));
                EditorGUI.PropertyField(materials2DRect, materials2D, new GUIContent("Physics Materials 2D"), true);
            }
#endif
            GUI.EndScrollView();
            serializedOwner.ApplyModifiedProperties();
        }

        private VisualElement CreateMappingTable(Object owner)
        {
            Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> interactions = GetInteractions(owner);
            Dictionary<string, PhysSoundSurface> surfaces = GetSurfaces(owner);
            VisualElement root = new();
            root.style.flexGrow = 1f;
            root.style.paddingLeft = 2f;
            root.style.paddingRight = 2f;
            root.style.paddingTop = 2f;
            root.style.paddingBottom = 2f;
            if (interactions == null || surfaces == null)
            {
                root.Add(new HelpBox("Interaction mapping data is unavailable.", HelpBoxMessageType.Error));
                return root;
            }

            BuildEntries(owner);
            HashSet<string> knownSurfaces = new(StringComparer.OrdinalIgnoreCase)
            {
                PhysSoundSettings.DefaultSurface
            };
            foreach (string surfaceName in surfaces.Keys)
            {
                if (!string.IsNullOrWhiteSpace(surfaceName))
                {
                    knownSurfaces.Add(surfaceName.Trim());
                }
            }

            List<MappingRow> rows = new();
            SerializedObject serializedOwner = new(owner);
            foreach ((PhysSoundInteractionKey key, PhysSoundInteraction interaction) in interactions)
            {
                rows.Add(new MappingRow(
                    key,
                    interaction,
                    FindInteractionPropertyPath(serializedOwner, "_interactions", key),
                    ValidateMappingRow(key, interaction, knownSurfaces)));
            }

            rows.Sort((left, right) => string.Compare(
                GetMappingLabel(left.Key),
                GetMappingLabel(right.Key),
                StringComparison.OrdinalIgnoreCase));

            Columns columns = new();
            columns.reorderable = false;
            columns.Add(new Column
            {
                name = "validation",
                title = " ",
                visible = true,
                width = 28f,
                minWidth = 28f,
                maxWidth = 28f,
                stretchable = false,
                optional = false,
                resizable = false,
                sortable = false,
                makeCell = () =>
                {
                    VisualElement cell = new();
                    cell.style.width = 28f;
                    cell.style.minWidth = 28f;
                    cell.style.maxWidth = 28f;
                    cell.style.flexShrink = 0f;
                    cell.style.alignItems = Align.Center;
                    cell.style.justifyContent = Justify.Center;

                    UnityEngine.UIElements.Image image = new();
                    image.name = "mapping-error-icon";
                    image.sprite = GetErrorSprite();
                    image.scaleMode = ScaleMode.ScaleToFit;
                    image.style.width = 16f;
                    image.style.height = 16f;
                    image.style.flexShrink = 0f;
                    cell.Add(image);
                    return cell;
                },
                bindCell = (element, index) =>
                {
                    string error = rows[index].Error;
                    element.tooltip = error;
                    UnityEngine.UIElements.Image image = (UnityEngine.UIElements.Image)element[0];
                    image.tooltip = error;
                    image.style.visibility = string.IsNullOrEmpty(error) ? Visibility.Hidden : Visibility.Visible;
                }
            });
            columns.Add(CreateSurfaceColumn("surfaceA", "Surface A", false, owner, interactions, surfaces, rows));
            columns.Add(CreateSurfaceColumn("surfaceB", "Surface B", true, owner, interactions, surfaces, rows));
            columns.Add(new Column
            {
                name = "source",
                title = "Source",
                width = 105f,
                minWidth = 90f,
                stretchable = false,
                optional = false,
                sortable = false,
                makeCell = () =>
                {
                    Button button = new();
                    button.text = "Copy From";
                    button.tooltip = "Replace this entire Interaction with an independent copy of another Interaction.";
                    button.clicked += () => ShowCopyFromDropdown(button, owner, button.userData as string);
                    return button;
                },
                bindCell = (element, index) =>
                {
                    Button button = (Button)element;
                    button.userData = rows[index].PropertyPath;
                    button.SetEnabled(!string.IsNullOrEmpty(rows[index].PropertyPath) && _entries.Count > 1);
                }
            });
            columns.Add(new Column
            {
                name = "remove",
                title = string.Empty,
                width = 28f,
                minWidth = 28f,
                maxWidth = 28f,
                stretchable = false,
                optional = false,
                resizable = false,
                sortable = false,
                makeCell = () =>
                {
                    Button button = new();
                    button.text = "−";
                    button.clicked += () =>
                    {
                        if (button.userData is not PhysSoundInteractionKey key)
                        {
                            return;
                        }

                        Undo.RecordObject(owner, "Remove Phys Sound Interaction Mapping");
                        interactions.Remove(key);
                        _selectedInteraction = 0;
                        EditorUtility.SetDirty(owner);
                        InvalidateValidation();
                        RebuildUIContent();
                    };
                    return button;
                },
                bindCell = (element, index) => element.userData = rows[index].Key
            });

            MultiColumnListView table = new(columns);
            table.fixedItemHeight = 22f;
            table.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            table.selectionType = SelectionType.None;
            table.reorderable = false;
            table.showBorder = true;
            table.showAlternatingRowBackgrounds = AlternatingRowBackground.None;
            table.style.flexGrow = 1f;
            table.itemsSource = rows;
            root.Add(table);
            root.Add(CreateMappingAddRow(owner, interactions, surfaces));
            return root;
        }

        private Column CreateSurfaceColumn(
            string name,
            string title,
            bool allowAny,
            Object owner,
            Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> interactions,
            Dictionary<string, PhysSoundSurface> surfaces,
            List<MappingRow> rows)
        {
            return new Column
            {
                name = name,
                title = title,
                width = 160f,
                minWidth = 90f,
                stretchable = true,
                optional = false,
                sortable = false,
                makeCell = () =>
                {
                    DropdownField field = new();
                    field.style.flexGrow = 1f;
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (field.userData is not MappingSurfaceBinding binding)
                        {
                            return;
                        }

                        int selected = binding.Labels.IndexOf(evt.newValue);
                        if (selected < 0)
                        {
                            return;
                        }

                        string surfaceA = binding.IsSurfaceB ? binding.Key.SurfaceA : binding.Values[selected];
                        string surfaceB = binding.IsSurfaceB ? binding.Values[selected] : binding.Key.SurfaceB;
                        ReplaceInteractionKey(
                            binding.Owner,
                            binding.Interactions,
                            binding.Key,
                            new PhysSoundInteractionKey(surfaceA, surfaceB));
                        RebuildUIContent();
                    });
                    return field;
                },
                bindCell = (element, index) =>
                {
                    DropdownField field = (DropdownField)element;
                    MappingRow row = rows[index];
                    List<string> values = BuildSurfaceValues(surfaces, allowAny);
                    List<string> labels = BuildSurfaceLabels(values);
                    field.userData = new MappingSurfaceBinding(
                        owner,
                        interactions,
                        row.Key,
                        allowAny,
                        values,
                        labels);
                    field.choices = labels;
                    string current = allowAny ? row.Key.SurfaceB : row.Key.SurfaceA;
                    int selected = values.FindIndex(value => string.Equals(value, current, StringComparison.OrdinalIgnoreCase));
                    string label = selected >= 0 ? labels[selected] : current;
                    field.tooltip = string.Empty;
                    field.SetValueWithoutNotify(label);
                }
            };
        }

        private VisualElement CreateMappingAddRow(
            Object owner,
            Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> interactions,
            Dictionary<string, PhysSoundSurface> surfaces)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexShrink = 0f;
            row.style.height = 24f;
            row.style.marginTop = 2f;

            DropdownField surfaceA = new();
            DropdownField surfaceB = new();
            DropdownField source = new();
            surfaceA.style.flexGrow = 1f;
            surfaceB.style.flexGrow = 1f;
            source.style.width = 105f;
            source.tooltip = "Interaction whose values will be copied into the new Mapping.";
            Button add = new(() =>
            {
                AddInteractionMapping(
                    owner,
                    interactions,
                    new PhysSoundInteractionKey(_newMappingSurfaceA, _newMappingSurfaceB),
                    _newMappingSourcePath);
                RebuildUIContent();
            })
            {
                text = "+"
            };
            add.style.width = 28f;

            void RefreshChoices()
            {
                BuildEntries(owner);
                List<PhysSoundInteractionKey> available = BuildAvailableMappings(surfaces, interactions);
                List<string> firstValues = new();
                for (int i = 0; i < available.Count; i++)
                {
                    if (!firstValues.Exists(value => string.Equals(value, available[i].SurfaceA, StringComparison.OrdinalIgnoreCase)))
                    {
                        firstValues.Add(available[i].SurfaceA);
                    }
                }

                int firstIndex = firstValues.FindIndex(value => string.Equals(value, _newMappingSurfaceA, StringComparison.OrdinalIgnoreCase));
                firstIndex = Mathf.Max(0, firstIndex);
                List<string> firstLabels = BuildSurfaceLabels(firstValues);
                surfaceA.choices = firstLabels;
                surfaceA.SetValueWithoutNotify(firstLabels.Count > 0 ? firstLabels[firstIndex] : "None");
                _newMappingSurfaceA = firstValues.Count > 0 ? firstValues[firstIndex] : null;

                List<string> secondValues = new();
                for (int i = 0; i < available.Count; i++)
                {
                    if (string.Equals(available[i].SurfaceA, _newMappingSurfaceA, StringComparison.OrdinalIgnoreCase))
                    {
                        secondValues.Add(available[i].SurfaceB);
                    }
                }

                int secondIndex = secondValues.FindIndex(value => string.Equals(value, _newMappingSurfaceB, StringComparison.OrdinalIgnoreCase));
                secondIndex = Mathf.Max(0, secondIndex);
                List<string> secondLabels = BuildSurfaceLabels(secondValues);
                surfaceB.choices = secondLabels;
                surfaceB.SetValueWithoutNotify(secondLabels.Count > 0 ? secondLabels[secondIndex] : "None");
                _newMappingSurfaceB = secondValues.Count > 0 ? secondValues[secondIndex] : null;

                List<string> sourceLabels = new() { "New" };
                for (int i = 0; i < _entries.Count; i++)
                {
                    sourceLabels.Add(_entries[i].Name);
                }

                int sourceIndex = 0;
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (string.Equals(_entries[i].PropertyPath, _newMappingSourcePath, StringComparison.Ordinal))
                    {
                        sourceIndex = i + 1;
                        break;
                    }
                }

                source.choices = sourceLabels;
                source.SetValueWithoutNotify(sourceLabels[sourceIndex]);
                _newMappingSourcePath = sourceIndex == 0 ? null : _entries[sourceIndex - 1].PropertyPath;
                add.SetEnabled(available.Count > 0);

                surfaceA.userData = firstValues;
                surfaceB.userData = secondValues;
            }

            surfaceA.RegisterValueChangedCallback(evt =>
            {
                if (surfaceA.userData is List<string> values)
                {
                    int index = surfaceA.choices.IndexOf(evt.newValue);
                    _newMappingSurfaceA = index >= 0 ? values[index] : null;
                    _newMappingSurfaceB = null;
                    RefreshChoices();
                }
            });
            surfaceB.RegisterValueChangedCallback(evt =>
            {
                if (surfaceB.userData is List<string> values)
                {
                    int index = surfaceB.choices.IndexOf(evt.newValue);
                    _newMappingSurfaceB = index >= 0 ? values[index] : null;
                }
            });
            source.RegisterValueChangedCallback(evt =>
            {
                int index = source.choices.IndexOf(evt.newValue);
                _newMappingSourcePath = index <= 0 ? null : _entries[index - 1].PropertyPath;
            });

            row.Add(surfaceA);
            row.Add(surfaceB);
            row.Add(source);
            row.Add(add);
            RefreshChoices();
            return row;
        }

        private void DrawInteractionMapping(Rect rect, Object owner)
        {
            Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> interactions = GetInteractions(owner);
            Dictionary<string, PhysSoundSurface> surfaces = GetSurfaces(owner);
            if (interactions == null || surfaces == null)
            {
                EditorGUI.HelpBox(rect, "Interaction mapping data is unavailable.", MessageType.Error);
                return;
            }

            List<PhysSoundInteractionKey> keys = new(interactions.Keys);
            keys.Sort((left, right) => string.Compare(
                GetMappingLabel(left),
                GetMappingLabel(right),
                StringComparison.OrdinalIgnoreCase));
            Rect footer = new(rect.x, rect.yMax - ClipRowHeight, rect.width, ClipRowHeight);
            rect.yMax = footer.y - Spacing;
            Rect header = TakeTop(ref rect, ClipRowHeight);
            GUI.Box(header, GUIContent.none, EditorStyles.toolbar);

            const float removeWidth = 24f;
            float contentHeight = Mathf.Max(rect.height, keys.Count * (ClipRowHeight + Spacing));
            float columnWidth = (header.width - 16f - removeWidth - Spacing * 2f) * 0.5f;
            GUI.Label(new Rect(header.x + 4f, header.y, columnWidth - 4f, header.height), "Surface A", EditorStyles.miniLabel);
            GUI.Label(
                new Rect(header.x + columnWidth + Spacing + 4f, header.y, columnWidth - 4f, header.height),
                "Surface B",
                EditorStyles.miniLabel);

            string[] surfaceOptions = BuildSurfaceOptions(surfaces);
            GUI.Box(rect, GUIContent.none);
            Rect scrollRect = new(rect.x + 2f, rect.y + 2f, rect.width - 4f, Mathf.Max(0f, rect.height - 4f));
            Rect view = new(0f, 0f, scrollRect.width - 16f, contentHeight);
            _mappingScroll = GUI.BeginScrollView(scrollRect, _mappingScroll, view, false, true);

            PhysSoundInteractionKey keyToRemove = default;
            PhysSoundInteractionKey oldKey = default;
            PhysSoundInteractionKey newKey = default;
            bool remove = false;
            bool replace = false;
            for (int i = 0; i < keys.Count; i++)
            {
                PhysSoundInteractionKey key = keys[i];
                float y = i * (ClipRowHeight + Spacing);
                Rect firstRect = new(0f, y, columnWidth, ClipRowHeight);
                Rect secondRect = new(firstRect.xMax + Spacing, y, columnWidth, ClipRowHeight);
                Rect removeRect = new(secondRect.xMax + Spacing, y, removeWidth, ClipRowHeight);

                string surfaceA = DrawSurfacePopup(firstRect, key.SurfaceA, surfaceOptions, false);
                string surfaceB = DrawSurfacePopup(secondRect, key.SurfaceB, surfaceOptions, true);
                PhysSoundInteractionKey editedKey = new(surfaceA, surfaceB);
                if (!editedKey.Equals(key))
                {
                    oldKey = key;
                    newKey = editedKey;
                    replace = true;
                }

                if (GUI.Button(removeRect, "−"))
                {
                    keyToRemove = key;
                    remove = true;
                }
            }

            GUI.EndScrollView();

            if (remove)
            {
                Undo.RecordObject(owner, "Remove Phys Sound Interaction Mapping");
                interactions.Remove(keyToRemove);
                _selectedInteraction = 0;
                EditorUtility.SetDirty(owner);
                return;
            }

            if (replace)
            {
                ReplaceInteractionKey(owner, interactions, oldKey, newKey);
                return;
            }

            List<PhysSoundInteractionKey> availableMappings = BuildAvailableMappings(surfaces, interactions);
            List<string> firstOptions = new();
            for (int i = 0; i < availableMappings.Count; i++)
            {
                string surfaceA = availableMappings[i].SurfaceA;
                if (!firstOptions.Exists(value => string.Equals(value, surfaceA, StringComparison.OrdinalIgnoreCase)))
                {
                    firstOptions.Add(surfaceA);
                }
            }

            Rect firstAddRect = new(footer.x, footer.y, columnWidth, footer.height);
            Rect secondAddRect = new(firstAddRect.xMax + Spacing, footer.y, columnWidth, footer.height);
            Rect addRect = new(secondAddRect.xMax + Spacing, footer.y, removeWidth, footer.height);
            using (new EditorGUI.DisabledScope(availableMappings.Count == 0))
            {
                _newMappingSurfaceA = DrawMappingOptionPopup(firstAddRect, _newMappingSurfaceA, firstOptions);

                List<string> secondOptions = new();
                for (int i = 0; i < availableMappings.Count; i++)
                {
                    PhysSoundInteractionKey candidate = availableMappings[i];
                    if (string.Equals(candidate.SurfaceA, _newMappingSurfaceA, StringComparison.OrdinalIgnoreCase))
                    {
                        secondOptions.Add(candidate.SurfaceB);
                    }
                }

                _newMappingSurfaceB = DrawMappingOptionPopup(secondAddRect, _newMappingSurfaceB, secondOptions);
                if (GUI.Button(addRect, "+"))
                {
                    AddInteractionMapping(
                        owner,
                        interactions,
                        new PhysSoundInteractionKey(_newMappingSurfaceA, _newMappingSurfaceB));
                }
            }
        }

        private void EnsureValidation(Object owner)
        {
            double time = EditorApplication.timeSinceStartup;
            if (!_validationDirty && owner == _validatedOwner && time < _nextValidationTime)
            {
                return;
            }

            _validatedOwner = owner;
            _validationDirty = false;
            _nextValidationTime = time + 0.5d;
            _tabContents[(int)PreviewMode.Surfaces] = CreateTabContent(
                "Materials",
                "Define named surfaces and assign their Physics Materials and Physics Materials 2D.",
                GetValidationError(owner, PreviewMode.Surfaces));
            _tabContents[(int)PreviewMode.InteractionMapping] = CreateTabContent(
                "Mapping",
                "Map pairs of named surfaces to Interactions. An empty Surface B matches any surface.",
                GetValidationError(owner, PreviewMode.InteractionMapping));
            _tabContents[(int)PreviewMode.ImpactRanges] = CreateTabContent(
                "Force",
                "Split impact impulse into nonlinear force ranges and test each range.",
                GetValidationError(owner, PreviewMode.ImpactRanges));
            _tabContents[(int)PreviewMode.ImpactAudio] = CreateTabContent(
                "Impact",
                "Assign impact clips to each force range and mark playable regions on their waveforms.",
                GetValidationError(owner, PreviewMode.ImpactAudio));
            _tabContents[(int)PreviewMode.SlideAudio] = CreateTabContent(
                "Slide",
                "Assign a sliding source clip and mark the loop region used for continuous contact audio.",
                GetValidationError(owner, PreviewMode.SlideAudio));
            _tabContents[(int)PreviewMode.Curves] = CreateTabContent(
                "Curves",
                "Edit the two-point volume and pitch response curves for impact and sliding audio.",
                GetValidationError(owner, PreviewMode.Curves));
        }

        private void InvalidateValidation()
        {
            _validationDirty = true;
        }

        private string GetValidationError(Object owner, PreviewMode mode)
        {
            if (mode == PreviewMode.Surfaces)
            {
                return ValidateSurfaces(owner);
            }

            if (mode == PreviewMode.InteractionMapping)
            {
                return ValidateInteractionMapping(owner);
            }

            if (_entries.Count == 0)
            {
                return "No Interaction is available.";
            }

            _selectedInteraction = Mathf.Clamp(_selectedInteraction, 0, _entries.Count - 1);
            PreviewEntry entry = _entries[_selectedInteraction];
            return mode switch
            {
                PreviewMode.ImpactRanges => ValidateImpactRanges(entry.Interaction),
                PreviewMode.ImpactAudio => ValidateImpactAudio(entry.Interaction),
                PreviewMode.SlideAudio => ValidateSlideAudio(entry.Interaction),
                PreviewMode.Curves => ValidateCurves(owner, entry),
                _ => string.Empty
            };
        }

        private static GUIContent CreateTabContent(string label, string description, string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                return new GUIContent(label, description);
            }

            return new GUIContent(label, GetErrorIcon(), $"{description}\n\nError: {error}");
        }

        private static Texture2D GetErrorIcon()
        {
            if (_errorIcon != null)
            {
                return _errorIcon;
            }

            _errorIcon = new Texture2D(16, 16, TextureFormat.RGBA32, false)
            {
                name = "Phys Sound Preview Error",
                hideFlags = HideFlags.HideAndDontSave
            };
            ImageConversion.LoadImage(_errorIcon, Convert.FromBase64String(ErrorIconString64), true);
            return _errorIcon;
        }

        private static Sprite GetErrorSprite()
        {
            if (_errorSprite != null)
            {
                return _errorSprite;
            }

            Texture2D texture = GetErrorIcon();
            _errorSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width);
            _errorSprite.name = "Phys Sound Preview Error";
            _errorSprite.hideFlags = HideFlags.HideAndDontSave;
            return _errorSprite;
        }

        private static string ValidateImpactRanges(PhysSoundInteraction interaction)
        {
            List<PhysSoundImpactRange> ranges = interaction?.ImpactRanges;
            if (ranges == null || ranges.Count == 0)
            {
                return "Create at least one impact force range.";
            }

            for (int i = 0; i < ranges.Count; i++)
            {
                PhysSoundImpactRange range = ranges[i];
                if (range == null)
                {
                    return $"Force range {i + 1} is missing.";
                }

                if (range.MinimumImpulse < 0f || range.MaximumImpulse <= range.MinimumImpulse)
                {
                    return $"Force range {i + 1} has invalid Min/Max values.";
                }

                if (i > 0 && Mathf.Abs(ranges[i - 1].MaximumImpulse - range.MinimumImpulse) > 0.001f)
                {
                    return $"Force ranges {i} and {i + 1} have a gap or overlap.";
                }
            }

            return string.Empty;
        }

        private static string ValidateImpactAudio(PhysSoundInteraction interaction)
        {
            string rangeError = ValidateImpactRanges(interaction);
            if (!string.IsNullOrEmpty(rangeError))
            {
                return rangeError;
            }

            for (int i = 0; i < interaction.ImpactRanges.Count; i++)
            {
                List<PhysSoundImpactClipSource> sources = interaction.ImpactRanges[i].ClipSources;
                if (sources == null || sources.Count == 0)
                {
                    return $"Force range {i + 1} has no impact clips.";
                }

                for (int j = 0; j < sources.Count; j++)
                {
                    PhysSoundImpactClipSource source = sources[j];
                    if (source?.SourceClip == null)
                    {
                        return $"Impact clip {j + 1} in force range {i + 1} is not assigned.";
                    }

                    string regionError = ValidateRegions(source.SourceClip, source.Regions);
                    if (!string.IsNullOrEmpty(regionError))
                    {
                        return $"Impact clip {j + 1} in force range {i + 1}: {regionError}";
                    }
                }
            }

            return string.Empty;
        }

        private static string ValidateSlideAudio(PhysSoundInteraction interaction)
        {
            if (interaction == null)
            {
                return "Interaction is missing.";
            }

            AudioClip source = interaction.SlideSourceClip;
            List<PhysSoundAudioRegion> regions = interaction.SlideRegions;
            bool hasRegions = regions != null && regions.Count > 0;
            if (source == null)
            {
                return hasRegions ? "Slide regions exist, but the source clip is not assigned." : string.Empty;
            }

            if (!hasRegions && !interaction.HasSlide)
            {
                return "Mark a slide loop or export a slide clip.";
            }

            return ValidateRegions(source, regions);
        }

        private static string ValidateRegions(AudioClip clip, List<PhysSoundAudioRegion> regions)
        {
            if (clip == null || regions == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < regions.Count; i++)
            {
                PhysSoundAudioRegion region = regions[i];
                if (region == null || region.StartTime < 0f || region.EndTime <= region.StartTime ||
                    region.EndTime > clip.length + 0.001f)
                {
                    return $"Region {i + 1} is outside the clip or has invalid bounds.";
                }
            }

            return string.Empty;
        }

        private static string ValidateCurves(Object owner, PreviewEntry entry)
        {
            if (string.IsNullOrEmpty(entry.PropertyPath))
            {
                return "Curve properties could not be resolved.";
            }

            SerializedObject serializedOwner = new(owner);
            string[] names = { "_impactVolume", "_impactPitch", "_slideVolume", "_slidePitch" };
            for (int i = 0; i < names.Length; i++)
            {
                SerializedProperty property = serializedOwner.FindProperty($"{entry.PropertyPath}.{names[i]}");
                AnimationCurve curve = property?.animationCurveValue;
                if (curve == null || curve.length != 2)
                {
                    return $"{ObjectNames.NicifyVariableName(names[i])} must contain exactly two points.";
                }

                Keyframe first = curve.keys[0];
                Keyframe last = curve.keys[1];
                if (Mathf.Abs(first.time) > 0.001f || Mathf.Abs(last.time - 1f) > 0.001f ||
                    !IsFinite(first.value) || !IsFinite(last.value) ||
                    !IsFinite(first.outTangent) || !IsFinite(last.inTangent))
                {
                    return $"{ObjectNames.NicifyVariableName(names[i])} contains invalid points or tangents.";
                }
            }

            return string.Empty;
        }

        private static string ValidateSurfaces(Object owner)
        {
            Dictionary<string, PhysSoundSurface> surfaces = GetSurfaces(owner);
            if (surfaces == null)
            {
                return "Surface data is unavailable.";
            }

            HashSet<string> normalizedNames = new(StringComparer.OrdinalIgnoreCase);
            HashSet<PhysicsMaterial> materials = new();
#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
            HashSet<PhysicsMaterial2D> materials2D = new();
#endif
            foreach ((string name, PhysSoundSurface surface) in surfaces)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return "A surface has no name.";
                }

                if (string.Equals(name.Trim(), PhysSoundSettings.DefaultSurface, StringComparison.OrdinalIgnoreCase))
                {
                    return $"\"{PhysSoundSettings.DefaultSurface}\" is reserved for the default surface.";
                }

                if (!normalizedNames.Add(name.Trim()))
                {
                    return $"Surface name \"{name}\" is duplicated with different casing.";
                }

                if (surface == null)
                {
                    return $"Surface \"{name}\" has no data.";
                }

                bool hasMaterial = false;
                PhysicsMaterial[] surfaceMaterials = surface.Materials;
                for (int i = 0; surfaceMaterials != null && i < surfaceMaterials.Length; i++)
                {
                    PhysicsMaterial material = surfaceMaterials[i];
                    if (material == null)
                    {
                        return $"Surface \"{name}\" has an unassigned Physics Material slot.";
                    }

                    hasMaterial = true;
                    if (!materials.Add(material))
                    {
                        return $"Physics Material \"{material.name}\" is assigned to more than one surface.";
                    }
                }

#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
                PhysicsMaterial2D[] surfaceMaterials2D = surface.Materials2D;
                for (int i = 0; surfaceMaterials2D != null && i < surfaceMaterials2D.Length; i++)
                {
                    PhysicsMaterial2D material = surfaceMaterials2D[i];
                    if (material == null)
                    {
                        return $"Surface \"{name}\" has an unassigned Physics Material 2D slot.";
                    }

                    hasMaterial = true;
                    if (!materials2D.Add(material))
                    {
                        return $"Physics Material 2D \"{material.name}\" is assigned to more than one surface.";
                    }
                }
#endif

                if (!hasMaterial)
                {
                    return $"Surface \"{name}\" has no Physics Materials.";
                }
            }

            return string.Empty;
        }

        private static string ValidateInteractionMapping(Object owner)
        {
            Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> interactions = GetInteractions(owner);
            Dictionary<string, PhysSoundSurface> surfaces = GetSurfaces(owner);
            if (interactions == null || surfaces == null)
            {
                return "Interaction mapping data is unavailable.";
            }

            HashSet<string> knownSurfaces = new(StringComparer.OrdinalIgnoreCase)
            {
                PhysSoundSettings.DefaultSurface
            };
            foreach (string surfaceName in surfaces.Keys)
            {
                if (!string.IsNullOrWhiteSpace(surfaceName))
                {
                    knownSurfaces.Add(surfaceName.Trim());
                }
            }

            HashSet<string> usedSurfaces = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> serializedPairs = new(StringComparer.OrdinalIgnoreCase);
            SerializedObject serializedOwner = new(owner);
            SerializedProperty serializedMappings = serializedOwner.FindProperty("_interactions");
            for (int i = 0; serializedMappings != null && i < serializedMappings.arraySize; i++)
            {
                string elementPath = $"_interactions.Array.data[{i}]";
                SerializedProperty surfaceAProperty = serializedOwner.FindProperty($"{elementPath}.key._surfaceA");
                SerializedProperty surfaceBProperty = serializedOwner.FindProperty($"{elementPath}.key._surfaceB");
                if (surfaceAProperty == null || surfaceBProperty == null)
                {
                    return $"Interaction mapping {i + 1} has invalid serialized data.";
                }

                string surfaceA = surfaceAProperty.stringValue?.Trim() ?? string.Empty;
                string surfaceB = surfaceBProperty.stringValue?.Trim() ?? string.Empty;
                string first = surfaceA;
                string second = surfaceB;
                if (string.Compare(first, second, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    (first, second) = (second, first);
                }

                if (!serializedPairs.Add($"{first}\n{second}"))
                {
                    return $"Interaction mapping {GetMappingLabel(new PhysSoundInteractionKey(surfaceA, surfaceB))} is duplicated.";
                }

                if (!string.IsNullOrEmpty(surfaceA))
                {
                    usedSurfaces.Add(surfaceA);
                }

                if (!string.IsNullOrEmpty(surfaceB))
                {
                    usedSurfaces.Add(surfaceB);
                }
            }

            if (serializedMappings != null && serializedMappings.arraySize != interactions.Count)
            {
                return "Some serialized interaction mappings could not be loaded. Check for duplicate pairs.";
            }

            foreach ((PhysSoundInteractionKey key, PhysSoundInteraction interaction) in interactions)
            {
                if (!key.HasConfiguredSurface)
                {
                    return "An interaction mapping has no surfaces.";
                }

                if (key.IsDefaultFallback)
                {
                    return "The Default fallback is edited separately and must not be duplicated in the mapping.";
                }

                if (interaction == null)
                {
                    return $"Interaction {GetMappingLabel(key)} has no data.";
                }

                if (!string.IsNullOrEmpty(key.SurfaceA) && !knownSurfaces.Contains(key.SurfaceA))
                {
                    return $"Interaction {GetMappingLabel(key)} references unknown Surface \"{key.SurfaceA}\".";
                }

                if (!string.IsNullOrEmpty(key.SurfaceB) && !knownSurfaces.Contains(key.SurfaceB))
                {
                    return $"Interaction {GetMappingLabel(key)} references unknown Surface \"{key.SurfaceB}\".";
                }
            }

            foreach (string surfaceName in surfaces.Keys)
            {
                if (!string.IsNullOrWhiteSpace(surfaceName) && !usedSurfaces.Contains(surfaceName.Trim()))
                {
                    return $"Surface \"{surfaceName}\" is not used by any Interaction mapping.";
                }
            }

            return string.Empty;
        }

        private static string ValidateMappingRow(
            PhysSoundInteractionKey key,
            PhysSoundInteraction interaction,
            HashSet<string> knownSurfaces)
        {
            if (!key.HasConfiguredSurface)
            {
                return "This Mapping has no surfaces.";
            }

            if (key.IsDefaultFallback)
            {
                return "Default ↔ Any is owned by the separate Default Interaction.";
            }

            if (interaction == null)
            {
                return "This Mapping has no Interaction data.";
            }

            if (!string.IsNullOrEmpty(key.SurfaceA) && !knownSurfaces.Contains(key.SurfaceA))
            {
                return $"Unknown Surface: {key.SurfaceA}.";
            }

            if (!string.IsNullOrEmpty(key.SurfaceB) && !knownSurfaces.Contains(key.SurfaceB))
            {
                return $"Unknown Surface: {key.SurfaceB}.";
            }

            return string.Empty;
        }

        private static Dictionary<string, PhysSoundSurface> GetSurfaces(Object owner)
        {
            return owner switch
            {
                PhysSoundSettings settings => settings.Surfaces,
                PhysSoundSubprofile subprofile => subprofile.Surfaces,
                _ => null
            };
        }

        private static Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> GetInteractions(Object owner)
        {
            return owner switch
            {
                PhysSoundSettings settings => settings.Interactions,
                PhysSoundSubprofile subprofile => subprofile.Interactions,
                _ => null
            };
        }

        private static string FindSurfacePropertyPath(SerializedObject serializedOwner, string surfaceName)
        {
            SerializedProperty dictionary = serializedOwner.FindProperty("_surfaces");
            for (int i = 0; dictionary != null && i < dictionary.arraySize; i++)
            {
                string elementPath = $"_surfaces.Array.data[{i}]";
                SerializedProperty key = serializedOwner.FindProperty($"{elementPath}.key");
                if (key != null && string.Equals(key.stringValue, surfaceName, StringComparison.Ordinal))
                {
                    return elementPath;
                }
            }

            return null;
        }

        private static string GetUniqueSurfaceName(Dictionary<string, PhysSoundSurface> surfaces)
        {
            const string baseName = "Surface";
            string candidate = baseName;
            int suffix = 2;
            while (surfaces.ContainsKey(candidate))
            {
                candidate = $"{baseName} {suffix++}";
            }

            return candidate;
        }

        private static bool TryRenameSurface(
            Object owner,
            Dictionary<string, PhysSoundSurface> surfaces,
            string oldName,
            string newName)
        {
            Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> interactions = GetInteractions(owner);
            List<(PhysSoundInteractionKey OldKey, PhysSoundInteractionKey NewKey, PhysSoundInteraction Value)>
                replacements = new();
            HashSet<PhysSoundInteractionKey> replacedKeys = new();
            if (interactions != null)
            {
                foreach ((PhysSoundInteractionKey key, PhysSoundInteraction interaction) in interactions)
                {
                    bool replaceA = string.Equals(key.SurfaceA, oldName, StringComparison.OrdinalIgnoreCase);
                    bool replaceB = string.Equals(key.SurfaceB, oldName, StringComparison.OrdinalIgnoreCase);
                    if (!replaceA && !replaceB)
                    {
                        continue;
                    }

                    PhysSoundInteractionKey newKey = new(
                        replaceA ? newName : key.SurfaceA,
                        replaceB ? newName : key.SurfaceB);
                    replacements.Add((key, newKey, interaction));
                    replacedKeys.Add(key);
                }

                HashSet<PhysSoundInteractionKey> newKeys = new();
                for (int i = 0; i < replacements.Count; i++)
                {
                    PhysSoundInteractionKey newKey = replacements[i].NewKey;
                    if (!newKeys.Add(newKey) ||
                        (interactions.ContainsKey(newKey) && !replacedKeys.Contains(newKey)))
                    {
                        return false;
                    }
                }
            }

            Undo.RecordObject(owner, "Rename Phys Sound Surface");
            PhysSoundSurface surface = surfaces[oldName];
            surfaces.Remove(oldName);
            surfaces.Add(newName, surface);

            if (interactions != null)
            {
                for (int i = 0; i < replacements.Count; i++)
                {
                    interactions.Remove(replacements[i].OldKey);
                }

                for (int i = 0; i < replacements.Count; i++)
                {
                    interactions.Add(replacements[i].NewKey, replacements[i].Value);
                }
            }

            EditorUtility.SetDirty(owner);
            return true;
        }

        private static List<PhysSoundInteractionKey> BuildAvailableMappings(
            Dictionary<string, PhysSoundSurface> surfaces,
            Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> interactions)
        {
            List<string> surfaceNames = new() { PhysSoundSettings.DefaultSurface };
            foreach (string surfaceName in surfaces.Keys)
            {
                if (!string.IsNullOrWhiteSpace(surfaceName) &&
                    !surfaceNames.Exists(value => string.Equals(value, surfaceName, StringComparison.OrdinalIgnoreCase)))
                {
                    surfaceNames.Add(surfaceName.Trim());
                }
            }

            surfaceNames.Sort(StringComparer.OrdinalIgnoreCase);
            HashSet<string> existingMappings = new(StringComparer.OrdinalIgnoreCase);
            foreach (PhysSoundInteractionKey key in interactions.Keys)
            {
                existingMappings.Add(GetMappingIdentity(key));
            }

            List<PhysSoundInteractionKey> available = new();
            for (int i = 0; i < surfaceNames.Count; i++)
            {
                for (int j = i; j < surfaceNames.Count; j++)
                {
                    PhysSoundInteractionKey candidate = new(surfaceNames[i], surfaceNames[j]);
                    if (!existingMappings.Contains(GetMappingIdentity(candidate)))
                    {
                        available.Add(candidate);
                    }
                }
            }

            for (int i = 0; i < surfaceNames.Count; i++)
            {
                if (string.Equals(surfaceNames[i], PhysSoundSettings.DefaultSurface, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                PhysSoundInteractionKey candidate = new(surfaceNames[i], PhysSoundSettings.AnySurface);
                if (!existingMappings.Contains(GetMappingIdentity(candidate)))
                {
                    available.Add(candidate);
                }
            }

            available.Sort((left, right) => string.Compare(
                GetMappingLabel(left),
                GetMappingLabel(right),
                StringComparison.OrdinalIgnoreCase));
            return available;
        }

        private static string GetMappingIdentity(PhysSoundInteractionKey key)
        {
            string first = key.SurfaceA;
            string second = key.SurfaceB;
            if (string.Compare(first, second, StringComparison.OrdinalIgnoreCase) > 0)
            {
                (first, second) = (second, first);
            }

            return $"{first.Length}:{first}{second.Length}:{second}";
        }

        private void AddInteractionMapping(
            Object owner,
            Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> interactions,
            PhysSoundInteractionKey newKey,
            string sourcePropertyPath = null)
        {
            if (!newKey.HasConfiguredSurface || newKey.IsDefaultFallback || interactions.ContainsKey(newKey))
            {
                return;
            }

            Undo.RecordObject(owner, "Add Phys Sound Interaction Mapping");
            PhysSoundInteraction interaction = new();
            if (!string.IsNullOrEmpty(sourcePropertyPath))
            {
                SerializedObject serializedOwner = new(owner);
                SerializedProperty source = serializedOwner.FindProperty(sourcePropertyPath);
                if (source?.boxedValue is PhysSoundInteraction copiedInteraction)
                {
                    interaction = copiedInteraction;
                }
            }

            interactions.Add(newKey, interaction);
            _selectedInteraction = _entries.Count;
            _mappingScroll.y = float.MaxValue;
            _newMappingSurfaceA = null;
            _newMappingSurfaceB = null;
            _newMappingSourcePath = null;
            EditorUtility.SetDirty(owner);
            InvalidateValidation();
        }

        private void ReplaceInteractionKey(
            Object owner,
            Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> interactions,
            PhysSoundInteractionKey oldKey,
            PhysSoundInteractionKey newKey)
        {
            if (!newKey.HasConfiguredSurface || newKey.IsDefaultFallback || interactions.ContainsKey(newKey))
            {
                return;
            }

            Undo.RecordObject(owner, "Change Phys Sound Interaction Mapping");
            PhysSoundInteraction interaction = interactions[oldKey];
            interactions.Remove(oldKey);
            interactions.Add(newKey, interaction);
            EditorUtility.SetDirty(owner);
            InvalidateValidation();
        }

        private static string[] BuildSurfaceOptions(Dictionary<string, PhysSoundSurface> surfaces)
        {
            List<string> options = new() { PhysSoundSettings.DefaultSurface };
            foreach (string name in surfaces.Keys)
            {
                bool exists = options.Exists(value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    options.Add(name);
                }
            }

            options.Sort(StringComparer.OrdinalIgnoreCase);
            return options.ToArray();
        }

        private static List<string> BuildSurfaceValues(
            Dictionary<string, PhysSoundSurface> surfaces,
            bool allowAny)
        {
            List<string> values = new();
            if (allowAny)
            {
                values.Add(PhysSoundSettings.AnySurface);
            }

            values.Add(PhysSoundSettings.DefaultSurface);
            foreach (string surfaceName in surfaces.Keys)
            {
                if (!values.Exists(value => string.Equals(value, surfaceName, StringComparison.OrdinalIgnoreCase)))
                {
                    values.Add(surfaceName);
                }
            }

            int start = allowAny ? 1 : 0;
            values.Sort(start, values.Count - start, StringComparer.OrdinalIgnoreCase);
            return values;
        }

        private static List<string> BuildSurfaceLabels(List<string> values)
        {
            List<string> labels = new(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                labels.Add(string.IsNullOrEmpty(values[i]) ? "Any" : values[i]);
            }

            return labels;
        }

        private static string DrawSurfacePopup(
            Rect rect,
            string current,
            string[] surfaceOptions,
            bool allowAny)
        {
            List<string> values = new(surfaceOptions);
            if (allowAny)
            {
                values.Insert(0, PhysSoundSettings.AnySurface);
            }

            int selected = values.FindIndex(value => string.Equals(value, current, StringComparison.OrdinalIgnoreCase));
            if (selected < 0)
            {
                values.Add(current);
                selected = values.Count - 1;
            }

            string[] labels = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                labels[i] = string.IsNullOrEmpty(values[i]) ? "Any" : values[i];
            }

            selected = EditorGUI.Popup(rect, selected, labels);
            return values[selected];
        }

        private static string DrawMappingOptionPopup(Rect rect, string current, List<string> options)
        {
            if (options.Count == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.Popup(rect, 0, new[] { "None" });
                }

                return string.Empty;
            }

            int selected = options.FindIndex(value => string.Equals(value, current, StringComparison.OrdinalIgnoreCase));
            selected = Mathf.Max(0, selected);
            string[] labels = new string[options.Count];
            for (int i = 0; i < options.Count; i++)
            {
                labels[i] = string.IsNullOrEmpty(options[i]) ? "Any" : options[i];
            }

            selected = EditorGUI.Popup(rect, selected, labels);
            return options[selected];
        }

        private static string GetMappingLabel(PhysSoundInteractionKey key)
        {
            string first = string.IsNullOrEmpty(key.SurfaceA) ? "Any" : key.SurfaceA;
            string second = string.IsNullOrEmpty(key.SurfaceB) ? "Any" : key.SurfaceB;
            return $"{first} ↔ {second}";
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private AudioClip DrawClipField(Rect rect, Object owner, PhysSoundInteraction interaction)
        {
            AudioClip current = interaction.SlideSourceClip;

            EditorGUI.BeginChangeCheck();
            AudioClip selected = EditorGUI.ObjectField(rect, "Source", current, typeof(AudioClip), false) as AudioClip;

            if (!EditorGUI.EndChangeCheck())
            {
                return current;
            }

            Undo.RecordObject(owner, "Change Phys Sound Preview Source");
            interaction.SlideSourceClip = selected;
            interaction.SlideRegions.Clear();

            EditorUtility.SetDirty(owner);
            Stop();
            _selectedRegion = -1;
            ResetView();
            ResetDetectionDurations(selected);
            return selected;
        }

        private List<PhysSoundAudioRegion> GetRegions(PhysSoundInteraction interaction)
        {
            return interaction.SlideRegions;
        }

        private void DrawImpactAudio(Rect rect, Object owner, PhysSoundInteraction interaction)
        {
            List<PhysSoundImpactRange> ranges = interaction.ImpactRanges;
            if (ranges.Count == 0)
            {
                EditorGUI.HelpBox(rect, "Create a force range in Slice Force first.", MessageType.Info);
                return;
            }

            _selectedImpactRange = Mathf.Clamp(_selectedImpactRange, 0, ranges.Count - 1);
            Rect rangeRow = TakeTop(ref rect, ClipRowHeight);
            string[] rangeNames = new string[ranges.Count];
            for (int i = 0; i < ranges.Count; i++)
            {
                rangeNames[i] = $"{ranges[i].MinimumImpulse:0.##}–{ranges[i].MaximumImpulse:0.##}";
            }

            int selectedRange = EditorGUI.Popup(rangeRow, "Force Range", _selectedImpactRange, rangeNames);
            if (selectedRange != _selectedImpactRange)
            {
                Stop();
                _selectedImpactRange = selectedRange;
                _selectedImpactSource = 0;
                _selectedRegion = -1;
                _impactSourceScroll = Vector2.zero;
                ResetView();
            }

            PhysSoundImpactRange range = ranges[_selectedImpactRange];
            List<PhysSoundImpactClipSource> sources = range.ClipSources;
            float listWidth = Mathf.Min(190f, rect.width * 0.32f);
            Rect listRect = new Rect(rect.x, rect.y, listWidth, rect.height);
            Rect workspace = new Rect(listRect.xMax + Spacing, rect.y, rect.width - listWidth - Spacing, rect.height);
            DrawImpactSourceList(listRect, owner, sources);

            if (sources.Count == 0)
            {
                EditorGUI.HelpBox(workspace, "Add an Impact AudioClip to mark it on the waveform.", MessageType.Info);
                return;
            }

            _selectedImpactSource = Mathf.Clamp(_selectedImpactSource, 0, sources.Count - 1);
            PhysSoundImpactClipSource source = sources[_selectedImpactSource];
            Rect sourceRow = TakeTop(ref workspace, ClipRowHeight);
            EditorGUI.BeginChangeCheck();
            AudioClip selected = EditorGUI.ObjectField(sourceRow, "Clip", source.SourceClip, typeof(AudioClip), false) as AudioClip;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(owner, "Change Phys Sound Impact Clip");
                source.SourceClip = selected;
                source.Regions.Clear();
                source.RuntimeClips = selected == null ? Array.Empty<AudioClip>() : new[] { selected };
                EditorUtility.SetDirty(owner);
                _selectedRegion = -1;
                ResetView();
            }

            if (selected == null)
            {
                EditorGUI.HelpBox(workspace, "Assign an AudioClip.", MessageType.Info);
                return;
            }

            Rect controls = new Rect(workspace.x, workspace.yMax - ControlsHeight, workspace.width, ControlsHeight);
            workspace.yMax = controls.y - Spacing;
            DrawWaveform(workspace, selected);
            DrawRegions(workspace, selected, source.Regions);
            HandleWaveformInput(workspace, selected, owner, source.Regions);
            DrawControls(controls, owner, interaction, selected, source.Regions, _selectedImpactSource);
        }

        private void DrawImpactSourceList(Rect rect, Object owner, List<PhysSoundImpactClipSource> sources)
        {
            GUI.Box(rect, GUIContent.none);
            Rect scrollRect = new(rect.x + 3f, rect.y + 3f, rect.width - 6f, Mathf.Max(0f, rect.height - 28f));
            float contentHeight = Mathf.Max(scrollRect.height, sources.Count * 22f);
            bool needsScrollbar = contentHeight > scrollRect.height;
            Rect viewRect = new(0f, 0f, scrollRect.width - (needsScrollbar ? 16f : 0f), contentHeight);
            _impactSourceScroll = GUI.BeginScrollView(scrollRect, _impactSourceScroll, viewRect, false, needsScrollbar);
            float y = 0f;
            for (int i = 0; i < sources.Count; i++)
            {
                PhysSoundImpactClipSource source = sources[i];
                Rect row = new Rect(0f, y, viewRect.width - 24f, 20f);
                Rect play = new Rect(row.xMax + 2f, y, 21f, 20f);
                string name = source.SourceClip == null ? "None" : source.SourceClip.name;
                if (GUI.Toggle(row, i == _selectedImpactSource, name, EditorStyles.miniButton) && i != _selectedImpactSource)
                {
                    Stop();
                    _selectedImpactSource = i;
                    _selectedRegion = -1;
                    ResetView();
                }

                if (GUI.Button(play, "▶") && source.SourceClip != null)
                {
                    PlayMarkedOrFull(source.SourceClip, source.Regions);
                }

                y += 22f;
            }

            GUI.EndScrollView();

            Rect add = new Rect(rect.x + 3f, rect.yMax - 22f, 45f, 19f);
            if (GUI.Button(add, "+"))
            {
                Undo.RecordObject(owner, "Add Phys Sound Impact Clip");
                sources.Add(new PhysSoundImpactClipSource(null));
                _selectedImpactSource = sources.Count - 1;
                _impactSourceScroll.y = contentHeight + 22f;
                EditorUtility.SetDirty(owner);
            }

            using (new EditorGUI.DisabledScope(sources.Count == 0))
            {
                if (GUI.Button(new Rect(add.xMax + 3f, add.y, 45f, add.height), "−"))
                {
                    Undo.RecordObject(owner, "Remove Phys Sound Impact Clip");
                    sources.RemoveAt(Mathf.Clamp(_selectedImpactSource, 0, sources.Count - 1));
                    _selectedImpactSource = Mathf.Clamp(_selectedImpactSource - 1, 0, Mathf.Max(0, sources.Count - 1));
                    EditorUtility.SetDirty(owner);
                }
            }
        }

        private void DrawCurves(Rect rect, Object owner, PreviewEntry entry)
        {
            if (string.IsNullOrEmpty(entry.PropertyPath))
            {
                EditorGUI.HelpBox(rect, "Could not resolve this interaction's serialized curve properties.", MessageType.Error);
                return;
            }

            Rect modeRect = TakeTop(ref rect, ToolbarHeight);
            modeRect.width = Mathf.Min(180f, modeRect.width);
            _curveMode = GUI.Toolbar(modeRect, _curveMode, new[] { "Impact", "Slide" }, EditorStyles.miniButton);
            SerializedObject serializedOwner = new(owner);
            serializedOwner.Update();
            string prefix = _curveMode == 0 ? "_impact" : "_slide";
            SerializedProperty volume = serializedOwner.FindProperty($"{entry.PropertyPath}.{prefix}Volume");
            SerializedProperty pitch = serializedOwner.FindProperty($"{entry.PropertyPath}.{prefix}Pitch");
            if (volume == null || pitch == null)
            {
                EditorGUI.HelpBox(rect, "Curve properties are unavailable.", MessageType.Error);
                return;
            }

            float height = (rect.height - Spacing) * 0.5f;
            Rect volumeRect = new(rect.x, rect.y, rect.width, height);
            Rect pitchRect = new(rect.x, volumeRect.yMax + Spacing, rect.width, height);
            EditorGUI.PropertyField(volumeRect, volume, new GUIContent("Volume"), true);
            EditorGUI.PropertyField(pitchRect, pitch, new GUIContent("Pitch"), true);
            serializedOwner.ApplyModifiedProperties();
        }

        private void DrawImpactRanges(Rect rect, Object owner, PhysSoundInteraction interaction)
        {
            List<PhysSoundImpactRange> ranges = interaction.ImpactRanges;
            if (ranges.Count == 0)
            {
                Rect button = new Rect(rect.center.x - 90f, rect.center.y - 10f, 180f, 22f);
                if (GUI.Button(button, "Create Impact Range"))
                {
                    Undo.RecordObject(owner, "Create Phys Sound Impact Range");
                    interaction.CreateInitialImpactRange();
                    _selectedImpactRange = 0;
                    EditorUtility.SetDirty(owner);
                }

                return;
            }

            _selectedImpactRange = Mathf.Clamp(_selectedImpactRange, 0, ranges.Count - 1);
            Rect controls = new Rect(rect.x, rect.yMax - 22f, rect.width, 22f);
            Rect axis = new Rect(rect.x + 8f, rect.y + 28f, rect.width - 16f, Mathf.Max(54f, rect.height - 60f));
            EditorGUI.DrawRect(axis, new Color(0.08f, 0.08f, 0.08f, 1f));

            float axisMaximum = 20f;
            for (int i = 0; i < ranges.Count; i++)
            {
                axisMaximum = Mathf.Max(axisMaximum, ranges[i].MaximumImpulse);
            }

            for (int i = 0; i < ranges.Count; i++)
            {
                PhysSoundImpactRange range = ranges[i];
                float xMin = ImpulseToPosition(axis, range.MinimumImpulse, axisMaximum);
                float xMax = ImpulseToPosition(axis, range.MaximumImpulse, axisMaximum);
                Rect segment = Rect.MinMaxRect(xMin, axis.y + 24f, xMax, axis.yMax);
                EditorGUI.DrawRect(
                    segment,
                    i == _selectedImpactRange ? SelectedRegionColor : RegionColor);
                GUI.Box(segment, GUIContent.none);

                Rect play = new Rect(Mathf.Clamp(segment.center.x - 11f, segment.x, segment.xMax - 22f), axis.y + 2f, 22f, 19f);
                if (GUI.Button(play, "▶"))
                {
                    _selectedImpactRange = i;
                    float testImpulse = (range.MinimumImpulse + range.MaximumImpulse) * 0.5f;
                    if (interaction.TryGetImpactPlayback(testImpulse, out PhysSoundImpactPlayback playback))
                    {
                        Play(playback.Clip, playback.StartTime, playback.EndTime);
                    }
                }

                GUI.Label(
                    new Rect(segment.x + 4f, segment.y + 3f, Mathf.Max(0f, segment.width - 8f), 18f),
                    $"{range.MinimumImpulse:0.##}–{range.MaximumImpulse:0.##}",
                    EditorStyles.miniLabel);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && segment.Contains(Event.current.mousePosition))
                {
                    _selectedImpactRange = i;
                }
            }

            HandleImpactBoundaries(axis, owner, ranges, axisMaximum);

            if (GUI.Button(new Rect(controls.x, controls.y, 70f, controls.height), "Split"))
            {
                SplitImpactRange(owner, ranges, axisMaximum);
            }

            using (new EditorGUI.DisabledScope(ranges.Count <= 1))
            {
                if (GUI.Button(new Rect(controls.x + 74f, controls.y, 70f, controls.height), "Delete"))
                {
                    DeleteImpactRange(owner, ranges);
                }
            }

            DrawSelectedImpactRangeFields(
                new Rect(controls.x + 152f, controls.y, controls.width - 152f, controls.height),
                owner,
                ranges,
                axisMaximum);
        }

        private void HandleImpactBoundaries(
            Rect axis,
            Object owner,
            List<PhysSoundImpactRange> ranges,
            float axisMaximum)
        {
            Event current = Event.current;
            for (int i = 0; i <= ranges.Count; i++)
            {
                float value = i == 0
                    ? ranges[0].MinimumImpulse
                    : ranges[i - 1].MaximumImpulse;
                float x = ImpulseToPosition(axis, value, axisMaximum);
                Rect handle = new Rect(x - 4f, axis.y + 20f, 8f, axis.height - 16f);
                EditorGUI.DrawRect(handle, WaveformColor);
                EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeHorizontal);
                if (current.type == EventType.MouseDown && current.button == 0 && handle.Contains(current.mousePosition))
                {
                    _dragImpactBoundary = i;
                    _impactBoundaryUndoRecorded = false;
                    _selectedImpactRange = Mathf.Clamp(i, 0, ranges.Count - 1);
                    current.Use();
                }
            }

            if (_dragImpactBoundary < 0 || _dragImpactBoundary > ranges.Count)
            {
                return;
            }

            if (current.type == EventType.MouseDrag)
            {
                if (!_impactBoundaryUndoRecorded)
                {
                    Undo.RecordObject(owner, "Edit Phys Sound Impact Ranges");
                    _impactBoundaryUndoRecorded = true;
                }

                float value = PositionToImpulse(axis, current.mousePosition.x, axisMaximum);
                SetImpactBoundary(ranges, _dragImpactBoundary, value, axisMaximum);
                EditorUtility.SetDirty(owner);
                current.Use();
            }
            else if (current.type == EventType.MouseUp)
            {
                _dragImpactBoundary = -1;
                _impactBoundaryUndoRecorded = false;
                current.Use();
            }
        }

        private void DrawSelectedImpactRangeFields(
            Rect rect,
            Object owner,
            List<PhysSoundImpactRange> ranges,
            float axisMaximum)
        {
            int selected = Mathf.Clamp(_selectedImpactRange, 0, ranges.Count - 1);
            PhysSoundImpactRange range = ranges[selected];
            float spacing = 4f;
            float width = (rect.width - spacing) * 0.5f;
            Rect minimumRect = new Rect(rect.x, rect.y, width, rect.height);
            Rect maximumRect = new Rect(minimumRect.xMax + spacing, rect.y, width, rect.height);

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 28f;
            EditorGUI.BeginChangeCheck();
            float minimum = EditorGUI.FloatField(minimumRect, "Min", range.MinimumImpulse);
            float maximum = EditorGUI.FloatField(maximumRect, "Max", range.MaximumImpulse);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUIUtility.labelWidth = previousLabelWidth;

            if (!changed)
            {
                return;
            }

            Undo.RecordObject(owner, "Edit Phys Sound Impact Range");
            SetImpactBoundary(ranges, selected, minimum, axisMaximum);
            SetImpactBoundary(ranges, selected + 1, maximum, Mathf.Max(axisMaximum, maximum));
            EditorUtility.SetDirty(owner);
        }

        private static void SetImpactBoundary(
            List<PhysSoundImpactRange> ranges,
            int boundary,
            float value,
            float axisMaximum)
        {
            if (boundary == 0)
            {
                ranges[0].MinimumImpulse = Mathf.Clamp(value, 0f, ranges[0].MaximumImpulse - 0.001f);
                return;
            }

            if (boundary == ranges.Count)
            {
                PhysSoundImpactRange last = ranges[ranges.Count - 1];
                last.MaximumImpulse = Mathf.Clamp(value, last.MinimumImpulse + 0.001f, axisMaximum);
                return;
            }

            PhysSoundImpactRange left = ranges[boundary - 1];
            PhysSoundImpactRange right = ranges[boundary];
            value = Mathf.Clamp(value, left.MinimumImpulse + 0.001f, right.MaximumImpulse - 0.001f);
            left.MaximumImpulse = value;
            right.MinimumImpulse = value;
        }

        private void SplitImpactRange(Object owner, List<PhysSoundImpactRange> ranges, float axisMaximum)
        {
            PhysSoundImpactRange source = ranges[_selectedImpactRange];
            float xMin = Mathf.Pow(Mathf.InverseLerp(0f, axisMaximum, source.MinimumImpulse), 1f / ImpactAxisExponent);
            float xMax = Mathf.Pow(Mathf.InverseLerp(0f, axisMaximum, source.MaximumImpulse), 1f / ImpactAxisExponent);
            float split = Mathf.Lerp(0f, axisMaximum, Mathf.Pow((xMin + xMax) * 0.5f, ImpactAxisExponent));
            if (split <= source.MinimumImpulse || split >= source.MaximumImpulse)
            {
                return;
            }

            Undo.RecordObject(owner, "Split Phys Sound Impact Range");
            float previousMaximum = source.MaximumImpulse;
            source.MaximumImpulse = split;
            ranges.Insert(_selectedImpactRange + 1, new PhysSoundImpactRange(split, previousMaximum));
            _selectedImpactRange++;
            EditorUtility.SetDirty(owner);
        }

        private void DeleteImpactRange(Object owner, List<PhysSoundImpactRange> ranges)
        {
            Undo.RecordObject(owner, "Delete Phys Sound Impact Range");
            PhysSoundImpactRange removed = ranges[_selectedImpactRange];
            if (_selectedImpactRange > 0)
            {
                ranges[_selectedImpactRange - 1].MaximumImpulse = removed.MaximumImpulse;
            }
            else
            {
                ranges[1].MinimumImpulse = removed.MinimumImpulse;
            }

            ranges.RemoveAt(_selectedImpactRange);
            _selectedImpactRange = Mathf.Clamp(_selectedImpactRange - 1, 0, ranges.Count - 1);
            EditorUtility.SetDirty(owner);
        }

        private static float ImpulseToPosition(Rect axis, float impulse, float maximum)
        {
            float normalized = Mathf.Pow(Mathf.InverseLerp(0f, maximum, impulse), 1f / ImpactAxisExponent);
            return Mathf.Lerp(axis.x, axis.xMax, normalized);
        }

        private static float PositionToImpulse(Rect axis, float x, float maximum)
        {
            float normalized = Mathf.InverseLerp(axis.x, axis.xMax, x);
            return Mathf.Lerp(0f, maximum, Mathf.Pow(normalized, ImpactAxisExponent));
        }

        private void DrawControls(
            Rect rect,
            Object owner,
            PhysSoundInteraction interaction,
            AudioClip clip,
            List<PhysSoundAudioRegion> regions,
            int impactSourceIndex)
        {
            Rect playbackRow = new(rect.x, rect.y, rect.width, 20f);
            Rect authoringRow = new(rect.x, playbackRow.yMax + Spacing, rect.width, 20f);
            Rect volumeRow = new(rect.x, authoringRow.yMax + Spacing, rect.width, 20f);
            Rect durationRow = new(rect.x, volumeRow.yMax + Spacing, rect.width, 20f);

            float x = playbackRow.x;
            if (DrawButton(ref x, playbackRow, "Play", 48f))
            {
                PlayMarkedOrFull(clip, regions);
            }

            using (new EditorGUI.DisabledScope(_selectedRegion < 0 || _selectedRegion >= regions.Count))
            {
                if (DrawButton(ref x, playbackRow, "Play Region", 82f))
                {
                    PhysSoundAudioRegion region = regions[_selectedRegion];
                    Play(clip, region.StartTime, region.EndTime);
                }

                if (DrawButton(ref x, playbackRow, "Delete", 54f))
                {
                    Undo.RecordObject(owner, "Delete Phys Sound Audio Region");
                    regions.RemoveAt(_selectedRegion);
                    _selectedRegion = Mathf.Min(_selectedRegion, regions.Count - 1);
                    EditorUtility.SetDirty(owner);
                }
            }

            if (DrawButton(ref x, playbackRow, "Stop", 46f))
            {
                Stop();
            }

            x = authoringRow.x;
            if (DrawButton(ref x, authoringRow, "Auto Detect", 86f))
            {
                AutoDetect(owner, clip, regions);
            }

            using (new EditorGUI.DisabledScope(Mathf.Approximately(_viewStartNormalized, 0f) &&
                                                Mathf.Approximately(_viewEndNormalized, 1f)))
            {
                if (DrawButton(ref x, authoringRow, "Fit", 40f))
                {
                    ResetView();
                }
            }

            float zoom = 1f / Mathf.Max(MinimumVisibleFraction, _viewEndNormalized - _viewStartNormalized);
            GUI.Label(new Rect(x, authoringRow.y, 72f, authoringRow.height), $"Zoom {zoom:0.#}x", EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(regions.Count == 0))
            {
                Rect exportRect = new(authoringRow.xMax - 66f, authoringRow.y, 66f, authoringRow.height);
                if (GUI.Button(exportRect, "Export"))
                {
                    PhysSoundAudioExporter.Export(
                        owner,
                        interaction,
                        _mode == PreviewMode.ImpactAudio,
                        _selectedImpactRange,
                        impactSourceIndex,
                        clip,
                        regions);
                }
            }

            DrawDetectionSliders(volumeRow, durationRow, clip);
        }

        private static void PlayMarkedOrFull(AudioClip clip, List<PhysSoundAudioRegion> regions)
        {
            int validCount = 0;
            for (int i = 0; regions != null && i < regions.Count; i++)
            {
                if (regions[i] != null && regions[i].StartTime < clip.length && regions[i].EndTime > regions[i].StartTime)
                {
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                Play(clip, 0f, clip.length);
                return;
            }

            int selected = UnityEngine.Random.Range(0, validCount);
            for (int i = 0; i < regions.Count; i++)
            {
                PhysSoundAudioRegion region = regions[i];
                if (region == null || region.StartTime >= clip.length || region.EndTime <= region.StartTime)
                {
                    continue;
                }

                if (selected-- == 0)
                {
                    Play(clip, region.StartTime, Mathf.Min(region.EndTime, clip.length));
                    return;
                }
            }
        }

        private void HandleWaveformInput(Rect rect, AudioClip clip, Object owner, List<PhysSoundAudioRegion> regions)
        {
            Event current = Event.current;

            if (HandleViewInput(rect, current))
            {
                return;
            }

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Delete &&
                _selectedRegion >= 0 && _selectedRegion < regions.Count)
            {
                Undo.RecordObject(owner, "Delete Phys Sound Audio Region");
                regions.RemoveAt(_selectedRegion);
                _selectedRegion = Mathf.Min(_selectedRegion, regions.Count - 1);
                EditorUtility.SetDirty(owner);
                current.Use();
                return;
            }

            if (current.button != 0 || (!rect.Contains(current.mousePosition) && _dragMode == DragMode.None))
            {
                return;
            }

            float time = PositionToTime(rect, clip, current.mousePosition.x);

            if (current.type == EventType.MouseDown)
            {
                GUI.FocusControl(null);
                _selectedRegion = HitTestRegion(rect, clip, regions, current.mousePosition.x, out _dragMode);
                _dragStartTime = time;

                if (_selectedRegion >= 0)
                {
                    PhysSoundAudioRegion region = regions[_selectedRegion];
                    _dragOriginalStart = region.StartTime;
                    _dragOriginalEnd = region.EndTime;
                    Undo.RecordObject(owner, "Edit Phys Sound Audio Region");
                }
                else
                {
                    _dragMode = DragMode.Create;
                }

                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && _selectedRegion >= 0 && _selectedRegion < regions.Count)
            {
                PhysSoundAudioRegion region = regions[_selectedRegion];
                float delta = time - _dragStartTime;

                switch (_dragMode)
                {
                    case DragMode.ResizeStart:
                        region.StartTime = Mathf.Min(time, _dragOriginalEnd);
                        break;
                    case DragMode.ResizeEnd:
                        region.EndTime = Mathf.Max(time, _dragOriginalStart);
                        break;
                    case DragMode.Move:
                        float duration = _dragOriginalEnd - _dragOriginalStart;
                        float start = Mathf.Clamp(_dragOriginalStart + delta, 0f, Mathf.Max(0f, clip.length - duration));
                        region.StartTime = start;
                        region.EndTime = start + duration;
                        break;
                }

                EditorUtility.SetDirty(owner);
                current.Use();
                return;
            }

            if (current.type == EventType.MouseUp)
            {
                if (_dragMode == DragMode.Create)
                {
                    float start = Mathf.Min(_dragStartTime, time);
                    float end = Mathf.Max(_dragStartTime, time);

                    if (TimeToRect(rect, clip, start, end).width >= MinimumRegionPixels)
                    {
                        Undo.RecordObject(owner, "Add Phys Sound Audio Region");
                        if (_mode == PreviewMode.SlideAudio)
                        {
                            regions.Clear();
                        }

                        regions.Add(new PhysSoundAudioRegion(start, end));
                        _selectedRegion = regions.Count - 1;
                        EditorUtility.SetDirty(owner);
                    }
                }

                _dragMode = DragMode.None;
                current.Use();
            }
        }

        private void DrawRegions(Rect rect, AudioClip clip, List<PhysSoundAudioRegion> regions)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                PhysSoundAudioRegion region = regions[i];
                Rect regionRect = TimeToRect(rect, clip, region.StartTime, region.EndTime);
                if (!TryClipHorizontally(regionRect, rect, out regionRect))
                {
                    continue;
                }

                EditorGUI.DrawRect(regionRect, i == _selectedRegion ? SelectedRegionColor : RegionColor);
                EditorGUI.DrawRect(new Rect(regionRect.x, regionRect.y, 1f, regionRect.height), WaveformColor);
                EditorGUI.DrawRect(new Rect(regionRect.xMax - 1f, regionRect.y, 1f, regionRect.height), WaveformColor);
                GUI.Label(new Rect(regionRect.x + 4f, regionRect.y + 2f, regionRect.width - 8f, 18f), (i + 1).ToString());
            }

            if (_dragMode == DragMode.Create)
            {
                float currentTime = PositionToTime(rect, clip, Event.current.mousePosition.x);
                Rect selection = TimeToRect(
                    rect,
                    clip,
                    Mathf.Min(_dragStartTime, currentTime),
                    Mathf.Max(_dragStartTime, currentTime));
                EditorGUI.DrawRect(selection, SelectedRegionColor);
            }
        }

        private void DrawWaveform(Rect rect, AudioClip clip)
        {
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.08f, 1f));
            float[] data = GetWaveform(clip);
            float center = rect.center.y;
            Handles.BeginGUI();
            Handles.color = WaveformColor;

            if (data != null && data.Length >= 2)
            {
                int pairCount = data.Length / 2;
                int pixelCount = Mathf.Max(1, Mathf.RoundToInt(rect.width));
                int firstPair = Mathf.Clamp(Mathf.FloorToInt(_viewStartNormalized * pairCount), 0, pairCount - 1);
                int lastPair = Mathf.Clamp(Mathf.CeilToInt(_viewEndNormalized * pairCount), firstPair + 1, pairCount);
                int visiblePairCount = lastPair - firstPair;
                for (int pixel = 0; pixel < pixelCount; pixel++)
                {
                    int pairIndex = Mathf.Min(lastPair - 1, firstPair + pixel * visiblePairCount / pixelCount);
                    float min = Mathf.Clamp(data[pairIndex * 2], -1f, 1f);
                    float max = Mathf.Clamp(data[pairIndex * 2 + 1], -1f, 1f);
                    float x = rect.x + pixel;
                    Handles.DrawLine(
                        new Vector3(x, center - max * rect.height * 0.48f),
                        new Vector3(x, center - min * rect.height * 0.48f));
                }
            }
            else
            {
                Handles.DrawLine(new Vector3(rect.x, center), new Vector3(rect.xMax, center));
            }

            if (_playingClip == clip && IsPreviewPlaying())
            {
                float normalized = clip.samples <= 0 ? 0f : Mathf.Clamp01((float)GetPreviewSamplePosition() / clip.samples);
                if (normalized >= _viewStartNormalized && normalized <= _viewEndNormalized)
                {
                    float visibleNormalized = (normalized - _viewStartNormalized) /
                                              (_viewEndNormalized - _viewStartNormalized);
                    float x = Mathf.Lerp(rect.x, rect.xMax, visibleNormalized);
                    Handles.color = Color.white;
                    Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.yMax));
                }
            }

            Handles.EndGUI();
            GUI.Box(rect, GUIContent.none);
        }

        private int HitTestRegion(
            Rect rect,
            AudioClip clip,
            List<PhysSoundAudioRegion> regions,
            float mouseX,
            out DragMode mode)
        {
            for (int i = regions.Count - 1; i >= 0; i--)
            {
                PhysSoundAudioRegion region = regions[i];
                Rect regionRect = TimeToRect(rect, clip, region.StartTime, region.EndTime);
                if (!TryClipHorizontally(regionRect, rect, out regionRect))
                {
                    continue;
                }

                if (Mathf.Abs(mouseX - regionRect.x) <= HandleWidth)
                {
                    mode = DragMode.ResizeStart;
                    return i;
                }

                if (Mathf.Abs(mouseX - regionRect.xMax) <= HandleWidth)
                {
                    mode = DragMode.ResizeEnd;
                    return i;
                }

                if (mouseX > regionRect.x && mouseX < regionRect.xMax)
                {
                    mode = DragMode.Move;
                    return i;
                }
            }

            mode = DragMode.None;
            return -1;
        }

        private static float[] GetWaveform(AudioClip clip)
        {
            if (WaveformCache.TryGetValue(clip, out float[] data))
            {
                return data;
            }

            AudioImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;
            data = importer == null ? null : GetMinMaxDataMethod?.Invoke(null, new object[] { importer }) as float[];
            WaveformCache[clip] = data;
            return data;
        }

        private static void Play(AudioClip clip, float startTime, float endTime)
        {
            Stop();
            int startSample = Mathf.Clamp(Mathf.RoundToInt(startTime * clip.frequency), 0, Mathf.Max(0, clip.samples - 1));
            _playingEndSample = Mathf.Clamp(Mathf.RoundToInt(endTime * clip.frequency), startSample + 1, clip.samples);
            _playingClip = clip;
            PlayPreviewClipMethod?.Invoke(null, new object[] { clip, startSample, false });
            EditorApplication.update += UpdatePlayback;
        }

        private static void UpdatePlayback()
        {
            if (_playingClip == null || !IsPreviewPlaying() || GetPreviewSamplePosition() >= _playingEndSample)
            {
                Stop();
                return;
            }

            InternalEditorUtility.RepaintAllViews();
        }

        private static bool IsPreviewPlaying()
        {
            return IsPreviewClipPlayingMethod != null && (bool)IsPreviewClipPlayingMethod.Invoke(null, null);
        }

        private static int GetPreviewSamplePosition()
        {
            return GetPreviewClipSamplePositionMethod == null
                ? 0
                : (int)GetPreviewClipSamplePositionMethod.Invoke(null, null);
        }

        private Rect TimeToRect(Rect rect, AudioClip clip, float start, float end)
        {
            float inverseLength = clip.length <= 0f ? 0f : 1f / clip.length;
            float visibleFraction = _viewEndNormalized - _viewStartNormalized;
            float startNormalized = (start * inverseLength - _viewStartNormalized) / visibleFraction;
            float endNormalized = (end * inverseLength - _viewStartNormalized) / visibleFraction;
            float x = Mathf.LerpUnclamped(rect.x, rect.xMax, startNormalized);
            float xMax = Mathf.LerpUnclamped(rect.x, rect.xMax, endNormalized);
            return Rect.MinMaxRect(x, rect.y, xMax, rect.yMax);
        }

        private float PositionToTime(Rect rect, AudioClip clip, float x)
        {
            float visibleNormalized = Mathf.InverseLerp(rect.x, rect.xMax, x);
            return Mathf.Lerp(_viewStartNormalized, _viewEndNormalized, visibleNormalized) * clip.length;
        }

        private bool HandleViewInput(Rect rect, Event current)
        {
            if (current.type == EventType.ScrollWheel && rect.Contains(current.mousePosition))
            {
                float cursorNormalized = Mathf.InverseLerp(rect.x, rect.xMax, current.mousePosition.x);
                float visibleFraction = _viewEndNormalized - _viewStartNormalized;
                float pivot = Mathf.Lerp(_viewStartNormalized, _viewEndNormalized, cursorNormalized);
                float zoomFactor = Mathf.Exp(current.delta.y * 0.12f);
                float newVisibleFraction = Mathf.Clamp(
                    visibleFraction * zoomFactor,
                    MinimumVisibleFraction,
                    1f);
                float start = pivot - cursorNormalized * newVisibleFraction;
                SetView(start, start + newVisibleFraction);
                current.Use();
                return true;
            }

            bool panButton = current.button == 2 || (current.button == 0 && current.alt);
            if (current.type == EventType.MouseDown && panButton && rect.Contains(current.mousePosition))
            {
                _isPanning = true;
                current.Use();
                return true;
            }

            if (current.type == EventType.MouseDrag && _isPanning)
            {
                float visibleFraction = _viewEndNormalized - _viewStartNormalized;
                float offset = -current.delta.x / Mathf.Max(1f, rect.width) * visibleFraction;
                SetView(_viewStartNormalized + offset, _viewEndNormalized + offset);
                current.Use();
                return true;
            }

            if (current.type == EventType.MouseUp && _isPanning)
            {
                _isPanning = false;
                current.Use();
                return true;
            }

            return false;
        }

        private void AutoDetect(Object owner, AudioClip clip, List<PhysSoundAudioRegion> regions)
        {
            List<Vector2> detected = PhysSoundAudioRegionDetector.Detect(
                GetWaveform(clip),
                clip.length,
                _mode == PreviewMode.ImpactAudio,
                DecibelsToAmplitude(_soundVolumeMinDb),
                DecibelsToAmplitude(_soundVolumeMaxDb),
                DecibelsToAmplitude(_pauseVolumeMinDb),
                DecibelsToAmplitude(_pauseVolumeMaxDb),
                _soundDurationMin,
                _soundDurationMax,
                _pauseDurationMin,
                _pauseDurationMax);

            Undo.RecordObject(owner, "Auto Detect Phys Sound Audio Regions");
            regions.Clear();
            for (int i = 0; i < detected.Count; i++)
            {
                regions.Add(new PhysSoundAudioRegion(detected[i].x, detected[i].y));
            }

            _selectedRegion = regions.Count > 0 ? 0 : -1;
            EditorUtility.SetDirty(owner);
            Debug.Log($"Phys Sound auto-detected {regions.Count} region(s) in {clip.name}.", owner);
        }

        private void DrawDetectionSliders(Rect volumeRow, Rect durationRow, AudioClip clip)
        {
            SplitRow(volumeRow, out Rect soundVolumeRect, out Rect pauseVolumeRect);
            DrawMinMaxSlider(
                soundVolumeRect,
                $"Sound dB  {_soundVolumeMinDb:0}..{_soundVolumeMaxDb:0}",
                ref _soundVolumeMinDb,
                ref _soundVolumeMaxDb,
                -80f,
                0f);
            DrawMinMaxSlider(
                pauseVolumeRect,
                $"Pause dB  {_pauseVolumeMinDb:0}..{_pauseVolumeMaxDb:0}",
                ref _pauseVolumeMinDb,
                ref _pauseVolumeMaxDb,
                -80f,
                0f);

            float maximumDuration = Mathf.Max(0.01f, clip.length);
            _soundDurationMin = Mathf.Clamp(_soundDurationMin, 0f, maximumDuration);
            _soundDurationMax = Mathf.Clamp(_soundDurationMax, _soundDurationMin, maximumDuration);
            _pauseDurationMin = Mathf.Clamp(_pauseDurationMin, 0f, maximumDuration);
            _pauseDurationMax = Mathf.Clamp(_pauseDurationMax, _pauseDurationMin, maximumDuration);

            SplitRow(durationRow, out Rect soundDurationRect, out Rect pauseDurationRect);
            DrawMinMaxSlider(
                soundDurationRect,
                $"Sound s  {_soundDurationMin:0.###}..{_soundDurationMax:0.###}",
                ref _soundDurationMin,
                ref _soundDurationMax,
                0f,
                maximumDuration);
            DrawMinMaxSlider(
                pauseDurationRect,
                $"Pause s  {_pauseDurationMin:0.###}..{_pauseDurationMax:0.###}",
                ref _pauseDurationMin,
                ref _pauseDurationMax,
                0f,
                maximumDuration);
        }

        private void ResetDetectionDurations(AudioClip clip)
        {
            float duration = clip == null ? 10f : Mathf.Max(0.01f, clip.length);
            _soundDurationMin = Mathf.Min(0.025f, duration);
            _soundDurationMax = duration;
            _pauseDurationMin = Mathf.Min(0.05f, duration);
            _pauseDurationMax = Mathf.Min(0.5f, duration);
        }

        private static void DrawMinMaxSlider(
            Rect rect,
            string label,
            ref float minimum,
            ref float maximum,
            float limitMinimum,
            float limitMaximum)
        {
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Min(145f, rect.width * 0.58f);
            float sliderMinimum = ToSliderPosition(minimum, limitMinimum, limitMaximum);
            float sliderMaximum = ToSliderPosition(maximum, limitMinimum, limitMaximum);
            EditorGUI.MinMaxSlider(rect, new GUIContent(label), ref sliderMinimum, ref sliderMaximum, 0f, 1f);
            minimum = FromSliderPosition(sliderMinimum, limitMinimum, limitMaximum);
            maximum = FromSliderPosition(sliderMaximum, limitMinimum, limitMaximum);
            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        private static float ToSliderPosition(float value, float minimum, float maximum)
        {
            float normalized = Mathf.InverseLerp(minimum, maximum, value);
            return Mathf.Pow(normalized, 1f / DetectionSliderExponent);
        }

        private static float FromSliderPosition(float position, float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, Mathf.Pow(position, DetectionSliderExponent));
        }

        private static void SplitRow(Rect row, out Rect left, out Rect right)
        {
            float width = (row.width - Spacing) * 0.5f;
            left = new Rect(row.x, row.y, width, row.height);
            right = new Rect(left.xMax + Spacing, row.y, width, row.height);
        }

        private static float DecibelsToAmplitude(float decibels)
        {
            return decibels <= -80f ? 0f : Mathf.Pow(10f, decibels / 20f);
        }

        private void SetView(float start, float end)
        {
            float visibleFraction = Mathf.Clamp(end - start, MinimumVisibleFraction, 1f);
            start = Mathf.Clamp(start, 0f, 1f - visibleFraction);
            _viewStartNormalized = start;
            _viewEndNormalized = start + visibleFraction;
        }

        private void ResetView()
        {
            _viewStartNormalized = 0f;
            _viewEndNormalized = 1f;
            _isPanning = false;
        }

        private static bool TryClipHorizontally(Rect source, Rect bounds, out Rect clipped)
        {
            float xMin = Mathf.Max(source.xMin, bounds.xMin);
            float xMax = Mathf.Min(source.xMax, bounds.xMax);
            clipped = Rect.MinMaxRect(xMin, source.yMin, xMax, source.yMax);
            return xMax > xMin;
        }

        private static Rect TakeTop(ref Rect rect, float height)
        {
            Rect top = new(rect.x, rect.y, rect.width, height);
            rect.yMin += height + Spacing;
            return top;
        }

        private static bool DrawButton(ref float x, Rect row, string label, float width)
        {
            Rect button = new(x, row.y, width, row.height);
            x += width + Spacing;
            return GUI.Button(button, label);
        }

        private static MethodInfo GetAudioUtilMethod(string name, params Type[] parameterTypes)
        {
            return AudioUtilType?.GetMethod(
                name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
        }

        private readonly struct PreviewEntry
        {
            internal PreviewEntry(string name, PhysSoundInteraction interaction, string propertyPath)
            {
                Name = name;
                Interaction = interaction;
                PropertyPath = propertyPath;
            }

            internal string Name { get; }
            internal PhysSoundInteraction Interaction { get; }
            internal string PropertyPath { get; }
        }

        private readonly struct MappingRow
        {
            internal MappingRow(
                PhysSoundInteractionKey key,
                PhysSoundInteraction interaction,
                string propertyPath,
                string error)
            {
                Key = key;
                Interaction = interaction;
                PropertyPath = propertyPath;
                Error = error;
            }

            internal PhysSoundInteractionKey Key { get; }
            internal PhysSoundInteraction Interaction { get; }
            internal string PropertyPath { get; }
            internal string Error { get; }
        }

        private sealed class MappingSurfaceBinding
        {
            internal MappingSurfaceBinding(
                Object owner,
                Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> interactions,
                PhysSoundInteractionKey key,
                bool isSurfaceB,
                List<string> values,
                List<string> labels)
            {
                Owner = owner;
                Interactions = interactions;
                Key = key;
                IsSurfaceB = isSurfaceB;
                Values = values;
                Labels = labels;
            }

            internal Object Owner { get; }
            internal Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> Interactions { get; }
            internal PhysSoundInteractionKey Key { get; }
            internal bool IsSurfaceB { get; }
            internal List<string> Values { get; }
            internal List<string> Labels { get; }
        }

        private enum PreviewMode
        {
            Surfaces,
            InteractionMapping,
            ImpactRanges,
            ImpactAudio,
            SlideAudio,
            Curves
        }

        private enum DragMode
        {
            None,
            Create,
            Move,
            ResizeStart,
            ResizeEnd
        }
    }
}
#endif

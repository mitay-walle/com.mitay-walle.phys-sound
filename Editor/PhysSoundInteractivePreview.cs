#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
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
        private int _selectedInteraction;
        private int _selectedRegion = -1;
        private int _selectedImpactRange;
        private int _selectedImpactSource;
        private int _dragImpactBoundary = -1;
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

        internal void Draw(Rect rect, Object owner)
        {
            BuildEntries(owner);
            EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f, 1f));

            if (_entries.Count == 0)
            {
                EditorGUI.LabelField(rect, "Add an interaction to use the audio preview.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _selectedInteraction = Mathf.Clamp(_selectedInteraction, 0, _entries.Count - 1);
            PreviewEntry entry = _entries[_selectedInteraction];

            Rect toolbarRect = TakeTop(ref rect, ToolbarHeight);
            DrawToolbar(toolbarRect);

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
            foreach ((PhysSoundInteractionKey key, PhysSoundInteraction interaction) in interactions)
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

        private void DrawToolbar(Rect rect)
        {
            float modeWidth = Mathf.Min(440f, rect.width * 0.72f);
            Rect interactionRect = new(rect.x, rect.y, rect.width - modeWidth - Spacing, rect.height);
            Rect modeRect = new(interactionRect.xMax + Spacing, rect.y, modeWidth, rect.height);

            string[] names = new string[_entries.Count];
            for (int i = 0; i < _entries.Count; i++)
            {
                names[i] = _entries[i].Name;
            }

            int selectedInteraction = EditorGUI.Popup(interactionRect, _selectedInteraction, names, EditorStyles.toolbarPopup);
            int selectedMode = GUI.Toolbar(
                modeRect,
                (int)_mode,
                new[] { "Slice Force", "Slice Impact Clips", "Slice Slide Clips", "Curves" },
                EditorStyles.toolbarButton);

            if (selectedInteraction != _selectedInteraction || selectedMode != (int)_mode)
            {
                Stop();
                _selectedInteraction = selectedInteraction;
                _mode = (PreviewMode)selectedMode;
                _selectedRegion = -1;
                _dragMode = DragMode.None;
                _impactSourceScroll = Vector2.zero;
                ResetView();
            }
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
                    Undo.RecordObject(owner, "Edit Phys Sound Impact Ranges");
                    _dragImpactBoundary = i;
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
                float value = PositionToImpulse(axis, current.mousePosition.x, axisMaximum);
                SetImpactBoundary(ranges, _dragImpactBoundary, value, axisMaximum);
                EditorUtility.SetDirty(owner);
                current.Use();
            }
            else if (current.type == EventType.MouseUp)
            {
                _dragImpactBoundary = -1;
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
            SetImpactBoundary(ranges, selected + 1, maximum, axisMaximum);
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

        private enum PreviewMode
        {
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

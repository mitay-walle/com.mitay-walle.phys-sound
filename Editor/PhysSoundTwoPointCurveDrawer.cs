#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using UnityEditor;
using UnityEngine;

namespace PhysSound.Editor
{
    [CustomPropertyDrawer(typeof(PhysSoundTwoPointCurveAttribute))]
    internal sealed class PhysSoundTwoPointCurveDrawer : PropertyDrawer
    {
        private const float Height = 126f;
        private const float HeaderHeight = 18f;
        private const float PointRadius = 5f;
        private const float MaximumTangentAngle = 80f;
        private const int CurveSegments = 48;
        private const float ValueFieldWidth = 58f;
        private const float Spacing = 4f;

        private static readonly Color CurveColor = new(0.35f, 0.9f, 0.45f, 1f);
        private static readonly Color TangentColor = new(0.55f, 0.78f, 1f, 0.9f);
        private static Texture2D _backgroundGradient;
        private static Texture2D _hoverGradient;
        private static int _activeControl;
        private static int _activeKey;
        private static Vector2 _dragStart;
        private static float _dragStartValue;
        private static float _dragStartAngle;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return property.propertyType == SerializedPropertyType.AnimationCurve
                ? Height
                : EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.AnimationCurve)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            PhysSoundTwoPointCurveAttribute settings = (PhysSoundTwoPointCurveAttribute)attribute;
            EditorGUI.BeginProperty(position, label, property);
            AnimationCurve curve = property.animationCurveValue ?? AnimationCurve.Linear(0f, settings.Minimum, 1f, settings.Maximum);
            if (Draw(position, label.text, curve, settings.Minimum, settings.Maximum, out _))
            {
                property.animationCurveValue = curve;
            }

            EditorGUI.EndProperty();
        }

        internal static bool Draw(
            Rect position,
            string label,
            AnimationCurve curve,
            float minimum,
            float maximum,
            out bool editStarted)
        {
            editStarted = false;
            Rect content = new(position.x, position.y + HeaderHeight, position.width, position.height - HeaderHeight);
            Rect minimumField = new(content.x, content.center.y - EditorGUIUtility.singleLineHeight * 0.5f, ValueFieldWidth, EditorGUIUtility.singleLineHeight);
            Rect maximumField = new(content.xMax - ValueFieldWidth, minimumField.y, ValueFieldWidth, minimumField.height);
            Rect graph = Rect.MinMaxRect(minimumField.xMax + Spacing, content.y, maximumField.xMin - Spacing, content.yMax);
            GUI.Label(new Rect(minimumField.x, position.y, minimumField.width, HeaderHeight), "Min", EditorStyles.centeredGreyMiniLabel);
            GUI.Label(new Rect(graph.x, position.y, graph.width, HeaderHeight), label, EditorStyles.boldLabel);
            GUI.Label(new Rect(maximumField.x, position.y, maximumField.width, HeaderHeight), "Max", EditorStyles.centeredGreyMiniLabel);

            Keyframe[] keys = GetTwoKeys(curve, minimum, maximum);
            EditorGUI.BeginChangeCheck();
            float minimumValue = Mathf.Clamp(EditorGUI.FloatField(minimumField, keys[0].value), minimum, maximum);
            float maximumValue = Mathf.Clamp(EditorGUI.FloatField(maximumField, keys[1].value), minimum, maximum);
            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                keys[0] = CreateKey(0, minimumValue, GetInnerTangent(keys, 0));
                keys[1] = CreateKey(1, maximumValue, GetInnerTangent(keys, 1));
                curve.keys = keys;
            }

            GUI.DrawTexture(graph, GetBackgroundGradient(), ScaleMode.StretchToFill, false);
            Rect leftZone = new(graph.x, graph.y, graph.width * 0.5f, graph.height);
            Rect rightZone = new(graph.center.x, graph.y, graph.width * 0.5f, graph.height);
            EditorWindow hoveredWindow = EditorWindow.mouseOverWindow;
            if (hoveredWindow != null)
            {
                hoveredWindow.wantsMouseMove = true;
                if (Event.current.type == EventType.MouseMove)
                {
                    hoveredWindow.Repaint();
                }
            }

            if (Event.current.type == EventType.Repaint && graph.Contains(Event.current.mousePosition))
            {
                bool leftHovered = Event.current.mousePosition.x < graph.center.x;
                GUI.DrawTextureWithTexCoords(
                    leftHovered ? leftZone : rightZone,
                    GetHoverGradient(),
                    leftHovered ? new Rect(0f, 0f, 1f, 1f) : new Rect(1f, 0f, -1f, 1f));
            }

            DrawCurve(graph, curve, keys, minimum, maximum);
            EditorGUIUtility.AddCursorRect(leftZone, MouseCursor.MoveArrow);
            EditorGUIUtility.AddCursorRect(rightZone, MouseCursor.MoveArrow);

            int controlId = GUIUtility.GetControlID(FocusType.Passive, graph);
            Event current = Event.current;
            EventType eventType = current.GetTypeForControl(controlId);
            if (eventType == EventType.MouseDown && current.button == 0 && graph.Contains(current.mousePosition))
            {
                _activeControl = controlId;
                _activeKey = current.mousePosition.x < graph.center.x ? 0 : 1;
                _dragStart = current.mousePosition;
                _dragStartValue = keys[_activeKey].value;
                _dragStartAngle = TangentToAngle(GetInnerTangent(keys, _activeKey), graph, minimum, maximum);
                GUIUtility.hotControl = controlId;
                editStarted = true;
                current.Use();
                return changed;
            }

            if (_activeControl != controlId || GUIUtility.hotControl != controlId)
            {
                return changed;
            }

            if (eventType == EventType.MouseDrag)
            {
                Vector2 delta = current.mousePosition - _dragStart;
                float value = Mathf.Clamp(
                    _dragStartValue - delta.y / Mathf.Max(1f, graph.height) * (maximum - minimum),
                    minimum,
                    maximum);
                float angle = Mathf.Clamp(
                    _dragStartAngle + delta.x / Mathf.Max(1f, graph.width * 0.5f) * MaximumTangentAngle * 2f,
                    -MaximumTangentAngle,
                    MaximumTangentAngle);
                float tangent = AngleToTangent(angle, graph, minimum, maximum);
                keys[_activeKey] = CreateKey(_activeKey, value, tangent);
                curve.keys = keys;
                current.Use();
                return true;
            }

            if (eventType == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;
                _activeControl = 0;
                current.Use();
            }

            return changed;
        }

        private static void DrawCurve(
            Rect graph,
            AnimationCurve curve,
            Keyframe[] keys,
            float minimum,
            float maximum)
        {
            Handles.BeginGUI();
            Handles.color = CurveColor;
            Vector3[] points = new Vector3[CurveSegments + 1];
            for (int i = 0; i <= CurveSegments; i++)
            {
                float time = i / (float)CurveSegments;
                points[i] = CurveToPosition(graph, time, curve.Evaluate(time), minimum, maximum);
            }

            Handles.DrawAAPolyLine(2f, points);
            Handles.color = TangentColor;
            for (int i = 0; i < 2; i++)
            {
                Vector3 point = CurveToPosition(graph, i, keys[i].value, minimum, maximum);
                float direction = i == 0 ? 0.2f : -0.2f;
                float tangentValue = keys[i].value + GetInnerTangent(keys, i) * direction;
                Vector3 tangent = CurveToPosition(graph, i + direction, tangentValue, minimum, maximum);
                tangent.y = Mathf.Clamp(tangent.y, graph.yMin, graph.yMax);
                Handles.DrawAAPolyLine(2f, point, tangent);
                Handles.DrawSolidDisc(point, Vector3.forward, PointRadius);
            }

            Handles.EndGUI();
            GUI.Label(new Rect(graph.x + 5f, graph.y + 3f, 70f, 18f), keys[0].value.ToString("0.###"), EditorStyles.miniLabel);
            GUI.Label(new Rect(graph.xMax - 75f, graph.y + 3f, 70f, 18f), keys[1].value.ToString("0.###"), EditorStyles.miniLabel);
        }

        private static Keyframe[] GetTwoKeys(AnimationCurve curve, float minimum, float maximum)
        {
            if (curve != null && curve.length > 0)
            {
                Keyframe first = curve.keys[0];
                Keyframe last = curve.keys[curve.length - 1];
                return new[]
                {
                    CreateKey(0, Mathf.Clamp(first.value, minimum, maximum), first.outTangent),
                    CreateKey(1, Mathf.Clamp(last.value, minimum, maximum), last.inTangent)
                };
            }

            return new[]
            {
                CreateKey(0, minimum, 0f),
                CreateKey(1, maximum, 0f)
            };
        }

        private static Keyframe CreateKey(int index, float value, float tangent)
        {
            Keyframe key = new(index, value, tangent, tangent)
            {
                weightedMode = WeightedMode.None
            };
            return key;
        }

        private static float GetInnerTangent(Keyframe[] keys, int index)
        {
            return index == 0 ? keys[0].outTangent : keys[1].inTangent;
        }

        private static Vector3 CurveToPosition(Rect graph, float time, float value, float minimum, float maximum)
        {
            return new Vector3(
                Mathf.Lerp(graph.x, graph.xMax, time),
                Mathf.Lerp(graph.yMax, graph.y, Mathf.InverseLerp(minimum, maximum, value)));
        }

        private static Texture2D GetBackgroundGradient()
        {
            if (_backgroundGradient != null)
            {
                return _backgroundGradient;
            }

            const int size = 64;
            _backgroundGradient = new Texture2D(1, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color bottom = new(0.025f, 0.03f, 0.035f, 1f);
            Color top = new(0.08f, 0.26f, 0.13f, 1f);
            for (int i = 0; i < size; i++)
            {
                _backgroundGradient.SetPixel(0, i, Color.Lerp(bottom, top, i / (size - 1f)));
            }

            _backgroundGradient.Apply(false, true);
            return _backgroundGradient;
        }

        private static Texture2D GetHoverGradient()
        {
            if (_hoverGradient != null)
            {
                return _hoverGradient;
            }

            const int size = 64;
            _hoverGradient = new Texture2D(size, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color strong = new(0.25f, 0.6f, 1f, 0.24f);
            Color clear = new(0.25f, 0.6f, 1f, 0f);
            for (int i = 0; i < size; i++)
            {
                _hoverGradient.SetPixel(i, 0, Color.Lerp(strong, clear, i / (size - 1f)));
            }

            _hoverGradient.Apply(false, true);
            return _hoverGradient;
        }

        private static float TangentToAngle(float tangent, Rect graph, float minimum, float maximum)
        {
            float normalizedSlope = tangent * graph.height / Mathf.Max(0.001f, (maximum - minimum) * graph.width);
            return Mathf.Atan(normalizedSlope) * Mathf.Rad2Deg;
        }

        private static float AngleToTangent(float angle, Rect graph, float minimum, float maximum)
        {
            return Mathf.Tan(angle * Mathf.Deg2Rad) * (maximum - minimum) * graph.width / Mathf.Max(1f, graph.height);
        }
    }
}
#endif

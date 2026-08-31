#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using UnityEditor;
using UnityEngine;

namespace PhysSound.Editor
{
    [CustomPropertyDrawer(typeof(PhysSoundMinMaxAttribute))]
    internal sealed class PhysSoundMinMaxDrawer : PropertyDrawer
    {
        private const float ValueWidth = 48f;
        private const float Spacing = 3f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PhysSoundMinMaxAttribute range = (PhysSoundMinMaxAttribute)attribute;
            if (property.propertyType == SerializedPropertyType.Float &&
                !string.IsNullOrEmpty(range.MaximumPropertyName))
            {
                DrawFloatPair(position, property, label, range);
                return;
            }

            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            Rect controls = EditorGUI.PrefixLabel(position, label);
            Vector2 value = property.vector2Value;
            float minimum = value.x;
            float maximum = value.y;
            DrawControls(controls, ref minimum, ref maximum, range.Minimum, range.Maximum);
            property.vector2Value = new Vector2(minimum, maximum);
            EditorGUI.EndProperty();
        }

        private static void DrawFloatPair(
            Rect position,
            SerializedProperty minimumProperty,
            GUIContent label,
            PhysSoundMinMaxAttribute range)
        {
            SerializedProperty maximumProperty = FindSibling(minimumProperty, range.MaximumPropertyName);
            if (maximumProperty == null || maximumProperty.propertyType != SerializedPropertyType.Float)
            {
                EditorGUI.PropertyField(position, minimumProperty, label);
                return;
            }

            EditorGUI.BeginProperty(position, label, minimumProperty);
            Rect controls = EditorGUI.PrefixLabel(position, label);
            float minimum = minimumProperty.floatValue;
            float maximum = maximumProperty.floatValue;
            float sliderMaximum = Mathf.Max(range.Maximum, maximum);
            DrawControls(controls, ref minimum, ref maximum, range.Minimum, sliderMaximum);
            minimumProperty.floatValue = minimum;
            maximumProperty.floatValue = maximum;
            EditorGUI.EndProperty();
        }

        private static void DrawControls(
            Rect controls,
            ref float minimum,
            ref float maximum,
            float limitMinimum,
            float limitMaximum)
        {
            Rect minimumRect = new Rect(controls.x, controls.y, ValueWidth, controls.height);
            Rect maximumRect = new Rect(controls.xMax - ValueWidth, controls.y, ValueWidth, controls.height);
            Rect sliderRect = Rect.MinMaxRect(
                minimumRect.xMax + Spacing,
                controls.y,
                maximumRect.xMin - Spacing,
                controls.yMax);

            minimum = Mathf.Max(limitMinimum, EditorGUI.DelayedFloatField(minimumRect, minimum));
            maximum = Mathf.Max(minimum, EditorGUI.DelayedFloatField(maximumRect, maximum));
            limitMaximum = Mathf.Max(limitMaximum, maximum);
            EditorGUI.MinMaxSlider(sliderRect, ref minimum, ref maximum, limitMinimum, limitMaximum);
        }

        private static SerializedProperty FindSibling(SerializedProperty property, string siblingName)
        {
            int separator = property.propertyPath.LastIndexOf('.');
            string path = separator < 0
                ? siblingName
                : property.propertyPath.Substring(0, separator + 1) + siblingName;
            return property.serializedObject.FindProperty(path);
        }
    }
}
#endif

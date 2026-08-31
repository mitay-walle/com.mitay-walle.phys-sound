#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using UnityEditor;
using UnityEngine;

namespace PhysSound.Editor
{
    [CustomPropertyDrawer(typeof(PhysSoundLabelAttribute))]
    internal sealed class PhysSoundLabelDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PhysSoundLabelAttribute customLabel = (PhysSoundLabelAttribute)attribute;
            EditorGUI.PropertyField(
                position,
                property,
                new GUIContent(customLabel.DisplayName, label.tooltip),
                true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, true);
        }
    }
}
#endif

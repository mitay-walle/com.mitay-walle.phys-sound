#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using UnityEditor;
using UnityEngine;

namespace PhysSound.Editor
{
    [CustomEditor(typeof(PhysSoundSettings))]
    internal sealed class PhysSoundSettingsEditor : UnityEditor.Editor
    {
        private readonly PhysSoundInteractivePreview _preview = new();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
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
    }
}
#endif

#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PhysSound.Editor
{
    [CustomEditor(typeof(PhysSoundSettings))]
    internal sealed class PhysSoundSettingsEditor : UnityEditor.Editor
    {
        private readonly PhysSoundInteractivePreview _preview = new();

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
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
    }
}
#endif

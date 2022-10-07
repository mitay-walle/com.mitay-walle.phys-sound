using UnityEngine;
using UnityEditor;

namespace PhysSound
{
    public class CreatePhysSoundMaterial
    {
        [MenuItem("Assets/Create/PhysSound Material")]

        public static void Create()
        {
            PhysSoundMaterial asset = ScriptableObject.CreateInstance<PhysSoundMaterial>();

            AssetDatabase.CreateAsset(asset, "Assets/PhysSoundMaterial.asset");
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();

            Selection.activeObject = asset;
        }
    }
}
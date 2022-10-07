using UnityEngine;
using UnityEditor;

namespace PhysSound
{
    [CustomEditor(typeof(PhysSoundTerrain))]
    public class PhysSoundTerrainEditor : Editor
    {
        PhysSoundTerrain physTerr;

        bool matFoldout;
        Vector2 matScroll;

        void Awake()
        {
            physTerr = target as PhysSoundTerrain;
            physTerr.Terrain = physTerr.GetComponent<Terrain>();
        }

        public override void OnInspectorGUI()
        {
            physTerr = target as PhysSoundTerrain;

            serializedObject.Update();

            if (physTerr.Terrain == null)
            {
                EditorGUILayout.HelpBox("No Terrain was found!", MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("PhysSound Terrain Materials:", EditorStyles.boldLabel);

            matFoldout = EditorGUILayout.Foldout(matFoldout, "PhysSound Materials List");

            SplatPrototype[] textures = physTerr.Terrain.terrainData.splatPrototypes;

            while (physTerr.SoundMaterials.Count > textures.Length)
            {
                physTerr.SoundMaterials.RemoveAt(physTerr.SoundMaterials.Count - 1);
            }

            if (matFoldout)
            {
                matScroll = EditorGUILayout.BeginScrollView(matScroll, GUILayout.MaxHeight(200));

                for (int i = 0; i < textures.Length; i++)
                {
                    if (i >= physTerr.SoundMaterials.Count)
                    {
                        physTerr.SoundMaterials.Add(null);
                    }

                    SplatPrototype sp = textures[i];
                    GUILayout.BeginHorizontal();

                    GUILayout.Box(sp.texture, GUILayout.Width(50), GUILayout.Height(50));

                    GUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Splat Texture: " + sp.texture.name);
                    physTerr.SoundMaterials[i] = (PhysSoundMaterial)EditorGUILayout.ObjectField(physTerr.SoundMaterials[i], typeof(PhysSoundMaterial), false);
                    GUILayout.EndVertical();

                    GUILayout.EndHorizontal();
                    GUILayout.Box("", GUILayout.MaxWidth(Screen.width - 25f), GUILayout.Height(1));
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Separator();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
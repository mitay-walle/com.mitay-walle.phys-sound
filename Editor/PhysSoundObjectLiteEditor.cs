using UnityEngine;
using UnityEditor;

namespace PhysSound
{
    [CustomEditor(typeof(PhysSoundObjectLite))]
    [CanEditMultipleObjects]
    public class PhysSoundObjectLiteEditor : Editor
    {
        float dividerHeight = 2;

        SerializedProperty mat, impactAudio, autoCreate, playClipPoint, hitsTriggers;
        PhysSoundObjectLite obj;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            obj = target as PhysSoundObjectLite;

            mat = serializedObject.FindProperty("SoundMaterial");
            impactAudio = serializedObject.FindProperty("ImpactAudio");
            autoCreate = serializedObject.FindProperty("AutoCreateSources");
            playClipPoint = serializedObject.FindProperty("PlayClipAtPoint");
            hitsTriggers = serializedObject.FindProperty("HitsTriggers");

            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("PhysSound Material:", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(mat, true);

            if (obj.SoundMaterial == null)
            {
                EditorGUILayout.HelpBox("No PhysSound Material is assigned!", MessageType.Warning);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            //EditorGUILayout.Separator();

            if (obj.SoundMaterial.AudioSets.Count > 0)
            {
                GUILayout.Box("", GUILayout.MaxWidth(Screen.width - 25f), GUILayout.Height(dividerHeight));

                EditorGUILayout.LabelField("Audio Sources:", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(autoCreate);
                EditorGUILayout.PropertyField(playClipPoint, new GUIContent("Use Audio Pool"));
                EditorGUILayout.PropertyField(hitsTriggers);

                EditorGUILayout.Separator();

                if (obj.AutoCreateSources)
                {
                    EditorGUILayout.PropertyField(impactAudio, new GUIContent("Template Audio"), true);
                }
                else
                {
                    if (!obj.PlayClipAtPoint)
                        EditorGUILayout.PropertyField(impactAudio, true);
                    else
                        EditorGUILayout.PropertyField(impactAudio, new GUIContent("Template Impact Audio"), true);

                }
            }

            EditorUtility.SetDirty(obj);
            EditorGUILayout.Separator();
            GUILayout.Box("", GUILayout.MaxWidth(Screen.width - 25f), GUILayout.Height(dividerHeight));

            EditorGUILayout.Separator();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
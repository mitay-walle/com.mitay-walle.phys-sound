using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace PhysSound
{
    public class PhysSoundTypeEditor : EditorWindow
    {
        float dividerHeight = 2;
        bool duplicateValBlock = false;
        List<string> tempTypes = new List<string>();
        int tempPoolSize;

        [MenuItem("Window/PhysSound/PhysSound Settings")]
        public static void ShowWindow()
        {
            PhysSoundTypeEditor w = EditorWindow.GetWindow<PhysSoundTypeEditor>();

#if UNITY_5_1
            w.titleContent = new GUIContent("PhysSound");
#else
            w.title = "PhysSound";
#endif

            w.Initialize();
        }

        public void Initialize()
        {
            loadTypes();
            loadPoolSize();
        }

        void OnGUI()
        {
            EditorGUILayout.Separator();

            //EditorGUILayout.LabelField("Use this editor to define your own Material Types for the PhysSound system.", EditorStyles.helpBox);
            EditorGUILayout.HelpBox("You must click the 'Save' button before you close the window if you want to save your changes!", MessageType.Warning);

            GUILayout.Box("", GUILayout.MaxWidth(Screen.width), GUILayout.Height(dividerHeight));

            EditorGUILayout.LabelField("Material Types", EditorStyles.boldLabel);
            bool hasFoundDupe = false;

            GUILayout.BeginVertical(EditorStyles.textField);

            EditorGUILayout.Separator();

            for (int i = 0; i < tempTypes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                if (hasDuplicates(i))
                {
                    GUI.color = new Color(1, 0.5f, 0.5f);
                    hasFoundDupe = true;
                }

                string newKey = EditorGUILayout.TextField(tempTypes[i]);

                updateMaterialType(i, newKey);

                GUI.color = new Color(1, 1, 0.5f);

                if (GUILayout.Button("Remove", GUILayout.MaxWidth(70), GUILayout.MaxHeight(15)))
                {
                    tempTypes.RemoveAt(i);
                    i--;
                }

                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();

                GUI.color = Color.white;
            }

            EditorGUILayout.Separator();

            if (hasFoundDupe)
                duplicateValBlock = true;
            else
                duplicateValBlock = false;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Add New Type", GUILayout.MaxWidth(150)))
            {
                tempTypes.Add("New Type");
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5f);
            GUILayout.EndVertical();

            if (duplicateValBlock)
            {
                EditorGUILayout.HelpBox("You have duplicate Type names! You will not be able to save your changes until you have no duplicates.", MessageType.Error);
                GUI.enabled = false;
            }

            EditorGUILayout.Separator();

            GUILayout.Box("", GUILayout.MaxWidth(Screen.width), GUILayout.Height(dividerHeight));
            EditorGUILayout.LabelField("Other Settings", EditorStyles.boldLabel);

            tempPoolSize = EditorGUILayout.IntField("Audio Pool Size", tempPoolSize);

            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Save", GUILayout.MaxWidth(125)))
            {
                saveTypes();
                savePoolSize();
            }

            GUI.color = new Color(1, 1, 0.5f);

            GUI.enabled = true;

            if (GUILayout.Button("Revert", GUILayout.MaxWidth(125)))
            {
                if (EditorUtility.DisplayDialog("Revert?", "This will discard all currently unsaved changes.", "Continue", "Cancel"))
                {
                    loadTypes();
                    loadPoolSize();
                }
            }

            GUI.color = Color.white;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();


        }

        #region Utility

        void updateMaterialType(int index, string newkey)
        {
            if (index < 0 || index >= tempTypes.Count)
                return;

            tempTypes[index] = newkey;
        }

        bool hasDuplicates(int index)
        {
            for (int j = 0; j < tempTypes.Count; j++)
            {
                if (index == j)
                    continue;

                if (tempTypes[index] == tempTypes[j])
                    return true;
            }

            return false;
        }

        #endregion

        #region Saving and Loading

        void saveTypes()
        {
            string filePath;

            string[] assets = AssetDatabase.FindAssets("PhysSoundTypeList");
            if (assets.Length > 0)
                filePath = AssetDatabase.GUIDToAssetPath(assets[0]).Split(new string[] { "Assets" }, System.StringSplitOptions.RemoveEmptyEntries)[0];
            else
            {
                Debug.LogError("Could not find PhysSoundTypeList.cs asset. Create a new one at a desired location for it to be overwritten.");
                return;
            }

            StreamWriter sw = new StreamWriter(Application.dataPath + filePath, false);
            sw.WriteLine("namespace PhysSound{");
            sw.WriteLine("//DO NOT EDIT THIS FILE DIRECTLY! Use the editor provided under Window/PhysSound.");
            sw.WriteLine("public class PhysSoundTypeList {");

            sw.Write("public static string[] PhysSoundTypes = new string[" + tempTypes.Count + "] {");

            for (int i = 0; i < tempTypes.Count; i++)
            {
                sw.Write("\"" + tempTypes[i] + "\"");

                if (i != tempTypes.Count - 1)
                    sw.Write(",");
            }

            sw.WriteLine("};");
            sw.WriteLine("");

            sw.WriteLine("public static string GetKey(int index) { return (index >= PhysSoundTypes.Length) || (index < 0) ? \"\" : PhysSoundTypes[index]; }");
            sw.WriteLine("");

            sw.WriteLine("public static bool HasKey(int index) { return index < PhysSoundTypes.Length && index >= 0; }");
            sw.WriteLine("");

            sw.WriteLine("}");
            sw.WriteLine("}");

            sw.Close();

            AssetDatabase.ImportAsset("Assets" + filePath);
        }

        void savePoolSize()
        {
            string oldFilePath;

            string[] assets = AssetDatabase.FindAssets("PhysSoundTempAudioPool");
            if (assets.Length > 0)
                oldFilePath = AssetDatabase.GUIDToAssetPath(assets[0]).Split(new string[] { "Assets" }, System.StringSplitOptions.RemoveEmptyEntries)[0];
            else
            {
                Debug.LogError("Could not find PhysSoundTempAudioPool.cs asset. Please reimport the package.");
                return;
            }

            string tempFilePath = Application.dataPath + "/temp.cs";

            FileStream f = File.Create(tempFilePath);

            string line;
            StreamReader sr = new StreamReader(Application.dataPath + oldFilePath);
            StreamWriter sw = new StreamWriter(f);

            line = sr.ReadLine();

            while (line != null)
            {
                if (line.Contains("public static int TempAudioPoolSize"))
                    sw.WriteLine("\t\tpublic static int TempAudioPoolSize=" + tempPoolSize + ";");
                else
                    sw.WriteLine(line);

                line = sr.ReadLine();
            }

            sw.Close();
            sr.Close();
            f.Close();

            File.Copy(tempFilePath, Application.dataPath + oldFilePath, true);
            File.Delete(tempFilePath);

            AssetDatabase.ImportAsset("Assets" + oldFilePath);
        }

        void loadTypes()
        {
            string filePath;

            string[] assets = AssetDatabase.FindAssets("PhysSoundTypeList");
            if (assets.Length > 0)
                filePath = AssetDatabase.GUIDToAssetPath(assets[0]).Split(new string[] { "Assets" }, System.StringSplitOptions.RemoveEmptyEntries)[0];
            else
            {
                Debug.LogWarning("Could not find PhysSoundTypeList.cs asset. Create a new one at a desired location for it to be overwritten.");
                return;
            }

            StreamReader sr = new StreamReader(Application.dataPath + filePath);

            string input = sr.ReadLine();

            while (input != null)
            {
                string trimmed = input.Trim(' ');
                if (trimmed.StartsWith("public static string[] PhysSoundTypes"))
                {
                    int start = trimmed.IndexOf('{') + 1;
                    int end = trimmed.IndexOf('}');

                    string[] values = trimmed.Substring(start, end - start).Split(',');

                    for (int i = 0; i < values.Length; i++)
                    {
                        values[i] = values[i].Substring(1, values[i].Length - 2);
                    }

                    tempTypes = new List<string>(values);
                    break;
                }

                input = sr.ReadLine();
            }

            sr.Close();
        }

        void loadPoolSize()
        {
            string filePath;

            string[] assets = AssetDatabase.FindAssets("PhysSoundTempAudioPool");
            if (assets.Length > 0)
                filePath = AssetDatabase.GUIDToAssetPath(assets[0]).Split(new string[] { "Assets" }, System.StringSplitOptions.RemoveEmptyEntries)[0];
            else
            {
                Debug.LogError("Could not find PhysSoundTempAudioPool.cs asset. Please reimport the package.");
                return;
            }

            StreamReader sr = new StreamReader(Application.dataPath + filePath);

            string line = sr.ReadLine();

            while (line != null)
            {
                if (line.Contains("public static int TempAudioPoolSize"))
                {
                    string num = line.Split('=', ';')[1];
                    tempPoolSize = int.Parse(num);
                    break;
                }

                line = sr.ReadLine();
            }

            sr.Close();

            EditorUtility.SetDirty(this);
        }

        #endregion
    }
}
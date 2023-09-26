using UnityEditor;
using UnityEngine;

namespace UnityExt.Core {

    #if UNITY_EDITOR

    /// <summary>
    /// Utility Tooling for Editor.
    /// </summary>
    public class EditorTools {

        #region ScriptableObject
        /// <summary>
        /// Given a mono script selection creates a ScriptableObject.
        /// </summary>
        [MenuItem("Assets/Create Scriptable Object",false)]
        static void CreateScriptableObject() {
            MonoScript[] msl = Selection.GetFiltered<MonoScript>(SelectionMode.Assets);
            //Debug.Log("EditorContext> CreateScriptableObject / count["+msl.Length+"]");
            if (msl.Length <= 0) { Debug.LogError("EditorContext> Invalid Scriptable Object Type"); return; }
            CreateScriptableObject(msl[0]);
        }

        /// <summary>
        /// Creates the scriptable object given the script type.
        /// </summary>
        /// <param name="p_target"></param>
        static void CreateScriptableObject(MonoScript p_target) {
            if (!p_target) { Debug.LogError("EditorContext> Invalid Scriptable Object Type"); return; }
            System.Type t = p_target.GetClass();
            if (!t.IsSubclassOf(typeof(ScriptableObject))) { Debug.LogError("EditorContext> Invalid Scriptable Object Type [" + t.FullName + "]"); return; }
            string path = EditorUtility.SaveFilePanelInProject("New " + t.Name,t.Name,"asset","");
            if (string.IsNullOrEmpty(path)) return;
            ScriptableObject asset = ScriptableObject.CreateInstance(t);
            AssetDatabase.CreateAsset(asset,path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }
        #endregion

        #region Profiling
        [MenuItem("Assets/Logs/Memory Usage",false)]
        static void OutputMemory() {
            UnityEngine.Object[] o = Selection.objects;
            if (o.Length <= 1) { Debug.Log($"EditorContext> asset[{o[0].name}] runtime-memory[{(UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(o[0]) / 1024)}kb]"); return; }
            string logs = "EditorContext> Memory Profile\n";
            long t = 0;
            foreach (UnityEngine.Object it in o) { long n = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(it); logs += $" asset[{it.name}] runtime-memory[{(n / 1024)}kb]\n"; t += n; }
            logs += $" Total: {t / 1024}kb";
            Debug.Log(logs);
        }
        #endregion
    
    }

    #endif

}
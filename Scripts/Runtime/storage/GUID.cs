using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityExt.Core.Storage { 

    /// <summary>
    /// Class that implements a GUID generation and tagging system for Component based elements.
    /// </summary>
    public class GUID : MonoBehaviour {

        #region static
        /// <summary>
        /// CTOR
        /// </summary>
        static GUID() {
            int k=0;
            for(int i='!';i<='~';i++) { m_ascii_set[k++] = ((char)i).ToString(); }
        }

        /// <summary>
        /// Internals
        /// </summary>
        static internal char  []      m_gen_buffer       = new char[1024 * 5];
        static internal string[]      m_ascii_set        = new string[('~' - '!')+1];
        static internal byte  []      m_ascii_set_idx    = new byte  [('~' - '!')+1];
        static internal System.Random m_gen_rnd          = new System.Random();
        static internal Regex         m_gen_default_regx = new Regex("[0-9]|[a-z]|[A-Z]");

        /// <summary>
        /// Generates a new GUID following the expression in the form of 'SOMEPREFIX-***-***-ANYSUFFIX' where '*' is replaced by the char matching the charset.
        /// </summary>
        /// <param name="p_expression">Generator expression.</param>
        /// <param name="p_charset">Character generation rule</param>
        /// <param name="p_buffer">GUID buffer to write into.</param>
        static public void Generate(string p_expression,Regex p_charset,char[] p_buffer,out int p_length) {
            int           len  = Math.Min(p_expression.Length,p_buffer.Length);            
            System.Random rnd  = m_gen_rnd;
            Regex         regx = p_charset==null ? m_gen_default_regx : p_charset;
            int k=0;
            //First iterate the full charset and store the needed indexes
            int aset_len=0;
            int a_len = m_ascii_set.Length;                        
            for(int j=0;j<a_len;j++) {
                string it = m_ascii_set[j];
                if(regx.IsMatch(it)) { m_ascii_set_idx[aset_len++]=(byte)j; }
            }
            //Iterate the expression and replace '*' by a random item of charset
            for(int i=0;i<len;i++) {
                char c   = p_expression[i];
                char res = '\0';
                switch(c) {
                    case '*': {                        
                        if(aset_len<=0) break;
                        int idx = rnd.Next(0,aset_len);
                        res = m_ascii_set[m_ascii_set_idx[idx]][0];
                    }
                    break;
                    default: res = c; break;
                }
                if(res<=0) continue;
                p_buffer[k] = res;
                k++;
            }
            p_length = k;
        }
        
        /// <summary>
        /// Generates a new GUID following the expression in the form of 'SOMEPREFIX-***-***-ANYSUFFIX' where '*' is replaced by the char matching the dictionary.
        /// </summary>
        /// <param name="p_expression">Generator expression.</param>        
        /// <param name="p_buffer">GUID buffer to write into.</param>
        static public void Generate(string p_expression,char[] p_buffer,out int p_length) { Generate(p_expression,null,p_buffer,out p_length); }

        /// <summary>
        /// Generates a new GUID following the expression in the form of 'SOMEPREFIX-***-***-ANYSUFFIX' where '*' is replaced by the char matching the dictionary.
        /// </summary>
        /// <param name="p_expression">Generator expression.</param>
        /// <param name="p_dictionary">Dictionary of allowed chars</param>
        /// <returns>GUID string</returns>
        static public string Generate(string p_expression,Regex p_dictionary) {
            int c=0;
            Generate(p_expression,p_dictionary,m_gen_buffer,out c);
            return new string(m_gen_buffer,0,c);
        }

        /// <summary>
        /// Generates a new GUID following the expression in the form of 'SOMEPREFIX-***-***-ANYSUFFIX' where '*' is replaced by the char matching the dictionary.
        /// </summary>
        /// <param name="p_expression">Generator expression.</param>        
        /// <returns>GUID string</returns>
        static public string Generate(string p_expression) { return Generate(p_expression,null); }
        #endregion

        /// <summary>
        /// Charset RegExp rule to generate the random characters.
        /// </summary>
        public string charset {
            get { return m_charset; }
            set {
                if(m_charset==value) return;
                m_charset        = value;                
                Generate();
            }
        }
        [SerializeField]
        private string m_charset = "[0-9]|[a-z]|[A-Z]";
        private Regex  m_charset_regx;

        /// <summary>
        /// Expression to generate the guid sequence.
        /// </summary>
        public string expression {
            get { return m_expression; }
            set { 
                if(m_expression == value) return;
                m_expression = value;
                Generate();
            }
        }
        [SerializeField]
        private string m_expression = "******-******-******";

        /// <summary>
        /// Flag that tells the pattern generation is correctly setup or not.
        /// </summary>
        public bool validCharset { get { return m_valid_charset; } private set { m_valid_charset = value; } }
        [SerializeField]
        private bool m_valid_charset;

        /// <summary>
        /// GUID for this object.
        /// </summary>
        public string guid {
            get { return m_guid;  }
            set { m_guid = value; }
        }
        [SerializeField]
        private string m_guid;

        /// <summary>
        /// Generates this object guid.
        /// </summary>
        public void Generate() { 
            //Restore if regexp is null
            if(m_charset_regx==null) m_prev_charset = "";
            if(m_prev_charset != m_charset) {
                m_prev_charset = m_charset;
                validCharset = true;
                try {
                    m_charset_regx = new Regex(m_charset);
                }
                catch(System.Exception) {
                    validCharset   = false;
                    m_charset_regx = null;
                }
            }            
            m_guid = Generate(expression,m_charset_regx); 
        }
        internal string m_prev_charset;

    }


    #if UNITY_EDITOR

    #region class GUIDInspector
    /// <summary>
    /// Custom inspector to handle the GUID component functionality.
    /// </summary>    
    [CanEditMultipleObjects]
    [CustomEditor(typeof(GUID))]
    public class GUIDInspector : Editor {

        /// <summary>
        /// Reference to the target object.
        /// </summary>
        new public GUID target { get { return (GUID)base.target; } }

        /// <summary>
        /// Start
        /// </summary>
        protected void OnEnable() {
            //Boot any empty guid.
            for(int i=0;i<targets.Length;i++) {
                GUID it = (GUID)targets[i];
                if(!it) continue;
                if(!string.IsNullOrEmpty(it.guid)) continue;
                it.Generate();
            }
        }

        /// <summary>
        /// GUI Render
        /// </summary>
        public override void OnInspectorGUI() {
            
            SerializedProperty sp;
            SerializedObject   so = serializedObject;
            bool               is_change = false;
            string             vs = "";

            GUIStyle tf_style  = new GUIStyle(GUI.skin.textField);
            tf_style.fontSize  = 14;
            tf_style.fontStyle = FontStyle.Bold;
            tf_style.alignment = TextAnchor.MiddleCenter;
            tf_style.normal.textColor = Colorf.yellow75;

            GUIStyle bt_small = new GUIStyle(GUI.skin.GetStyle("toolbarbutton"));
            GUIStyle lb_small = new GUIStyle(GUI.skin.GetStyle("MiniLabel"));
            

            EditorGUILayout.Separator();

            if(targets.Length<=1) {
                GUILayout.BeginHorizontal();                                
                GUI.backgroundColor = Color.gray;
                vs = EditorGUILayout.TextField(target.guid,tf_style,GUILayout.Height(30f));                
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
                if(vs!=target.guid) {
                    target.guid = vs;
                }
            }
            
            EditorGUILayout.Separator();
            
            tf_style  = GUI.skin.textField;
            tf_style.fontSize  = 10;            
            tf_style.alignment = TextAnchor.MiddleLeft;

            EditorGUILayout.BeginHorizontal();
            EditorGUIUtility.labelWidth = 65f;
            sp = serializedObject.FindProperty("m_expression");
            EditorGUILayout.PropertyField(sp,new GUIContent("Expression"),GUILayout.ExpandWidth(true));
            EditorGUIUtility.labelWidth = 50f;
            sp = serializedObject.FindProperty("m_charset");
            EditorGUILayout.PropertyField(sp,new GUIContent("Charset"),GUILayout.MaxWidth(155f));
            EditorGUILayout.EndHorizontal();

            if(so.hasModifiedProperties) {
                so.ApplyModifiedProperties();                
                is_change = true;                
            }

            //Bulk generate the selections guid.

            if(GUILayout.Button("Generate",bt_small)) {
                is_change=true;                
            }

            if(is_change) {
                for(int i=0;i<targets.Length;i++) {
                    GUID it = (GUID)targets[i];
                    if(!it) continue;                    
                    it.Generate();
                    EditorUtility.SetDirty(it.gameObject);
                }
            }

            bool is_valid = true;
            for(int i=0;i<targets.Length;i++) {
                GUID it = (GUID)targets[i];
                if(!it) continue;                    
                if(!it.validCharset) is_valid=false;
            }

            if(!is_valid) {
                EditorGUILayout.HelpBox("Invalid Charset Found", MessageType.Error);
            }

            tf_style.fontSize  = 11;            
            tf_style.alignment = TextAnchor.UpperLeft;

        }
    }
    #endregion

    #endif

}
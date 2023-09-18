using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Jobs;
using System.Reflection;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityExt.Core {

    #region enum ActivityContext

    /// <summary>
    /// Enumeration that describes the execution context of the activity.
    /// </summary>
    public enum ActivityContext {
        /// <summary>
        /// No context
        /// </summary>
        None=-1,
        /// <summary>
        /// All contexts wild card.
        /// </summary>
        All = 0,
        /// <summary>
        /// Runs inside Monobehaivour.Update
        /// </summary>
        Update,
        /// <summary>
        /// Runs inside Monobehaivour.LateUpdate
        /// </summary>
        LateUpdate,
        /// <summary>
        /// Runs inside Monobehaivour.FixedUpdate
        /// </summary>
        FixedUpdate,
        /// <summary>
        /// Runs inside Monobehaivour.Update but only inside 'async-slice' duration per frame
        /// </summary>
        Async,
        /// <summary>
        /// Runs inside a thread, watch out to not use Unity API elements inside it.
        /// </summary>        
        Thread,        
        /// <summary>
        /// Runs the job passed as parameter in a loop and using schedule
        /// </summary>
        JobAsync,
        /// <summary>
        /// Runs the job passed as parameter in a loop and using run
        /// </summary>
        Job,
        /// <summary>
        /// Runs inside EditorApplication.update
        /// </summary>
        Editor
    }

    #endregion

    #region enum ActivityState

    /// <summary>
    /// Enumeration that describes the execution state of the activity.
    /// </summary>
    public enum ActivityState {
        /// <summary>
        /// Just created activity.
        /// </summary>
        Idle,
        /// <summary>
        /// Waiting for execution
        /// </summary>
        Queued,
        /// <summary>
        /// Active execution.
        /// </summary>
        Running,
        /// <summary>
        /// Finished
        /// </summary>
        Complete,
        /// <summary>
        /// Stopped
        /// </summary>
        Stopped        
    }

    #endregion

    /// <summary>
    /// Class that implements any async activity/processing.
    /// It can run in different contexts inside unity (mainthread) or separate thread, offering an abstraction layer Monobehaviour/Thread agnostic.
    /// </summary>
    public class Activity : INotifyCompletion, IStatusProvider, IProgressProvider {

        #region Manager
        /*
        /// <summary>
        /// Behaviour to handle all activity  executions.
        /// </summary>        
        public class Manager : MonoBehaviour {

            /// <summary>
            /// Handler for all activity execution.
            /// </summary>
            public ActivityManager handler { get { return m_handler==null ? (m_handler = new ActivityManager()) : m_handler; } }
            protected ActivityManager m_handler;

            /// <summary>
            /// Unity Calls
            /// </summary>
            protected void Awake()       { 
                handler.Awake();       
            }
            internal void Start()        { 
                handler.Start();       
            }
            internal void Update()       { 
                //if(UnityEngine.Profiling.Profiler.enabled) UnityEngine.Profiling.Profiler.BeginSample("Activity.Update");
                handler.Update();      
                //if(UnityEngine.Profiling.Profiler.enabled) UnityEngine.Profiling.Profiler.EndSample();
            }            
            internal void FixedUpdate()  { 
                //if(UnityEngine.Profiling.Profiler.enabled) UnityEngine.Profiling.Profiler.BeginSample("Activity.FixedUpdate");
                handler.FixedUpdate(); 
                //if(UnityEngine.Profiling.Profiler.enabled) UnityEngine.Profiling.Profiler.EndSample();
            }            
            internal void LateUpdate()   { 
                //if(UnityEngine.Profiling.Profiler.enabled) UnityEngine.Profiling.Profiler.BeginSample("Activity.LateUpdate");
                handler.LateUpdate();  
                //if(UnityEngine.Profiling.Profiler.enabled) UnityEngine.Profiling.Profiler.EndSample();
            }
            internal void OnDestroy()    { if(m_handler!=null) { m_handler.Clear(); m_handler=null; }  }

            #if UNITY_EDITOR
            /// <summary>
            /// Execution Loop for Editor.
            /// </summary>
            public   void EditorUpdate() { 
                //Skip editor updates when playing
                if(Application.isPlaying) return; 
                //Update similarly as Mono.Update
                handler.Update(); 
                //If no nodes or during compiling destroy context
                if(handler.IsEmpty() || EditorApplication.isCompiling) {
                    EditorApplication.update -= EditorUpdate; 
                    DestroyImmediate(gameObject); 
                } 
            }
            #endif

        }
        //*/
        /*
        /// <summary>
        /// Reference to the global Activity manager.
        /// </summary>
        static public Manager manager {
            get {
                if(m_manager)  return m_manager;
                if(!m_manager) m_manager = GameObject.FindObjectOfType<Manager>();
                if(m_manager)  return m_manager;
                GameObject g = new GameObject("activity-manager");
                if(Application.isPlaying) GameObject.DontDestroyOnLoad(g);
                m_manager = g.AddComponent<Manager>();
                #if UNITY_EDITOR
                //If not playing, an editor script or tool might want looping callbacks
                if(!Application.isPlaying) {                     
                    //Emulate Awake
                    m_manager.handler.Awake();
                    //Emulate Start
                    m_manager.handler.Start();
                    //Add a 'suffix' to easily see on hierarchy
                    m_manager.name+="-editor";
                    //Prevent state inconsistencies
                    m_manager.hideFlags = HideFlags.DontSave;
                    //Register method for update looping
                    EditorApplication.update += manager.EditorUpdate;
                    //Register for destroy context in case of playmode change
                    EditorApplication.playModeStateChanged += 
                    delegate(PlayModeStateChange m) {                         
                        //Unregister callback
                        EditorApplication.update -= m_manager.EditorUpdate; 
                        //Destroy offline manager
                        GameObject.DestroyImmediate(m_manager.gameObject); 
                    };                    
                }
                #endif
                return m_manager;
            }
        }
        static private Manager m_manager;
        //*/

        #region Manager Inspector
        #if UNITY_EDITOR
        /*
        /// <summary>
        /// Class to handle the activity manager inspector.
        /// </summary>
        [CustomEditor(typeof(Manager))]
        public class ManagerInspector : Editor {

            /// <summary>
            /// GUI
            /// </summary>
            public override void OnInspectorGUI() {            
                SerializedObject so = serializedObject;
                SerializedProperty sp = so.GetIterator();
                //Enter
                sp.NextVisible(true);
                //Iterate properties
                while(sp.NextVisible(false)) {
                    switch(sp.displayName.ToLower()) {
                        case "script": break;
                        default: {
                            EditorGUILayout.PropertyField(sp);
                        }
                        break;
                    }
                }

                Manager t = target as Manager;
                if(!t)      return;
                ActivityManager h = t.handler;
                if(h==null) return;
                
                //GUIStyle stl;

                GUILayout.Space(5f);

                if(HeaderFoldoutGUI("Stats","")) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Max Threads: ",Activity.maxThreadCount.ToString(),EditorStyles.miniLabel);
                    EditorGUILayout.LabelField("Total Nodes: ",h.GetCountTotal().ToString(),EditorStyles.miniLabel);
                    EditorGUI.indentLevel--;
                }

                GUILayout.Space(2f);

                if(HeaderFoldoutGUI($"Update",$"{h.lu.profilerTimeStr} | {h.lu.Count}")) {
                    EditorGUI.indentLevel++;  
                    if(h.lu.Count <= 0) {
                        EditorGUILayout.HelpBox("No Nodes Running", MessageType.Warning);
                    }
                    else {
                        ListActivityGUI(h.lu.la);
                        ListInterfaceGUI(h.lu.li);
                    }
                    EditorGUI.indentLevel--;
                }

                GUILayout.Space(2f);

                if(HeaderFoldoutGUI($"AsyncUpdate",$"{h.lau.profilerTimeStr} | {h.lau.Count}")) {
                    EditorGUI.indentLevel++;
                    if(h.lau.Count <= 0) {
                        EditorGUILayout.HelpBox("No Nodes Running", MessageType.Warning);
                    }
                    else {
                        ListActivityGUI(h.lau.la);
                        ListInterfaceGUI(h.lau.li);
                    }
                    EditorGUI.indentLevel--;
                }

                GUILayout.Space(2f);

                if(HeaderFoldoutGUI($"LateUpdate",$"{h.llu.profilerTimeStr} | {h.llu.Count}")) {
                    EditorGUI.indentLevel++;  
                    if(h.llu.Count <= 0) {
                        EditorGUILayout.HelpBox("No Nodes Running", MessageType.Warning);
                    }
                    else {
                        ListActivityGUI(h.llu.la);
                        ListInterfaceGUI(h.llu.li);
                    }
                    EditorGUI.indentLevel--;
                }

                GUILayout.Space(2f);

                if(HeaderFoldoutGUI($"FixedUpdate",$"{h.lfu.profilerTimeStr} | {h.lfu.Count}")) {                    
                    EditorGUI.indentLevel++;                    
                    if(h.lfu.Count <= 0) {
                        EditorGUILayout.HelpBox("No Nodes Running", MessageType.Warning);
                    }
                    else {
                        ListActivityGUI(h.lfu.la);
                        ListInterfaceGUI(h.lfu.li);
                    }
                    EditorGUI.indentLevel--;
                }

                GUILayout.Space(2f);

                if(HeaderFoldoutGUI("Threads","")) {                    
                    GUILayout.Space(1f);
                    EditorGUI.indentLevel++;
                    for(int i=0;i<h.lt.Length;i++) {
                        if(HeaderFoldoutGUI($"Pool {(i+1)}",$"{h.lt[i].profilerTimeStr} | {h.lt[i].Count}",new Color(0.2f,0.2f,0.2f),EditorStyles.miniBoldLabel)) {
                            if(h.lt[i].Count <= 0) {
                                EditorGUILayout.HelpBox("No Nodes Running", MessageType.Warning);
                            }
                            else {
                                ListActivityGUI(h.lt[i].la);
                                ListInterfaceGUI(h.lt[i].li);
                            }                            
                        }
                        GUILayout.Space(1f);
                    }
                    EditorGUI.indentLevel--;
                }

                GUILayout.Space(2f);
                
                Repaint();

            }

            /// <summary>
            /// Custom Foldout GUI
            /// </summary>
            /// <param name="p_label"></param>
            /// <returns></returns>
            public bool HeaderFoldoutGUI(string p_label,string p_caption,Color p_color,GUIStyle p_style=null) {
                GUIStyle stl = null;
                string fo_k = $"activity-manager-{p_label.ToLower()}";
                bool vb1 = EditorPrefs.GetBool(fo_k,false);
                Rect r = GUILayoutUtility.GetRect(Screen.width,EditorGUIUtility.singleLineHeight*1.5f);
                r.xMin -= 15f;
                r.xMax += 15f;
                GUI.DrawTexture(r,Texture2D.whiteTexture, ScaleMode.StretchToFill,true,1f,p_color,0f,2f);
                r = EditorGUI.IndentedRect(r); r.xMin+=15f; r.y+=5f;
                bool vb2 = EditorGUI.Foldout(r,vb1,"");
                if(vb1!=vb2) { EditorPrefs.SetBool(fo_k,vb2); }
                if(p_style!=null) { stl = new GUIStyle(p_style); r.xMin += 2f; r.y-=1f; }
                if(stl==null)     { stl = new GUIStyle(EditorStyles.whiteLargeLabel); stl.fontSize = 14; stl.fontStyle = FontStyle.Bold; stl.alignment=TextAnchor.MiddleLeft; r.xMin += 16f; r.y-=6f; }                
                EditorGUI.LabelField(r,p_label,stl);   
                if(!string.IsNullOrEmpty(p_caption)) {
                    r.xMax -= 80f;
                    r.xMin = r.xMax-120f;
                    if(p_style!=null) r.y -= 6f;
                    stl.fontSize  = 10;
                    stl.alignment = TextAnchor.MiddleRight;
                    EditorGUI.LabelField(r,p_caption,stl);   
                }                
                return vb2;
            }

            /// <summary>
            /// Custom Foldout GUI
            /// </summary>
            /// <param name="p_label"></param>
            /// <param name="p_color"></param>
            /// <param name="p_style"></param>
            /// <returns></returns>
            public bool HeaderFoldoutGUI(string p_label,string p_caption,GUIStyle p_style = null) { return HeaderFoldoutGUI(p_label,p_caption,new Color(0.22f,0.22f,0.22f),p_style); }

            /// <summary>
            /// Draws the activity list gui for each item.
            /// </summary>
            /// <param name="p_list"></param>
            public void ListActivityGUI(IList<Activity> p_list) {
                if(p_list==null) return;
                GUIStyle toggle_stl   = new GUIStyle(EditorStyles.miniLabel);                
                GUIStyle caption_stl  = new GUIStyle(EditorStyles.miniLabel);
                caption_stl.alignment = TextAnchor.MiddleRight;
                GUIStyle mini_btn_stl = new GUIStyle(EditorStyles.miniButton);
                for(int i=0;i<p_list.Count;i++) {
                    Activity it = p_list[i];
                    if(it==null) continue;
                    //Rect r = GUILayoutUtility.GetRect(Screen.width,EditorGUIUtility.singleLineHeight*0.8f);
                    EditorGUILayout.BeginHorizontal(GUILayout.Width(Screen.width-87f)); {
                        it.enabled = EditorGUILayout.ToggleLeft(it.id,it.enabled,toggle_stl);
                        EditorGUILayout.LabelField(it.profilerTimeStr,caption_stl,GUILayout.Width(80f));
                        if(GUILayout.Button("Stop",mini_btn_stl,GUILayout.Width(80f))) {
                            it.Stop(); 
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(-1f);
                }
            }

            /// <summary>
            /// Draws the interface list gui for each item.
            /// </summary>
            /// <param name="p_list"></param>
            public void ListInterfaceGUI<T>(IList<T> p_list) {
                if(p_list==null) return;
                GUIStyle toggle_stl   = new GUIStyle(EditorStyles.miniLabel);
                GUIStyle caption_stl  = new GUIStyle(EditorStyles.miniLabel);
                caption_stl.alignment = TextAnchor.MiddleRight;
                GUIStyle mini_btn_stl = new GUIStyle(EditorStyles.miniButton);
                bool vb;
                for(int i=0;i<p_list.Count;i++) {
                    T it = p_list[i];
                    if(it==null) continue;
                    UnityEngine.Behaviour it_b = it is UnityEngine.Behaviour ? (UnityEngine.Behaviour)(object)it : null;
                    string itf_n = it_b ? it_b.name : it.GetType().Name;
                    //Rect r = GUILayoutUtility.GetRect(Screen.width,EditorGUIUtility.singleLineHeight*0.8f);
                    EditorGUILayout.BeginHorizontal(GUILayout.Width(Screen.width-87f)); {
                        ActivityBehaviour it_ab=null;
                        if(it_b) {                                
                            vb = EditorGUILayout.ToggleLeft(itf_n,it_b.enabled,toggle_stl);
                            if(vb != it_b.enabled) it_b.enabled = vb;
                            it_ab = it_b as ActivityBehaviour;                            
                        }
                        else {
                            GUI.color = new Color(1f,1f,1f,0.05f);
                            EditorGUILayout.ToggleLeft(itf_n,false,toggle_stl);
                            GUI.color = Color.white;
                        }                        
                        EditorGUILayout.LabelField(it_ab ? it_ab.profilerTimeStr : "---ms",caption_stl,GUILayout.Width(80f));
                        if(GUILayout.Button("Stop",mini_btn_stl,GUILayout.Width(80f))) {
                            Activity.Remove(it);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(-1f);
                }
            }

        }
        //*/
        #endif
        #endregion

        #endregion

        #region static

        /// <summary>
        /// CTOR
        /// </summary>
        static Activity() {
            //Warmup non-thread-safe data
            m_app_persistent_dp = Application.persistentDataPath;
            m_app_platform      = Application.platform.ToString().ToLower();
        }
        static internal string m_app_persistent_dp;
        static internal string m_app_platform;

        /// <summary>
        /// Reference to the activity manager.
        /// </summary>
        static public ActivityManager manager { get { return ActivityManager.manager; } }
        
        /// <summary>
        /// Execution time slice for async nodes.
        /// </summary>
        static public int asyncTimeSlice = 4;

        /// <summary>
        /// Maximum created threads for paralell nodes.
        /// </summary>
        static public int maxThreadCount = Mathf.Max(1,Environment.ProcessorCount/4);

        #region Add/Remove/Find

        /// <summary>
        /// Adds any object implementing the interfaces for exection.
        /// </summary>
        /// <param name="p_node">Execution node. Must implement one or more Activity related interfaces.</param>
        //static public void Add(object p_node) { if(manager)manager.handler.AddInterface(p_node); }
        static public void Add(object p_node) { if(manager)manager.AddInterface(p_node); }

        /// <summary>
        /// Removes any object implementing the interfaces for exection.
        /// </summary>
        /// <param name="p_node">Execution node. Must implement one or more Activity related interfaces.</param>
        //static public void Remove(object p_node) { if(m_manager)m_manager.handler.RemoveInterface(p_node); }
        static public void Remove(object p_node) { if(manager)manager.RemoveInterface(p_node); }

        /// <summary>
        /// Removes all activities and interfaces from exection.
        /// </summary>
        //static public void Kill() { if(m_manager)m_manager.handler.Clear(); }
        static public void Kill() { if(manager)manager.Kill(); }

        /// <summary>
        /// Searches for a single activity by id and context.
        /// </summary>
        /// <param name="p_id">Activity id to search.</param>
        /// <param name="p_context">Specific context to be searched.</param>
        /// <typeparam name="T">Activity derived type.</typeparam>
        /// <returns>Activity found or null</returns>
        //static public T Find<T>(string p_id,ActivityContext p_context) where T : Activity { if(manager) return manager.handler.Find<T>(p_id,p_context); return null; }
        static public T Find<T>(string p_id,ActivityContext p_context) where T : Activity { if(manager) return manager.Find<T>(p_id,p_context); return null; }

        /// <summary>
        /// Searches for a single activity by id in all contexts.
        /// </summary>
        /// <param name="p_id">Activity id to search.</param>        
        /// <typeparam name="T">Activity derived type.</typeparam>
        /// <returns>Activity found or null</returns>
        static public T Find<T>(string p_id) where T : Activity { return Find<T>(p_id,ActivityContext.All); }

        /// <summary>
        /// Searches for all activities matching the id and context.
        /// </summary>
        /// <param name="p_id">Activity id to search.</param>
        /// <param name="p_context">Specific context to be searched.</param>
        /// <typeparam name="T">Activity derived type.</typeparam>
        /// <returns>List of results or empty list.</returns>
        //static public List<T> FindAll<T>(string p_id,ActivityContext p_context) where T : Activity { List<T> res=null; if(manager) res = manager.handler.FindAll<T>(p_id,p_context); return res==null ? new List<T>() : res; }
        static public List<T> FindAll<T>(string p_id,ActivityContext p_context) where T : Activity {
            List<T> res = null;
            if(manager) {
                if(p_context == ActivityContext.All) {
                    res = new List<T>();
                    res.AddRange(manager.FindAll<T>(p_id,ActivityContext.Update     ));
                    res.AddRange(manager.FindAll<T>(p_id,ActivityContext.LateUpdate ));
                    res.AddRange(manager.FindAll<T>(p_id,ActivityContext.FixedUpdate));
                    res.AddRange(manager.FindAll<T>(p_id,ActivityContext.Thread     ));
                    res.AddRange(manager.FindAll<T>(p_id,ActivityContext.Async      ));
                }
                else {
                    res = manager.FindAll<T>(p_id,p_context);
                }                                
            }
            return res == null ? new List<T>() : res;
        }

        /// <summary>
        /// Searches for all activities matching the context.
        /// </summary>        
        /// <param name="p_context">Specific context to be searched.</param>
        /// <typeparam name="T">Activity derived type.</typeparam>
        /// <returns>List of results or empty list.</returns>
        static public List<T> FindAll<T>(ActivityContext p_context) where T : Activity { return FindAll<T>("",p_context); }

        /// <summary>
        /// Searches for all activities matching the id in all contexts.
        /// </summary>
        /// <param name="p_id">Activity id to search.</param>        
        /// <typeparam name="T">Activity derived type.</typeparam>
        /// <returns>List of results or empty list.</returns>
        static public List<T> FindAll<T>(string p_id) where T : Activity { return FindAll<T>(p_id,(ActivityContext)(-1)); }

        /// <summary>
        /// Searches all activities regardless of 'id'.
        /// </summary>
        /// <typeparam name="T">Activity derived type.</typeparam>
        /// <returns>List of results or empty list.</returns>
        static public List<T> FindAll<T>() where T : Activity { return FindAll<T>("",ActivityContext.All); }

        #endregion

        #region Run/Loop

        /// <summary>
        /// Helper
        /// </summary>        
        static internal Activity Create(string p_id,System.Predicate<Activity> p_on_execute,System.Action<Activity> p_on_complete,ActivityContext p_context) {
            Activity n = new Activity(p_id,p_context);
            n.OnCompleteEvent = p_on_complete;
            n.OnExecuteEvent  = p_on_execute;
            return n;
        }
        
        /// <summary>
        /// Creates and starts an activity for constant loop execution.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>
        /// <param name="p_context">Execution context to run.</param>
        /// <returns>The running activity</returns>
        static public Activity Run(string p_id,System.Predicate<Activity> p_callback,ActivityContext p_context = ActivityContext.Update) { Activity a = Create(p_id,p_callback,null,p_context); a.Start(); return a; }
            
        /// <summary>
        /// Creates and starts an activity for constant loop execution.
        /// </summary>        
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>
        /// <param name="p_context">Execution context to run.</param>
        /// <returns>The running activity</returns>
        static public Activity Run(System.Predicate<Activity> p_callback,ActivityContext p_context = ActivityContext.Update) { Activity a = Create("",p_callback,null,p_context); a.Start(); return a; }

        /// <summary>
        /// Creates and start a single execution activity.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <param name="p_context">Execution context.</param>
        /// <returns>The running activity</returns>
        static public Activity Run(string p_id,System.Action<Activity> p_callback,ActivityContext p_context = ActivityContext.Update) { Activity a = Create(p_id,null,p_callback,p_context); a.Start(); return a; }
        
        /// <summary>
        /// Creates and start a single execution activity.
        /// </summary>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <param name="p_context">Execution context.</param>
        /// <returns>The running activity</returns>
        static public Activity Run(System.Action<Activity> p_callback,ActivityContext p_context = ActivityContext.Update) { Activity a = Create("",null,p_callback,p_context); a.Start(); return a; }

        #endregion

        #endregion

        /// <summary>
        /// Reference to the process containing this activity
        /// </summary>
        internal ActivityProcess process { get; set; }

        /// <summary>
        /// Id of this Activity.
        /// </summary>
        public string id;

        /// <summary>
        /// Last execution ms.
        /// </summary>
        public float profilerMs;

        /// <summary>
        /// Last execution ns
        /// </summary>
        public long profilerUs { get { return (long)(Mathf.Round(profilerMs*10f)*100f); } }

        /// <summary>
        /// Returns a formatted string telling the profiled time.
        /// </summary>
        public string profilerTimeStr { get { long ut = profilerUs; return profilerMs<1f ? (ut<=0 ? "0 ms" : $"{ut} us") : $"{Mathf.RoundToInt(profilerMs)} ms"; }  }

        /// <summary>
        /// Execution state
        /// </summary>
        public ActivityState state { get; internal set; }        
        
        /// <summary>
        /// Execution context.
        /// </summary>
        public ActivityContext context { get { return m_context; } internal set { m_context = value == ActivityContext.All ? ActivityContext.Update : value; } }
        private ActivityContext m_context;

        /// <summary>
        /// Has the activity finished.
        /// </summary>
        public bool completed { get { return state == ActivityState.Complete; } }

        /// <summary>
        /// Flag that tells this activity can run.
        /// </summary>
        public bool enabled { get { return m_enabled; } set { if(m_enabled!=value) { m_enabled=value; if(m_enabled) OnEnable(); else OnDisable(); } } }
        private bool m_enabled;

        /// <summary>
        /// Handler for when this activity is enabled.
        /// </summary>
        virtual protected void OnEnable() { }

        /// <summary>
        /// Handler for when this activity is disabled.
        /// </summary>
        virtual protected void OnDisable() { }

        #region Events

        /// <summary>
        /// Callback for completion.
        /// </summary>
        public Action<Activity> OnCompleteEvent {
            get { return (Action<Activity>)m_on_complete_event; }
            set { m_on_complete_event = value;                  }
        }
        protected Delegate m_on_complete_event;

        /// <summary>
        /// Callback for execution.
        /// </summary>
        public Predicate<Activity> OnExecuteEvent {
            get { return (Predicate<Activity>)m_on_execute_event; }
            set { m_on_execute_event = value;                  }
        }
        protected Delegate m_on_execute_event;

        /// <summary>
        /// Auxiliary class to method invoke.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="p_event"></param>
        /// <param name="p_arg"></param>
        /// <returns></returns>
        virtual internal bool InvokeEvent(Delegate p_event,Activity p_arg,bool p_default=false) {
            bool res = p_default;
            if(p_event==null) return res;
            if(p_event is Action<Activity>)    { Action<Activity>    cb = (Action<Activity>)p_event;          cb(this); } else
            if(p_event is Predicate<Activity>) { Predicate<Activity> cb = (Predicate<Activity>)p_event; res = cb(this); }
            return res;
        }

        #endregion

        /// <summary>
        /// Helper task to use await.
        /// </summary>
        internal Task m_task;
        internal int  m_yield_ms;
        internal CancellationTokenSource m_task_cancel;
        
        #region CTOR

        /// <summary>
        /// Creates a new Activity.
        /// </summary>
        /// <param name="p_id">Activity id for searching</param>
        /// <param name="p_context">Execution Context</param>
        public Activity(string p_id,ActivityContext p_context) { InitActivity(p_id,p_context); }
        
        /// <summary>
        /// Creates a new activity, default to 'Update' context.
        /// </summary>
        /// <param name="p_id">Activity id for searching</param>
        public Activity(string p_id) { InitActivity(p_id, ActivityContext.Update); }

        /// <summary>
        /// Creates a new activity, default to 'Update' context and auto generated id.
        /// </summary>
        public Activity() { InitActivity("", ActivityContext.Update); }

        /// <summary>
        /// Internal build the activity
        /// </summary>
        /// <param name="p_id"></param>
        /// <param name="p_context"></param>
        internal void InitActivity(string p_id,ActivityContext p_context) {
            state       = ActivityState.Idle;
            context     = p_context;
            enabled     = true;
            id = p_id;
            if(!string.IsNullOrEmpty(id)) { return; }
            if(m_type_name_lut == null) m_type_name_lut = new Dictionary<Type, string>();
            if(m_id_sb==null)           m_id_sb         = new StringBuilder();
            m_id_sb.Clear();
            Type t = GetType();
            string tn = "";
            if(m_type_name_lut.ContainsKey(t)) {
                tn=m_type_name_lut[t];
            }
            else { 
                Type[] gtl = t.GetGenericArguments();
                tn = t.Name.ToLower().Replace("`","");
                if(gtl.Length>0) { tn=tn.Replace("1",""); tn+=$"<{gtl[0].Name.ToLower()}>"; }
                m_type_name_lut[t]=tn; 
            }
            if(!string.IsNullOrEmpty(tn)) { m_id_sb.Append(tn); m_id_sb.Append("-"); }
            uint hc = (uint)GetHashCode();
            //GC Free ToHex
            while(hc>0) { m_id_sb.Append(m_id_hex_lut[(int)(hc&0xf)]); hc = hc>>4; }            
            id = m_id_sb.ToString();                        
        }
        static private Dictionary<Type,string> m_type_name_lut;
        static private StringBuilder           m_id_sb;
        static private string                  m_id_hex_lut = "0123456789abcdef";
        
        #endregion

        /// <summary>
        /// Adds this activity to the queue.
        /// </summary>
        public void Start() {
            //If activity properly not running reset to idle
            if(state == ActivityState.Complete) state = ActivityState.Idle;
            if(state == ActivityState.Stopped)  state = ActivityState.Idle;
            //If idle init task
            if(state == ActivityState.Idle)
            if(m_task==null) {
                m_task_cancel = new CancellationTokenSource();
                m_task        = new Task(OnTaskCompleteDummy,m_task_cancel.Token);                
                m_yield_ms    = 0;
            }            
            //Add to execution queue
            //if(manager)manager.handler.AddActivity(this);            
            if(manager)manager.Add(this);            
        }
        private void OnTaskCompleteDummy() {    
            //Sleep is inside task thread, so safe to use
            if(m_yield_ms>0) System.Threading.Thread.Sleep(m_yield_ms);
            //Clear up
            m_task        = null;
            m_task_cancel = null;
        }
        
        /// <summary>
        /// Removes this activity from the execution pool.
        /// </summary>
        //public void Stop() { if(manager)manager.handler.RemoveActivity(this); }
        public void Stop() { if(manager)manager.Remove(this); }

        /// <summary>
        /// Executes one loop step.
        /// </summary>
        virtual internal void Execute() {            
            switch(state) {
                case ActivityState.Stopped:  return;
                case ActivityState.Complete: return;
                case ActivityState.Idle:     return;
                case ActivityState.Queued:  { 
                    if(!CanStart())         break;
                    if(!CanStartInternal()) break;
                    OnStart();                    
                    state=ActivityState.Running; 
                }
                break;
            }
            switch(state) {
                case ActivityState.Running:  {
                    //Internal execution
                    bool v0 = OnExecuteInternal();
                    //Repeat until completion (always completed for regular activity)
                    if(v0)  break;
                    //Execute main activity handler
                    bool v1 = OnExecute();
                    //Default delegate always completed if undefined
                    bool v2 = InvokeEvent(m_on_execute_event,this,false);
                    //Early break if either execution wants to continue
                    if(v1) break;
                    if(v2) break;
                    //Extra validate completion
                    bool v3 = CanCompleteInternal();
                    //If can't complete keep going
                    if(!v3) break;
                    //Mark complete
                    state = ActivityState.Complete;
                    //Call internal handler
                    OnComplete();
                    //Call delegate
                    InvokeEvent(m_on_complete_event,this);
                    //Start 'await' Task
                    if(m_task!=null) m_task.Start();                    
                }
                break;
            }
        }

        #region Virtuals

        /// <summary>
        /// Handler for when this activity was just added.
        /// </summary>
        virtual protected void OnAdded() { }

        /// <summary>
        /// Handler for activity execution start
        /// </summary>
        virtual protected void OnStart() { }

        /// <summary>
        /// Handler for activity execution loop steps.
        /// </summary>
        /// <returns>Flag telling the execution loop must continue or not</returns>
        virtual protected bool OnExecute() { return false; }

        /// <summary>
        /// Handler for activity completion.
        /// </summary>
        virtual protected void OnComplete() { }

        /// <summary>
        /// Handler for the activity stop.
        /// </summary>
        virtual protected void OnStop() { }

        /// <summary>
        /// Auxiliary method to validate if starting is allowed.
        /// </summary>
        /// <returns>Flag telling the activity can start and execute its loop, otherwise will keep looping here and 'Queued'</returns>
        virtual protected bool CanStart() { return true; }

        #endregion

        #region Internal Extensions

        /// <summary>
        /// Helper method to handle unity jobs.
        /// </summary>
        /// <returns></returns>
        virtual internal bool OnExecuteInternal() { return false; }

        /// <summary>
        /// Helper to allow the activity to start or not.
        /// </summary>
        /// <returns></returns>
        virtual internal bool CanStartInternal()  { return true; }

        /// <summary>
        /// Helper to allow the activity to complete or not.
        /// </summary>
        /// <returns></returns>
        virtual internal bool CanCompleteInternal()  { return true; }

        /// <summary>
        /// Handler for when the activity was officially removed from Activity Manager.
        /// </summary>
        virtual internal void OnManagerRemoveInternal() { 
            //Only run if not completed
            if(state == ActivityState.Complete) return;
            state = ActivityState.Stopped;
            if(m_task==null) return;
            m_task_cancel.Cancel();
            m_task        = null;
            m_task_cancel = null;
            OnStop();
        } 
        
        /// <summary>
        /// Handler for when this node is added in the manager.
        /// </summary>
        virtual internal void OnManagerAddInternal() { 
            OnAdded(); 
        }

        #endregion

        #region Async/Await

        /// <summary>
        /// Yields this activity until completion and wait delay seconds before continuying.
        /// </summary>
        /// <param name="p_delay">Extra delay seconds after completion</param>
        /// <returns>Task to be waited</returns>
        public Task Yield(float p_delay=0f) { m_yield_ms = (int)(p_delay*1000f); return m_task; }

        /// <summary>
        /// Reference to the awaiter.
        /// </summary>
        /// <returns>Current awaiter for 'await' operator.</returns>
        public TaskAwaiter GetAwaiter() { return m_task==null ? new TaskAwaiter() : m_task.GetAwaiter(); }

        /// <summary>
        /// INotification implement.
        /// </summary>
        /// <param name="completed">Continue callback.</param>
        public void OnCompleted(System.Action completed) { completed(); }

        #endregion

        #region IStatusProvider

        /// <summary>
        /// Returns this activity execution state converted to status flags.
        /// </summary>
        /// <returns>Current activity status</returns>
        virtual public StatusType GetStatus() {
            switch(state) {
                case ActivityState.Idle:
                case ActivityState.Queued:   return StatusType.Idle;
                case ActivityState.Running:  return StatusType.Running;
                case ActivityState.Complete: return StatusType.Success;
                case ActivityState.Stopped:  return StatusType.Cancelled;
            }
            return StatusType.Invalid;
        }

        #endregion

        #region IProgressProvider

        /// <summary>
        /// Returns this activity progress status
        /// </summary>
        /// <returns>Activity progress, eihter 0.0 = not running, 0.5f = running and 1.0 = complete/stopped.</returns>
        virtual public float GetProgress() {
            switch(state) {
                case ActivityState.Idle:  
                case ActivityState.Queued:   return 0f;
                case ActivityState.Running:  return 0.5f;
                case ActivityState.Complete: return 1f;
                case ActivityState.Stopped:  return 1f;
            }
            return 0f;
        }

        #endregion

    }

    #region class Activity<T>

    /// <summary>
    /// Activity class extension to support unity jobs creation in some methods.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Activity<T> : Activity where T : struct {

        #region static

        /// <summary>
        /// Workaround for unity's annoying warning to force complete jobs when they exceed this limit.
        /// </summary>
        //static public int jobFrameLimit = 4;

        #region CRUD

        /// <summary>
        /// Auxiliary activity creation
        /// </summary>
        /// <param name="p_id"></param>
        /// <param name="p_on_execute"></param>
        /// <param name="p_on_complete"></param>
        /// <param name="p_context"></param>
        /// <returns></returns>
        static internal Activity CreateJobActivity(string p_id,System.Predicate<Activity> p_on_execute,System.Action<Activity> p_on_complete,bool p_async) {            
            Activity<T> a = new Activity<T>(p_id,p_async);
            a.OnCompleteEvent = p_on_complete;
            a.OnExecuteEvent  = p_on_execute;
            return a;                        
        }

        #endregion

        #region Run / Loop

        /// <summary>
        /// Creates and starts an Activity that executes the desired Unity job in a loop.
        /// </summary>
        /// <typeparam name="T">Type derived from unity's job interfaces</typeparam>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>        
        /// <param name="p_async">Flag that tells if the job will run async (Schedule) or sync (Run)</param>
        /// <returns>The running activity</returns>
        static public Activity<T> Run(string p_id,System.Predicate<Activity> p_callback,bool p_async=true) { Activity a = CreateJobActivity(p_id,p_callback,null,p_async); a.Start(); return (Activity<T>)a; }

        /// <summary>
        /// Creates and starts an Activity that executes the desired Unity job in a loop.
        /// </summary>        
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>        
        /// <param name="p_async">Flag that tells if the job will run async (Schedule) or sync (Run)</param>
        /// <returns></returns>
        static public Activity<T> Run(System.Predicate<Activity> p_callback,bool p_async=true) { Activity a = CreateJobActivity("",p_callback,null,p_async); a.Start(); return (Activity<T>)a; }

        #endregion

        #region Run / Once

        /// <summary>
        /// Creates and start a single execution activity performing the specified unity job.
        /// </summary>        
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <param name="p_async">Flag that tells if the job will run async (Schedule) or sync (Run)</param>
        /// <returns>The running activity</returns>
        static public Activity<T> Run(string p_id,System.Action<Activity> p_callback=null,bool p_async=true) { Activity a = CreateJobActivity(p_id,null,p_callback,p_async); a.Start(); return (Activity<T>)a; }

        /// <summary>
        /// Creates and start a single execution activity performing the specified unity job.
        /// </summary>        
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <param name="p_async">Flag that tells if the job will run async (Schedule) or sync (Run)</param>
        /// <returns>The running activity</returns>
        static public Activity<T> Run(System.Action<Activity> p_callback=null,bool p_async=true) { Activity a = CreateJobActivity("",null,p_callback,p_async); a.Start(); return (Activity<T>)a; }

        #endregion

        #endregion

        #region CTOR

        /// <summary>
        /// Creates a new activity choosing between running the job sync or async (Run or Schedule)
        /// </summary>
        /// <param name="p_id"></param>
        /// <param name="p_async"></param>
        public Activity(string p_id,bool p_async) { InitActivity(p_id,p_async); }

        /// <summary>
        /// Creates a new activity choosing between running the job sync or async (Run or Schedule)
        /// </summary>
        /// <param name="p_id"></param>
        public Activity(string p_id) { InitActivity(p_id,true); }

        /// <summary>
        /// Creates a new activity choosing between running the job sync or async (Run or Schedule)
        /// </summary>
        public Activity() { InitActivity("",true); }

        #region InitActivity

        /// <summary>
        /// Internal build the activity for jobs.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="p_id"></param>
        internal void InitActivity(string p_id,bool p_async) {
            //Init base activity
            InitActivity(p_id,p_async ? ActivityContext.JobAsync : ActivityContext.Job);            
            //Init flags
            m_has_job          = false;
            m_has_job_parallel = false;
            //Check if it allows parallel for
            Type[] itf_l = typeof(T).GetInterfaces();
            for(int i = 0; i<itf_l.Length; i++) {
                string tn = itf_l[i].Name;
                if(tn.Contains("IJobParallelFor")) { m_has_job=true; m_has_job_parallel=true; }
                if(tn.Contains("IJob"))            { m_has_job=true; }
            }

            if(!m_has_job) {
                Debug.LogWarning($"Activity.{typeof(T).Name}> Type 'T' does not implement no 'IJob' related interface. UnityJobs will not work.");
            }

            //Create job instance
            job = m_has_job ? new T() : default(T);
            //Init flags            
            m_is_scheduled = false;
            m_is_scheduled = false;
            //If parallel length and step <=0 execute as regular job
            m_job_parallel_length = 0;
            m_job_parallel_step   = 0;
            //Some caching
            if(m_jb_ext_type   == null) m_jb_ext_type   = typeof(IJobExtensions);
            if(m_jbpf_ext_type == null) m_jbpf_ext_type = typeof(IJobParallelForExtensions);
            if(m_mkg_args      == null) m_mkg_args      = new Type[1];
            //Fetch reflection static methods converted to the desired type
            if(m_jb_run==null)      m_jb_run        = GetMethod(m_jb_ext_type,"Run",     ref m_jb_run_base);
            if(m_jb_schedule==null) m_jb_schedule   = GetMethod(m_jb_ext_type,"Schedule",ref m_jb_schedule_base);
            //Create arguments containers for each method signatures
            if(m_args1==null)       m_args1 = new object[1];
            if(m_args2==null)       m_args2 = new object[2];            
            //Skip if no parallel job
            if(!m_has_job_parallel) return;
            //Fetch reflection static methods converted to the desired type
            if(m_jbpf_run==null)        m_jbpf_run      = GetMethod(m_jbpf_ext_type,"Run",     ref m_jbpf_run_base);
            if(m_jbpf_schedule==null)   m_jbpf_schedule = GetMethod(m_jbpf_ext_type,"Schedule",ref m_jbpf_schedule_base);
            //Create arguments containers for each method signatures
            if(m_args4==null)           m_args4 = new object[4];            
        }
        static Type m_jb_ext_type;
        static Type m_jbpf_ext_type;
        static Type[] m_mkg_args;
        static MethodInfo m_jb_run_base;
        static MethodInfo m_jb_schedule_base;
        static MethodInfo m_jbpf_run_base;
        static MethodInfo m_jbpf_schedule_base;
        MethodInfo m_jb_run;
        MethodInfo m_jb_schedule;
        MethodInfo m_jbpf_run;
        MethodInfo m_jbpf_schedule;
        object[] m_args1;
        object[] m_args2;        
        object[] m_args4;

        /// <summary>
        /// Helper to extract and convert the job run/schedule methods and work by reflection
        /// </summary>
        /// <param name="p_type"></param>
        /// <param name="p_name"></param>
        /// <returns></returns>
        internal static MethodInfo GetMethod(Type p_type,string p_name,ref MethodInfo p_cache) {            
            //Assign cache
            MethodInfo res = p_cache;            
            if(res==null) { 
                //If no cache get methods and search
                MethodInfo[] l = p_type.GetMethods();
                for(int i=0;i<l.Length;i++) if(l[i].Name == p_name) { res = l[i]; break; }
            }            
            //If still null skip
            if(res==null) return res;
            //Assign cache
            if(p_cache==null) p_cache = res;
            //Create the generic method
            m_mkg_args[0] = typeof(T);
            return res.MakeGenericMethod(m_mkg_args);
        }

        #endregion

        #endregion

        /// <summary>
        /// Overrides the base class allowing to switch between job and jobsync
        /// </summary>
        new public ActivityContext context {
            get { return base.context; }
            set {
                if(value != ActivityContext.Job) if(value != ActivityContext.JobAsync) { Debug.LogWarning($"Activity.{typeof(T).Name}> Can't choose contexts different than Job/JobAsync, will ignore."); return; }
                if(m_is_scheduled) { handle.Complete(); m_is_scheduled = false; }
                base.context = value;
            }
        }

        /// <summary>
        /// Reference to the job
        /// </summary>
        public T job;

        /// <summary>
        /// Job handle is any.
        /// </summary>
        public JobHandle handle;
        
        /// <summary>
        /// Flag that tells if the job handle is valid.
        /// </summary>
        public bool scheduled { get { return m_is_scheduled; } }
        internal bool m_is_scheduled;

        /// <summary>
        /// Helper to use with unity jobs
        /// </summary>                
        internal bool m_has_job;
        internal bool m_has_job_parallel;
        //internal int  m_job_frame_limit;
        
        /// <summary>
        /// Set the desired loop execution parameters of the parallel job. If both params are 0 the job will execute as a regular IJob
        /// </summary>
        /// <param name="p_length"></param>
        /// <param name="p_steps"></param>
        public void SetJobForLoop(int p_length=0,int p_steps=0) {
            m_job_parallel_length = p_length;
            m_job_parallel_step   = p_steps;
        }
        internal int             m_job_parallel_length;
        internal int             m_job_parallel_step;

        /// <summary>
        /// Handles when the job leaves the execution pool.
        /// </summary>
        internal override void OnManagerRemoveInternal() {
            switch(state) {
                //No need to run because 'complete' already did it
                case ActivityState.Complete: break;
                default: {
                    //Ensure completion and clears handle
                    if(m_is_scheduled) { handle.Complete(); m_is_scheduled=false; }
                    //Calls the job component complete callback
                    if(job is IJobComponent) { IJobComponent itf = (IJobComponent)job; itf.OnDestroy(); job = (T)itf; }
                }
                break;
            }
            //Execute the rest of the stopping and signal extension's 'OnStop'
            base.OnManagerRemoveInternal();
        }

        /// <summary>
        /// Overrides to handle unity's job execution
        /// </summary>
        /// <returns></returns>
        internal override bool OnExecuteInternal() {
            //If no job instance return 'complete'
            if(!m_has_job) return false;
            //Invalid contexts return 'complete'
            if(context != ActivityContext.Job) 
            if(context != ActivityContext.JobAsync) return false;
            //If type is parallel and length/steps are set
            bool is_parallel = m_has_job_parallel ? (m_job_parallel_length>=0 && m_job_parallel_step>=0) : false;                                    
            //Init the local variables
            //If parallel use IJobParallelFor extensions otherwise IJobExtensions
            //Job      == Run
            //JobAsync == Schedule
            object[]   job_fn_args = null;
            MethodInfo job_fn      = null;
            switch(context) {                    
                case ActivityContext.Job:           { job_fn = is_parallel ? m_jbpf_run      : m_jb_run;       job_fn_args = is_parallel ? m_args2 : m_args1; } break; 
                case ActivityContext.JobAsync:      { job_fn = is_parallel ? m_jbpf_schedule : m_jb_schedule;  job_fn_args = is_parallel ? m_args4 : m_args2; } break;
            }            
            //If invalids return 'completed'
            if(job_fn_args == null) return false;
            if(job_fn      == null) return false;
            //Flag that tells Run/Schedule should be called.
            bool will_invoke = false;
            //Prepare arguments for Run/Schedule based on context
            switch(context) {
                //IJobParallelForExtensions.Run(job,length)
                //IJobExtensions.Run(job)
                case ActivityContext.Job: {                    
                    //Assign parameters
                    if(is_parallel) {
                        job_fn_args[1] = m_job_parallel_length;
                    }                    
                    //Reinforce not-scheduled
                    m_is_scheduled = false; 
                    //Flag to invoke
                    will_invoke = true;
                }
                break;
                //IJobParallelForExtensions.Schedule(job,length,steps,depend_handle)                    
                //IJobExtensions.Schedule(job,depend_handle)
                case ActivityContext.JobAsync: {
                    //If something is scheduled already, skip
                    if(m_is_scheduled) break; 
                    //Assign parameters
                    if(is_parallel) {
                        job_fn_args[1] = m_job_parallel_length;
                        job_fn_args[2] = m_job_parallel_step; 
                        job_fn_args[3] = default(JobHandle);
                    }
                    else {
                        job_fn_args[1] = default(JobHandle);
                    }
                    //Flag to invoke
                    will_invoke = true;
                }
                break;
            }
            //If invoke is needed, update the job and invoke the method
            if(will_invoke) {
                if(job is IJobComponent) { IJobComponent itf = (IJobComponent)job; itf.OnInit(); job = (T)itf; }
                //Set the most up-to-date struct
                job_fn_args[0] = job;
                //Invoke the method
                object invoke_res = job_fn.Invoke(null,job_fn_args);
                //If async mark scheduled as true and store the handle
                if(context == ActivityContext.JobAsync) {
                    m_is_scheduled = true;
                    handle = (JobHandle)invoke_res;
                }                
            }
            //If has handle use 'IsCompleted' otherwise its sync run and should be finished now
            bool is_completed = m_is_scheduled ? handle.IsCompleted : true;            
            /*
            //Decreases the frame counter to prevent temp_alloc warnings
            if(m_has_job_handle) {
                m_job_frame_limit--;
                is_completed = m_job_frame_limit<=4 ? true : is_completed;
            }
            //*/
            //Continue if not completed
            if(!is_completed) return true;
            //Ensure completion and clears handle
            if(m_is_scheduled) { handle.Complete(); m_is_scheduled=false; }
            //Calls the job component complete callback
            if(job is IJobComponent) { IJobComponent itf = (IJobComponent)job; itf.OnComplete(); job = (T)itf; }
            //Return 'completed'
            return false;
        }

    }

    #endregion

}
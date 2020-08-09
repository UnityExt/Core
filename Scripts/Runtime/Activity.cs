using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityExt.Core {

    #region Interfaces

    /// <summary>
    /// Interfaces for objects that wants to perform update loops.
    /// </summary>
    public interface IUpdateable { 
        /// <summary>
        /// Runs inside Monobehaviour.Update
        /// </summary>
        void OnUpdate(); 
    }
    
    /// <summary>
    /// Interfaces for objects that wants to perform late update loops.
    /// </summary>
    public interface ILateUpdateable { 
        /// <summary>
        /// Runs inside Monobehaviour.LateUpdate
        /// </summary>
        void OnLateUpdate(); 
    }

    /// <summary>
    /// Interfaces for objects that wants to perform fixed update loops.
    /// </summary>
    public interface IFixedUpdateable { 
        /// <summary>
        /// Runs inside Monobehaviour.FixedUpdate
        /// </summary>
        void OnFixedUpdate(); 
    }
    
    /// <summary>
    /// Interfaces for objects that wants to perform update loops not bound by frames
    /// </summary>
    public interface IAsyncUpdateable { 
        /// <summary>
        /// Runs inside Monobehaviour.Update and only during the 'async-slice' duration per frame, so it can skip a few frames depending on the execution load.
        /// </summary>
        void OnAsyncUpdate(); 
    }

    /// <summary>
    /// Interfaces for objects that wants to perform update loops inside a thread
    /// </summary>
    public interface IThreadUpdateable { 
        /// <summary>
        /// Runs inside a thread
        /// </summary>
        void OnThreadUpdate(); 
    }

    #endregion

    /// <summary>
    /// Class that implements any async activity/processing.
    /// It can run in different contexts inside unity (mainthread) or separate thread, offering an abstraction layer Monobehaviour/Thread agnostic.
    /// </summary>
    public class Activity : INotifyCompletion  {

        #region enum Context

        /// <summary>
        /// Enumeration that describes the execution context of the activity.
        /// </summary>
        public enum Context {
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
            Thread
        }

        #endregion

        #region enum State

        /// <summary>
        /// Enumeration that describes the execution state of the activity.
        /// </summary>
        public enum State {
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
            Complete
        }

        #endregion

        #region class Manager

        #if UNITY_EDITOR
        [MenuItem("UnityExt/Debug/Activity/Test Activity")]
        static protected void TestActivity() {
            float t = (float)UnityEditor.EditorApplication.timeSinceStartup;
            Activity.Run(delegate(Activity a) {
                float e = (float)UnityEditor.EditorApplication.timeSinceStartup - t;
                Debug.Log(e);
                if(e>15f) return false;
                return true;
            });
        }
        #endif

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
            protected void Awake()       { handler.Awake();       }
            internal void Start()        { handler.Start();       }
            internal void Update()       { handler.Update();      }            
            internal void FixedUpdate()  { handler.FixedUpdate(); }            
            internal void LateUpdate()   { handler.LateUpdate();  }
            internal void OnDestroy()    { if(m_handler!=null) { m_handler.Clear(); m_handler=null; }  }

            #if UNITY_EDITOR
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

        /// <summary>
        /// Execution time slice for async nodes.
        /// </summary>
        static public int asyncTimeSlice = 4;

        /// <summary>
        /// Maximum created threads for paralell nodes.
        /// </summary>
        static public int maxThreadCount = 3;

        #endregion

        #region static

        #region CRUD

        /// <summary>
        /// Adds activity for exection.
        /// </summary>
        /// <param name="p_node">Execution node. Must implement one or more Activity related interfaces.</param>
        static public void Add(object p_node) { if(manager)manager.handler.AddInterface(p_node); }

        /// <summary>
        /// Removes the activity from exection.
        /// </summary>
        /// <param name="p_node">Execution node. Must implement one or more Activity related interfaces.</param>
        static public void Remove(object p_node) { if(m_manager)m_manager.handler.RemoveInterface(p_node); }

        /// <summary>
        /// Removes the activity from exection.
        /// </summary>
        static public void Clear() { if(m_manager)m_manager.handler.Clear(); }

        /// <summary>
        /// Searches for a single activity by id and context.
        /// </summary>
        /// <param name="p_id">Activity id to search.</param>
        /// <param name="p_context">Specific context to be searched.</param>
        /// <returns>Activity found or null</returns>
        static public Activity Find(string p_id,Context p_context) { if(manager) return manager.handler.Find(p_id,p_context); return null; }

        /// <summary>
        /// Searches for a single activity by id in all contexts.
        /// </summary>
        /// <param name="p_id">Activity id to search.</param>        
        /// <returns>Activity found or null</returns>
        static public Activity Find(string p_id) { return Find(p_id,(Context)(-1)); }

        /// <summary>
        /// Searches for all activities matching the id and context.
        /// </summary>
        /// <param name="p_id">Activity id to search.</param>
        /// <param name="p_context">Specific context to be searched.</param>
        /// <returns>List of results or an empty list.</returns>
        static public List<Activity> FindAll(string p_id,Context p_context) { List<Activity> res=null; if(manager) res = manager.handler.FindAll(p_id,p_context); return res==null ? new List<Activity>() : res; }

        /// <summary>
        /// Searches for all activities matching the id in all contexts.
        /// </summary>
        /// <param name="p_id">Activity id to search.</param>        
        /// <returns>List of results or an empty list.</returns>
        static public List<Activity> FindAll(string p_id) { return FindAll(p_id,(Context)(-1)); }

        #endregion

        #region Run / Loop

        /// <summary>
        /// Creates and starts an activity for constant loop execution.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>
        /// <param name="p_context">Execution context to run.</param>
        /// <returns></returns>
        static public Activity Run(string p_id,System.Predicate<Activity> p_callback,Context p_context) {
            Activity a = new Activity(p_id,p_context);
            a.OnCompleteEvent = null;
            a.OnExecuteEvent  = p_callback;
            a.Start();
            return a;
        }

        /// <summary>
        /// Creates and starts an activity for constant loop execution.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>
        /// <param name="p_context">Execution context to run.</param>
        /// <returns></returns>
        static public Activity Run(System.Predicate<Activity> p_callback,Context p_context) { return Run("",p_callback,p_context); }

        /// <summary>
        /// Creates and starts an activity for constant loop execution.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>        
        /// <returns></returns>
        static public Activity Run(string p_id,System.Predicate<Activity> p_callback) { return Run(p_id,p_callback,Context.Update); }

        /// <summary>
        /// Creates and starts an activity for constant loop execution.
        /// </summary>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>        
        /// <returns></returns>
        static public Activity Run(System.Predicate<Activity> p_callback) { return Run("",p_callback,Context.Update); }

        #endregion

        #region Run / Once

        /// <summary>
        /// Creates and start a single execution activity.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <param name="p_context">Execution context.</param>
        /// <returns></returns>
        static public Activity Run(string p_id,System.Action<Activity> p_callback,Context p_context) {
            Activity a = new Activity(p_id,p_context);
            a.OnCompleteEvent = p_callback;
            a.OnExecuteEvent  = null;
            a.Start();
            return a;
        }

        /// <summary>
        /// Creates and start a single execution activity.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <returns></returns>
        static public Activity Run(string p_id,System.Action<Activity> p_callback) { return Run(p_id,p_callback,Context.Update); }

        /// <summary>
        /// Creates and start a single execution activity.
        /// </summary>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <param name="p_context">Execution context.</param>
        /// <returns></returns>
        static public Activity Run(System.Action<Activity> p_callback,Context p_context) { return Run("",p_callback,p_context); }

        /// <summary>
        /// Creates and start a single execution activity.
        /// </summary>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <returns></returns>
        static public Activity Run(System.Action<Activity> p_callback) { return Run("",p_callback,Context.Update); }

        #endregion

        #endregion

        /// <summary>
        /// Id of this Activity.
        /// </summary>
        public string id;

        /// <summary>
        /// Execution state
        /// </summary>
        public State state { get; internal set; }        
        
        /// <summary>
        /// Execution context.
        /// </summary>
        public Context context { get; internal set; }

        /// <summary>
        /// Has the activity finished.
        /// </summary>
        public bool completed { get { return state == State.Complete; } }

        /// <summary>
        /// Callback for completion.
        /// </summary>
        public System.Action<Activity> OnCompleteEvent;

        /// <summary>
        /// Callback for execution.
        /// </summary>
        public System.Predicate<Activity> OnExecuteEvent;

        /// <summary>
        /// Helper task to use await.
        /// </summary>
        internal Task m_task;
        internal int  m_yield_ms;
        internal CancellationTokenSource m_task_cancel;
        
        /// <summary>
        /// Creates a new Activity.
        /// </summary>
        /// <param name="p_id">Activity id for searching</param>
        /// <param name="p_context">Execution Context</param>
        public Activity(string p_id,Context p_context) {                        
            state       = State.Idle;
            context     = p_context;
            string tn   = string.IsNullOrEmpty(m_type_name) ? (m_type_name = GetType().Name.ToLower()) : m_type_name;
            id          = string.IsNullOrEmpty(p_id) ? tn+"-"+GetHashCode().ToString("x6") : p_id;                             
        }
        private string m_type_name;
        private void OnTaskCompleteDummy() {            
            if(m_yield_ms>0) System.Threading.Thread.Sleep(m_yield_ms);
            m_task        = null;
            m_task_cancel = null;
        }

        /// <summary>
        /// Creates a new activity, default to 'Update' context.
        /// </summary>
        /// <param name="p_id">Activity id for searching</param>
        public Activity(string p_id) : this(p_id,Context.Update) { }

        /// <summary>
        /// Creates a new activity, default to 'Update' context and auto generated id.
        /// </summary>
        public Activity() : this("") { }

        /// <summary>
        /// Adds this activity to the queue.
        /// </summary>
        public void Start() {
            if(m_task==null) {
                m_task_cancel = new CancellationTokenSource();
                m_task = new Task(OnTaskCompleteDummy,m_task_cancel.Token);
            }
            m_yield_ms  = 0;
            if(manager)manager.handler.AddActivity(this);            
        }

        /// <summary>
        /// Removes this activity from the execution pool.
        /// </summary>
        public void Stop() { if(manager)manager.handler.RemoveActivity(this); }

        /// <summary>
        /// Auxiliary method to validate if starting is allowed.
        /// </summary>
        /// <returns></returns>
        virtual protected bool IsReady() { return true; }

        /// <summary>
        /// Handler for when the activity was removed.
        /// </summary>
        internal void OnStop() { 
            if(m_task==null) return;
            m_task_cancel.Cancel();
            m_task=null;
            m_task_cancel=null;
        }

        /// <summary>
        /// Executes one step.
        /// </summary>
        virtual internal void Execute() {
            switch(state) {
                case State.Complete: return;
                case State.Idle:     return;
                case State.Queued:  { 
                    if(!IsReady())   return;
                    OnStart(); 
                    state=State.Running; 
                }
                break;
            }
            switch(state) {
                case State.Running:  { 
                    bool v0 = OnExecute();
                    bool v1 = true;
                    if(OnExecuteEvent!=null) v1 = OnExecuteEvent(this);
                    if(v0 && v1) break;
                    state = State.Complete;
                    OnComplete();
                    if(OnCompleteEvent!=null) OnCompleteEvent(this);
                    if(m_task!=null) m_task.Start();                    
                }
                break;
            }
        }

        #region Virtuals

        /// <summary>
        /// Handler for activity execution start
        /// </summary>
        virtual protected void OnStart() { }

        /// <summary>
        /// Handler for activity execution loop steps.
        /// </summary>
        virtual protected bool OnExecute() { return OnExecuteEvent!=null; }

        /// <summary>
        /// Handler for activity completion.
        /// </summary>
        virtual protected void OnComplete() { }

        #endregion

        #region Async/Await

        /// <summary>
        /// Yields this activity until completion and wait delay seconds before continuying.
        /// </summary>
        /// <param name="p_delay">Extra delay seconds after completion</param>
        /// <returns></returns>
        public Task Yield(float p_delay=0f) {             
            m_yield_ms = (int)(p_delay*1000f);
            return m_task; 
        }

        /// <summary>
        /// Reference to the awaiter.
        /// </summary>
        /// <returns>Current awaiter for 'await' operator.</returns>
        public TaskAwaiter GetAwaiter() { return m_task==null ? new TaskAwaiter() : m_task.GetAwaiter(); }

        /// <summary>
        /// INotification implement.
        /// </summary>
        /// <param name="completed">Coninutation callback.</param>
        public void OnCompleted(System.Action completed) {
            completed();
        }

        #endregion

    }

}
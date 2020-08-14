using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Jobs;
using System.Reflection;

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

    /// <summary>
    /// Interface that helps job instances to be notified before and after execution to perform data management.
    /// </summary>
    public interface IJobComponent {
        /// <summary>
        /// Method called before either Run or Schedule in main thread.
        /// </summary>        
        void OnInit();
        /// <summary>
        /// Method called after either Run or Schedule in main thread.
        /// </summary>        
        void OnComplete();
        /// <summary>
        /// Method called after the activity is complete or stopped and left the execution pool.
        /// </summary>        
        void OnDestroy();
    }

    #endregion

    /// <summary>
    /// Class that implements any async activity/processing.
    /// It can run in different contexts inside unity (mainthread) or separate thread, offering an abstraction layer Monobehaviour/Thread agnostic.
    /// </summary>
    public class Activity : INotifyCompletion {

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
            Thread,
            /// <summary>
            /// Runs the job passed as parameter in a loop and using schedule
            /// </summary>
            JobAsync,
            /// <summary>
            /// Runs the job passed as parameter in a loop and using run
            /// </summary>
            Job
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
            Complete,
            /// <summary>
            /// Stopped
            /// </summary>
            Stopped
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

        #endregion

        #region static

        /// <summary>
        /// Execution time slice for async nodes.
        /// </summary>
        static public int asyncTimeSlice = 4;

        /// <summary>
        /// Maximum created threads for paralell nodes.
        /// </summary>
        static public int maxThreadCount = 4;

        #region CRUD

        #region Create

        /// <summary>
        /// Auxiliary activity creation
        /// </summary>
        /// <param name="p_id"></param>
        /// <param name="p_on_execute"></param>
        /// <param name="p_on_complete"></param>
        /// <param name="p_context"></param>
        /// <returns></returns>
        static internal Activity Create(string p_id,System.Predicate<Activity> p_on_execute,System.Action<Activity> p_on_complete,Context p_context) {
            Activity a = new Activity(p_id,p_context);
            a.OnCompleteEvent = p_on_complete;
            a.OnExecuteEvent  = p_on_execute;
            return a;
        }
        
        #endregion

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

        #region Activity

        /// <summary>
        /// Creates and starts an activity for constant loop execution.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>
        /// <param name="p_context">Execution context to run.</param>
        /// <returns>The running activity</returns>
        static public Activity Run(string p_id,System.Predicate<Activity> p_callback,Context p_context) { Activity a = Create(p_id,p_callback,null,p_context); a.Start(); return a; }
            
        /// <summary>
        /// Creates and starts an activity for constant loop execution.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>
        /// <param name="p_context">Execution context to run.</param>
        /// <returns>The running activity</returns>
        static public Activity Run(System.Predicate<Activity> p_callback,Context p_context) { Activity a = Create("",p_callback,null,p_context); a.Start(); return a; }

        /// <summary>
        /// Creates and starts an activity for constant loop execution.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>        
        /// <returns>The running activity</returns>
        static public Activity Run(string p_id,System.Predicate<Activity> p_callback) { Activity a = Create(p_id,p_callback,null,Context.Update); a.Start(); return a; }

        /// <summary>
        /// Creates and starts an activity for constant loop execution.
        /// </summary>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>        
        /// <returns>The running activity</returns>
        static public Activity Run(System.Predicate<Activity> p_callback) { Activity a = Create("",p_callback,null,Context.Update); a.Start(); return a; }

        #endregion

        #endregion

        #region Run / Once

        #region Activity

        /// <summary>
        /// Creates and start a single execution activity.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <param name="p_context">Execution context.</param>
        /// <returns>The running activity</returns>
        static public Activity Run(string p_id,System.Action<Activity> p_callback,Context p_context) { Activity a = Create(p_id,null,p_callback,p_context); a.Start(); return a; }
            
        /// <summary>
        /// Creates and start a single execution activity.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <returns>The running activity</returns>
        static public Activity Run(string p_id,System.Action<Activity> p_callback) { Activity a = Create(p_id,null,p_callback,Context.Update); a.Start(); return a; }

        /// <summary>
        /// Creates and start a single execution activity.
        /// </summary>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <param name="p_context">Execution context.</param>
        /// <returns>The running activity</returns>
        static public Activity Run(System.Action<Activity> p_callback,Context p_context) { Activity a = Create("",null,p_callback,p_context); a.Start(); return a; }

        /// <summary>
        /// Creates and start a single execution activity.
        /// </summary>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <returns>The running activity</returns>
        static public Activity Run(System.Action<Activity> p_callback) { Activity a = Create("",null,p_callback,Context.Update); a.Start(); return a; }

        #endregion

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
        
        #region CTOR

        /// <summary>
        /// Creates a new Activity.
        /// </summary>
        /// <param name="p_id">Activity id for searching</param>
        /// <param name="p_context">Execution Context</param>
        public Activity(string p_id,Context p_context) { InitActivity(p_id,p_context); }
        
        /// <summary>
        /// Creates a new activity, default to 'Update' context.
        /// </summary>
        /// <param name="p_id">Activity id for searching</param>
        public Activity(string p_id) { InitActivity(p_id, Context.Update); }

        /// <summary>
        /// Creates a new activity, default to 'Update' context and auto generated id.
        /// </summary>
        public Activity() { InitActivity("", Context.Update); }

        /// <summary>
        /// Internal build the activity
        /// </summary>
        /// <param name="p_id"></param>
        /// <param name="p_context"></param>
        internal void InitActivity(string p_id,Context p_context) {
            state       = State.Idle;
            context     = p_context;
            string tn   = string.IsNullOrEmpty(m_type_name) ? (m_type_name = GetType().Name.ToLower()) : m_type_name;
            id          = string.IsNullOrEmpty(p_id) ? tn+"-"+GetHashCode().ToString("x6") : p_id;                             
        }
        private string m_type_name;

        #endregion

        /// <summary>
        /// Adds this activity to the queue.
        /// </summary>
        public void Start() {
            //If activity properly not running reset to idle
            if(state == State.Complete) state = State.Idle;
            if(state == State.Stopped)  state = State.Idle;
            //If idle init task
            if(state == State.Idle)
            if(m_task==null) {
                m_task_cancel = new CancellationTokenSource();
                m_task        = new Task(OnTaskCompleteDummy,m_task_cancel.Token);
                m_yield_ms    = 0;
            }            
            //Add to execution queue
            if(manager)manager.handler.AddActivity(this);
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
        public void Stop() { if(manager)manager.handler.RemoveActivity(this); }

        /// <summary>
        /// Handler for when the activity was removed.
        /// </summary>
        virtual internal void OnStop() { 
            //Only run if not completed
            if(state == State.Complete) return;
            state = State.Stopped;
            if(m_task==null) return;
            m_task_cancel.Cancel();
            m_task        = null;
            m_task_cancel = null;
        }

        /// <summary>
        /// Executes one loop step.
        /// </summary>
        virtual internal void Execute() {
            switch(state) {
                case State.Stopped:  return;
                case State.Complete: return;
                case State.Idle:     return;
                case State.Queued:  { 
                    if(!CanStart())   return;
                    OnStart();                    
                    state=State.Running; 
                }
                break;
            }
            switch(state) {
                case State.Running:  {
                    //Execute the job loop
                    bool v0 = OnExecuteJob();
                    //Repeat until 'completed == true' (always true for regular activity)
                    if(!v0) break;
                    //Execute main activity thread
                    bool v1 = OnExecute();
                    //Default delegate always completed
                    bool v2 = true;
                    //Execute delegate and check result
                    if(OnExecuteEvent!=null) v1 = OnExecuteEvent(this);
                    //Repeat until all steps are done
                    if(v0) if(v1) if(v2) break;
                    //Mark complete
                    state = State.Complete;
                    //Call internal handler
                    OnComplete();
                    //Call delegate
                    if(OnCompleteEvent!=null) OnCompleteEvent(this);
                    //Start 'await' Task
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
        /// <returns>Flag telling the execution loop must continue or not</returns>
        virtual protected bool OnExecute() { return OnExecuteEvent!=null; }

        /// <summary>
        /// Handler for activity completion.
        /// </summary>
        virtual protected void OnComplete() { }

        /// <summary>
        /// Auxiliary method to validate if starting is allowed.
        /// </summary>
        /// <returns>Flag telling the activity can start and execute its loop, otherwise will keep looping here and 'Queued'</returns>
        virtual protected bool CanStart() { return true; }

        /// <summary>
        /// Helper method to handle unity jobs.
        /// </summary>
        /// <returns></returns>
        virtual internal bool OnExecuteJob() { return true; }

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
        static public Activity<T> Run(string p_id,System.Predicate<Activity> p_callback,bool p_async) { Activity a = CreateJobActivity(p_id,p_callback,null,p_async); a.Start(); return (Activity<T>)a; }

        /// <summary>
        /// Creates and starts an Activity that executes the desired Unity job in a loop.
        /// </summary>        
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>        
        /// <param name="p_async">Flag that tells if the job will run async (Schedule) or sync (Run)</param>
        /// <returns></returns>
        static public Activity<T> Run(System.Predicate<Activity> p_callback,bool p_async) { Activity a = CreateJobActivity("",p_callback,null,p_async); a.Start(); return (Activity<T>)a; }

        /// <summary>
        /// Creates and starts an Activity that executes the desired Unity job in a loop.
        /// </summary>        
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>                
        /// <returns>The running activity</returns>
        new static public Activity<T> Run(string p_id,System.Predicate<Activity> p_callback) { Activity a = CreateJobActivity(p_id,p_callback,null,true); a.Start(); return (Activity<T>)a; }

        /// <summary>
        /// Creates and starts an Activity that executes the desired Unity job in a loop.
        /// </summary>        
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>                
        /// <returns>The running activity</returns>
        new static public Activity<T> Run(System.Predicate<Activity> p_callback) { Activity a = CreateJobActivity("",p_callback,null,true); a.Start(); return (Activity<T>)a; }

        #endregion

        #region Run / Once

        /// <summary>
        /// Creates and start a single execution activity performing the specified unity job.
        /// </summary>        
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <param name="p_async">Flag that tells if the job will run async (Schedule) or sync (Run)</param>
        /// <returns>The running activity</returns>
        static public Activity<T> Run(string p_id,System.Action<Activity> p_callback,bool p_async) { Activity a = CreateJobActivity(p_id,null,p_callback,p_async); a.Start(); return (Activity<T>)a; }

        /// <summary>
        /// Creates and start a single execution activity performing the specified unity job.
        /// </summary>        
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the activity completion.</param>                
        /// <returns>The running activity</returns>
        new static public Activity<T> Run(string p_id,System.Action<Activity> p_callback) { Activity a = CreateJobActivity(p_id,null,p_callback,true); a.Start(); return (Activity<T>)a; }

        /// <summary>
        /// Creates and start a single execution activity performing the specified unity job.
        /// </summary>        
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <param name="p_async">Flag that tells if the job will run async (Schedule) or sync (Run)</param>
        /// <returns>The running activity</returns>
        static public Activity<T> Run(System.Action<Activity> p_callback,bool p_async) { Activity a = CreateJobActivity("",null,p_callback,p_async); a.Start(); return (Activity<T>)a; }

        /// <summary>
        /// Creates and start a single execution activity performing the specified unity job.
        /// </summary>        
        /// <param name="p_callback">Callback for handling the activity completion.</param>                
        /// <returns>The running activity</returns>
        new static public Activity<T> Run(System.Action<Activity> p_callback) { Activity a = CreateJobActivity("",null,p_callback,true); a.Start(); return (Activity<T>)a; }

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

        /// <summary>
        /// Internal build the activity for jobs.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="p_id"></param>
        internal void InitActivity(string p_id,bool p_async) {
            //Init base activity
            InitActivity(p_id,p_async ? Context.JobAsync : Context.Job);            
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

        /// <summary>
        /// Overrides the base class allowing to switch between job and jobsync
        /// </summary>
        new public Context context {
            get { return base.context; }
            set {
                if(value != Context.Job) if(value != Context.JobAsync) { Debug.LogWarning($"Activity.{typeof(T).Name}> Can't choose contexts different than Job/JobAsync, will ignore."); return; }
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
        internal override void OnStop() {
            base.OnStop();
            //No need to run because 'complete' already did it
            if(state == State.Complete) return;
            //Ensure completion and clears handle
            if(m_is_scheduled) { handle.Complete(); m_is_scheduled=false; }
            //Calls the job component complete callback
            if(job is IJobComponent) { IJobComponent itf = (IJobComponent)job; itf.OnDestroy(); job = (T)itf; }
        }

        /// <summary>
        /// Overrides to handle unity's job execution
        /// </summary>
        /// <returns></returns>
        internal override bool OnExecuteJob() {
            //If no job instance return 'true == completed'
            if(!m_has_job) return true;
            //Invalid contexts return 'true == completed'
            if(context != Context.Job) 
            if(context != Context.JobAsync) return true;
            //If type is parallel and length/steps are set
            bool is_parallel = m_has_job_parallel ? (m_job_parallel_length>=0 && m_job_parallel_step>=0) : false;                                    
            //Init the local variables
            //If parallel use IJobParallelFor extensions otherwise IJobExtensions
            //Job      == Run
            //JobAsync == Schedule
            object[]   job_fn_args = null;
            MethodInfo job_fn      = null;
            switch(context) {                    
                case Context.Job:           { job_fn = is_parallel ? m_jbpf_run      : m_jb_run;       job_fn_args = is_parallel ? m_args2 : m_args1; } break; 
                case Context.JobAsync:      { job_fn = is_parallel ? m_jbpf_schedule : m_jb_schedule;  job_fn_args = is_parallel ? m_args4 : m_args2; } break;
            }            
            //If invalids return 'completed'
            if(job_fn_args == null) return true;
            if(job_fn      == null) return true;
            //Flag that tells Run/Schedule should be called.
            bool will_invoke = false;
            //Prepare arguments for Run/Schedule based on context
            switch(context) {
                //IJobParallelForExtensions.Run(job,length)
                //IJobExtensions.Run(job)
                case Context.Job: {                    
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
                case Context.JobAsync: {
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
                if(context == Context.JobAsync) {
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
            //Return if not completed
            if(!is_completed) return false;
            //Ensure completion and clears handle
            if(m_is_scheduled) { handle.Complete(); m_is_scheduled=false; }
            //Calls the job component complete callback
            if(job is IJobComponent) { IJobComponent itf = (IJobComponent)job; itf.OnComplete(); job = (T)itf; }
            //Return 'completed'
            return true;
        }

    }

#endregion

}
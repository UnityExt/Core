using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Threading.Tasks;
using UnityEngine;
using System.Threading;
using System;

namespace UnityExt.Core {

    #region class Activity<T>
    /// <summary>
    /// Extension of Activity to handle FSM functionalities. It has a simple API to handle state change detection and looping.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Activity<T> : Activity, IFSMHandler<T> where T : Enum {

        /// <summary>
        /// Returns the current state
        /// </summary>
        public T state { get { return fsm==null ? default(T) : fsm.state; } set { if (fsm != null) fsm.state = value; } }

        /// <summary>
        /// Handler for execution loop
        /// </summary>
        public Action<Activity<T>> OnExecuteEvent;

        /// <summary>
        /// Handler for state changes
        /// </summary>
        public Action<Activity<T>,T,T> OnChangeEvent;

        /// <summary>
        /// Internals
        /// </summary>
        protected FSM<T> fsm;
        
        /// <summary>
        /// CTOR.
        /// </summary>
        /// <param name="p_id"></param>
        public Activity(string p_id = "") : base(p_id) {
            fsm = new FSM<T>(this,default(T));
        }

        /// <summary>
        /// Handler called during execution with the current active state
        /// </summary>
        /// <param name="p_state"></param>
        virtual protected void OnStateUpdate(T p_state) { }

        /// <summary>
        /// Handler called upon state changes, returns true|false to "approve" the change or not.
        /// </summary>
        /// <param name="p_from"></param>
        /// <param name="p_to"></param>        
        virtual protected void OnStateChange(T p_from,T p_to) { }

        /// <summary>
        /// Callled upon state reset.
        /// </summary>
        virtual protected void OnStateReset() { }

        /// <summary>
        /// Auxiliary handler to easily call events of any type variation
        /// </summary>
        /// <param name="p_state"></param>
        virtual protected void InternalExecuteEvent(T p_state) { if (OnExecuteEvent != null) OnExecuteEvent(this); }

        /// <summary>
        /// Auxiliary handler to easily call events of any type variation
        /// </summary>
        /// <param name="p_state"></param>
        virtual protected void InternalChangeEvent(T p_from,T p_to) { if(OnChangeEvent!=null) OnChangeEvent(this,p_from,p_to); }

        /// <summary>
        /// IFSMHandler internals
        /// </summary>        
        void IFSMHandler<T>.OnStateUpdate(FSM<T> p_fsm,T p_state)       { OnStateUpdate(p_state    ); InternalExecuteEvent(p_state    ); }
        void IFSMHandler<T>.OnStateChange(FSM<T> p_fsm,T p_from,T p_to) { OnStateChange(p_from,p_to); InternalChangeEvent (p_from,p_to); }
        void IFSMHandler<T>.OnStateReset (FSM<T> p_fsm)                 { OnStateReset(); }

        /// <summary>
        /// Proxy execute the FSM
        /// </summary>        
        override protected void OnExecute(ProcessContext p_context) { fsm.Update(); }
    }
    #endregion

    #region class Activity
    /// <summary>
    /// Most basic class depicting an activity that is attached to a process.
    /// It contains the most raw access to the execution loops.
    /// Can be paired with the interfaces implmenting different loops such as IUpdateable and IThread.
    /// Can be used for editor context too.
    /// </summary>
    public class Activity : IProcess, IProgressProvider {

        #region static
        /// <summary>
        /// CTOR
        /// </summary>
        static Activity() {
            //Warmup non-thread-safe data
            m_app_persistent_dp = Application.persistentDataPath.Replace("\\","/");
            m_app_platform      = Application.platform.ToString().ToLower();
        }
        static internal string m_app_persistent_dp;
        static internal string m_app_platform;

        /// <summary>
        /// Default Context to start Activities
        /// </summary>
        static public ProcessContext DefaultContext = ProcessContext.None;

        #endregion

        #region State/Variables

        /// <summary>
        /// Activity id string
        /// </summary>
        public string id;

        /// <summary>
        /// Flag that allows this activity to run or not.
        /// </summary>
        public bool enabled;

        /// <summary>
        /// Elapsed Time
        /// </summary>
        public float elapsed { get; protected set; }

        /// <summary>
        /// Current DeltaTime
        /// </summary>
        public float deltaTime { get; protected set; }

        /// <summary>
        /// Flag that tells this activity is completed        
        /// </summary>
        public bool completed { get; protected set; }

        /// <summary>
        /// Flag that tells this activity is threaded.
        /// </summary>
        public bool threaded { get; private set; }

        /// <summary>
        /// Flag that tells this activity is editor bound
        /// </summary>
        public bool editor { get; private set; }
        
        /// <summary>
        /// Flag that tells this activity is or is not affected by timescale
        /// </summary>
        public bool useTimeScale { get { return process == null ? m_use_time_scale : process.useTimeScale; } set { m_use_time_scale = value; if (process != null) process.useTimeScale = value; } }
        private bool m_use_time_scale;

        /// <summary>
        /// Flag that tells this activity is or is not deferred (runs within timeslice)
        /// </summary>
        public bool deferred { get { return process == null ? m_deferred : process.deferred; } set { m_deferred = value;  if(process != null) process.deferred = value; } }
        private bool m_deferred;

        /// <summary>
        /// Get the exception in case of errouneous state
        /// </summary>
        public Exception exception { get; protected set; }

        /// <summary>
        /// Returns a flag in case this activity is in error state
        /// </summary>
        public bool isError { get { return exception != null; } }

        /// <summary>
        /// Flag that ensures execution of multiple contexts to be thread safe
        /// </summary>
        public bool threadSafe;

        /// <summary>
        /// Executing context flag, during start/stop its the combined flags and during execution its the currently active one.
        /// </summary>
        public ProcessContext context { get; protected set; }

        /// <summary>
        /// Internals
        /// </summary>
        private bool m_active;
        private object m_lock_thread = new object();

        #endregion

        #region CTOR
        /// <summary>
        /// CTOR.
        /// </summary>
        /// <param name="p_id"></param>
        public Activity(string p_id="") {
            id           = p_id;
            elapsed      = 0f;
            deltaTime    = 0f;
            threaded     = false;
            editor       = false;
            useTimeScale = false;
            deferred     = false;
            completed    = false;
            enabled      = true;
            context      = ProcessContext.None;
        }
        #endregion

        #region Virtuals

        virtual protected void OnStart() { }

        virtual protected void OnStop() { }

        virtual protected void OnExecute(ProcessContext p_context) { }

        virtual protected void OnError() { }

        #endregion

        #region Operation
        /// <summary>
        /// Starts this activity at the specified context flags
        /// </summary>
        public Activity Start(ProcessContext p_context) {
            if (m_active) return this;
            m_active = true;
            InternalStart(false,p_context); 
            return this;  
        }

        /// <summary>
        /// Starts this activity based by the interfaces implemented.
        /// </summary>
        public Activity Start() {
            if (m_active) return this;
            m_active = true;
            InternalStart(false,DefaultContext); 
            return this;  
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Starts this activity for editor context.
        /// </summary>
        public Activity StartEditor(ProcessContext p_context) {
            if (m_active) return this;
            m_active = true;
            InternalStart(true,p_context); 
            return this; 
        }

        /// <summary>
        /// Starts this activity in editor context based by the interfaces implemented.
        /// </summary>
        public Activity StartEditor() {
            if(m_active) return this; 
            m_active = true;
            InternalStart(false,ProcessContext.None); 
            return this;  
        }
        #endif

        /// <summary>
        /// Stops this activity
        /// </summary>
        public void Stop() {
            if (!m_active) return;
            m_active = false;
            InternalStop(); 
        }

        /// <summary>
        /// Throws an exception, stop execution and raise or not a C# exception
        /// </summary>
        /// <param name="p_error"></param>
        /// <param name="p_raise_event"></param>
        public void Throw(Exception p_error,bool p_raise_event=false) {
            exception = p_error;
            OnError();
            Stop();
            if(p_raise_event) {
                throw p_error;
            }
        }

        #endregion

        #region Async/Await        
        /// <summary>
        /// Yields this activity until completion and wait delay seconds before continuying.
        /// </summary>
        /// <param name="p_delay">Extra delay seconds after completion</param>
        /// <returns>Task to be waited</returns>
        public Task Yield(float p_delay = 0f) {
            return process == null ? Task.Delay((int)(p_delay * 1000f)) : process.Yield(p_delay);            
        }

        /// <summary>
        /// Waits for completion until timeout, then stops this activity.
        /// </summary>
        /// <param name="p_delay"></param>
        /// <returns></returns>
        public Task<bool> Wait(float p_timeout = 0f) {
            return process==null ? Task.FromResult<bool>(false) : process.Wait(p_timeout);
        }

        /// <summary>
        /// Reference to the awaiter.
        /// </summary>
        /// <returns>Current awaiter for 'await' operator.</returns>
        public TaskAwaiter GetAwaiter() { return process == null ? new TaskAwaiter() : process.GetAwaiter(); }

        #endregion

        #region IProgressProvider
        /// <summary>
        /// Returns the execution progress of this activity.
        /// </summary>
        /// <returns></returns>
        virtual public float GetProgress() { return m_progress; }
        private float m_progress;
        #endregion

        #region Internals
        /// <summary>
        /// Reference to the process
        /// </summary>
        private Process process { get { return ((IProcess)this).process; } }
        Process IActivity.process { get; set; }        
        /// <summary>
        /// Activity Start
        /// </summary>        
        virtual protected void InternalStart(bool p_editor,ProcessContext p_context) {            
            threaded  = (Process.Locals.GetContexts(this,p_editor) & ProcessContext.ThreadMask)!=0;
            editor    = p_editor;
            completed = false;            
            ProcessFlags f = ProcessFlags.None;
            if(m_deferred       ) f |= ProcessFlags.Deferred;
            if(m_use_time_scale ) f |= ProcessFlags.TimeScale;

            m_progress  = 0f;
            elapsed     = 0f;
            deltaTime   = 0f;
            exception   = null;

            Process p = null;            
            if (p_editor) {
                #if UNITY_EDITOR
                p = Process.StartEditor(this,p_context,f);
                #endif
            } else {
                p = Process.Start(this,p_context,f);
            }
            p.name = id;
            context = p==null ? ProcessContext.None : p.context;            
        }        
        /// <summary>
        /// Timings/Clocks
        /// </summary>        
        private void InternalTiming(ProcessContext p_context,ProcessState p_state) {
            ProcessContext ctx = p_context;
            switch (p_state) {
                case ProcessState.Start: { elapsed = 0f; deltaTime = 0f; } break;
                case ProcessState.Run: {
                    //If disabled don't update time
                    if (!enabled) { deltaTime = 0f; break; }
                    float dt = process==null ? 0f : process.deltaTime;
                    //Only use 'delta time' when matching threaded/non threaded context
                    bool is_ctx_thread = (ctx & ProcessContext.ThreadMask) != 0;
                    if ( threaded) { if ( is_ctx_thread) { deltaTime = dt; elapsed += deltaTime; } }
                    if (!threaded) { if (!is_ctx_thread) { deltaTime = dt; elapsed += deltaTime; } }
                }
                break;
            }
        }
        /// <summary>
        /// Internal Process Loop Call
        /// </summary>        
        void IProcess.OnProcessUpdate(ProcessContext p_context,ProcessState p_state) { InternalProcessUpdate(p_context,p_state); }
        /// <summary>
        /// Handler for process callbacks
        /// </summary>
        /// <param name="p_context"></param>
        /// <param name="p_state"></param>
        private void InternalProcessUpdate(ProcessContext p_context,ProcessState p_state) {
            ProcessContext ctx = p_context;
            ProcessState s = p_state;            
            //Execute main loop
            switch (s) {
                case ProcessState.Start: {
                    m_progress = 0.0f;
                    elapsed    = 0f;
                    deltaTime  = 0f;
                    context = p_context;
                    OnStart();
                    m_progress = 0.5f;
                }
                break;
                case ProcessState.Stop: {
                    m_progress = 1.0f;
                    completed = true;
                    context = p_context;
                    OnStop();                                        
                    elapsed   = 0f;
                    deltaTime = 0f;
                }
                break;
                case ProcessState.Run: {                                        
                    if (!enabled)  break;
                    if (!m_active) break;
                    if(threadSafe) {
                        lock(m_lock_thread) {
                            //Update Clocks
                            InternalTiming(ctx,s);
                            //Execute logic
                            context = ctx;
                            OnExecute(ctx);
                        }
                    }
                    else {
                        //Update Clocks
                        InternalTiming(ctx,s);
                        //Execute logic
                        context = ctx;
                        OnExecute(ctx);
                    }                    
                }
                break;                
            }
        }
        virtual protected void InternalStop() {            
            if (process != null) process.Dispose();            
        }
        
        #endregion

    }
    #endregion

}
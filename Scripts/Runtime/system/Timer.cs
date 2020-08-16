using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityExt.Core {

    /// <summary>
    /// Class that extends an Activity to give support of time tracking, laps and delayed execution start.
    /// </summary>
    public class Timer : Activity, IProgressProvider {

        #region enum Mode

        /// <summary>
        /// Timer running mode.
        /// </summary>
        public enum Type {
            /// <summary>
            /// Runs using unity's time tracking.
            /// </summary>
            Unity,
            /// <summary>
            /// Runs using a stopwatch class and track time out if the main thread.
            /// </summary>
            System            
        }

        #endregion

        #region static

        /// <summary>
        /// Static CTOR
        /// </summary>
        static Timer() {
            if(m_clock_sys == null) { m_clock_sys = new System.Diagnostics.Stopwatch(); m_clock_sys.Start(); }
        }  
        /// <summary>
        /// Single system time instance
        /// </summary>
        static System.Diagnostics.Stopwatch m_clock_sys;

        /// <summary>
        /// Path to be used on the creation of an atomic timer.
        /// </summary>
        static public string atomicClockPath = Application.persistentDataPath;

        /// <summary>
        /// Returns the folder where the atomic clock references are stored.
        /// </summary>
        static public string atomicClockFolder {
            get {
                if(AssertAtomicPaths()) return m_clock_atomic_root+"ueac/";
                return atomicClockPath;
            }
        }

        #region Atomic Timestamp

        /// <summary>
        /// Asserts the location of the atomic clock reference files. Return true if all good.
        /// </summary>
        /// <returns></returns>
        static internal bool AssertAtomicPaths() {
            //Validate clock file root
            if(string.IsNullOrEmpty(m_clock_atomic_root)) {
                m_clock_atomic_root = atomicClockPath;
                m_clock_atomic_root = m_clock_atomic_root.Replace("\\","/");
                if(!m_clock_atomic_root.EndsWith("/")) m_clock_atomic_root+="/";
                m_clock_atomic_folder = m_clock_atomic_root+"ueac/";
            }
            //Assume all fine
            m_clock_atomic_allowed = true;
            //Assert directory to create and test if IO is allowed
            if(!Directory.Exists(m_clock_atomic_folder)) {
                try {
                    m_clock_atomic_allowed = true;
                    Directory.CreateDirectory(m_clock_atomic_folder);
                }
                catch(System.Exception p_err) {
                    m_clock_atomic_allowed = false;
                    Debug.LogWarning($"Timer> Failed to create atomic clock root at [{m_clock_atomic_folder}]\n{p_err.Message}");
                }
            }
            return m_clock_atomic_allowed;
        }
        static bool   m_clock_atomic_allowed;
        static string m_clock_atomic_root;
        static string m_clock_atomic_folder;

        /// <summary>
        /// Creates a temporary file and sample its current timestamp.
        /// </summary>
        /// <param name="p_id">Atomic Clock Id</param>
        /// <returns>DateTime stamp of the atomic clock</returns>
        static public DateTime GetAtomicTimestamp(string p_id="") {
            //Default file name
            if(string.IsNullOrEmpty(p_id)) p_id = "__system_time";
            bool path_success = AssertAtomicPaths();
            //If not fallback to C# DateTime Now            
            if(!path_success) return DateTime.UtcNow;
            //Update/Create th file and sample the last access datetime
            string   fp = m_clock_atomic_folder+p_id+".clk";            
            FileInfo fi = new FileInfo(fp);
            if(!fi.Exists) {
                File.WriteAllBytes(fp,m_clock_atomic_dummy_data);
            }            
            fi.Refresh();
            return fi.CreationTime;
        }        
        static byte[] m_clock_atomic_dummy_data = new byte[1];

        /// <summary>
        /// Clears the atomic clock of a given id.
        /// </summary>
        /// <param name="p_id">Atomic Clock Id</param>
        static public void ClearAtomicTimestamp(string p_id="") {
            //Default file name
            if(string.IsNullOrEmpty(p_id)) p_id = "__system_time";
            bool path_success = AssertAtomicPaths();
            if(!path_success) return;
            //Update/Create th file and sample the last access datetime
            string   fp = m_clock_atomic_folder+p_id+".clk";            
            FileInfo fi = new FileInfo(fp);
            if(fi.Exists) {     
                File.SetCreationTimeUtc(fp,DateTime.UtcNow);
                fi.Delete();                
                fi.Refresh();                                
            }            
        }

        /// <summary>
        /// Returns the elapsed timespan of an atomic clock, returns Timespan(0) if not created yet.
        /// </summary>
        /// <param name="p_id">Atomic Clock Id</param>
        /// <returns>Elapsed timespan of a given atomic block</returns>
        static public TimeSpan GetAtomicClockElapsed(string p_id="") {
            //Default file name
            if(string.IsNullOrEmpty(p_id)) p_id = "__system_time";
            bool path_success = AssertAtomicPaths();
            if(!path_success) return new TimeSpan(0);
            string tmp_id = "__current_clock_"+(UnityEngine.Random.value*1000f).ToString("0000000");
            DateTime t1 = GetAtomicTimestamp(tmp_id);
            DateTime t0 = GetAtomicTimestamp(p_id);
            ClearAtomicTimestamp(tmp_id);
            return t1-t0;
        }
        
        #endregion

        #endregion

        /// <summary>
        /// Timer clock type.
        /// </summary>
        public Type type { get; internal set; }

        /// <summary>
        /// Number of loop steps to perform, if '0' runs undefinetely.
        /// </summary>
        public int count { get; set; }

        /// <summary>
        /// Current step.
        /// </summary>
        public int step { get; set; }

        /// <summary>
        /// Current elapsed time.
        /// </summary>
        public float elapsed { 
            get {
                switch(state) {
                    case State.Idle:   return 0f;
                    case State.Queued: return 0f;                    
                }
                return m_elapsed;
            }
            set { 
                m_elapsed = value; 
            }
        }
        private float m_elapsed;

        /// <summary>
        /// Duration per execution loop. If '0.0' the timer will not perform any execution step.
        /// </summary>
        public float duration { get; set; }

        /// <summary>
        /// Delay before starting execution the loop.
        /// </summary>
        public float delay { get; set; }

        /// <summary>
        /// Flag that tells if the timer will suffer time scale or not. Defaults to true.
        /// </summary>
        public bool unscaled { get; set; }

        /// <summary>
        /// Flag that tells if the timer will keep updating or not.
        /// </summary>
        public bool paused { get; set; }

        /// <summary>
        /// Returns the execution progress of the timer in the [0,1] range.
        /// </summary>
        public float progress { get { return duration<=0f ? 0f : Mathf.Clamp01(elapsed/duration); } }

        #region Events

        /// <summary>
        /// Event called on each step complete.
        /// </summary>
        public Predicate<Timer> OnStepEvent {
            get { return (Predicate<Timer>)m_on_step_event; }
            set { m_on_step_event = value;                  }                
        }
        protected Delegate m_on_step_event;

        /// <summary>
        /// Event called upon timer completion
        /// </summary>
        new public Action<Timer> OnCompleteEvent {
            get { return (Action<Timer>)m_on_complete_event; }
            set { m_on_complete_event = value;               }
        }

        /// <summary>
        /// Event called upon timer execution step.
        /// </summary>
        new public Predicate<Timer> OnExecuteEvent {
            get { return (Predicate<Timer>)m_on_execute_event; }
            set { m_on_execute_event = value;               }
        }

        /// <summary>
        /// Auxiliary class to method invoke.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="p_event"></param>
        /// <param name="p_arg"></param>
        /// <returns></returns>
        override internal bool InvokeEvent(Delegate p_event,Activity p_arg,bool p_default) {
            bool res = p_default;
            if(p_event==null) return res;
            if(p_event is Action<Timer>)    { Action<Timer>    cb = (Action<Timer>)p_event;          cb(this); } else
            if(p_event is Predicate<Timer>) { Predicate<Timer> cb = (Predicate<Timer>)p_event; res = cb(this); }
            return res;
        }

        #endregion

        /// <summary>
        /// Internals.
        /// </summary>
        private float    m_clock_time;
        private float    m_clock_stamp;
        private float    m_clock_delta_time;

        /// <summary>
        /// Get current clock based on the 
        /// </summary>
        /// <returns>Elapsed time in seconds since the last timestamp.</returns>
        public float GetClock() {
            float t = 0f;  
            float ivts = Time.timeScale<=0f ? 1f : (1f/Time.timeScale);
            switch(type) {                
                case Type.Unity:  { t = Time.time * (unscaled ? ivts : 1f); } break; 
                case Type.System: { double ms = (double)m_clock_sys.ElapsedMilliseconds; t = (float)(ms*0.001); } break;
            }
            #if UNITY_EDITOR
            if(!Application.isPlaying) {
                if(type == Type.Unity) {
                    t = (float)UnityEditor.EditorApplication.timeSinceStartup;
                }
            }
            #endif
            return t - m_clock_stamp;
        }
        
        /// <summary>
        /// Resets the time stamps.
        /// </summary>
        internal void ResetClock() {
            m_clock_stamp      = 0f;
            m_clock_stamp      = GetClock();
            m_clock_time       = 0f;
        }

        #region CTOR

        /// <summary>
        /// Creates a new timer instance.
        /// </summary>
        /// <param name="p_id">Id of the Timer.</param>
        /// <param name="p_delay">Delay in seconds before execution start.</param>
        /// <param name="p_duration">Duration in seconds per execution step.</param>
        /// <param name="p_count">Max number of steps before completion.</param>
        /// <param name="p_mode">Clock time tracking mode.</param>
        /// <param name="p_context">Activity execution context.</param>
        public Timer(string p_id,float p_duration,int p_count=1,Type p_mode = Type.Unity,Context p_context = Context.Update) : base(p_id,p_context) { CreateTimer(p_duration,p_count,p_mode); }
        /// <summary>
        /// Creates a new Timer instance.
        /// </summary>        
        /// <param name="p_delay">Delay in seconds before execution start.</param>
        /// <param name="p_duration">Duration in seconds per execution step.</param>
        /// <param name="p_count">Max number of steps before completion.</param>
        /// <param name="p_mode">Clock time tracking mode.</param>
        /// <param name="p_context">Activity execution context.</param>
        public Timer(float p_duration,int p_count=1,Type p_mode = Type.Unity,Context p_context = Context.Update) : base("",p_context) { CreateTimer(p_duration,p_count,p_mode); }

        #endregion

        #region CRUD

        /// <summary>
        /// Helper to create the time.
        /// </summary>
        /// <param name="p_delay"></param>
        /// <param name="p_duration"></param>
        /// <param name="p_count"></param>
        /// <param name="p_mode"></param>
        internal void CreateTimer(float p_duration,int p_count,Type p_mode) {
            delay    = 0f;
            duration = p_duration;
            count    = p_count;
            type     = p_mode;            
            unscaled = true;
            paused   = false; 
            m_clock_time = 0f;
            m_elapsed    = 0f;
            switch(type) {
                case Type.Unity: {
                    if(context == Context.Thread) {
                        Debug.LogWarning("Timer> Using Unity.Tiem in Thread context will throw an error. Fallback to 'System'");
                        type = Type.System;
                    }                    
                }
                break;
            }
        }

        #endregion

        /// <summary>
        /// Starts the timer immediately.
        /// </summary>
        new public void Start() { Start(0f); }

        /// <summary>
        /// Starts the timer after a delay in seconds
        /// </summary>
        /// <param name="p_delay">Delay before start in seconds.</param>
        public void Start(float p_delay) {
            delay = p_delay;
            base.Start();
        }

        /// <summary>
        /// Resets the Timer entirely, but keep it  running.
        /// </summary>
        public void Restart() {            
            paused = false;
            switch(state) {
                case State.Queued:
                case State.Running: {
                    m_elapsed = 0f;
                    step      = 0;                    
                }
                break;

                default: {
                    Start();
                }
                break;
            }
        }

        /// <summary>
        /// Restarts only the current step, but keep it running.
        /// </summary>
        public void RestartStep() {
            paused = false;
            switch(state) {
                case State.Queued:
                case State.Running: {
                    m_elapsed = 0f;                                        
                }
                break;
                default: {
                    Start();
                }
                break;
            }
        }

        /// <summary>
        /// Timer was just added for queueing and execution.
        /// </summary>
        internal override void OnAddedInternal() {            
            m_elapsed          = 0f;                        
            m_clock_delta_time = 0f;            
            m_clock_stamp      = 0f;
            m_clock_time       = 0f;
        }

        /// <summary>
        /// Called when the time has stopped.
        /// </summary>
        internal override void OnStopInternal() {            
            step               = 0;
            m_elapsed          = 0f;                        
            m_clock_delta_time = 0f;            
            m_clock_stamp      = 0f;
            m_clock_time       = 0f;
            paused             = false;
            base.OnStopInternal();
        }

        /// <summary>
        /// Updates the elapsed time and return true when the delay has passed.
        /// </summary>
        /// <returns></returns>
        internal override bool CanStartInternal() {
            if(m_elapsed>=delay) { 
                m_elapsed = 0f; 
                return true; 
            }
            m_elapsed += m_clock_delta_time;
            return false;
        }

        /// <summary>
        /// Executes the timer and early initialize data for execution.
        /// </summary>
        internal override void Execute() {            
            //Keep going the execution
            base.Execute();
            //In the first loop init the clock stamp
            if(m_clock_stamp<=0f) { 
                ResetClock(); 
            }
            //Computes the timer own delta-time based on the timestamps
            float t = GetClock();
            m_clock_delta_time = paused ? 0f : (t - m_clock_time);
            m_clock_time = t;
            //If running start updating 'elapsed' past the delay
            switch(state) {
                case State.Running: {
                    //Update Elapsed
                    m_elapsed += m_clock_delta_time;
                    //Fix has duration clamp to its value
                    if(duration>0f) m_elapsed = Mathf.Min(duration,Mathf.Max(0f,m_elapsed));
                }
                break;
            }            
        }

        /// <summary>
        /// Execute loop.
        /// </summary>
        /// <returns></returns>
        protected override bool OnExecute() {                        
            //If duration is '0.0' steps will never occur, otherwise elapsed>=duration
            return duration<=0f ? true : (m_elapsed<duration);
        }

        /// <summary>
        /// Executed before actually completing and validate steps
        /// </summary>
        /// <returns></returns>
        internal override bool CanCompleteInternal() {
            //Check if user event orders the completion
            bool v1 = InvokeEvent(m_on_step_event,this,true);
            //If 'stop' return 'completed'
            if(!v1) return true;
            //Increment step
            step++;                            
            //If 'steps' reached 'count' complete and 'count>0' (can finish steps)
            if(count>0)if(step>=count) { step=count-1; return true; }
            //Reset elapsed with negative to enforce first step 'elapsed=0'
            m_elapsed=0f;
            //Keep going
            return false;
        }

        #region IProgressProvider

        /// <summary>
        /// Provides timer execution progress.
        /// </summary>
        /// <returns>Execution progress in the range [0,1]</returns>
        override public float GetProgress() {
            switch(state) {
                case State.Running: return progress;
            }
            return base.GetProgress();
        }

        #endregion

    }

}
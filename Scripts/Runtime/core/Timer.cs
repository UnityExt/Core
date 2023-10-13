using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Threading.Tasks;
using UnityEngine;
using System.Threading;
using System;
using System.IO;

namespace UnityExt.Core {

    #region enum TimerState
    /// <summary>
    /// State enumeration for the most basic activity for tasks
    /// </summary>
    public enum TimerState {
        /// <summary>
        /// Just created
        /// </summary>
        Idle=0,
        /// <summary>
        /// Waiting to run
        /// </summary>
        Queue,
        /// <summary>
        /// Started
        /// </summary>
        Start,
        /// <summary>
        /// Waiting Delay for starting
        /// </summary>
        Wait,
        /// <summary>
        /// Running
        /// </summary>
        Run,
        /// <summary>
        /// Step Count
        /// </summary>
        Step,
        /// <summary>
        /// Complete with success
        /// </summary>
        Success,
        /// <summary>
        /// Stopped
        /// </summary>
        Stop,
        /// <summary>
        /// Complete with error
        /// </summary>
        Error
    }
    #endregion

    #region struct TimerStart
    /// <summary>
    /// Struct that initializes a Timer Start
    /// </summary>
    public struct TimerStart {

        #region static
        /// <summary>
        /// Returns a populated start struct using the input instance
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        static internal TimerStart From(Timer t) {
            return new TimerStart {                
                delay     = t.delay,
                duration  = t.duration,
                count     = t.count,
                speed     = t.speed,
                useFrames = t.useFrames
            };
        }
        #endregion

        /// <summary>
        /// Delay before starting
        /// </summary>
        public float delay { get { return m_delay ?? 0f; } set { m_delay = value; } }
        float? m_delay;

        /// <summary>
        /// Duration of the timer
        /// </summary>
        public float duration { get { return m_duration ?? 1f; } set { m_duration = value; } }
        float? m_duration;

        /// <summary>
        /// Number of steps
        /// </summary>
        public int count { get { return m_count ?? 1; } set { m_count = value; } }
        int? m_count;

        /// <summary>
        /// Speed of time increments
        /// </summary>
        public float speed { get { return m_speed ?? 1f; } set { m_speed = value; } }
        float? m_speed;

        /// <summary>
        /// Speed of time increments
        /// </summary>
        public bool useFrames { get { return m_use_frames ?? false; } set { m_use_frames = value; } }
        bool? m_use_frames;

        /// <summary>
        /// Helper to populate a target instance
        /// </summary>        
        internal void To(Timer t) {
            if(m_delay      != null) { t.delay     = delay;     }
            if(m_duration   != null) { t.duration  = duration;  }
            if(m_count      != null) { t.count     = count;     }
            if(m_speed      != null) { t.speed     = speed;     }
            if(m_use_frames != null) { t.useFrames = useFrames; }
        }
    }
    #endregion

    /// <summary>
    /// Activity extension to handle a timer that updates time with variable speed, as well keeping track of 'repeat' loops
    /// </summary>
    public class Timer : Activity<TimerState> {

        #region Atomic Clock

        /// <summary>
        /// Default Context to spawn Timers.
        /// </summary>
        new static public ProcessContext DefaultContext = ProcessContext.Update;

        /// <summary>
        /// Path to be used on the creation of an atomic timer.
        /// </summary>
        static public string AtomicClockRoot = $"{m_app_persistent_dp}/unityex/{m_app_platform}/timer/";

        /// <summary>
        /// Default atomic clock file name
        /// </summary>
        static public string AtomicClockfile = "ue-atomic-clock";

        /// <summary>
        /// Returns the folder where the atomic clock references are stored.
        /// </summary>
        static public string AtomicClockPath {
            get {
                string path = AtomicClockRoot.Replace('\\','/').Replace("//","/");
                if (!m_path_checked) if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                m_path_checked = true;
                return path;
            }
        }
        static private bool m_path_checked = false;

        /// <summary>
        /// Creates a temporary file and sample its current timestamp.
        /// </summary>
        /// <param name="p_id">Atomic Clock Id</param>
        /// <returns>DateTime stamp of the atomic clock</returns>
        static public DateTime GetAtomicTimestamp(string p_id = "") {
            bool is_default = string.IsNullOrEmpty(p_id);
            //File Name
            string fn = p_id;
            //Default file name
            if (is_default) { fn = AtomicClockfile; }
            //File Path
            string fp = AtomicClockPath + fn;
            //Fetch file info
            FileInfo fi = new FileInfo(fp);
            if (!fi.Exists) {
                FileStream fs = File.Open(fp,FileMode.Create,FileAccess.ReadWrite,FileShare.ReadWrite);
                //Write a byte and close
                fs.WriteByte(1);
                fs.FlushAsync();
                fs.Close();
                fs.Dispose();
            }
            fi.Refresh();
            //Return last access
            return fi.CreationTime;
        }

        /// <summary>
        /// Clears the atomic clock of a given id.
        /// </summary>
        /// <param name="p_id">Atomic Clock Id</param>
        static public void ClearAtomicTimestamp(string p_id = "") {
            bool is_default = string.IsNullOrEmpty(p_id);
            //Default file name
            string fn = p_id;
            if (is_default) { fn = AtomicClockfile; }
            //File Path
            string fp = AtomicClockPath + fn;
            FileInfo fi = new FileInfo(fp);
            if (!fi.Exists) return;
            File.SetCreationTimeUtc(fp,DateTime.UtcNow);
            fi.Delete();
            fi.Refresh();
        }

        /// <summary>
        /// Returns the elapsed timespan of an atomic clock, returns Timespan(0) if not created yet.
        /// </summary>
        /// <param name="p_id">Atomic Clock Id</param>
        /// <returns>Elapsed timespan of a given atomic block</returns>
        static public TimeSpan GetAtomicClockElapsed(string p_id = "") {
            //Fetch the tmp file information
            string tmp_fp = AtomicClockPath + tmp_clock_fn;
            if (tmp_fi == null) { tmp_fi = new FileInfo(tmp_fp); tmp_fs = File.Open(tmp_fp,FileMode.OpenOrCreate); }
            tmp_fs.WriteByte(1);
            tmp_fs.Flush();
            tmp_fi.Refresh();
            //File Path            
            DateTime t1 = tmp_fi.LastAccessTime;
            DateTime t0 = GetAtomicTimestamp(p_id);
            return t1 - t0;
        }
        static private string tmp_clock_fn = "$sys-clock";
        static private FileInfo tmp_fi = null;
        static private FileStream tmp_fs = null;
        #endregion

        /// <summary>
        /// Current Time
        /// </summary>
        public float time;

        /// <summary>
        /// Timer Duration
        /// </summary>
        public float duration;

        /// <summary>
        /// Delay before starting execution the loop.
        /// </summary>
        public float delay;

        /// <summary>
        /// Current number of steps
        /// </summary>
        public int step;

        /// <summary>
        /// Max number of steps
        /// </summary>
        public int count;

        /// <summary>
        /// Time increment speed.
        /// </summary>
        public float speed;

        /// <summary>
        /// Returns the current cycle's progress
        /// </summary>
        public float progress { get { return GetProgress(false); } }

        /// <summary>
        /// Flag that tells this timer will count frames instead of time.
        /// </summary>
        public bool useFrames;

        #region Events

        /// <summary>
        /// Handler for execution loop
        /// </summary>
        new public Action<Timer> OnExecuteEvent;

        /// <summary>
        /// Handler for state changes
        /// </summary>
        new public Action<Timer,TimerState,TimerState> OnChangeEvent;

        /// <summary>
        /// Auxiliary Event Calling
        /// </summary>        
        protected override void InternalExecuteEvent(TimerState p_state               ) { if (OnExecuteEvent != null) OnExecuteEvent(this            ); }
        protected override void InternalChangeEvent (TimerState p_from,TimerState p_to) { if (OnChangeEvent  != null) OnChangeEvent (this,p_from,p_to); }

        #endregion

        /// <summary>
        /// Delay to start running
        /// </summary>
        private float m_delay_time;

        /// <summary>
        /// CTOR.
        /// </summary>
        /// <param name="p_id"></param>
        public Timer(string p_id="") : base(p_id) {
            speed = 1f;
            count =  1;
        }

        /// <summary>
        /// Populate this Timer arguments
        /// </summary>
        /// <param name="p_args"></param>
        public void Set(TimerStart p_args) { p_args.To(this); }

        #region Operation

        /// <summary>
        /// Starts this timer with a duration and count number
        /// </summary>
        /// <param name="p_context"></param>
        /// <param name="p_duration"></param>
        /// <param name="p_count"></param>
        public Timer Start(ProcessContext p_context,TimerStart p_args) { Set(p_args); return (Timer)Start(p_context); }

        /// <summary>
        /// Starts this timer with a duration and count number
        /// </summary>
        /// <param name="p_context"></param>
        /// <param name="p_duration"></param>
        /// <param name="p_count"></param>
        public Timer Start(TimerStart p_args,bool p_thread=false) { return (Timer)Start(p_thread ? ProcessContext.Thread : ProcessContext.Update,p_args); }

        /// <summary>
        /// Starts the timer with current parameters at default context
        /// </summary>
        /// <returns></returns>
        new public Timer Start() { return Start(DefaultContext,TimerStart.From(this)); }

        #if UNITY_EDITOR
        /// <summary>
        /// Starts this timer with a duration and count number
        /// </summary>
        /// <param name="p_context"></param>
        /// <param name="p_duration"></param>
        /// <param name="p_count"></param>
        public Timer StartEditor(TimerStart p_args,bool p_thread=false) { return (Timer)Start(p_thread ? ProcessContext.EditorThread : ProcessContext.Editor,p_args); }

        /// <summary>
        /// Starts the timer with current parameters at default context
        /// </summary>
        /// <returns></returns>
        public Timer StartEditor(bool p_thread=false) { return Start(TimerStart.From(this),p_thread); }
        #endif

        /// <summary>
        /// Resets the timer
        /// </summary>
        public void Restart() {
            time = speed < 0f ? duration : 0f;
            step = speed < 0f ? count    : 0 ;
            Stop();
            Start();
        }

        #endregion

        #region IProgressProvider
        /// <summary>
        /// Returns the execution progress accounting for all steps or just timer versus duration
        /// </summary>
        /// <param name="p_include_steps"></param>
        /// <returns></returns>
        public float GetProgress(bool p_include_steps) {
            if (state == TimerState.Error  ) return 1f;
            if (state == TimerState.Success) return 1f;
            float t = Mathf.Abs(time);
            float d = Mathf.Abs(duration);
            if (useFrames) d -= 1f;
            float p = d <= 0f ? 0f : Mathf.Clamp01(t / d);
            if (!p_include_steps) return p;
            if (count <= 0f) return p;
            float c = Mathf.Abs(count);
            float s = Mathf.Abs(step);
            //if (state == TimerState.Step) if (s > 0f) s -= 1f;
            p = speed > 0f ? p : -(1f - p);
            float sp = Mathf.Clamp01((s + p) / (c));            
            return sp;
        }

        /// <summary>
        /// Returns the timer's execution progress based on time versus duration plus the stepping count
        /// </summary>
        /// <returns></returns>
        public override float GetProgress() { return GetProgress(true); }
        #endregion

        protected override void OnStateUpdate(TimerState p_state) {
            switch(p_state) {

                case TimerState.Wait: {
                    //Deltatime
                    float dt = useFrames ? 1f : deltaTime;
                    m_delay_time += dt * Mathf.Abs(speed);
                    float off = useFrames ? 0 : (dt * 2f);
                    if (m_delay_time < (delay-off)) break;
                    m_delay_time = delay;
                    state = TimerState.Run;
                }
                break;

                case TimerState.Run: {                    
                    //Deltatime
                    float dt = useFrames ? 1f : deltaTime;
                    //Increment Time using 'dt' and 'speed'
                    time += dt * speed;
                    //Will step increment happen
                    bool will_step = false;                    
                    //Step increment check (use a margin of 'dt' so 'Run' spans 0% to 100% ratios)
                    if(speed<0f)if(time <= 0f      ) { will_step = true; }
                    if(speed>0f)if(time >= duration) { will_step = true; }
                    //If no duration keep looping forever
                    if (duration <= 0) will_step = false;
                    //Skip until time reach duration
                    if (!will_step) break;
                    //Remap time into range
                    time = Mathf.Clamp(time,0f,duration);                    
                    //Switch to Step then back to run
                    state = TimerState.Step;                    
                }
                break;

                case TimerState.Step: {
                    //Increment step
                    step += speed < 0f ? -1 : 1;
                    //Range adjust
                    step = count <= 0 ? step : Mathf.Clamp(step,0,count);
                    //Is timer complete
                    bool step_end = speed < 0f ? step <= 0 : step >= count;
                    bool is_complete = count <= 0 ? false : step_end;
                    //If complete switch to 'stop'
                    if (is_complete) { Stop(); break; }
                    //Reset time accumulator and back running
                    time = speed < 0f ? duration : 0f;
                    state = TimerState.Run;
                }
                break;

            }
        }

        /// <summary>
        /// Task just started
        /// </summary>
        protected override void OnStart() {
            state = TimerState.Start;
            state = (delay > 0f ? TimerState.Wait : TimerState.Run);
        }

        /// <summary>
        /// Internal start handler
        /// </summary>
        /// <param name="p_editor"></param>
        /// <param name="p_context"></param>
        protected override void InternalStart(bool p_editor,ProcessContext p_context) {
            base.InternalStart(p_editor,p_context);
            delay = Mathf.Max(delay,0f);
            m_delay_time = 0f;
            state = TimerState.Queue;
        }

        /// <summary>
        /// Task removed from the pool
        /// </summary>
        protected override void OnStop() {
            switch (state) {
                case TimerState.Queue: break;
                default: {
                    state = TimerState.Stop;
                    state = isError ? TimerState.Error : TimerState.Success;
                }
                break;
            }
        }

    }

}
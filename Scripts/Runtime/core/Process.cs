using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UI;
using UnityExt.Core;
using Task = System.Threading.Tasks.Task;

#pragma warning disable CS0414

namespace UnityExt.Core {

    #region enum ProcessState
    /// <summary>
    /// Enumeration that describes the process execution state.
    /// </summary>
    public enum ProcessState : byte {
        /// <summary>
        /// Process is idle
        /// </summary>
        Idle = 0,
        /// <summary>
        /// Process is ready for use
        /// </summary>
        Ready,        
        /// <summary>
        /// Process Added into execution pool
        /// </summary>
        Add,
        /// <summary>
        /// Process Started
        /// </summary>
        Start,
        /// <summary>
        /// Process is running
        /// </summary>
        Run,
        /// <summary>
        /// Process removed from pool
        /// </summary>
        Remove,
        /// <summary>
        /// Process Stopped
        /// </summary>
        Stop
    }
    #endregion

    #region enum ProcessContext
    /// <summary>
    /// Execution Context the process must run
    /// </summary>
    public enum ProcessContext : byte {
        /// <summary>
        /// No Loop
        /// </summary>
        None            =  0,
        /// <summary>
        /// Engine's Update Loop 
        /// </summary>
        Update          = (1<<1),
        /// <summary>
        /// Engine's Late Update Loop
        /// </summary>
        LateUpdate      = (1<<2),
        /// <summary>
        /// Engine's Fixed Update 
        /// </summary>
        FixedUpdate     = (1<<3),
        /// <summary>
        /// System's Thread
        /// </summary>
        Thread          = (1<<4),
        /// <summary>
        /// Editor's loop
        /// </summary>
        Editor          = (1<<5),
        /// <summary>
        /// Editor's thread
        /// </summary>
        EditorThread    = (1<<6),
        /// <summary>
        /// Mask of Editor related contexts
        /// </summary>
        EditorMask   = Editor | EditorThread,
        /// <summary>
        /// Mask of Unity related contexts
        /// </summary>
        UnityMask    = Update | LateUpdate | FixedUpdate,
        /// <summary>
        /// Mask of Runtime related contexts
        /// </summary>
        RuntimeMask  = UnityMask | Thread,
        /// <summary>
        /// Mask of Thread related contexts
        /// </summary>
        ThreadMask        = Thread | EditorThread
    }
    #endregion

    #region enum ProcessFlags
    /// <summary>
    /// Helper enumeration to register which kinds of interfaces an activity implements
    /// </summary>    
    public enum ProcessFlags : short {
        /// <summary>
        /// No Interfaces
        /// </summary>
        None = 0,
        /// <summary>
        /// Flag that tells this process will run in deferred mode (until frame timeslice timesout)
        /// </summary>
        Deferred            = (1 << 1),
        /// <summary>
        /// Flag that tells this process will be affected by time scale (unless threaded)
        /// </summary>
        TimeScale           = (1 << 2),
        /// <summary>
        /// Is a component
        /// </summary>
        Component           = (1 << 3),
        /// <summary>
        /// Contains IProcess Interface
        /// </summary>
        IProcess            = (1 << 4),
        /// <summary>
        /// Contains IUpdateable Interface
        /// </summary>
        IUpdateable         = (1 << 5),
        /// <summary>
        /// Contains ILateUpdateable Interface
        /// </summary>
        ILateUpdateable     = (1 << 6),
        /// <summary>
        /// Contains IFixedUpdateable Interface
        /// </summary>
        IFixedUpdateable    = (1 << 7),
        /// <summary>
        /// Contains IThreadUpdateable Interface
        /// </summary>
        IThreadUpdateable   = (1 << 8),
        /// <summary>
        /// Contains IThreadUpdateable Interface
        /// </summary>
        IJobProcess         = (1 << 9),
        /// <summary>
        /// Flag that tell its a delegate
        /// </summary>
        Delegate            = (1 << 10),        
        /// <summary>
        /// Mask for all interfaces
        /// </summary>
        Interfaces = IUpdateable | ILateUpdateable | IFixedUpdateable | IThreadUpdateable | IProcess | IJobProcess | Component | Delegate,
    }
    #endregion

    /// <summary>
    /// Type for anonymous process handlers
    /// </summary>
    /// <param name="c"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    public delegate bool ProcessAction(ProcessContext p_context,Process p_process);

    /// <summary>
    /// Class that describes a process structure, containing one or more process interfaces to run inside its context.
    /// </summary>
    [System.Serializable]
    public class Process {

        #region class UnityJobReflection
        /// <summary>
        /// Auxiliary Class to hold reflection data of unity job system
        /// </summary>
        internal class UnityJobReflection {

            internal MethodInfo jobSchedule;
            internal MethodInfo jobRun;
            internal MethodInfo jobForSchedule;
            internal MethodInfo jobForRun;
            internal MethodInfo jobParallelForSchedule;
            internal MethodInfo jobParallelForRun;

            internal Type[] jobCtorArgs0;
            internal object[] jobArgs0;
            internal object[] jobArgs1;
            internal object[] jobArgs2;
            internal object[] jobArgs3;
            internal object[] jobArgs4;

            /// <summary>
            /// CTOR.
            /// </summary>
            public UnityJobReflection() {
                BindingFlags bf = BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic;
                Type jb_ext_t;
                jb_ext_t = typeof(IJobExtensions); jobSchedule = jb_ext_t.GetMethod("Schedule",bf); jobRun = jb_ext_t.GetMethod("Run",bf);
                jb_ext_t = typeof(IJobForExtensions); jobForSchedule = jb_ext_t.GetMethod("Schedule",bf); jobForRun = jb_ext_t.GetMethod("Run",bf);
                jb_ext_t = typeof(IJobParallelForExtensions); jobParallelForSchedule = jb_ext_t.GetMethod("Schedule",bf); jobParallelForRun = jb_ext_t.GetMethod("Run",bf);                
                jobCtorArgs0 = new Type[0];
                jobArgs0 = new object[0]; //()
                jobArgs1 = new object[1]; //(IJob)
                jobArgs2 = new object[2]; //(IJob,JobHandle) (IJob,int)
                jobArgs3 = new object[3]; //(IJob,int,JobHandle)
                jobArgs4 = new object[4]; //(IJob,int,int,JobHandle)                                              
            }

        }
        /// <summary>
        /// Internals
        /// </summary>
        static private UnityJobReflection u_job_r;
        #endregion

        #region class Locals
        /// <summary>
        /// Auxiliary class to hold internal variables used across the process.
        /// </summary>
        [System.Serializable]
        internal class Locals {

            /// <summary>
            /// Returns the process context by interfaces implemented
            /// </summary>
            /// <param name="a"></param>
            /// <param name="e"></param>
            /// <returns></returns>
            static internal ProcessContext GetContexts(IActivity a, bool e) {
                ProcessContext ctx = ProcessContext.None;
                if(e) {
                    if (a is IUpdateable      ) ctx |= ProcessContext.Editor;
                    if (a is IThreadUpdateable) ctx |= ProcessContext.EditorThread;
                }
                else {
                    if (a is IUpdateable       ) ctx |= ProcessContext.Update;
                    if (a is ILateUpdateable   ) ctx |= ProcessContext.LateUpdate;
                    if (a is IFixedUpdateable  ) ctx |= ProcessContext.FixedUpdate;
                    if (a is IThreadUpdateable ) ctx |= ProcessContext.Thread;
                    if (a is IJobProcess       ) ctx |= ProcessContext.Update;
                }
                return ctx;
            }

            /// <summary>
            /// Casting shortcuts
            /// </summary>
            [NonSerialized]
            internal Process            proc;
            internal IUpdateable        u;
            internal ILateUpdateable    lu;
            internal IFixedUpdateable   fu;
            internal IThreadUpdateable  tu;
            internal IProcess           p;
            internal IJobProcess        j;
            internal JobHandle          jh;
            internal bool               is_jf;
            internal bool               is_jpf;
            internal object             ji;
            internal MethodInfo         jOnJobCreate;
            internal MethodInfo         jSchedule;
            internal MethodInfo         jRun;
            internal UnityJobReflection jrfl { get { return u_job_r; } }
            internal Task               tsk;
            internal float              tsk_yield;
            internal CancellationTokenSource tsk_cancel;
            [SerializableField] internal Component c;

            /// <summary>
            /// Populate activity and interface data
            /// </summary>
            /// <param name="pp"></param>
            /// <param name="a"></param>
            internal bool Set(IActivity a) {
                    //Locals
                IActivity it = a;
                //Assertion
                if (it == null) { Clear(); return false; }
                //Flag bit mask
                bool is_component = it is Component;            
                //If component store its reference (serialization)
                c = is_component ? it as Component : null;
                //Ignore invalids (unity null check)
                if (is_component) 
                if (!c) { Clear(); return false; }
                //Mark bit flag
                if (is_component) proc.flags |= ProcessFlags.Component;
                //Store reference
                proc.activity = it;            
                //Casting shortcuts
                if (it is IUpdateable       )  { proc.flags |= ProcessFlags.IUpdateable;        u  = it as IUpdateable;       } else u  = null;
                if (it is ILateUpdateable   )  { proc.flags |= ProcessFlags.ILateUpdateable;    lu = it as ILateUpdateable;   } else lu = null;
                if (it is IFixedUpdateable  )  { proc.flags |= ProcessFlags.IFixedUpdateable;   fu = it as IFixedUpdateable;  } else fu = null;
                if (it is IThreadUpdateable )  { proc.flags |= ProcessFlags.IThreadUpdateable;  tu = it as IThreadUpdateable; } else tu = null;
                if (it is IProcess          )  { proc.flags |= ProcessFlags.IProcess;           p  = it as IProcess;          } else p  = null;
                if (it is IJobProcess       )  { proc.flags |= ProcessFlags.IJobProcess;        j  = it as IJobProcess;       } else j  = null;
                //Job System Reflection Crazyness
                if (j != null) {
                    //Fetch list of interfaces
                    Type[] jil = j.GetType().GetInterfaces();                    
                    //Reference to the Generics version of the interface
                    Type   jgt = null;
                    Type   jgt_job_t        = null;
                    //Reset job type flags
                    is_jf = is_jpf = false;
                    for (int i = 0;i < jil.Length;i++) {
                        Type jit = jil[i];
                        //Must have <T>
                        if (!jit.IsGenericType) continue;
                        //Must be IProcess<T>
                        if (!jit.Name.Contains("IJobProcess")) continue;
                        //Save Type
                        jgt = jit; 
                        break;                        
                    }
                    //If no generic IProcess<T> found
                    if (jgt == null) { j = null; return false; }
                    //Fetch the <T> type
                    jgt_job_t = jgt.GenericTypeArguments[0];
                    //If failed
                    if (jgt_job_t == null) { j = null; return false; }
                    //IProcess<T>.OnJobCreated(job)
                    jOnJobCreate = j.GetType().GetMethod("OnJobCreate");
                    //Aux
                    jrfl.jobArgs1[0] = ji;
                    //Invoke OnJobCreated
                    ji = jOnJobCreate.Invoke(j,jrfl.jobArgs0);
                    //Fetch IJob vairations flags
                    is_jf  = ji is IJobFor;
                    is_jpf = ji is IJobParallelFor;                    
                    if (is_jf ) jSchedule = jrfl.jobForSchedule        .MakeGenericMethod(jgt_job_t); else
                    if (is_jpf) jSchedule = jrfl.jobParallelForSchedule.MakeGenericMethod(jgt_job_t); else
                                jSchedule = jrfl.jobSchedule           .MakeGenericMethod(jgt_job_t);

                    if (is_jf ) jRun = jrfl.jobForRun        .MakeGenericMethod(jgt_job_t); else
                    if (is_jpf) jRun = jrfl.jobParallelForRun.MakeGenericMethod(jgt_job_t); else
                                jRun = jrfl.jobRun           .MakeGenericMethod(jgt_job_t);

                }
                //All good
                return true;
            }

            internal void InitTask() {
                if (tsk != null) return;
                tsk_cancel = new CancellationTokenSource();
                tsk = new Task(TaskComplete,tsk_cancel.Token);
                tsk_yield = 0f;                
            }

            internal void TaskComplete() {
                //Sleep is inside task thread, so safe to use
                if (tsk_yield > 0f) System.Threading.Thread.Sleep((int)(tsk_yield*1000f));
                //Clear up
                tsk = null;
                tsk_cancel = null;
            }

            internal void StartTask() {
                if (tsk == null) return;                
                switch(tsk.Status) {
                    case TaskStatus.Canceled:
                    case TaskStatus.Running:
                    case TaskStatus.WaitingToRun: break;
                    default: tsk.Start(); break;
                }                
            }

            internal TaskAwaiter TaskAwaiter() { return tsk==null ? default : tsk.GetAwaiter(); }

            internal void ClearTask() {
                if (tsk == null) return;
                switch(tsk.Status) {                    
                    case TaskStatus.Running:
                    case TaskStatus.WaitingForActivation:
                    case TaskStatus.WaitingToRun: {
                        tsk_cancel.Cancel();
                    }
                    break;
                    default: break;
                }                
                tsk = null;
                tsk_cancel = null;
            }

            internal bool UpdateJob(bool p_deferred) {
                //Iteration locals
                int c = 0;
                int b = 0;
                if (is_jf || is_jpf) c = j.GetForCount  ();
                if (is_jpf         ) c = j.GetBatchCount();
                if (!p_deferred) {
                    //Run(jobData,arrayLength)
                    if (is_jf) {
                        jrfl.jobArgs2[0] = ji; 
                        jrfl.jobArgs2[1] = c ;
                        jRun.Invoke(null,jrfl.jobArgs2);
                    }
                    //Run(jobData,arrayLength)
                    else
                    if (is_jpf){
                        jrfl.jobArgs2[0] = ji;
                        jrfl.jobArgs2[1] = c;
                        jRun.Invoke(null,jrfl.jobArgs2);
                    }
                    //Run(jobData)
                    else {
                        jrfl.jobArgs1[0] = ji;
                        jRun.Invoke(null,jrfl.jobArgs1);
                    }
                    return true;
                }
                
                //Wait for completion
                if (!jh.IsCompleted) return false;
                //Schedule(jobData,arrayLength,JobHandle)
                if (is_jf) {
                    jrfl.jobArgs3[0] = ji;
                    jrfl.jobArgs3[1] = c;
                    jrfl.jobArgs3[2] = default(JobHandle);
                    jh = (JobHandle)jSchedule.Invoke(null,jrfl.jobArgs3);
                }
                //Schedule(jobData,arrayLength,innerloopBatchCount,JobHandle)
                else
                if (is_jpf) {
                    jrfl.jobArgs4[0] = ji;
                    jrfl.jobArgs4[1] = c;
                    jrfl.jobArgs4[2] = b;
                    jrfl.jobArgs4[3] = default(JobHandle);
                    jh = (JobHandle)jSchedule.Invoke(null,jrfl.jobArgs4);
                }
                //Schedule(jobData,JobHandle)
                else {
                    jrfl.jobArgs2[0] = ji;
                    jrfl.jobArgs2[1] = default(JobHandle);
                    jh = (JobHandle)jSchedule.Invoke(null,jrfl.jobArgs2);
                }
                return true;
                
            }

            /// <summary>
            /// Clear all references
            /// </summary>
            internal void Clear() {                
                u  = null; 
                lu = null; 
                fu = null;
                tu = null;
                p  = null;
                j  = null;
                jh     = default;
                is_jf  = false;
                is_jpf = false;
            }
        }
        #endregion

        #region static

        /// <summary>
        /// CTOR.
        /// </summary>
        static Process() {
            u_job_r = new UnityJobReflection();
        }

        /// <summary>
        /// Returns a process instance
        /// </summary>
        /// <returns></returns>
        static private Process New(string p_name,bool p_editor) { 
            Process p = ProcessManager.instance.PopProcess(p_editor ? ProcessContext.Editor : ProcessContext.Update); 
            p.name  = p_name;
            p.state = ProcessState.Idle; 
            return p; 
        }

        #region Start

        #region Start.IActivity

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process Start(string p_name,IActivity p_activity,ProcessContext p_context,ProcessFlags p_flags = ProcessFlags.None) { Process p = New(p_name,false);  p.Run(p_activity,Locals.GetContexts(p_activity,false) | p_context,p_flags); return p; }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <param name="p_activity"></param>
        /// <param name="p_flags"></param>
        /// <returns></returns>
        static public Process Start(string p_name,IActivity p_activity,ProcessFlags p_flags = ProcessFlags.None) { return Start(p_name,p_activity,ProcessContext.None,p_flags); }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process Start(IActivity p_activity,ProcessContext p_context,ProcessFlags p_flags = ProcessFlags.None) { return Start("",p_activity,p_context,p_flags); }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <param name="p_activity"></param>
        /// <param name="p_flags"></param>
        /// <returns></returns>
        static public Process Start(IActivity p_activity,ProcessFlags p_flags = ProcessFlags.None) { return Start("",p_activity,p_flags); }

        #endregion

        #region Start.ProcessAction

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process Start(string p_name,ProcessAction p_callback,ProcessContext p_context = ProcessContext.Update,ProcessFlags p_flags = ProcessFlags.None) { Process p = New(p_name,(p_context & ProcessContext.EditorMask) != 0); p.Run(p_callback,p_context,p_flags); return p;  }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process Start(string p_name,ProcessAction p_callback,bool p_threaded) {  return Start(p_name,p_callback,p_threaded ? ProcessContext.Thread : ProcessContext.Update);  }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process Start(ProcessAction p_callback,ProcessContext p_context = ProcessContext.Update,ProcessFlags p_flags = ProcessFlags.None) { return Start("",p_callback,p_context,p_flags); }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process Start(ProcessAction p_callback,bool p_threaded) { return Start("",p_callback,p_threaded); }

        #endregion

        #if UNITY_EDITOR
        
        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process StartEditor(string p_name,IActivity p_activity,ProcessContext p_context = ProcessContext.None,ProcessFlags p_flags = ProcessFlags.None) { Process p = New(p_name,true);  p.Run(p_activity,Locals.GetContexts(p_activity,true) | p_context,p_flags); return p; }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process StartEditor(string p_name,IActivity p_activity,ProcessFlags p_flags = ProcessFlags.None) { return StartEditor(p_name,p_activity,ProcessContext.None,p_flags); }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process StartEditor(IActivity p_activity,ProcessContext p_context = ProcessContext.None,ProcessFlags p_flags = ProcessFlags.None) { return StartEditor("",p_activity,p_context,p_flags); }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process StartEditor(IActivity p_activity,ProcessFlags p_flags = ProcessFlags.None) { return StartEditor("",p_activity,p_flags); }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process StartEditor(string p_name,ProcessAction p_callback,ProcessFlags p_flags = ProcessFlags.None,bool p_threaded=false) { Process p = New(p_name,true); p.Run(p_callback,p_threaded ? ProcessContext.EditorThread : ProcessContext.EditorThread); return p; }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process StartEditor(string p_name,ProcessAction p_callback,bool p_threaded) { return StartEditor(p_name,p_callback,ProcessFlags.None,p_threaded); }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process StartEditor(ProcessAction p_callback,ProcessFlags p_flags = ProcessFlags.None,bool p_threaded=false) { return StartEditor("",p_callback,p_flags,p_threaded); }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process StartEditor(ProcessAction p_callback,bool p_threaded) { return StartEditor("",p_callback,p_threaded); }
        
        #endif

        #endregion

        /// <summary>
        /// Returns a simple process that yield for 'time' seconds and then completes.
        /// </summary>
        /// <param name="p_time"></param>
        /// <returns></returns>
        static public Process Delay(float p_time) {
            return Start(delegate (ProcessContext ctx,Process p) { return p.time < p_time; });
        }

        /// <summary>
        /// Disposes an activity properly
        /// </summary>
        /// <param name="p_activity"></param>
        static public void Dispose(IActivity p_activity) {
            if (p_activity == null) return;
            if (p_activity.process != null) p_activity.process.Dispose();
        }

        #endregion

        /// <summary>
        /// Reference to the manager owning this process
        /// </summary>
        public ProcessManager manager { get; internal set; }

        /// <summary>
        /// Get/Set the Process name
        /// </summary>
        public string name;

        /// <summary>
        /// Process ID
        /// </summary>
        public int pid { get; internal set; }

        /// <summary>
        /// List of addresses per process unit
        /// </summary>
        [SerializeField] internal int[] addresses;

        /// <summary>
        /// Returns a flag telling if this process is currently inside all target process unit
        /// </summary>
        internal bool inUnit { 
            get {
                int k = 0, uc = 0,f = 1;
                for (int i = 0;i < addresses.Length;i++) {
                    if (addresses[i] >= 0) k++;
                    if ((context & ((ProcessContext)f)) != 0) uc++;
                    f = f << 1;
                }
                //Number of available addresses must match chosen contexts
                return k==uc; 
            } 
        }

        /// <summary>
        /// Returns a flag that tells this process is outside all possible units
        /// </summary>
        internal bool outUnit {
            get {                             
                for (int i = 0;i < addresses.Length;i++) { if (addresses[i] >= 0) return false; }                
                return true;
            }
        }

        /// <summary>
        /// Executing Context
        /// </summary>
        public ProcessContext context { get { return m_context; } internal set { m_context = value; } }
        [SerializeField] private ProcessContext m_context;
        
        /// <summary>
        /// Executing state
        /// </summary>
        public ProcessState state;

        /// <summary>
        /// Process execution time;
        /// </summary>
        public float time;

        /// <summary>
        /// Delta time since last execution
        /// </summary>
        public float deltaTime;

        /// <summary>
        /// Flag that tells to use time scale.
        /// </summary>
        public bool useTimeScale { get { return GetFlag(ProcessFlags.TimeScale); } set { SetFlag(ProcessFlags.TimeScale,value); } }

        /// <summary>
        /// Flag that tells this process runs in deferred mode
        /// After the core time slice ends, it will skip until next frame
        /// </summary>
        public bool deferred { get { return GetFlag(ProcessFlags.Deferred); } set { SetFlag(ProcessFlags.Deferred,value); } }

        /// <summary>
        /// Reference to the running activity.
        /// </summary>        
        public IActivity activity;

        /// <summary>
        /// Callback handlers
        /// </summary>
        public ProcessAction callback;

        /// <summary>
        /// Process creation flags
        /// </summary>
        public ProcessFlags flags { get { return m_flags; } private set { m_flags = value; } }
        [SerializeField] private ProcessFlags m_flags;

        //Locals helper
        private Locals lv;        
        [SerializeField] public float[] m_lt_lut;
        [SerializeField] public float[] m_t_lut;
        [SerializeField] public float[] m_dt_lut;

        /// <summary>
        /// CTOR.
        /// </summary>
        public Process() {            
            Clear();
        }

        #region Clear

        /// <summary>
        /// Clears this process to start fresh
        /// </summary>
        public void Clear() {                        
            name          = "";
            state         = ProcessState.Idle;
            context       = ProcessContext.None;
            time          = 0f;
            deltaTime     = 0f;            
            activity      = null;            
            //Clear Flags
            flags = ProcessFlags.None;
            //Clear casts
            if (lv == null) { lv = new Locals(); lv.proc = this; }
            lv.Clear();
            //Remove all callbacks
            callback = null;
            ClearUnitData();
        }

        /// <summary>
        /// Clear all Unit's addresses
        /// </summary>
        internal void ClearUnitData() {            
            InitUnitList<int>(ref addresses,6,-1);
            InitUnitList<float>(ref m_t_lut,6,0f);
            InitUnitList<float>(ref m_dt_lut,6,0f);
            InitUnitList<float>(ref m_lt_lut,6,0f);            
        }

        /// <summary>
        /// Helper
        /// </summary>
        /// <param name="vl"></param>
        /// <param name="l"></param>
        private void InitUnitList<T>(ref T[] vl,int l,T v) {
            int c = vl == null ? 0 : vl.Length;
            if (c < l) vl = new T[l];
            for (int i = 0;i < vl.Length;i++) vl[i] = v;
        }

        #endregion

        #region Unity Jobs

        /// <summary>
        /// Returns the job instance if Process is unity job based
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetJob<T>() where T : struct { return lv.ji == null ? default(T) : ((T)lv.ji); }

        /// <summary>
        /// Sets the job struct
        /// </summary>
        /// <param name="p_job"></param>
        public void SetJob(object p_job) { lv.ji = p_job; }

        #endregion

        #region Async/Await

        /// <summary>
        /// Yields this process until completion and wait delay seconds before continuying.
        /// </summary>
        /// <param name="p_delay">Extra delay seconds after completion</param>
        /// <returns>Task to be waited</returns>
        public Task Yield(float p_delay = 0f) { lv.tsk_yield = p_delay; return lv.tsk; }

        /// <summary>
        /// Waits until 'timeout' to continue execution in 'await'
        /// </summary>
        /// <param name="p_timeout"></param>
        /// <returns></returns>
        public Task<bool> Wait(float p_timeout) {
            Task tsk_ref = lv.tsk;
            return
            Task.Run<bool>(delegate () {
                Thread.Sleep((int)(p_timeout * 1000f));
                if (tsk_ref != null)
                    switch (tsk_ref.Status) {
                        //In case of just created or still running stop this activity
                        case TaskStatus.Created:
                        case TaskStatus.Running: {
                            Dispose();
                        }
                        return false;
                    }
                return true;
            });            
        }

        /// <summary>
        /// Reference to the awaiter (used by 'await' call)
        /// </summary>
        /// <returns>Current awaiter for 'await' operator.</returns>
        public TaskAwaiter GetAwaiter() { return lv.TaskAwaiter(); }

        #endregion

        #region Run

        /// <summary>
        /// Starts this process
        /// </summary>
        /// <param name="p_context"></param>
        /// <param name="p_flags"></param>
        /// <param name="p_activity"></param>
        /// <returns></returns>
        public bool Run(IActivity p_activity,ProcessContext p_context = ProcessContext.Update,ProcessFlags p_flags = ProcessFlags.None) {
            //Locals
            IActivity it = p_activity;
            //Assertion
            if (it == null) { Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Activity is <null>"); return false; }
            return InternalRun(p_context,p_flags,p_activity,null);
        }

        /// <summary>
        /// Starts this process
        /// </summary>
        /// <param name="p_context"></param>
        /// <param name="p_flags"></param>
        /// <param name="p_callback"></param>
        /// <returns></returns>
        public bool Run(ProcessAction p_callback,ProcessContext p_context = ProcessContext.Update,ProcessFlags p_flags = ProcessFlags.None) {
            //Locals
            ProcessAction it = p_callback;
            //Assertion
            if (it == null) { Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Callback is <null>"); return false; }
            return InternalRun(p_context,p_flags,null,p_callback);
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Starts activity in editor context
        /// </summary>
        /// <param name="p_activity"></param>
        public bool RunEditor(IActivity p_activity,bool p_threaded=false) {            
            return Run(p_activity,p_threaded ? ProcessContext.EditorThread : ProcessContext.Editor,ProcessFlags.None);
        }

        /// <summary>
        /// Starts activity in editor context
        /// </summary>
        /// <param name="p_activity"></param>
        public bool RunEditor(ProcessAction p_callback,bool p_threaded=false) {         
            return Run(p_callback,p_threaded ? ProcessContext.EditorThread : ProcessContext.Editor,ProcessFlags.None);
        }
        #endif

        /// <summary>
        /// Starts this process
        /// </summary>
        internal bool InternalRun(ProcessContext p_context,ProcessFlags p_flags,IActivity p_activity,ProcessAction p_callback) {            
            //Context Assertion (only needed in editor
            #if UNITY_EDITOR            
            if((p_context & ProcessContext.RuntimeMask)!=0) {
                if(!manager.isPlaying) {
                    Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Can't Start Runtime Loops outside PlayMode!");
                    Clear();
                    return false;
                }                
            }            
            #endif
            //Locals
            IActivity     a  = p_activity;
            ProcessAction cb = p_callback;
            //Debug.Log($"Process> START - {state} - {a} @ [{a?.process?.pid}] [{pid}]");
            if(state != ProcessState.Idle) { Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Invalid State to Start - {state}"); return false; }
            //At least 'callback' or 'activity' must be valid
            bool has_handlers = (a != null) || (cb != null);
            if (!has_handlers) return false;
            //Clear Flags
            flags = ProcessFlags.None;
            //If activity populate            
            if (a != null) {
                //if(a.process != null) { /*Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Activity already in a process - {state}");*/ return false; }
                //Populate IActivity and other interfaces data
                //Set the activity process
                a.process = this;
                //Populate interfaces data
                lv.Set(a);
                //Assertion when Unity Component
                if (GetFlag(ProcessFlags.Component)) if (lv.c == null) { Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Activity as Component is <null>"); return false; }
                
            }            
            //If delegate assign reference
            if(cb != null) {
                callback = cb;
                flags |= ProcessFlags.Delegate;
            }            
            //Store Context
            context = p_context;
            //Set flags
            SetFlag(p_flags,true);
            //Set as Ready
            SetState(context,ProcessState.Ready);
            //Start the Task system for 'await'
            lv.InitTask();
            //Schedule process to run
            if (manager) manager.AddProcess(this,false);
            return true;
        }

        #endregion

        #region Flags & States

        /// <summary>
        /// Checks if a given flag is enabled
        /// </summary>
        /// <param name="p_mask"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetFlag(ProcessFlags p_mask) { return (flags & p_mask) != 0; }

        /// <summary>
        /// Enable or disable flag bits
        /// </summary>
        /// <param name="p_mask"></param>
        /// <param name="p_value"></param>
        public void SetFlag(ProcessFlags p_mask,bool p_value) {
            ProcessFlags f = flags;
            ProcessFlags m = p_mask;
            //Mask out interface related ones (handled differently)
            m = m & ~(ProcessFlags.Interfaces);
            //Enable/Disable bit masks
            flags = p_value ? (f | m) : (f & (~m));
        }


        /// <summary>
        /// Set the state and notify 
        /// </summary>
        /// <param name="p_state"></param>
        internal void SetState(ProcessContext p_context,ProcessState p_state) {
            //if(state == p_state) return;
            state = p_state;
            bool has_handlers = (activity!=null) || (callback !=null);
            if (!has_handlers) return;            
            if (GetFlag(ProcessFlags.IProcess)) lv.p.OnProcessUpdate(p_context,state);            
            switch(state) {                
                case ProcessState.Stop: {                    
                    if (GetFlag(ProcessFlags.IJobProcess)) {
                        //If 'deferred' and job ongoing, force completion                        
                        if (deferred) lv.jh.Complete();
                        lv.j.OnJobDispose();
                    }
                    //Execute task that will run in 1 tick or yield_ms and will unlock the 'await'
                    lv.StartTask();
                }
                break;
            }            
        }

        /// <summary>
        /// Updates the internal clocking.
        /// </summary>
        /// <param name="p_time"></param>
        internal void SetTime(int p_id,float p_time,bool p_first) {
            //Updates the timing info per unit id
            int n = p_id;
            if (p_first) { m_lt_lut[n] = p_time; m_dt_lut[n] = 0f; return; }
            m_dt_lut[n]  = Mathf.Max(0f,p_time - m_lt_lut[n]);
            m_lt_lut[n]  = p_time;            
            m_t_lut[n]  += m_dt_lut[n];
            //Set the current time state 
            time      = m_t_lut[n];
            deltaTime = m_dt_lut[n];
        }

        #endregion

        #region Execution

        /// <summary>
        /// Execute process iterations
        /// </summary>
        internal void Update(ProcessContext p_context) {            
            //Skip if not run state
            if (state != ProcessState.Run) return;
            //Locals
            ProcessContext       ctx = p_context;                        
            //If is component do checks
            if (GetFlag(ProcessFlags.Component)) { 
                //If null dispose
                if (!lv.c) { Dispose(); return; }
                //If by chance 'activity' is null (maybe recompilation) - try recovering
                if(activity==null) lv.Set(lv.c as IActivity);
            }            
            //Dispose in case of invalids
            if(GetFlag(ProcessFlags.Delegate)) {
                if (callback == null) { Dispose(); return; }
            }
            else {
                if (activity == null) { Dispose(); return; }
            }
            //Run Process
            //Update basic activity            
            if (GetFlag(ProcessFlags.IProcess)) lv.p.OnProcessUpdate(ctx,state);
            //Update loop specific ones            
            switch (ctx) {
                case ProcessContext.Editor:
                case ProcessContext.Update:         if(GetFlag(ProcessFlags.IUpdateable       )) lv.u .OnUpdate      (); break;                
                case ProcessContext.LateUpdate:     if(GetFlag(ProcessFlags.ILateUpdateable   )) lv.lu.OnLateUpdate  (); break;
                case ProcessContext.FixedUpdate:    if(GetFlag(ProcessFlags.IFixedUpdateable  )) lv.fu.OnFixedUpdate (); break;
                case ProcessContext.EditorThread:
                case ProcessContext.Thread:         if(GetFlag(ProcessFlags.IThreadUpdateable )) lv.tu.OnThreadUpdate(); break;
            }
            //Update callback based
            if (GetFlag(ProcessFlags.Delegate)) { 
                //If return 'false' stop execution
                bool res = callback(ctx,this); 
                if (!res) { 
                    Dispose(); 
                    return; 
                } 
            }
            //Update Job based ones
            if (GetFlag(ProcessFlags.IJobProcess))
            switch(ctx) {
                case ProcessContext.Update:
                case ProcessContext.LateUpdate:
                case ProcessContext.FixedUpdate: {                    
                    //If job is not schedules, always update otherwise use JobHandle
                    bool can_update   = deferred ? lv.jh.IsCompleted : true;                    
                    if(can_update) {
                        //Deferred needs JobHandle to be 'Complete' after finishing
                        if(deferred) lv.jh.Complete();
                        //Update interface and stop if requested
                        if(!lv.j.OnJobUpdate()) { Dispose(); return; }
                        //Trigger next job run
                        lv.UpdateJob(deferred);
                    }                                        
                }
                break;
            }

        }

        /// <summary>
        /// Disposes this process and returns it to the pool
        /// </summary>
        public void Dispose(bool p_force=false) {
            if (manager) manager.RemoveProcess(this,p_force);
        }

        #endregion

    }

}
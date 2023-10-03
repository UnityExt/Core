using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Jobs;
using UnityEngine;
using UnityExt.Core;

#pragma warning disable CS0414

namespace UnityExt.Sys {

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
        /// Mask of Runtime related contexts
        /// </summary>
        RuntimeMask  = Update | LateUpdate | FixedUpdate | Thread,
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
        /// Flag that tell its a job process
        /// </summary>
        Job                 = (1 << 11),
        /// <summary>
        /// Flag that tell its a job for process
        /// </summary>
        JobFor              = (2 << 11),
        /// <summary>
        /// Flag that tell its a job for process
        /// </summary>
        JobParalellFor      = (3 << 11),
        /// <summary>
        /// Bit Mask to check if jobs are available
        /// </summary>
        JobMask             = (3 << 11),
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

        #region class ICasts
        /// <summary>
        /// Auxiliary class to hold interface casts
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
            [SerializableField] internal Component c;

            /// <summary>
            /// Populate activity and interface data
            /// </summary>
            /// <param name="pp"></param>
            /// <param name="a"></param>
            internal bool Set(Process pp,IActivity a) {
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
                if (is_component) pp.flags |= ProcessFlags.Component;
                //Store reference
                pp.activity = it;            
                //Casting shortcuts
                if (it is IUpdateable       )  { pp.flags |= ProcessFlags.IUpdateable;        u  = it as IUpdateable;       } else u  = null;
                if (it is ILateUpdateable   )  { pp.flags |= ProcessFlags.ILateUpdateable;    lu = it as ILateUpdateable;   } else lu = null;
                if (it is IFixedUpdateable  )  { pp.flags |= ProcessFlags.IFixedUpdateable;   fu = it as IFixedUpdateable;  } else fu = null;
                if (it is IThreadUpdateable )  { pp.flags |= ProcessFlags.IThreadUpdateable;  tu = it as IThreadUpdateable; } else tu = null;
                if (it is IProcess          )  { pp.flags |= ProcessFlags.IProcess;           p  = it as IProcess;          } else p  = null;
                if (it is IJobProcess       )  { pp.flags |= ProcessFlags.IJobProcess;        j  = it as IJobProcess;       } else j  = null;
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
            public void Clear() {                
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
        static private Process New(bool p_editor) { 
            Process p = ProcessManager.instance.PopProcess(p_editor ? ProcessContext.Editor : ProcessContext.Update); 
            p.state = ProcessState.Idle; 
            return p; 
        }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process New(ProcessContext p_context,ProcessFlags p_flags,IActivity p_activity) { Process p = New((p_context & ProcessContext.EditorMask)!=0); p.Start(p_context,p_flags,p_activity); return p; }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process New(ProcessContext p_context,IActivity p_activity) { return New(p_context,ProcessFlags.None,p_activity); }

        /// <summary>
        /// Returns a process instance and choose the context based on interface layout
        /// </summary>
        /// <returns></returns>
        static public Process New(IActivity p_activity,ProcessFlags p_flags,bool p_editor=false) {
            ProcessContext ctx = Locals.GetContexts(p_activity,p_editor);
            return New(ctx,p_flags,p_activity); 
        }

        /// <summary>
        /// Returns a process instance and choose the context based on interface layout
        /// </summary>
        /// <param name="p_activity"></param>
        /// <param name="p_editor"></param>
        /// <returns></returns>
        static public Process New(IActivity p_activity,bool p_editor = false) { return New(p_activity,ProcessFlags.None,p_editor); }

        /// <summary>
        /// Disposes an activity properly
        /// </summary>
        /// <param name="p_activity"></param>
        static public void Dispose(IActivity p_activity) {
            if (p_activity == null) return;
            if (p_activity.process != null) p_activity.process.Dispose();            
        }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process New(ProcessContext p_context,ProcessFlags p_flags,ProcessAction p_callback) { 
            Process p = New((p_context & ProcessContext.EditorMask) != 0); 
            p.Start(p_context,p_flags,p_callback); 
            return p; 
        }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process New(ProcessContext p_context,ProcessAction p_callback) { return New(p_context,ProcessFlags.None,p_callback); }

        /// <summary>
        /// Returns a process instance adds its activity and starts it
        /// </summary>
        /// <returns></returns>
        static public Process New(ProcessAction p_callback,bool p_editor = false) { return New(p_editor ? ProcessContext.Editor : ProcessContext.Update,ProcessFlags.None,p_callback); }

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
        public int[] addresses;

        /// <summary>
        /// Returns a flag telling if this process is currently inside all target process unit
        /// </summary>
        public bool inUnit { 
            get {
                int  k  = 0;
                int  uc = 0;
                int  f  = 1;
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
        public bool outUnit {
            get {                             
                for (int i = 0;i < addresses.Length;i++) {
                    if (addresses[i] >= 0) return false;
                }                
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
            if (lv == null) lv = new Locals();
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

        /// <summary>
        /// Checks if a given flag is enabled
        /// </summary>
        /// <param name="p_mask"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetFlag(ProcessFlags p_mask) { return (flags & p_mask) != 0;  }

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
            //Mask out job related ones (handled differently)
            m = m & ~(ProcessFlags.JobMask);
            //Enable/Disable bit masks
            flags = p_value ? (f | m) : (f & (~m));            
        }

        /// <summary>
        /// Starts this process
        /// </summary>
        /// <param name="p_context"></param>
        /// <param name="p_flags"></param>
        /// <param name="p_activity"></param>
        /// <returns></returns>
        public bool Start(ProcessContext p_context,ProcessFlags p_flags,IActivity p_activity) {
            //Locals
            IActivity it = p_activity;
            //Assertion
            if (it == null) { Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Activity is <null>"); return false; }
            return InternalStart(p_context,p_flags,p_activity,null);
        }

        /// <summary>
        /// Starts this proces
        /// </summary>
        /// <param name="p_context"></param>
        /// <param name="p_activity"></param>
        public bool Start(ProcessContext p_context,IActivity p_activity) { return Start(p_context,ProcessFlags.None,p_activity); }

        /// <summary>
        /// Starts this proces
        /// </summary>
        /// <param name="p_context"></param>
        /// <param name="p_activity"></param>
        public bool Start(IActivity p_activity) { return Start(ProcessContext.Update,ProcessFlags.None,p_activity); }

        /// <summary>
        /// Starts activity in editor context
        /// </summary>
        /// <param name="p_activity"></param>
        public bool StartEditor(IActivity p_activity) {
            #if UNITY_EDITOR
            return Start(ProcessContext.Editor,ProcessFlags.None,p_activity);
            #else
            return false;
            #endif
        }

        /// <summary>
        /// Starts this process
        /// </summary>
        /// <param name="p_context"></param>
        /// <param name="p_flags"></param>
        /// <param name="p_callback"></param>
        /// <returns></returns>
        public bool Start(ProcessContext p_context,ProcessFlags p_flags,ProcessAction p_callback) {
            //Locals
            ProcessAction it = p_callback;
            //Assertion
            if (it == null) { Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Callback is <null>"); return false; }
            return InternalStart(p_context,p_flags,null,p_callback);
        }

        /// <summary>
        /// Starts this proces
        /// </summary>
        /// <param name="p_context"></param>
        /// <param name="p_activity"></param>
        public bool Start(ProcessContext p_context,ProcessAction p_callback) { return Start(p_context,ProcessFlags.None,p_callback); }

        /// <summary>
        /// Starts this proces
        /// </summary>
        /// <param name="p_context"></param>
        /// <param name="p_activity"></param>
        public bool Start(ProcessAction p_callback) { return Start(ProcessContext.Update,ProcessFlags.None,p_callback); }

        /// <summary>
        /// Starts activity in editor context
        /// </summary>
        /// <param name="p_activity"></param>
        public bool StartEditor(ProcessAction p_callback) {
            #if UNITY_EDITOR
            return Start(ProcessContext.Editor,ProcessFlags.None,p_callback);
            #else
            return false;
            #endif
        }

        /// <summary>
        /// Starts this process
        /// </summary>
        internal bool InternalStart(ProcessContext p_context,ProcessFlags p_flags,IActivity p_activity,ProcessAction p_callback) {            
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
                if(a.process != null) { /*Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Activity already in a process - {state}");*/ return false; }
                //Populate IActivity and other interfaces data
                //Set the activity process
                a.process = this;
                //Populate interfaces data
                lv.Set(this,a);
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
            //Schedule process to run
            if (manager) manager.AddProcess(this,false);
            return true;
        }

        /// <summary>
        /// Disposes this process and returns it to the pool
        /// </summary>
        public void Dispose() {
            if(manager)manager.RemoveProcess(this);
        }

        /// <summary>
        /// Set the state and notify 
        /// </summary>
        /// <param name="p_state"></param>
        internal void SetState(ProcessContext p_context,ProcessState p_state) {
            //if(state == p_state) return;
            state = p_state;
            if (activity == null) return;
            if (GetFlag(ProcessFlags.IProcess)) lv.p.OnProcessUpdate(p_context,state);
            if (GetFlag(ProcessFlags.IJobProcess)) {
                switch(state) {
                    case ProcessState.Start: break;
                    case ProcessState.Stop: {
                        //If 'deferred' and job ongoing, force completion                        
                        if (deferred)if (!lv.jh.IsCompleted) lv.jh.Complete();
                        lv.j.OnJobDispose();
                    }
                    break;
                }
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
                //If by chance 'activity' is null (maybe recompile) - try recovering
                if(activity==null) lv.Set(this,lv.c as IActivity);
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
                if (!res) { Dispose(); return; } 
            }
            //Update Job based ones
            if (GetFlag(ProcessFlags.IJobProcess))
            switch(ctx) {
                case ProcessContext.Update:
                case ProcessContext.LateUpdate:
                case ProcessContext.FixedUpdate: {                    
                    //Update allowed flag
                    bool can_update   = deferred ? lv.jh.IsCompleted : true;
                    //Deferred needs JobHandle to be 'Complete' after finishing
                    if(can_update) {
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

        
        
    }

}
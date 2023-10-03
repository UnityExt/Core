using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        /// Flag that tell its a delegate
        /// </summary>
        Delegate            = (1 << 9),
        /// <summary>
        /// Mask for all interfaces
        /// </summary>
        Interfaces = IUpdateable | ILateUpdateable | IFixedUpdateable | IThreadUpdateable | IProcess | Component | Delegate,
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

        #region static

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
            ProcessContext ctx = ProcessContext.None;
            if(p_editor) {
                if (p_activity is IUpdateable      ) ctx |= ProcessContext.Editor;
                if (p_activity is IThreadUpdateable) ctx |= ProcessContext.EditorThread;
            }
            else {
                if (p_activity is IUpdateable       ) ctx |= ProcessContext.Update;
                if (p_activity is ILateUpdateable   ) ctx |= ProcessContext.LateUpdate;
                if (p_activity is IFixedUpdateable  ) ctx |= ProcessContext.FixedUpdate;
                if (p_activity is IThreadUpdateable ) ctx |= ProcessContext.Thread;
            }
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
        public float deltatime;

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

        /// <summary>
        /// Flag that tells its the first tick.
        /// </summary>
        
        //Casting helpers
        [HideInInspector][SerializeField] private Component cst_c;
        private IUpdateable       cst_u;
        private ILateUpdateable   cst_lu;
        private IFixedUpdateable  cst_fu;
        private IThreadUpdateable cst_tu;
        private IProcess          cst_p;
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
            deltatime     = 0f;            
            activity      = null;            
            //Clear Flags
            flags = ProcessFlags.None;
            //Clear casts
            cst_c  = null;
            cst_u  = null;
            cst_lu = null;
            cst_fu = null;
            cst_tu = null;
            cst_p  = null;
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
            switch(p_context) {
                case ProcessContext.Update:
                case ProcessContext.LateUpdate:
                case ProcessContext.FixedUpdate:
                case ProcessContext.Thread: 
                    if(manager.isPlaying) break;
                    Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Can't Start Runtime Loops outside PlayMode!"); 
                    Clear(); 
                return false;                
            }            
            #endif
            //Locals
            IActivity     a  = p_activity;
            ProcessAction cb = p_callback;
            //Debug.Log($"Process> START - {state} - {a} @ [{a?.process?.pid}] [{pid}]");
            if(state != ProcessState.Idle) { Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Invalid State to Start - {state}"); return false; }
            bool has_handlers = (a != null) || (cb != null);
            if (!has_handlers) return false;
            
            if(a != null) {
                if(a.process != null) { /*Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Activity already in a process - {state}");*/ return false; }
                //Set flags
                SetActivity(a);
                //Assertion when Unity Component
                if (GetFlag(ProcessFlags.Component)) if (cst_c == null) { Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Activity as Component is <null>"); return false; }
                //Set the activity process
                a.process = this;
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
        /// Helper to set activity instance and all flags
        /// </summary>
        /// <param name="p_activity"></param>
        internal void SetActivity(IActivity p_activity) {
            //Clear Flags
            flags = ProcessFlags.None;
            //Clear casts
            cst_u  = null;
            cst_lu = null;
            cst_fu = null;
            cst_tu = null;
            cst_p  = null;
            //Locals
            IActivity it = p_activity;
            //Assertion
            if (it == null) return;
            //Flag bit mask
            bool is_component = it is Component;            
            //If component store its reference (serialization)
            cst_c = is_component ? it as Component : null;
            //Ignore invalids (unity null check)
            if (is_component) if (!cst_c) return;
            //Mark bit flag
            if (is_component) flags |= ProcessFlags.Component;
            //Store reference
            activity = it;            
            //Casting shortcuts
            if (it is IUpdateable       )  { flags |= ProcessFlags.IUpdateable;        cst_u  = it as IUpdateable;       }
            if (it is ILateUpdateable   )  { flags |= ProcessFlags.ILateUpdateable;    cst_lu = it as ILateUpdateable;   }
            if (it is IFixedUpdateable  )  { flags |= ProcessFlags.IFixedUpdateable;   cst_fu = it as IFixedUpdateable;  }      
            if (it is IThreadUpdateable )  { flags |= ProcessFlags.IThreadUpdateable;  cst_tu = it as IThreadUpdateable; }
            if (it is IProcess          )  { flags |= ProcessFlags.IProcess;           cst_p  = it as IProcess;          }
        }

        /// <summary>
        /// Set the state and notify 
        /// </summary>
        /// <param name="p_state"></param>
        internal void SetState(ProcessContext p_context,ProcessState p_state) {
            //if(state == p_state) return;
            state = p_state;
            if (activity != null) if (GetFlag(ProcessFlags.IProcess)) cst_p.OnProcessUpdate(p_context,state);
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
            deltatime = m_dt_lut[n];
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
                if (!cst_c) { Dispose(); return; }
                //If by chance 'activity' is null (maybe recompile) - try recovering
                if(activity==null) SetActivity(cst_c as IActivity);
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
            if (GetFlag(ProcessFlags.IProcess)) cst_p.OnProcessUpdate(ctx,state);
            //Update loop specific ones            
            switch (ctx) {
                case ProcessContext.Editor:
                case ProcessContext.Update:         if(GetFlag(ProcessFlags.IUpdateable       )) cst_u .OnUpdate      (); break;                
                case ProcessContext.LateUpdate:     if(GetFlag(ProcessFlags.ILateUpdateable   )) cst_lu.OnLateUpdate  (); break;
                case ProcessContext.FixedUpdate:    if(GetFlag(ProcessFlags.IFixedUpdateable  )) cst_fu.OnFixedUpdate (); break;
                case ProcessContext.EditorThread:
                case ProcessContext.Thread:         if(GetFlag(ProcessFlags.IThreadUpdateable )) cst_tu.OnThreadUpdate(); break;
            }
            if (GetFlag(ProcessFlags.Delegate)) { bool res = callback(p_context,this); if(!res) Dispose(); }

        }

        
        
    }

}
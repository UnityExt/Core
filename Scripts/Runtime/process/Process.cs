using System.Collections;
using System.Collections.Generic;
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
        /// Invalid State
        /// </summary>
        Invalid=0,
        /// <summary>
        /// Idle -> Waiting for process follow up
        /// </summary>
        Idle,
        /// <summary>
        /// Process is ready for use
        /// </summary>
        Ready,
        /// <summary>
        /// Process Added into execution pool
        /// </summary>
        Added,
        /// <summary>
        /// Process is running
        /// </summary>
        Run,
        /// <summary>
        /// Process removed from pool
        /// </summary>
        Removed
        
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
    }
    #endregion

    /// <summary>
    /// Class that describes a process structure, containing one or more process interfaces to run inside its context.
    /// </summary>
    [System.Serializable]
    public class Process {

        #region static

        /// <summary>
        /// CTOR.
        /// </summary>
        static Process() {
            m_rnd = new System.Random();
        }

        /// <summary>
        /// Internals
        /// </summary>
        static private System.Random m_rnd;
        
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
        /// Executing Context
        /// </summary>
        public ProcessContext context { get { return m_context; } private set { m_context = value; } }
        [SerializeField] private ProcessContext m_context;
        
        /// <summary>
        /// Executing state
        /// </summary>
        //public ProcessState state;

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
        public bool useTimeScale = false;

        /// <summary>
        /// Flag that tells this process runs in deferred mode
        /// After the core time slice ends, it will run in the next frame
        /// </summary>
        public bool deferred;

        /// <summary>
        /// Reference to the running activity.
        /// </summary>
        public IActivity activity;

        /// <summary>
        /// List of child processes that are components and can be serialized to survive recompilation
        /// </summary>
        [HideInInspector][SerializeField] private Component m_activity_c;

        /// <summary>
        /// Flag that tells its the first tick.
        /// </summary>
        private bool  m_first_tick;
        private float m_last_time;
        [HideInInspector][SerializeField]private bool m_ua_cast ;
        [HideInInspector][SerializeField]private bool m_lua_cast;
        [HideInInspector][SerializeField]private bool m_aua_cast;
        [HideInInspector][SerializeField]private bool m_ta_cast;
        [HideInInspector][SerializeField]private bool m_fua_cast;
        [HideInInspector][SerializeField]private bool m_pa_cast ;


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
            name      = "";
            //state     = ProcessState.Ready;
            context   = ProcessContext.None;
            time      = 0f;
            deltatime = 0f;
            activity     = null;
            m_activity_c = null;
            m_ua_cast    = false;
            m_lua_cast   = false;
            m_aua_cast   = false;
            m_ta_cast    = false;
            m_fua_cast   = false;
            m_pa_cast    = false;
            ClearAddresses();
        }

        internal void ClearAddresses() {
            int ac = addresses==null ? 0 : addresses.Length;
            if(ac<6) addresses = new int[] { -1,-1,-1,-1,-1,-1 };
            for (int i = 0;i < addresses.Length;i++) addresses[i] = -1;
        }

        /// <summary>
        /// Starts this process
        /// </summary>
        public void Start(ProcessContext p_context,IActivity p_activity) {

            switch(p_context) {
                case ProcessContext.Update:
                case ProcessContext.LateUpdate:
                case ProcessContext.FixedUpdate:
                case ProcessContext.Thread: 
                    if(!manager.isPlaying) { Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Can't Start Runtime Loops outside PlayMode!"); Clear(); return; }
                break;
            }

            IActivity it = p_activity;
            //Ignore invalid
            if (it == null) { Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Invalid Activity"); return; }
            bool is_component = it is Component;
            Component ac = is_component ? it as Component : null;
            //Ignore invalids (unity null check)
            if (is_component) if (!ac) { Debug.LogWarning($"Process> Start 0x{pid.ToString("x")} / Invalid Activity"); return; }
            //Store reference
            activity = it;
            //Casting shortcuts
            if (it is IUpdateable)        m_ua_cast  = it is IUpdateable      ;
            if (it is ILateUpdateable)    m_lua_cast = it is ILateUpdateable  ;
            if (it is IFixedUpdateable)   m_fua_cast = it is IFixedUpdateable ;
            if (it is IAsyncUpdateable) { m_aua_cast = it is IAsyncUpdateable ; deferred = true; }
            if (it is IThreadUpdateable)  m_ta_cast  = it is IThreadUpdateable;
            if (it is IProcessActivity)   m_pa_cast  = it is IProcessActivity ;
            //No context initially
            context = p_context;
            //Update ProcessActivity
            //if (m_pa_cast) { ((IProcessActivity)it).OnProcessState(ProcessState.Ready); }
            //Schedule execution in the desired context
            manager.Add(this);
        }

        /// <summary>
        /// Stops this process
        /// </summary>
        public void Stop() {
            manager.Remove(this);
            Clear();            
        }

        /// <summary>
        /// Updates the internal clocking.
        /// </summary>
        /// <param name="p_time"></param>
        internal void SetTime(float p_time) {
            deltatime = Mathf.Max(0f,p_time - m_last_time);
            m_last_time = p_time;                        
            if (m_first_tick) { deltatime = 0f; m_first_tick = false; }
            time += deltatime;
        }

        /// <summary>
        /// Execute process iterations
        /// </summary>
        internal void Step(ProcessContext p_context) {
            //Locals
            ProcessContext ctx = p_context;
            //Iterate children                    
            IActivity it = activity;
            //Ignore invalid
            if (it == null) { /*DISPOSE*/ return; }
            Component it_c = it is Component ? it as Component : null;
            //Ignore null components
            if (!it_c) { /*DISPOSE*/ return; }

            //If 'state' is 'running' and 'completed' return to process 'complete' state
            //if (state == ProcessState.Run) if (it.completed) { state = ProcessState.Idle; /*DISPOSE*/ return; }

            //Update ProcessActivity
            //if (m_pa_cast) { ((IProcessActivity)it).OnProcessState(state); }

            //Update basic activity
            it.OnStep(ctx);
            //Update loop specific ones
            switch (ctx) {
                case ProcessContext.Editor:
                case ProcessContext.Update: if (m_ua_cast) ((IUpdateable)it).OnUpdate(); break;
                case ProcessContext.EditorThread:
                case ProcessContext.Thread: if (m_ta_cast) ((IThreadUpdateable)it).OnThreadUpdate(); break;
                case ProcessContext.LateUpdate: if (m_lua_cast) ((ILateUpdateable)it).OnLateUpdate(); break;
                case ProcessContext.FixedUpdate: if (m_fua_cast) ((IFixedUpdateable)it).OnFixedUpdate(); break;
            }

        }

        
        
    }

}
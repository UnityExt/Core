using System.Collections;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEditor;
using UnityEngine;
using static UnityExt.Sys.ProcessManager;
using UnityExt.Core;
using System.Threading;
using static UnityEngine.UI.CanvasScaler;

namespace UnityExt.Sys {

    public class BasicActivity : IActivity {
        /// <summary>
        /// Reference to the process
        /// </summary>
        public Process process { get; set; }

        /// <summary>
        /// Flag that tells if the activity completed
        /// </summary>
        public bool completed { get; set; }

        public float elapsedEditor = 0f;

        public float elapsedUpdate = 0f;

        /// <summary>
        /// Execution Loop
        /// </summary>        
        public void OnStep(ProcessContext p_context) {
            
            switch(p_context) {
                case ProcessContext.Editor: elapsedEditor += process.deltatime; break;
                case ProcessContext.Update: elapsedUpdate += process.deltatime; break;
            }

        }
    }

    /// <summary>
    /// Class that implements the creation and execution of process instances.
    /// </summary>
    [ExecuteInEditMode]
    public class ProcessManager : ScriptableObject {

        #region class Runner
        /// <summary>
        /// Interna class to proxy unity callbacks into the manager system
        /// </summary>
        internal class Runner : MonoBehaviour {

            /// <summary>
            /// Reference to the process manager.
            /// </summary>
            internal ProcessManager manager;

            /// <summary>
            /// Internal loops
            /// </summary>
            internal void Update     () { manager.Step(ProcessContext.Update     ,-1); }
            internal void LateUpdate () { manager.Step(ProcessContext.LateUpdate ,-1); }
            internal void FixedUpdate() { manager.Step(ProcessContext.FixedUpdate,-1); }

        }
        #endregion

        /// <summary>
        /// Reference to the global process manager instance.
        /// </summary>
        static public ProcessManager instance { get { return m_instance ? m_instance : (m_instance = Resources.Load<ProcessManager>("ProcessManager")); } }
        static private ProcessManager m_instance;

        /// <summary>
        /// Process pool.
        /// </summary>
        public Process[] process;

        /// <summary>
        /// Reference to the non-engine clock.
        /// </summary>
        public Stopwatch clock;

        /// <summary>
        /// Current timestamp upon clock start
        /// </summary>
        public int clockStart;

        /// <summary>
        /// Flag that tells if the app/editor is in playmode
        /// </summary>
        public bool isPlaying;

        /// <summary>
        /// Returns the elapsed seconds since realtime startup
        /// </summary>
        public float clockTime {
            get {
                double cms  = (double)clock.ElapsedMilliseconds;
                double cms0 = (double)clockStart;
                return (float)((cms0 + cms) * 0.001);
            }
        }

        /// <summary>
        /// List of available threads.
        /// </summary>
        public List<Thread> threads;

        /// <summary>
        /// Number of running threads
        /// </summary>
        public int maxThreads = 6;

        /// <summary>
        /// List of units
        /// </summary>
        public List<ProcessUnit> units;

        /// <summary>
        /// Internals
        /// </summary>
        private Runner m_runner;
        private bool m_runner_exist;
        
        /// <summary>
        /// CTOR.
        /// </summary>
        internal void Awake() {
            //If clock instance is null create and start it
            if (clock == null) { clock = new Stopwatch(); clock.Start(); }
            AssertInternal(ProcessContext.Update);
        }

        /// <summary>
        /// Helper to match process context to an ID
        /// </summary>
        /// <param name="p_context"></param>
        /// <returns></returns>
        internal int GetUnitId(ProcessContext p_context) {
            switch(p_context) {
                case ProcessContext.Update:       return 0;
                case ProcessContext.LateUpdate:   return 1;
                case ProcessContext.FixedUpdate:  return 2;
                case ProcessContext.Thread:       return 3;
                case ProcessContext.Editor:       return 4;
                case ProcessContext.EditorThread: return 5;
            }
            return -1;
        }

        /// <summary>
        /// Executes the loop in the chosen process context.
        /// </summary>
        /// <param name="p_context"></param>
        internal void Step(ProcessContext p_context,int p_thread_index) {
            //Assert the runner creation
            //AssertInternal(p_context);
            //Capture is-playing flag            
            switch(p_context) {
                case ProcessContext.Update:
                case ProcessContext.Editor: isPlaying = Application.isPlaying; break;
            }

            int unit_idx = GetUnitId(p_context);
            
            if(unit_idx<units.Count)
            if(unit_idx>=0) {
                ProcessUnit pu = units[unit_idx];
                int o = p_thread_index < 0 ? 0 : p_thread_index;
                int c = p_thread_index < 0 ? 1 : maxThreads;
                pu.Step(o,c);
            }            

        }

        /// <summary>
        /// Handler for editor loops
        /// </summary>
        private void EditorStep() { Step(ProcessContext.Editor,-1); }

        private void EditorPlayModeChange(PlayModeStateChange p_state) {

        }

        /// <summary>
        /// Thread looping method
        /// </summary>
        /// <param name="p_args"></param>
        private void ThreadStep(object p_args) {
            int thd = (int)p_args;
            while (true) {
                #if UNITY_EDITOR
                Step(ProcessContext.EditorThread,thd);
                #endif
                Step(ProcessContext.Thread      ,thd); 
                Thread.Sleep(0); 
            }
        }

        /// <summary>
        /// Schedule a process for running
        /// </summary>
        /// <param name="p_process"></param>
        /// <param name="p_context"></param>
        internal void Add(Process p_process) {
            int unit_idx = GetUnitId(p_process.context);
            if (unit_idx < 0) return;
            units[unit_idx].Add(p_process.pid,true);
        }

        internal void Remove(Process p_process) {
            int unit_idx = GetUnitId(p_process.context);
            if (unit_idx < 0) return;
            units[unit_idx].Remove(p_process.pid,true);
        }

        protected void OnEnable() {

            //Store playmode flag
            bool is_playing = Application.isPlaying;

            //Debug.Log($"OnEnable / is-playing[{is_playing}]");

            //If clock instance is null create and start it
            if (clock == null) { 
                clock = new Stopwatch();
                clockStart = (int)(Time.realtimeSinceStartup*1000f);
                clock.Start(); 
            }

            #if UNITY_EDITOR
            //Refresh editor event handlers
            UnityEditor.EditorApplication.update -= EditorStep;
            UnityEditor.EditorApplication.update += EditorStep;
            UnityEditor.EditorApplication.playModeStateChanged -= EditorPlayModeChange;
            UnityEditor.EditorApplication.playModeStateChanged += EditorPlayModeChange;
            #endif     

            //Reset units
            for (int i = 0;i < units.Count;i++) {
                units[i].manager = this;
                units[i].Init();
            }

            //Iterate processes for init
            for (int i = 0;i < process.Length;i++) { 
                Process it = process[i];
                it.pid     = i;
                it.manager = this;
                //Only clear addresses to redistribute process in units
                it.ClearAddresses();
                //Flag that tells this process is editor bound
                bool is_editor = (it.context == ProcessContext.Editor) || (it.context == ProcessContext.EditorThread);

                bool will_add   = is_editor ? true  :  is_playing;
                bool will_clear = is_editor ? false : !is_playing;

                //Debug.Log($"Process [{it.name}] {it.pid} context[{it.context}] add[{will_add}] clear[{will_clear}]");

                if(will_add)   { Add(it);    }
                if(will_clear) { it.Clear(); }

            }

            /*
            Process p;

            for(int i=0;i<4;i++) {
                p = process[i];
                p.Stop();
                p.name = $"basic-activity-{p.pid}";
                ProcessContext ctx = ProcessContext.Editor;
                switch(i) {
                    case 0: ctx = ProcessContext.Editor; break;
                    case 1: ctx = ProcessContext.EditorThread; break;
                    case 2: ctx = ProcessContext.Update; break;
                    case 3: ctx = ProcessContext.LateUpdate; break;                    
                }
                p.Start(ctx,new BasicActivity());
            }
            //*/
            


        }

        protected void OnDisable() {            
            #if UNITY_EDITOR
            //Refresh editor update loop handler
            UnityEditor.EditorApplication.update -= EditorStep;
            UnityEditor.EditorApplication.update += EditorStep;
            UnityEditor.EditorApplication.playModeStateChanged -= EditorPlayModeChange;
            UnityEditor.EditorApplication.playModeStateChanged += EditorPlayModeChange;
            #endif
        }

        protected void OnValidate() {
            //Debug.Log("OnValidate");
            //If threads list available kill them all
            if (threads != null) for(int i = 0;i < threads.Count;i++) threads[i].Abort();
            //Create new list
            threads = new List<Thread>();
            //Create threads
            for (int i = 0;i < maxThreads;i++) {
                Thread thd = new Thread(ThreadStep);
                thd.Name = $"thread-{i.ToString("000")}";
                thd.Start(i);
                threads.Add(thd);
            }
        }

        /// <summary>
        /// Check the need of instantiate the runner.
        /// </summary>
        /// <param name="p_context"></param>
        private void AssertInternal(ProcessContext p_context) {
            //Ignore non mono loops
            switch(p_context) {
                case ProcessContext.EditorThread: return;
                case ProcessContext.Thread:       return;
                case ProcessContext.None:         return;                
            }            
            //Check if playmode
            if (!Application.isPlaying) { m_runner_exist = false; return; }
            //Check if 'runner' is already created
            if (m_runner_exist) return;
            //Create and cache 'runner'            
            GameObject go = new GameObject("@process",typeof(Runner));
            Runner go_r = go.GetComponent<Runner>();
            m_runner = go_r;
            go_r.manager = this;
            DontDestroyOnLoad(go);
            m_runner_exist = true;
        }

    }

}
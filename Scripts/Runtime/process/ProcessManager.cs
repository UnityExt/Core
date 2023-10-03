using System.Collections;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEditor;
using UnityEngine;
using static UnityExt.Sys.ProcessManager;
using UnityExt.Core;
using System.Threading;
using static UnityEngine.UI.CanvasScaler;
using System;
using System.Security.Cryptography;

namespace UnityExt.Sys {

    /// <summary>
    /// Class that implements the creation and execution of process instances.
    /// </summary>
    [ExecuteInEditMode]
    public class ProcessManager : ScriptableObject {

        #region class Runner
        /// <summary>
        /// Internal class to proxy unity callbacks into the manager system
        /// </summary>
        internal class Runner : MonoBehaviour {

            /// <summary>
            /// Reference to the process manager.
            /// </summary>
            public ProcessManager manager;

            /// <summary>
            /// Internal loops
            /// </summary>            
            internal void Awake() { DontDestroyOnLoad(gameObject); }
            internal void Update     () { if(manager)manager.UpdateUnitContext(ProcessContext.Update     ,-1); }
            internal void LateUpdate () { if(manager)manager.UpdateUnitContext(ProcessContext.LateUpdate ,-1); }
            internal void FixedUpdate() { if(manager)manager.UpdateUnitContext(ProcessContext.FixedUpdate,-1); }

        }
        #endregion

        /// <summary>
        /// Reference to the global process manager instance.
        /// </summary>
        static internal ProcessManager instance { get { return m_instance ? m_instance : (m_instance = Resources.Load<ProcessManager>("ProcessManager")); } }
        static private ProcessManager m_instance;

        /// <summary>
        /// Runtime Process Pool.
        /// </summary>        
        public Process[] runtimeProcess;

        /// <summary>
        /// Runtime processes stack for fetching
        /// </summary>
        public List<int> runtimeStack;

        /// <summary>
        /// Indexer for fetching processes from top
        /// </summary>
        public int runtimeStackTop;

        /// <summary>
        /// Editor Process Pool.
        /// </summary>
        public Process[] editorProcess;

        /// <summary>
        /// Runtime processes stack for fetching
        /// </summary>
        public List<int> editorStack;

        /// <summary>
        /// Indexer for fetching processes from top
        /// </summary>
        public int editorStackTop;

        /// <summary>
        /// Flag that tells if the app/editor is in playmode
        /// </summary>
        public bool isPlaying;

        /// <summary>
        /// Flag that tells if the app/editor is paused or not
        /// </summary>
        public bool isPaused;

        /// <summary>
        /// Flag that tells if the editor is compiling
        /// </summary>
        public bool isCompiling;

        /// <summary>
        /// List of available threads.
        /// </summary>
        public List<Thread> threads;

        /// <summary>
        /// Number of threads proportional to core count
        /// </summary>
        [Range(0f,2f)]
        public float threadCoreRatio = 0.5f;

        /// <summary>
        /// Resulting max number of threads.
        /// </summary>
        public int maxThreads;

        /// <summary>
        /// List of units
        /// </summary>
        public List<ProcessUnit> units;

        /// <summary>
        /// Internals
        /// </summary>
        private Runner m_runner;
        private bool m_runner_exist;
        private bool m_thread_kill;
        
        /// <summary>
        /// CTOR.
        /// </summary>
        internal void Awake() {
            //Assert runner and other runtime steps
            m_runner_exist = false;
            AssertRuntime();
        }

        /// <summary>
        /// Pushes a process back after completion
        /// </summary>
        /// <param name="p_process"></param>
        internal void PushProcess(Process p_process) {            
            Process p = p_process;
            if (p == null) return;
            //Lastly clear the process reference
            if (p.activity != null) p.activity.process = null;
            bool is_editor = false;            
            if ((p.context & ProcessContext.EditorMask) != 0) is_editor = true;
            p.Clear();            
            if (is_editor) {
                lock (editorStack) {
                    PushStack(editorStack,ref editorStackTop,p.pid);                    
                }
            }
            else {
                lock (runtimeStack) {
                    PushStack(runtimeStack,ref runtimeStackTop,p.pid);                    
                }
            }            
        }

        /// <summary>
        /// Pops a process from the stack
        /// </summary>
        /// <param name="p_context"></param>
        /// <returns></returns>
        internal Process PopProcess(ProcessContext p_context) {
            int pi = -1;
            Process[] pp = null;
            bool is_editor = false;
            if ((p_context & ProcessContext.EditorMask) != 0) is_editor = true;            
            if(is_editor) {
                lock (editorStack) {
                    pp = editorProcess;
                    pi = PopStack(editorStack,ref editorStackTop);                    
                }
            }
            else {
                lock (runtimeStack) {
                    pp = runtimeProcess;
                    pi = PopStack(runtimeStack,ref runtimeStackTop);                    
                }
            }
            if (pi < 0) return null;            
            return pp[pi];
        }

        /// <summary>
        /// Helper for stack op
        /// </summary>
        private int  PopStack (List<int> s,ref int t) { int v = s[t]; s[t++] = -1; return v;  }
        private void PushStack(List<int> s,ref int t,int v) { if (t <= 0) return; s[--t] = v; }

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
        internal void UpdateUnitContext(ProcessContext p_context,int p_thread_index) {                        
            //Fetch unit idx
            int unit_idx = GetUnitId(p_context);
            if (unit_idx < 0) return;
            if (unit_idx >= units.Count) return;            
            //Fetch ProcessUnit
            ProcessUnit pu = units[unit_idx];            
            //Flag telling if its threaded or not
            bool is_thread = p_thread_index >= 0;
            //Offset and Count for iteration of N threads
            //Otherwise steps fom 0 and 1 by 1
            int o = !is_thread ? 0 : p_thread_index;
            int c = !is_thread ? 1 : maxThreads;                
            //Step execute all processes associated with unit
            pu.Execute(o,c);            
        }

        /// <summary>
        /// Handler for editor loops
        /// </summary>
        private void UpdateEditor() {
            //Keep storing the last clock sample
            isCompiling = EditorApplication.isCompiling;            
            //Step Editor Update context
            UpdateUnitContext(ProcessContext.Editor,-1); 
        }

        /// <summary>
        /// Thread looping method
        /// </summary>
        /// <param name="p_args"></param>
        private void UpdateThread(object p_args) {
            int thd = (int)p_args;
            //Allows some editor bound flags to update
            Thread.Sleep(500);
            while (true) {
                //If master kill or compile exit loop
                if (m_thread_kill || isCompiling) break;
                //Yield CPU
                Thread.Sleep(0);
                #if UNITY_EDITOR
                //Run Editor Step
                UpdateUnitContext(ProcessContext.EditorThread,thd);
                #endif
                //Skip is Paused
                if( isPaused)  continue;
                if(!isPlaying) continue;
                //Run Runtime Step
                UpdateUnitContext(ProcessContext.Thread,thd);
            }
        }

        /// <summary>
        /// Schedule a process for running
        /// </summary>
        /// <param name="p_process"></param>
        /// <param name="p_context"></param>
        internal void AddProcess(Process p_process,bool p_force) {
            Process p = p_process;
            if (p == null) return;
            if (p.state == ProcessState.Add) return;
            //Debug.Log($"Process> {p.activity} ADD   - {p.state} @ [{p.activity?.process?.pid}] [{p.pid}]");
            for (int i=0;i<units.Count;i++) {
                ProcessUnit it = units[i];
                if ((it.context & p.context) == 0) continue;
                it.Add(p_process,p_force);
            }
        }

        /// <summary>
        /// Schedule a process for removal
        /// </summary>
        /// <param name="p_process"></param>
        internal void RemoveProcess(Process p_process) {
            Process p = p_process;
            if (p == null) return;
            if (p.state == ProcessState.Remove) return;
            //Debug.Log($"Process> {p.activity} REMOVE - {p.state} @ [{p.activity?.process?.pid}] [{p.pid}]");
            for (int i = 0;i < units.Count;i++) {
                ProcessUnit it = units[i];
                if ((it.context & p.context) == 0) continue;
                it.Remove(p_process);
            }
        }

        /// <summary>
        /// Handler for playmode change
        /// </summary>
        /// <param name="p_state"></param>
        private void EditorPlayModeChange(PlayModeStateChange p_state) {            
            switch (p_state) {
                case PlayModeStateChange.ExitingEditMode: { isPlaying = true ; } break;
                case PlayModeStateChange.ExitingPlayMode: { 
                    isPlaying = false; 
                    ValidateRuntimeProcess();
                    ValidateRuntimeStack();
                }
                break;
                case PlayModeStateChange.EnteredPlayMode: {
                    m_runner_exist = false;
                    AssertRuntime(true);
                }
                break;
            }
        }

        /// <summary>
        /// Handler for pause change
        /// </summary>
        /// <param name="p_state"></param>
        private void EditorPauseChange(PauseState p_state) {
            isPaused = p_state == PauseState.Paused;   
            for(int i=0;i<units.Count;i++) {
                ProcessUnit it = units[i];
                it.SetPause(isPaused);
            }
        }

        /// <summary>
        /// CTOR.
        /// </summary>
        protected void OnEnable() {
            Debug.Log($"ON ENABLE play: {isPlaying} | compile: {isCompiling}");
            
            #if UNITY_EDITOR
            //Refresh editor event handlers
            UnityEditor.EditorApplication.update -= UpdateEditor;
            UnityEditor.EditorApplication.update += UpdateEditor;
            UnityEditor.EditorApplication.playModeStateChanged -= EditorPlayModeChange;
            UnityEditor.EditorApplication.playModeStateChanged += EditorPlayModeChange;
            UnityEditor.EditorApplication.pauseStateChanged -= EditorPauseChange;
            UnityEditor.EditorApplication.pauseStateChanged += EditorPauseChange;
            #endif

            //Reset units
            for (int i = 0;i < units.Count;i++) {
                units[i].manager = this;
                units[i].Boot();
            }
            //Validate Runtime Processes
            ValidateRuntimeProcess();
            ValidateEditorProcess();

        }

        private void ValidateRuntimeProcess() {
            Process[] pp;
            //Iterate Runtime Processes for init
            pp = runtimeProcess;
            for (int i = 0;i < pp.Length;i++) {
                Process it = pp[i];
                it.pid = i;
                it.manager = this;
                //Clear address in case a fresh Process Unit will happen
                it.ClearUnitData();
                //If runtime and playmode re-add otherwise clear
                if (isPlaying) { AddProcess(it,true); } else { it.Clear(); }
            }
        }

        private void ValidateEditorProcess() {
            Process[] pp;
            //Iterate Editor Processes for init
            pp = editorProcess;
            for (int i = 0;i < pp.Length;i++) {
                Process it = pp[i];
                it.pid = i;
                it.manager = this;
                //Clear address in case a fresh Process Unit will happen
                it.ClearUnitData();
                //Editor Process always keep running
                AddProcess(it,true);
            }
        }

        /// <summary>
        /// Disable Callback
        /// </summary>
        protected void OnDisable() {      
            Debug.Log($"ON DISABLE play: {isPlaying} | compile: {isCompiling}");
            #if UNITY_EDITOR
            //Refresh editor update loop handler
            UnityEditor.EditorApplication.update -= UpdateEditor;
            UnityEditor.EditorApplication.update += UpdateEditor;
            UnityEditor.EditorApplication.playModeStateChanged -= EditorPlayModeChange;
            UnityEditor.EditorApplication.playModeStateChanged += EditorPlayModeChange;
            UnityEditor.EditorApplication.pauseStateChanged -= EditorPauseChange;
            UnityEditor.EditorApplication.pauseStateChanged += EditorPauseChange;            
            #endif
        }

        /// <summary>
        /// Validate callback called upon compile or inspector changes
        /// </summary>
        protected void OnValidate() {
            Debug.Log($"VALIDATE play: {isPlaying} | compile: {isCompiling}");
            //If threads list available kill them all
            m_thread_kill = true;
            if (threads != null) 
            for (int i = 0;i < threads.Count;i++) {                                
                threads[i].Abort();
            }
            //Uncheck the kill flag
            m_thread_kill = false;
            //Create new list
            threads = new List<Thread>();
            //Adjust number of threads based on core count
            int cpu_count = Mathf.Max(1,Environment.ProcessorCount);
            maxThreads = threadCoreRatio <= 0f ? 0 : Mathf.Max(1,Mathf.RoundToInt(((float)cpu_count) * threadCoreRatio));
            //Create threads
            for (int i = 0;i < maxThreads;i++) {
                Thread thd = new Thread(UpdateThread);
                thd.Name = $"process-thread-{i.ToString("000")}";
                thd.Start(i);
                threads.Add(thd);
            }
            //Locals
            List<int> stk;
            Process[] pp;
            ValidateRuntimeStack();

            //Sync Runtime index stack
            stk = editorStack;
            pp = editorProcess;
            while (stk.Count < pp.Length) stk.Add(0);
            while (stk.Count > pp.Length) stk.RemoveAt(stk.Count - 1);
            //Init its indexes in order
            for (int i = 0;i < stk.Count;i++) stk[i] = i;
            //Remove active processes from runtime            
            for (int i = 0;i < pp.Length;i++) if (pp[i].context != ProcessContext.None) { stk[pp[i].pid] = -1; }
            //Sort the stack to '-1' goes on top
            stk.Sort();
            //Reset top iterator with at the number of active processes
            editorStackTop = 0;
            for (int i = 0;i < stk.Count;i++) if (stk[i] < 0) editorStackTop++; else break;
        }

        private void ValidateRuntimeStack() {
            //Locals
            List<int> stk;
            Process[] pp;
            //Sync Runtime index stack            
            stk = runtimeStack;
            pp = runtimeProcess;
            while (stk.Count < pp.Length) stk.Add(0);
            while (stk.Count > pp.Length) stk.RemoveAt(stk.Count - 1);
            //Init its indexes in order
            for (int i = 0;i < stk.Count;i++) stk[i] = i;
            //Remove active processes from runtime (to restore in case of compile)
            if (isCompiling)
                for (int i = 0;i < pp.Length;i++) if (pp[i].context != ProcessContext.None) { stk[pp[i].pid] = -1; }
            //Sort the stack to '-1' goes on top
            stk.Sort();
            //Reset top iterator
            runtimeStackTop = 0;
            //Recount based on number of active processes found
            if (isCompiling)
            for (int i = 0;i < stk.Count;i++) if (stk[i] < 0) runtimeStackTop++; else break;
        }

        /// <summary>
        /// Check the need of instantiate the runner.
        /// </summary>
        /// <param name="p_context"></param>
        private void AssertRuntime(bool p_force=false) {
            isPlaying = Application.isPlaying;
            if (m_runner_exist) return;
            if(!p_force)if (!isPlaying) { m_runner_exist = false; return; }

            //Create and cache 'runner'            
            GameObject go = new GameObject("@process",typeof(Runner));
            Runner go_r = go.GetComponent<Runner>();
            m_runner = go_r;
            go_r.manager = this;            
            m_runner_exist = true;
        }

    }

}
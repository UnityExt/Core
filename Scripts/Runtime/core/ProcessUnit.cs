using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using Stopwatch = System.Diagnostics.Stopwatch;


namespace UnityExt.Core {

    /// <summary>
    /// Class that represents a mimicry of CPU where processes are queued and added for execution.
    /// It tries to leverage a fast algorithm to add / remove nodes from execution to be thread safe but without killing performance
    /// </summary>
    [System.Serializable]
    public class ProcessUnit : ScriptableObject {

        #region class Memory
        /// <summary>
        /// Emulation of a memory system that allocates process pids as quick as possible for execution.
        /// But also allow defragging invalid slots to condense the execution pool
        /// </summary>
        [System.Serializable]
        public class Memory {

            /// <summary>
            /// Reference to the parent unit.
            /// </summary>
            public ProcessUnit unit;

            /// <summary>
            /// List of values
            /// </summary>
            public List<int> address;

            /// <summary>
            /// Invalid value.
            /// </summary>
            public int invalid = -1;

            /// <summary>
            /// List of added values.
            /// </summary>
            public int count;

            /// <summary>
            /// Index of the last available position
            /// </summary>
            public int pointer;

            /// <summary>
            /// CTOR.
            /// </summary>
            public Memory() {
                address  = new List<int>();                
                pointer  = 0;
                count    = 0;
            }

            /// <summary>
            /// Resizes this list.
            /// </summary>
            /// <param name="p_length"></param>
            public void Resize(int p_length) {
                address.Clear();
                InternalClear(p_length,true);
            }

            /// <summary>
            /// Clears all values.
            /// </summary>
            public void Clear() { InternalClear(address.Count,false); }

            /// <summary>
            /// Clear method for resize and Clear
            /// </summary>
            /// <param name="p_add"></param>
            private void InternalClear(int p_count,bool p_add) {
                for (int i = 0;i < p_count;i++) { if (p_add) address.Add(invalid); else address[i] = invalid; }
                count   = 0;
                pointer = 0;                
            }

            /// <summary>
            /// Adds a value and returns its index in the sparse list.
            /// </summary>
            /// <param name="p_value"></param>
            /// <returns></returns>
            public void Add(Process p_process) {
                //lock (address) {
                    //Locals
                    Process p = p_process;
                    //Range Check
                    if (count >= address.Count) return;
                    //Even so return in case GC fails
                    if (pointer >= address.Count) { Debug.LogError($"ProcessUnit> Failed to add Process at [{unit.context}] / No addresses available!"); return; }
                    //Check if value is occupied                
                    if (address[pointer] != invalid) return;
                    //Assign process to empty slot
                    address[pointer] = p.pid;
                    //Set its address                
                    p.addresses[unit.id] = pointer;
                    //Increment count
                    count++;
                    //Debug.Log($"ProcessUnit.Memory> Add / process[{p.pid} @ {tail}] count[{count}]");
                    //Increment next pointer
                    pointer = Mathf.Min(pointer + 1,address.Count);
                //}
            }

            

            /// <summary>
            /// Removes a value by set to 'invalid' in the desired location for later 'defrag' adjust all gaps
            /// </summary>
            /// <param name="p_process"></param>
            public void Remove(Process p_process) {
                //string log;
                //lock (address) {                    
                    //Locals
                    Process p = p_process;
                    int addr = p.addresses[unit.id];
                    //If not allocated ignore
                    if (addr < 0) return;                    
                    //If 'invalid' ignore
                    if (address[addr] == invalid) return;
                    //log = $"[0] {string.Join(",",blocks)} @ c: {count} t: {tail}";
                    //Stored value for debug
                    int v = address[addr];
                    //Clear the block with 'invalid'
                    address[addr] = invalid;
                    //Invalidates the address
                    p.addresses[unit.id] = invalid;
                    //Decrease count
                    count--;                    
                    //log = $"[1] {string.Join(",",blocks)} @ c: {count} t: {tail}";
                    //If empty restart indexers and return
                    if (count <= 0) {
                        count   = 0;
                        pointer = 0;                        
                        return;
                    }                    
                    //Iterate Tail Back until next invalid                    
                    while (address[pointer-1] == invalid) if (pointer <= 0) break; else pointer--;
                    //log = $"[2] {string.Join(",",blocks)} @ c: {count} t: {tail}";
                    //If trail is zero all empty -> skip
                    if (pointer <= 0) return;
                    //If tail matches count+1 (next available) -> 0% fragmentation -> skip                    
                    if (pointer == count) return;                    
                    //If address is already at tail skip
                    if (addr == pointer) return;
                    //Peek process before tail
                    int prev_t      = pointer-1;                    
                    int prev_t_addr = address[prev_t];
                    //Fetch Process
                    p = unit.pool[prev_t_addr];
                    //Store Process PID in removed address
                    address[addr] = prev_t_addr;
                    //Invalidate Tail 
                    address[prev_t] = invalid;
                    //log = $"[3] {string.Join(",",blocks)} @ c: {count} t: {tail}";
                    //Store new address
                    p.addresses[unit.id] = addr;
                    //Iterate Tail Back until next invalid                    
                    while (address[pointer - 1] == invalid) if (pointer <= 0) break; else pointer--;
                     //log = $"[4] {string.Join(",",blocks)} @ c: {count} t: {tail}";
                //}
                //Debug.Log($"ProcessUnit.Memory> Remove / process[{v} @ {addr}] count[{count}]");
            }
        }
        #endregion

        /// <summary>
        /// Reference to the manager.
        /// </summary>
        public ProcessManager manager;

        /// <summary>
        /// Returns the desired process pool based on this unit being editor or runtime aimed
        /// </summary>
        public Process[] pool { get { return editor ? manager.editorProcess : manager.runtimeProcess; } }

        /// <summary>
        /// Memory struct to store Process references as indexes
        /// </summary>
        public Memory memory;

        /// <summary>
        /// Unit Id
        /// </summary>
        public int id;

        /// <summary>
        /// Context Flag to run this unit
        /// </summary>
        public ProcessContext context;

        /// <summary>
        /// Flag that tells if this process unit is for editor
        /// </summary>
        public bool editor { get { return (context & ProcessContext.EditorMask) != 0; } }

        /// <summary>
        /// Time control for runtime
        /// </summary>
        private Stopwatch clkRuntime;
        private Stopwatch clkEditor;
        private bool      clkRuntimeRunning;
        
        public float time;
        public float timeUnscaled;
        public float deltaTime;
        public float deltaTimeUnscaled;

        private Queue<Process> queue_add { get { return m_queue_add == null ? (m_queue_add = new Queue<Process>()) : m_queue_add; } }
        private Queue<Process> m_queue_add;
        private Queue<Process> queue_remove { get { return m_queue_remove == null ? (m_queue_remove = new Queue<Process>()) : m_queue_remove; } }
        private Queue<Process> m_queue_remove;

        /// <summary>
        /// Init this process unit
        /// </summary>
        public void Boot() {
            id    = manager.GetUnitId(context);            
            lock(memory) {
                memory.unit = this;
                memory.Resize(pool.Length);
            }
            //Create Thread based clock
            if ((context & (ProcessContext.EditorThread | ProcessContext.Editor)) != 0) { clkEditor  = clkEditor  == null ? new Stopwatch() : clkEditor ; clkEditor .Start(); }
            if ((context & (ProcessContext.Thread                              )) != 0) { clkRuntime = clkRuntime == null ? new Stopwatch() : clkRuntime; clkRuntime.Start(); clkRuntimeRunning = true; }
            //Time accumulators
            time         = 0f;
            timeUnscaled = 0f;            
            deltaTime    = 0f;
            deltaTimeUnscaled = 0f;            
        }

        /// <summary>
        /// Adds a process into the memory
        /// </summary>
        /// <param name="p_process"></param>
        /// <param name="p_lock"></param>
        public void Add(Process p_process,bool p_force) {
            Process p = p_process;
            Queue<Process> q = queue_add;
            if (p_force) { lock (memory) { memory.Add(p); } return; }
            lock (q) {
                //Debug.Log($"Process> {p.activity} QUEUE ADD   - {p.state} @ [{p.activity?.process?.pid}] [{p.pid}]");
                q.Enqueue(p);                
            }
        }

        /// <summary>
        /// Removes the process
        /// </summary>
        /// <param name="p_process"></param>
        /// <param name="p_lock"></param>
        public void Remove(Process p_process,bool p_force) {            
            Process p = p_process;                
            Queue<Process> q = queue_remove;
            if (p_force) { lock (memory) { memory.Remove(p); } return; }
            lock (q) {
                //Debug.Log($"Process> {p.activity} QUEUE REMOVE   - {p.state} @ [{p.activity?.process?.pid}] [{p.pid}]");
                q.Enqueue(p);                    
            }            
        }
        
        /// <summary>
        /// Steps the allocated processes
        /// </summary>
        /// <param name="p_offset"></param>
        /// <param name="p_count"></param>
        /// <param name="p_lock"></param>
        public void Execute(int p_offset,int p_count) {
            
            //Process pool to step
            Process[] pp = pool;
            Queue<Process> q;
            //Update Timing
            UpdateTime();

            //Skip Empty
            if (queue_add.Count    <= 0)
            if (queue_remove.Count <= 0)
            if (memory.count       <= 0) return;

            float t = time;
            float tu = timeUnscaled;
            Process p = null;
            //Process Removals
            q = queue_remove;
            //Empty the queue
            while (true) {
                lock (q) {
                    //Skip if empty
                    if (q.Count <= 0) break;
                    //Fetch Process
                    p = q.Dequeue();
                    if (p.state != ProcessState.Remove) { continue; }
                }
                //Skip if null
                if (p == null) continue;
                //Request 'memory' and process the removal                    
                lock (memory) { memory.Remove(p); }
                //Set state to 'remove'
                p.SetState(context,ProcessState.Remove);
                lock (p) {
                    bool out_unit = p.outUnit;
                    if (out_unit) {
                        p.SetState(p.context,ProcessState.Stop);
                        #if UNITY_EDITOR && PROCESS_PROFILER
                        p.profilerEnabled = false;
                        #endif
                        manager.PushProcess(p);
                    }
                }
            }
            //Process Additions
            q = queue_add;
            //Empty the queue
            while (true) {
                lock (q) {
                    //Skip if empty
                    if (q.Count <= 0) break;
                    //Fetch Process
                    p = q.Dequeue();
                    if (p.state != ProcessState.Add) continue;
                }
                //Skip if null
                if (p == null) continue;
                //Request 'memory' access and add the process                        
                lock (memory) { memory.Add(p); }
                //Switch to run                
                lock (p) {
                    bool in_unit = p.inUnit;
                    //Init
                    #if UNITY_EDITOR && PROCESS_PROFILER
                    p.profilerKey     = $"Proc.{context}@{(string.IsNullOrEmpty(p.name) ? $"P#{p.pid}" : p.name)}";
                    CustomSampler cps = CustomSampler.Create(p.profilerKey);
                    p.profilerSampler = cps == null ? (CustomSampler)CustomSampler.Get(p.profilerKey) : cps;
                    p.profilerEnabled = p.profilerSampler != null;
                    #endif
                    p.SetTime(id,p.useTimeScale ? t : tu,true);
                    //Set 'Add' state
                    p.SetState(context,ProcessState.Add);
                    if (in_unit) {
                        p.SetState(p.context,ProcessState.Start);
                        p.state = ProcessState.Run;
                    }
                }
            }
            //Fetch last 'pointer' as 'count'
            int tc = memory.pointer;
            //Iterate allocated processes
            //offset and count are meant for multithreads where each thread iterates a section of the memory
            for (int i = 0;i < tc;i += p_count) {
                //Offset iterator
                int k = (i + p_offset);
                int pi = -1;
                //Skip out of range
                if (k >= memory.address.Count) continue;
                //Safely fetch the process address
                lock (memory) {
                    pi = memory.address[k];
                    tc = memory.pointer;
                }
                //Assertion
                if (pi < 0) continue;
                if (pi >= pp.Length) continue;
                //Fetch the process
                p = pp[pi];
                //Skip non matching context
                if ((p.context & context) == 0) continue;
                //Update internal clock
                p.SetTime(id,p.useTimeScale ? t : tu,false);
                #if UNITY_EDITOR  && PROCESS_PROFILER
                if(p.profilerEnabled) if(p.profilerSampler!=null)p.profilerSampler.Begin();
                #endif
                //Update process
                p.Update(context);
                #if UNITY_EDITOR  && PROCESS_PROFILER
                if(p.profilerEnabled) if (p.profilerSampler != null) p.profilerSampler.End();
                #endif
            }
            
        }

        /// <summary>
        /// Set the Pause flag
        /// </summary>
        /// <param name="p_flag"></param>
        internal void SetPause(bool p_flag) {
            if ((context & ProcessContext.Thread) == 0) return;            
            if ( p_flag) if ( clkRuntimeRunning) { clkRuntime.Stop (); clkRuntimeRunning = false; }
            if (!p_flag) if (!clkRuntimeRunning) { clkRuntime.Start(); clkRuntimeRunning = true;  }            
        }

        /// <summary>
        /// Helper to shortcut fetching the proper delta time
        /// </summary>
        /// <param name="p_context"></param>
        /// <param name="p_scaled"></param>
        /// <returns></returns>
        internal void UpdateTime() {
            //Reset deltaTime
            deltaTime         = 0f;
            deltaTimeUnscaled = 0f;
            //Assign based on context
            switch (context) {
                case ProcessContext.Update:      deltaTime = Time.deltaTime      ; deltaTimeUnscaled = Time.unscaledDeltaTime     ; break;
                case ProcessContext.LateUpdate:  deltaTime = Time.deltaTime      ; deltaTimeUnscaled = Time.unscaledDeltaTime     ; break;
                case ProcessContext.FixedUpdate: deltaTime = Time.fixedDeltaTime ; deltaTimeUnscaled = Time.fixedUnscaledDeltaTime; break;                                
            }
            //Prevent pause/unpause spikes
            #if UNITY_EDITOR
            if(deltaTime > 0.3f)         deltaTime         = 0.3f;
            if(deltaTimeUnscaled > 0.3f) deltaTimeUnscaled = 0.3f;            
            #endif            
            //Update elapsed time
            switch (context) {
                case ProcessContext.Update:      
                case ProcessContext.LateUpdate:  
                case ProcessContext.FixedUpdate: time += deltaTime; timeUnscaled += deltaTimeUnscaled; break;
                case ProcessContext.Thread:      time  = timeUnscaled = (float)((double)clkRuntime.ElapsedMilliseconds * 0.001); break;
                #if UNITY_EDITOR
                case ProcessContext.Editor:       time = timeUnscaled = (float)UnityEditor.EditorApplication.timeSinceStartup;  break;
                case ProcessContext.EditorThread: time = timeUnscaled = (float)((double)clkEditor.ElapsedMilliseconds * 0.001); break;
                #endif
                
            }
            
        }

    }

}
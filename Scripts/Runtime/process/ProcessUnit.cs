using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityExt.Sys {

    [System.Serializable]
    public class ProcessUnit : ScriptableObject {

        /// <summary>
        /// Reference to the manager.
        /// </summary>
        public ProcessManager manager;

        /// <summary>
        /// Unit Id
        /// </summary>
        public int id;

        /// <summary>
        /// Context Flag to run this unit
        /// </summary>
        public ProcessContext context;

        /// <summary>
        /// List of addresses.
        /// </summary>
        public List<int> targets;

        /// <summary>
        /// Adress Start
        /// </summary>
        public int start;

        /// <summary>
        /// Address End
        /// </summary>
        public int end;

        /// <summary>
        /// Address count of the range.
        /// </summary>
        public int count;

        /// <summary>
        /// Defrag index
        /// </summary>
        public int defrag;

        public void Init() {
            id = manager.GetUnitId(context);
            name = context.ToString();
            start = 0;
            end = 0;
            count = 0;
            targets = new List<int> { -1,-1,-1,-1,-1 };
        }

        public void Add(int p_process,bool p_lock) {
            if (p_lock) {
                lock (targets) { InternalAdd(p_process); }
            } else {
                InternalAdd(p_process);
            }
        }

        public void Remove(int p_process,bool p_lock) {
            if (p_lock) {
                lock (targets) { InternalRemove(p_process); }
            } else {
                InternalRemove(p_process);
            }
        }

        private void InternalAdd(int p_process) {
            //Fetch process
            Process p = manager.process[p_process];
            //Fetch current address for this unit block
            int addr = p.addresses[id];
            //Range Check (expand if missing indexes)
            if (count >= targets.Count) return;
            Debug.Log($"Add > {p_process} addr[{addr}] {count}");
            //If positive process is allocated already
            if (addr >= 0) return;
            //Otherwise assign process to block location
            targets[end] = p.pid;
            //Set its address
            p.addresses[id] = end;
            //Increment next pointer
            end = (end + 1) % targets.Count;
            //Increment count
            count++;

        }

        private void InternalRemove(int p_process) {
            //Fetch process
            Process p = manager.process[p_process];
            //Fetch current address for this unit block
            int addr = p.addresses[id];
            Debug.Log($"Remove > {p_process} addr[{addr}] {count}");
            //Range Check
            if (count <= 0) return;
            //If negative process is not allocated
            if (addr < 0) return;
            //Otherwise invalidate the process in the block
            targets[addr] = -1;

            //Decrease count
            if (count > 0) count--;
            //Assign address
            p.addresses[id] = -1;
        }

        public void Defrag(int p_steps,bool p_lock) {
            if (p_lock) {
                lock (targets) {
                    InternalDefrag(p_steps);
                }
            } else {
                InternalDefrag(p_steps);
            }
        }

        private void InternalDefrag(int p_steps) {
            for (int i = 0;i < p_steps;i++) {
                //Match index ranging
                int k0 = start + defrag;
                k0 = k0 % targets.Count;
                //Check first process in pair
                int t0 = targets[k0];
                //If null no gap to fill
                if (t0 < 0) { defrag = (defrag + 1) % targets.Count; continue; }
                //Fetch next in pair
                int k1 = (k0 + 1) % targets.Count;
                int t1 = targets[k1];
                //If not null pair is valid 
                if (t1 >= 0) { defrag = (defrag + 1) % targets.Count; continue; }
                //Pair can be swapped
                targets[k0] = t1; //null
                targets[k1] = t0; //process
                //Set new process id after swap
                int pi = targets[k1];
                Process p = manager.process[pi];
                p.addresses[id] = k1;
            }
            //Move start point to next valid process
            for (int i = 0;i < count;i++) if (targets[start] < 0) start++;
        }

        public void Step(int p_offset,int p_count) {
            bool will_lock = (context == ProcessContext.Thread) || (context == ProcessContext.EditorThread);
            if (will_lock) {
                lock (targets) {
                    InternalStep(p_offset,p_count);
                }
            } else {
                InternalStep(p_offset,p_count);
            }
        }

        private void InternalStep(int p_offset,int p_count) {

            int c = count;

            for (int i = 0;i < c;i += p_count) {
                int k = (start + i + p_offset);
                int btc = targets.Count;
                if (btc <= 0) break;
                k = (k % btc);
                int pi = targets[k];
                if (pi < 0) continue;
                if(pi>=manager.process.Length) continue;
                Process p = manager.process[pi];
                if (p.context != context) continue;
                float t = 0f;
                switch (context) {
                    case ProcessContext.Update:         t = p.useTimeScale ? Time.time : Time.unscaledTime; break;
                    case ProcessContext.LateUpdate:     t = p.useTimeScale ? Time.time : Time.unscaledTime; break;
                    case ProcessContext.FixedUpdate:    t = p.useTimeScale ? Time.fixedTime : Time.fixedUnscaledTime; break;
                    case ProcessContext.Editor:         t = Time.realtimeSinceStartup; break;
                    case ProcessContext.EditorThread:
                    case ProcessContext.Thread:         t = manager.clockTime; break;
                }
                //If running start updating the time
                p.SetTime(t);
                p.Step(context);
            }
        }

    }

}
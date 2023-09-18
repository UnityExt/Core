using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;
using UnityEngine.SceneManagement;
using System.Threading;
using System;
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityExt.Core {


    #region class ActivityProcessBase
    /// <summary>
    /// Abstract component that describes a single execution process of all activities in the application.
    /// </summary>
    internal  class ActivityProcessBase : MonoBehaviour {

        /// <summary>
        /// Activity Context
        /// </summary>
        internal  ActivityContext context { get; private set; }

        /// <summary>
        /// Flag that tells the core will run async and inside the time slice
        /// </summary>
        internal  bool async;

        /// <summary>
        /// Time slice in ms this core will execute before deferring to another frame
        /// </summary>
        internal  int slice;

        /// <summary>
        /// Flag that tells this process is enabled or not
        /// </summary>
        new public bool enabled { 
            get { return context == ActivityContext.Thread ? m_enabled : base.enabled; }
            set { base.enabled = m_enabled = value; }
        }
        private bool m_enabled;

        /// <summary>
        /// Internals
        /// </summary>
        private Thread    m_thread;
        private bool      m_thread_kill;
        static private int m_thread_count;

        /// <summary>
        /// Clears this process execution
        /// </summary>
        virtual internal void Kill() { }

        /// <summary>
        /// CTOR.
        /// </summary>
        protected void Initialize(ActivityContext p_context) {
            if(m_thread!=null) return;
            context        = p_context;
            m_enabled      = true;
            //Can't use threads in webgl
            #if UNITY_WEBGL
            context = ActivityContext.Update;
            #endif            
            #if UNITY_EDITOR
            if(context == ActivityContext.Editor) {
                //Register method for update looping
                EditorApplication.update += EditorUpdate;                                      
            }            
            #endif
            AssertThread();
        }

        private void AssertThread() {
            if(context != ActivityContext.Thread) return;
            if(m_thread!=null) return;
            m_thread_kill = false;
            m_thread = new Thread(ThreadLoop);
            m_thread.Name = $"ac-thread-{m_thread_count}";
            m_thread_count++;                
            m_thread.Start();
        }

        protected void OnEnable () { 
            m_enabled=true;  
            AssertThread();
        }
        protected void OnDisable() { m_enabled=false; }

        /// <summary>
        /// Handler for executing the activity list associated with this core.
        /// </summary>
        virtual protected void Execute() { }

        #region Loops
        protected void EditorUpdate() { if(!enabled) Update(); }
        protected void Update () {              
            switch(context) {
                case ActivityContext.Editor:
                case ActivityContext.Job:
                case ActivityContext.JobAsync:
                case ActivityContext.Update:
                case ActivityContext.Async: 
                    Execute();
                break;

            }            
        }
        protected void LateUpdate  () { if(context == ActivityContext.LateUpdate ) Execute(); }
        protected void FixedUpdate () { if(context == ActivityContext.FixedUpdate) Execute(); }
        protected void ThreadUpdate() { Execute(); }
        protected void ThreadLoop() {
            if(context != ActivityContext.Thread) return;
            while(true) {
                if(m_thread_kill) break;
                if(!enabled) continue;
                ThreadUpdate();
                Thread.Sleep(0);
            }
        }
        #endregion

        /// <summary>
        /// DTOR
        /// </summary>
        protected void OnDestroy() {
            m_thread_kill = true;
            #if UNITY_EDITOR
            if(context == ActivityContext.Editor) {
                //Unregister callback
                EditorApplication.update -= EditorUpdate;
            }            
            #endif
        }

    }
    #endregion

    #region class ActivityProcess<T>
    /// <summary>
    /// Abstract component that describes a single execution core of all activities in the application.
    /// </summary>
    internal  class ActivityProcess<T> : ActivityProcessBase {

        /// <summary>
        /// List of executing elements.
        /// </summary>        
        protected List<T> list { get { return m_list==null ? (m_list = new List<T>()) : m_list; } }
        private   List<T> m_list;
        private   List<T> add { get { return m_add==null ? (m_add = new List<T>()) : m_add; } }
        private   List<T> m_add;
        private   List<T> remove { get { return m_remove==null ? (m_remove = new List<T>()) : m_remove; } }
        private   List<T> m_remove;
        
        /// <summary>
        /// Lock variable for thread safe operations at the list.
        /// </summary>        
        protected object list_lock { get { return m_list_lock==null ? (m_list_lock= new object()) : m_list_lock; } } 
        private   object m_list_lock;
        private   object add_lock { get { return m_add_lock==null ? (m_add_lock= new object()) : m_add_lock; } } 
        private   object m_add_lock;
        private   object remove_lock { get { return m_remove_lock==null ? (m_remove_lock= new object()) : m_remove_lock; } } 
        private   object m_remove_lock;
        

        /// <summary>
        /// Internals
        /// </summary>
        private int       m_iterator;
        private Stopwatch async_clock { get { return m_async_clock==null ? (m_async_clock = new Stopwatch()) : m_async_clock; } }
        private Stopwatch m_async_clock;

        
        /// <summary>
        /// CTOR.
        /// </summary>
        internal void Initialize(ActivityContext p_context,bool p_async) {
            base.Initialize(p_context);        
            if(list!=null) return;
            m_iterator     = 0;                     
        }

        /// <summary>
        /// Adds an executing element.
        /// </summary>
        /// <param name="n"></param>
        internal void Add(T n) { object ll = add_lock; lock(ll) { if(!add.Contains(n)) add.Add(n); } }

        /// <summary>
        /// Removes an executing element.
        /// </summary>
        /// <param name="v"></param>
        internal void Remove(T n) {  object ll = remove_lock; lock(ll) { if(remove.Contains(n)) remove.Remove(n); } }

        /// <summary>
        /// Check if a node exists in the list.
        /// </summary>        
        /// <param name="v"></param>
        /// <returns></returns>
        internal bool Contains(T n) { object ll = list_lock; bool f = false; lock(ll) { f = list.Contains(n); } return f; }

        /// <summary>
        /// Kill the execution list.
        /// </summary>
        override internal void Kill() { object ll = list_lock; lock(ll) { list.Clear(); } }
        
        /// <summary>
        /// Handler for executing a single activity.
        /// </summary>
        /// <param name="it"></param>
        virtual protected void OnExecute(T it) { }

        /// <summary>
        /// Handler for executing the activity list associated with this core.
        /// </summary>
        override protected void Execute() {             
            int c_add    = add    ==null ? 0 : add.Count;            
            int c_remove = remove ==null ? 0 : remove.Count;
            int c_list   = list   ==null ? 0 : list.Count;
            //Skip if no activity
            if(c_add<=0) if(c_remove<=0) if(c_list<=0) {
                if(context == ActivityContext.Thread) Thread.Sleep(16);
                return;
            }
            object ll;
            //Lock 'add' scheduled elements
            ll = add_lock;
            lock(ll) {
                int c = add.Count;
                int s = (c/500)<=0 ? c : (c/4);
                //Async add batches of elements to not introduce stress in the hot execution area of 'list'
                if(c>0)
                for(int i=0;i<s;i++) {
                    T it = add[0];        
                    add.RemoveAt(0);
                    if(!list.Contains(it)) {
                        list.Add(it);
                    }
                    if(add.Count<=0) break;
                }
            }
            //Lock 'remove' scheduled elements
            ll = remove_lock;
            lock(ll) {
                int c = remove.Count;
                int s = c;
                //Async remove batches of elements to not introduce stress in the hot execution area of 'list'
                if(c>0)
                for(int i=0;i<s;i++) {
                    T it = remove[0];        
                    remove.RemoveAt(0);
                    if(list.Contains(it)) {
                        list.Remove(it);
                    }
                    if(remove.Count<=0) break;
                }
            }
            //Thread-safe list use
            ll = list_lock;
            lock(ll) {
                //If 'async' restart slice clock
                if(async) async_clock.Restart();
                //Iterate elements
                for(int i = 0; i < list.Count; i++) {
                    //Execute element
                    OnExecute(list[m_iterator]);
                    //Increment iterator
                    m_iterator = (m_iterator+1)%list.Count;
                    //If not async keep running
                    if(!async) continue;                    
                    //Sample slice timing and break the loop if timed out
                    int t = (int)async_clock.ElapsedMilliseconds;
                    int s = slice<=0 ? Activity.asyncTimeSlice : slice;
                    if(t>s) break;
                }
            }

        }

    }
    #endregion

    #region Activity Core Variants
    /// <summary>
    /// Implementation of an activity core for interfaces
    /// </summary>
    internal  class APUpdateable : ActivityProcess<IUpdateable> {        
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        protected override void OnExecute(IUpdateable it) { it.OnUpdate(); }        
    }
    /// <summary>
    /// Implementation of an activity core for interfaces
    /// </summary>
    internal  class APLateUpdateable : ActivityProcess<ILateUpdateable> {        
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        protected override void OnExecute(ILateUpdateable it) { it.OnLateUpdate(); }        
    }
    /// <summary>
    /// Implementation of an activity core for interfaces
    /// </summary>
    internal  class APFixedUpdateable : ActivityProcess<IFixedUpdateable> {        
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        protected override void OnExecute(IFixedUpdateable it) { it.OnFixedUpdate(); }
    }
    /// <summary>
    /// Implementation of an activity core for interfaces
    /// </summary>
    internal  class APAsyncUpdateable : ActivityProcess<IAsyncUpdateable> {        
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        protected override void OnExecute(IAsyncUpdateable it) { it.OnAsyncUpdate(); }
    }
    /// <summary>
    /// Implementation of an activity core for interfaces
    /// </summary>
    internal  class APThreadUpdateable : ActivityProcess<IThreadUpdateable> {
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        protected override void OnExecute(IThreadUpdateable it) { it.OnThreadUpdate(); }
    }
    /// <summary>
    /// Implementation of an activity core for activities
    /// </summary>
    internal  class ActivityProcess : ActivityProcess<Activity> {

        /// <summary>
        /// Searches for an activity by its id.
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        internal T Find<T>(string p_id) where T : Activity { 
            object ll = list_lock; 
            T res = null;
            lock(ll) { for(int i=0;i<list.Count;i++) if(list[i].id == p_id) { if(list[i] is T) { res = (T)list[i]; break; } } }
            return res; 
        }

        /// <summary>
        /// Searches for all activities matching the id.
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        internal List<T> FindAll<T>(string p_id) where T : Activity { 
            object ll = list_lock; 
            List<T> res = new List<T>();
            bool empty_id = string.IsNullOrEmpty(p_id);
            lock(ll) { 
                for(int i=0;i<list.Count;i++) {
                    if(!(list[i] is T)) continue;
                    bool is_match = empty_id || (list[i].id == p_id);
                    if(is_match) { res.Add((T)list[i]); } 
                } 
            }
            return res;
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        protected override void OnExecute(Activity it) { 
            it.Execute(); 
        }        
    }
    #endregion

}

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityExt.Core {

    #region Interfaces

    /// <summary>
    /// Interfaces for objects that wants to perform update loops.
    /// </summary>
    public interface IUpdateable { 
        /// <summary>
        /// Runs inside Monobehaviour.Update
        /// </summary>
        void OnUpdate(); 
    }
    
    /// <summary>
    /// Interfaces for objects that wants to perform late update loops.
    /// </summary>
    public interface ILateUpdateable { 
        /// <summary>
        /// Runs inside Monobehaviour.LateUpdate
        /// </summary>
        void OnLateUpdate(); 
    }

    /// <summary>
    /// Interfaces for objects that wants to perform fixed update loops.
    /// </summary>
    public interface IFixedUpdateable { 
        /// <summary>
        /// Runs inside Monobehaviour.FixedUpdate
        /// </summary>
        void OnFixedUpdate(); 
    }
    
    /// <summary>
    /// Interfaces for objects that wants to perform update loops not bound by frames
    /// </summary>
    public interface IAsyncUpdateable { 
        /// <summary>
        /// Runs inside Monobehaviour.Update and only during the 'async-slice' duration per frame, so it can skip a few frames depending on the execution load.
        /// </summary>
        void OnAsyncUpdate(); 
    }

    #endregion

    /// <summary>
    /// Class that implements any async activity/processing.
    /// It can run in different contexts inside unity (mainthread) or separate thread, offering an abstraction layer Monobehaviour/Thread agnostic.
    /// </summary>
    public class Activity : INotifyCompletion  {

        #region enum Context

        /// <summary>
        /// Enumeration that describes the execution context of the activity.
        /// </summary>
        public enum Context {
            /// <summary>
            /// Runs inside Monobehaivour.Update
            /// </summary>
            Update,
            /// <summary>
            /// Runs inside Monobehaivour.LateUpdate
            /// </summary>
            LateUpdate,
            /// <summary>
            /// Runs inside Monobehaivour.FixedUpdate
            /// </summary>
            FixedUpdate,
            /// <summary>
            /// Runs inside Monobehaivour.Update but only inside 'async-slice' duration per frame
            /// </summary>
            Async,
            /// <summary>
            /// Runs inside a thread, watch out to not use Unity API elements inside it.
            /// </summary>
            Thread
        }

        #endregion

        #region enum State

        /// <summary>
        /// Enumeration that describes the execution state of the activity.
        /// </summary>
        public enum State {
            /// <summary>
            /// Just created activity.
            /// </summary>
            Idle,
            /// <summary>
            /// Waiting for execution
            /// </summary>
            Queued,
            /// <summary>
            /// Active execution.
            /// </summary>
            Running,
            /// <summary>
            /// Finished
            /// </summary>
            Complete
        }

        #endregion

        #region class Manager

        /// <summary>
        /// Behaviour to handle all activity  executions.
        /// </summary>
        public class Manager : MonoBehaviour {

            #region class ActivityList

            /// <summary>
            /// Base class to handle activities.
            /// </summary>
            internal class ActivityList {

                /// <summary>
                /// Activities
                /// </summary>
                public List<Activity> la;

                /// <summary>
                /// Current iterator for activities
                /// </summary>
                public int ia;

                /// <summary>
                /// Context of execution.
                /// </summary>
                public Context context;

                /// <summary>
                /// Timer for async loops.
                /// </summary>
                public System.Diagnostics.Stopwatch timer;

                /// <summary>
                /// Internals.
                /// </summary>
                private string m_id_query;

                /// <summary>
                /// CTOR.
                /// </summary>
                public ActivityList(Context p_context) {
                    la = new List<Activity>(1000);
                    ia = 0;
                    context = p_context;
                    timer   = context == Context.Async ? new System.Diagnostics.Stopwatch() : null;
                }

                /// <summary>
                /// Clear the lists
                /// </summary>
                virtual public void Clear() {
                    if(la!=null) la.Clear();
                    context = (Context)(-1);
                    if(timer!=null) timer.Stop();
                }

                /// <summary>
                /// Searches for an activity by its id.
                /// </summary>
                /// <param name="p_id"></param>
                /// <returns></returns>
                public Activity Find(string p_id) {
                    Activity res = null;
                    if(la==null) return res;
                    m_id_query = p_id;
                    res = la.Find(FindById);
                    m_id_query = "";
                    return res;
                }

                /// <summary>
                /// Finds all activities matching the id.
                /// </summary>
                /// <param name="p_id"></param>
                /// <returns></returns>
                public List<Activity> FindAll(string p_id) {
                    List<Activity> res = null;
                    if(la==null) return res = new List<Activity>();
                    m_id_query = p_id;
                    res = la.FindAll(FindById);
                    m_id_query = "";
                    return res;
                }

                /// <summary>
                /// Helper method to search activities.
                /// </summary>
                /// <param name="it"></param>
                /// <returns></returns>
                private bool FindById(Activity it) { return it==null ? false : (it.id == m_id_query); }

                #region Execute

                /// <summary>
                /// Executes all activities.
                /// </summary>
                virtual public void Execute() {
                    //Prune activities
                    for(int i=0;i<la.Count;i++) { if(la[i]==null ? true : la[i].completed)la.RemoveAt(i--); }
                    //Shortcut bool
                    bool is_async = context == Context.Async;
                    //If async prepare timer
                    if(is_async)  timer.Restart();
                    //If not async iterator is back to 0
                    if(!is_async) ia=0;
                    //Iterate across the list bounds
                    for(int i=0;i<la.Count;i++) {
                        //If async check timer slice limit and break out
                        if(is_async) if(timer.ElapsedMilliseconds>=asyncTimeSlice) break;
                        //Use iterator for async cases
                        Activity it = la[ia];
                        //Check if node can be executed
                        bool is_valid = it==null ? false : !it.completed;                        
                        //Steps the activity if valid
                        if(is_valid) it.Execute();                        
                        //Increment-loop the iterator (async might be mid iteration)
                        ia = (ia+1)%la.Count;                        
                    }

                }

                #endregion

            }

            /// <summary>
            /// Auxiliary class to contain activity and interface lists.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            internal class ActivityList<T> : ActivityList {

                /// <summary>
                /// Interface nodes
                /// </summary>
                public List<T> li;

                /// <summary>
                /// Current iterator for interfaces
                /// </summary>
                public int ii;

                /// <summary>
                /// CTOR
                /// </summary>
                public ActivityList(Context p_context) : base(p_context) {                    
                    li = new List<T>(1000);
                    ii = 0;                    
                }

                /// <summary>
                /// Clear this execution list.
                /// </summary>
                override public void Clear() {
                    //Clear activities
                    base.Clear(); 
                    //Clear interfaces
                    if(li!=null) li.Clear();                                        
                }

                #region Execute

                /// <summary>
                /// Executes the lists in the chosen context.
                /// </summary>
                override public void Execute() {                    
                    //Iterate activities
                    base.Execute();                                        
                    //Prune interfaces
                    for(int i=0;i<li.Count;i++) { if(li[i]==null)li.RemoveAt(i--); }
                    //Shortcut bool
                    bool is_async = context == Context.Async;
                    //Same of interfaces
                    if(is_async)  timer.Restart();
                    if(!is_async) ii=0;
                    //Loop
                    for(int i=0;i<li.Count;i++) {
                        //Cast the interface based on the context.
                        switch(context) {
                            case Context.Update:      { IUpdateable      it = (IUpdateable)     li[ii]; it.OnUpdate();      } break;
                            case Context.LateUpdate:  { ILateUpdateable  it = (ILateUpdateable) li[ii]; it.OnLateUpdate();  } break;
                            case Context.Async:       { IAsyncUpdateable it = (IAsyncUpdateable)li[ii]; it.OnAsyncUpdate(); } break;
                            case Context.FixedUpdate: { IFixedUpdateable it = (IFixedUpdateable)li[ii]; it.OnFixedUpdate(); } break;
                        }
                        //Same as above
                        ii = (ii+1)%li.Count;
                        if(is_async) if(timer.ElapsedMilliseconds>=asyncTimeSlice) break;
                    }
                }

                #endregion

            }

            #endregion

            /// <summary>
            /// Internals.
            /// </summary>
            static internal System.Threading.Thread thread_loop;
            internal ActivityList<IUpdateable>      lu;
            internal ActivityList<ILateUpdateable>  llu;
            internal ActivityList<IFixedUpdateable> lfu;
            internal ActivityList<IAsyncUpdateable> lau;
            internal List<Activity> lt;
            internal List<Activity> ltq;            
            internal float  thread_keep_alive_tick;            
            internal bool   thread_kill_flag;

            /// <summary>
            /// CTOR
            /// </summary>
            protected void Awake() {
                if(lu==null)  lu  = new ActivityList<IUpdateable>(Context.Update);
                if(llu==null) llu = new ActivityList<ILateUpdateable>(Context.LateUpdate);
                if(lfu==null) lfu = new ActivityList<IFixedUpdateable>(Context.FixedUpdate);
                if(lau==null) lau = new ActivityList<IAsyncUpdateable>(Context.Async);
                if(lt==null)  lt  = new List<Activity>();                
                if(ltq==null) ltq = new List<Activity>();
            }

            /// <summary>
            /// DTOR.
            /// </summary>
            internal void OnDestroy() {
                Clear();   
            }

            /// <summary>
            /// CTOR.
            /// </summary>
            internal void Start() {
                //Assert thread on each start in case of scene changes and thread suspension or abortion
                thread_keep_alive_tick = 0f;             
                //Kill flag to cleanly stop the thread
                thread_kill_flag       = false;
            }

            /// <summary>
            /// Clears the manager execution.
            /// </summary>
            internal void Clear() {
                if(lu!=null)  lu.Clear();
                if(llu!=null) llu.Clear();
                if(lfu!=null) lfu.Clear();
                if(lau!=null) lau.Clear();
                thread_kill_flag = true;
            }

            #region Add/Remove

            /// <summary>
            /// Returns the desired activity list by context
            /// </summary>
            /// <param name="p_context"></param>
            /// <returns></returns>
            internal ActivityList GetActivityList(Context p_context) {
                switch(p_context) {
                    case Context.Update:      return lu;
                    case Context.LateUpdate:  return llu;
                    case Context.FixedUpdate: return lfu;
                    case Context.Async:       return lau;
                }
                return null;
            }

            /// <summary>
            /// Adds a new activity node.
            /// </summary>
            /// <param name="p_activity"></param>
            internal void AddActivity(Activity p_activity) {
                Activity a = p_activity;
                if(a==null)               return;
                if(a.state != State.Idle) return;
                a.state = State.Queued;
                switch(a.context) {
                    case Context.Update:      if(!lu.la.Contains(a))  lu.la.Add(a);  break;
                    case Context.LateUpdate:  if(!llu.la.Contains(a)) llu.la.Add(a); break;
                    case Context.FixedUpdate: if(!lfu.la.Contains(a)) lfu.la.Add(a); break;
                    case Context.Async:       if(!lau.la.Contains(a)) lau.la.Add(a); break;
                    //Threaded lists its better to enqueue in a secondary list and let the main execution add the list in a synced way
                    case Context.Thread:    { if(!ltq.Contains(a)) ltq.Add(a); AssertThread(); break; }
                }
            }

            /// <summary>
            /// Removes a executing node.
            /// </summary>
            /// <param name="p_activity"></param>
            internal void RemoveActivity(Activity p_activity) {
                Activity a = p_activity;
                if(a==null)               return;                
                if(a.state == State.Idle) return;                
                int idx;
                switch(a.context) {
                    case Context.Update:      if(lu.la.Contains(a))  lu.la.Remove(a);  break;
                    case Context.LateUpdate:  if(llu.la.Contains(a)) llu.la.Remove(a); break;
                    case Context.FixedUpdate: if(lfu.la.Contains(a)) lfu.la.Remove(a); break;
                    case Context.Async:       if(lau.la.Contains(a)) lau.la.Remove(a); break;
                    //Threaded lists its better to null the element and allow removal during the synced execution of the thread
                    case Context.Thread:      { 
                        idx = lt.IndexOf(a);  if(idx>=0) lt[idx]  = null; 
                        idx = ltq.IndexOf(a); if(idx>=0) ltq[idx] = null;
                    }
                    break;
                }
                a.OnStop();
            }

            /// <summary>
            /// Adds an executing node
            /// </summary>
            /// <param name="p_interface"></param>
            internal void AddInterface(object p_interface) {
                if(p_interface==null) return;
                if(p_interface is IUpdateable)      { IUpdateable     itf = (IUpdateable)      p_interface; if(!lu.li.Contains(itf))  lu.li.Add(itf);  }
                if(p_interface is ILateUpdateable)  { ILateUpdateable itf = (ILateUpdateable)  p_interface; if(!llu.li.Contains(itf)) llu.li.Add(itf); }
                if(p_interface is IFixedUpdateable) { IFixedUpdateable itf = (IFixedUpdateable)p_interface; if(!lfu.li.Contains(itf)) lfu.li.Add(itf); }
                if(p_interface is IAsyncUpdateable) { IAsyncUpdateable itf = (IAsyncUpdateable)p_interface; if(!lau.li.Contains(itf)) lau.li.Add(itf); }
            }

            /// <summary>
            /// Removes an executing node
            /// </summary>
            /// <param name="p_interface"></param>
            internal void RemoveInterface(object p_interface) {
                if(p_interface==null) return;
                if(p_interface is IUpdateable)      { IUpdateable      itf = (IUpdateable)      p_interface; if(!lu.li.Contains(itf))  lu.li.Add(itf);  }
                if(p_interface is ILateUpdateable)  { ILateUpdateable  itf = (ILateUpdateable)  p_interface; if(!llu.li.Contains(itf)) llu.li.Add(itf); }
                if(p_interface is IFixedUpdateable) { IFixedUpdateable itf = (IFixedUpdateable) p_interface; if(!lfu.li.Contains(itf)) lfu.li.Add(itf); }
                if(p_interface is IAsyncUpdateable) { IAsyncUpdateable itf = (IAsyncUpdateable) p_interface; if(!lau.li.Contains(itf)) lau.li.Add(itf); }
            }

            #endregion

            #region Find

            /// <summary>
            /// Searches an activity by id.
            /// </summary>
            /// <param name="p_id"></param>
            /// <returns></returns>
            internal Activity Find(string p_id,Context p_context) {
                //Local ref
                Activity res = null;
                //Shortcut bool
                bool all_ctx = ((int)p_context)<0;
                //Search threads if threaded or all ctxs
                bool search_threads = p_context == Context.Thread ? true : all_ctx;
                //Search thread nodes
                if(p_context == Context.Thread) {
                    m_id_query = p_id;
                    //Search active nodes
                    res = lt.Find(FindActivityById);
                    //Search queued nodes if no result
                    if(res==null) res = ltq.Find(FindActivityById);
                    //Clear global search query
                    m_id_query = "";
                    //If thread only return
                    if(p_context == Context.Thread) return res;
                    //If there is a result return
                    if(res!=null) return res;
                }
                //Try fetch the activity list
                ActivityList la = GetActivityList(p_context);
                //If single context find in the single list
                if(la!=null) return la.Find(p_id);
                //If not search in all
                res = lu.Find(p_id);  if(res!=null) return res;
                res = llu.Find(p_id); if(res!=null) return res;
                res = lfu.Find(p_id); if(res!=null) return res;
                res = lau.Find(p_id); if(res!=null) return res;
                //Return whatever
                return res;
            }
            /// <summary>
            /// Helpers for Find/FindAll
            /// </summary>
            private string m_id_query;
            private bool FindActivityById(Activity it) { return it==null ? false : (it.id == m_id_query); }
            
            /// <summary>
            /// Finds an activity searching all contexts.
            /// </summary>
            /// <param name="p_id"></param>
            /// <returns></returns>
            internal Activity Find(string p_id) { return Find(p_id,(Context)(-1)); }

            /// <summary>
            /// Finds all activities, matching the id.
            /// </summary>
            /// <param name="p_id"></param>
            /// <param name="p_context"></param>
            /// <returns></returns>
            internal List<Activity> FindAll(string p_id,Context p_context) {
                //Local ref
                List<Activity> res = new List<Activity>();
                //Shortcut bool
                bool all_ctx = ((int)p_context)<0;
                //Search threads if threaded or all ctxs
                bool search_threads = p_context == Context.Thread ? true : all_ctx;
                //Search thread nodes
                if(search_threads) {
                    m_id_query = p_id;
                    //Search active nodes
                    if(lt!=null) res.AddRange(lt.FindAll(FindActivityById));
                    //Search queued nodes
                    if(ltq!=null)res.AddRange(ltq.FindAll(FindActivityById));
                    //Clear global search query
                    m_id_query = "";
                    //If thread only return
                    if(p_context == Context.Thread) return res;
                }
                //Try fetch the activity list
                ActivityList la = GetActivityList(p_context);
                //If single context find in the single list
                if(la!=null) { res.AddRange(la.FindAll(p_id)); return res; }
                //If not search in all
                res.AddRange(lu.FindAll(p_id));  
                res.AddRange(llu.FindAll(p_id)); 
                res.AddRange(lfu.FindAll(p_id)); 
                res.AddRange(lau.FindAll(p_id)); 
                //Return results
                return res;
            }

            /// <summary>
            /// Searches all activities in all contexts.
            /// </summary>
            /// <param name="p_id"></param>
            /// <returns></returns>
            internal List<Activity> FindAll(string p_id) { return FindAll(p_id,(Context)(-1)); }

            #endregion

            #region Loops

            /// <summary>
            /// Asserts the lifetime of the thread running the thread loop nodes.
            /// </summary>
            internal void AssertThread() {
                //If kill switch skip
                if(thread_kill_flag)             return;
                //If no nodes skip
                if(lt.Count<=0) if(ltq.Count<=0) return;
                //If thread active skips
                if(thread_loop!=null) if(thread_loop.ThreadState == System.Threading.ThreadState.Running) return;
                //Create and start the thread
                thread_loop = new System.Threading.Thread(
                delegate() { 
                    while(true) {
                        //If kill thread clear all and break out, reset the flag
                        if(thread_kill_flag) { lt.Clear(); ltq.Clear(); thread_kill_flag=false; break; }
                        //Execute all nodes
                        ThreadUpdate();
                        //Sleep 0 to yield CPU if possible
                        System.Threading.Thread.Sleep(0);
                        //Stop the thread if no nodes
                        if(lt.Count<=0) if(ltq.Count<=0) break;
                    }
                    thread_loop=null;
                });
                thread_loop.Name = "activity-thread";
                thread_loop.Start();
                
            }

            /// <summary>
            /// Update Loop
            /// </summary>
            internal void Update() {
                lu.Execute();
                lau.Execute();
                //Check health state of threading each 2s
                if(thread_keep_alive_tick<=0) { AssertThread(); thread_keep_alive_tick=2f; }
                thread_keep_alive_tick-=Time.unscaledDeltaTime;
            }

            /// <summary>
            /// FixedUpdate Loop
            /// </summary>
            internal void FixedUpdate() { lfu.Execute();  }

            /// <summary>
            /// LateUpdate Loop
            /// </summary>
            internal void LateUpdate()  { llu.Execute();  }

            /// <summary>
            /// ThreadUpdate Loop
            /// </summary>
            internal void ThreadUpdate() {
                //Move new queued elements into the main list
                for(int i=0;i<ltq.Count;i++) {
                    //Skip invalids
                    if(ltq[i]==null) continue;
                    //Assert before insertion
                    if(!lt.Contains(ltq[i]))lt.Add(ltq[i]);
                }
                //Clear the queue
                ltq.Clear();
                //Executes all threaded nodes.
                for(int i=0;i<lt.Count;i++) {
                    Activity it = lt[i];
                    //Remove non active nodes
                    if(it==null ? true : it.completed) { lt.RemoveAt(i--); continue; }
                    //Step
                    it.Execute();                    
                }
            }

            #endregion

        }

        /// <summary>
        /// Reference to the global Activity manager.
        /// </summary>
        static public Manager manager {
            get {
                if(m_manager)  return m_manager;
                if(!m_manager) m_manager = GameObject.FindObjectOfType<Manager>();
                if(m_manager)  return m_manager;
                GameObject g = new GameObject("activity-manager");
                GameObject.DontDestroyOnLoad(g);
                return m_manager = g.AddComponent<Manager>();
            }
        }
        static private Manager m_manager;

        /// <summary>
        /// Execution time slice for async nodes.
        /// </summary>
        static public int asyncTimeSlice = 4;

        #endregion

        #region static

        #region CRUD

        /// <summary>
        /// Adds activity for exection.
        /// </summary>
        /// <param name="p_node">Execution node. Must implement one or more Activity related interfaces.</param>
        static public void Add(object p_node) { if(manager)manager.AddInterface(p_node); }

        /// <summary>
        /// Removes the activity from exection.
        /// </summary>
        /// <param name="p_node">Execution node. Must implement one or more Activity related interfaces.</param>
        static public void Remove(object p_node) { if(manager)manager.RemoveInterface(p_node); }

        /// <summary>
        /// Removes the activity from exection.
        /// </summary>
        static public void Clear() { if(manager)manager.Clear(); }

        /// <summary>
        /// Searches for a single activity by id and context.
        /// </summary>
        /// <param name="p_id">Activity id to search.</param>
        /// <param name="p_context">Specific context to be searched.</param>
        /// <returns>Activity found or null</returns>
        static public Activity Find(string p_id,Context p_context) { if(manager) return manager.Find(p_id,p_context); return null; }

        /// <summary>
        /// Searches for a single activity by id in all contexts.
        /// </summary>
        /// <param name="p_id">Activity id to search.</param>        
        /// <returns>Activity found or null</returns>
        static public Activity Find(string p_id) { return Find(p_id,(Context)(-1)); }

        /// <summary>
        /// Searches for all activities matching the id and context.
        /// </summary>
        /// <param name="p_id">Activity id to search.</param>
        /// <param name="p_context">Specific context to be searched.</param>
        /// <returns>List of results or an empty list.</returns>
        static public List<Activity> FindAll(string p_id,Context p_context) { List<Activity> res=null; if(manager) res = manager.FindAll(p_id,p_context); return res==null ? new List<Activity>() : res; }

        /// <summary>
        /// Searches for all activities matching the id in all contexts.
        /// </summary>
        /// <param name="p_id">Activity id to search.</param>        
        /// <returns>List of results or an empty list.</returns>
        static public List<Activity> FindAll(string p_id) { return FindAll(p_id,(Context)(-1)); }

        #endregion

        #region Run / Loop

        /// <summary>
        /// Creates and starts an activity for constant loop execution.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>
        /// <param name="p_context">Execution context to run.</param>
        /// <returns></returns>
        static public Activity Run(string p_id,System.Predicate<Activity> p_callback,Context p_context) {
            Activity a = new Activity(p_id,p_context);
            a.OnCompleteEvent = null;
            a.OnExecuteEvent  = p_callback;
            a.Start();
            return a;
        }

        /// <summary>
        /// Creates and starts an activity for constant loop execution.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>
        /// <param name="p_context">Execution context to run.</param>
        /// <returns></returns>
        static public Activity Run(System.Predicate<Activity> p_callback,Context p_context) { return Run("",p_callback,p_context); }

        /// <summary>
        /// Creates and starts an activity for constant loop execution.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>        
        /// <returns></returns>
        static public Activity Run(string p_id,System.Predicate<Activity> p_callback) { return Run(p_id,p_callback,Context.Update); }

        /// <summary>
        /// Creates and starts an activity for constant loop execution.
        /// </summary>
        /// <param name="p_callback">Callback for handling the execution loop. Return 'true' to keep running or 'false' to stop.</param>        
        /// <returns></returns>
        static public Activity Run(System.Predicate<Activity> p_callback) { return Run("",p_callback,Context.Update); }

        #endregion

        #region Run / Once

        /// <summary>
        /// Creates and start a single execution activity.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <param name="p_context">Execution context.</param>
        /// <returns></returns>
        static public Activity Run(string p_id,System.Action<Activity> p_callback,Context p_context) {
            Activity a = new Activity(p_id,p_context);
            a.OnCompleteEvent = p_callback;
            a.OnExecuteEvent  = null;
            a.Start();
            return a;
        }

        /// <summary>
        /// Creates and start a single execution activity.
        /// </summary>
        /// <param name="p_id">Activity id for searching.</param>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <returns></returns>
        static public Activity Run(string p_id,System.Action<Activity> p_callback) { return Run(p_id,p_callback,Context.Update); }

        /// <summary>
        /// Creates and start a single execution activity.
        /// </summary>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <param name="p_context">Execution context.</param>
        /// <returns></returns>
        static public Activity Run(System.Action<Activity> p_callback,Context p_context) { return Run("",p_callback,p_context); }

        /// <summary>
        /// Creates and start a single execution activity.
        /// </summary>
        /// <param name="p_callback">Callback for handling the activity completion.</param>        
        /// <returns></returns>
        static public Activity Run(System.Action<Activity> p_callback) { return Run("",p_callback,Context.Update); }

        #endregion

        #endregion

        /// <summary>
        /// Id of this Activity.
        /// </summary>
        public string id;

        /// <summary>
        /// Execution state
        /// </summary>
        public State state { get; internal set; }        
        
        /// <summary>
        /// Execution context.
        /// </summary>
        public Context context { get; internal set; }

        /// <summary>
        /// Has the activity finished.
        /// </summary>
        public bool completed { get { return state == State.Complete; } }

        /// <summary>
        /// Callback for completion.
        /// </summary>
        public System.Action<Activity> OnCompleteEvent;

        /// <summary>
        /// Callback for execution.
        /// </summary>
        public System.Predicate<Activity> OnExecuteEvent;

        /// <summary>
        /// Helper task to use await.
        /// </summary>
        internal Task m_task;
        internal CancellationTokenSource m_task_cancel;
        internal int  m_yield_ms;

        /// <summary>
        /// Creates a new Activity.
        /// </summary>
        /// <param name="p_id">Activity id for searching</param>
        /// <param name="p_context">Execution Context</param>
        public Activity(string p_id,Context p_context) {                        
            state       = State.Idle;
            context     = p_context;
            string tn   = string.IsNullOrEmpty(m_type_name) ? (m_type_name = GetType().Name.ToLower()) : m_type_name;
            id          = string.IsNullOrEmpty(p_id) ? tn+"-"+GetHashCode().ToString("x6") : p_id;                             
        }
        private string m_type_name;
        private void OnTaskCompleteDummy() {            
            if(m_yield_ms>0) System.Threading.Thread.Sleep(m_yield_ms);
            m_task        = null;
            m_task_cancel = null;
        }

        /// <summary>
        /// Creates a new activity, default to 'Update' context.
        /// </summary>
        /// <param name="p_id">Activity id for searching</param>
        public Activity(string p_id) : this(p_id,Context.Update) { }

        /// <summary>
        /// Creates a new activity, default to 'Update' context and auto generated id.
        /// </summary>
        public Activity() : this("") { }

        /// <summary>
        /// Adds this activity to the queue.
        /// </summary>
        public void Start() {
            if(m_task==null) {
                m_task_cancel = new CancellationTokenSource();
                m_task = new Task(OnTaskCompleteDummy,m_task_cancel.Token);
            }
            m_yield_ms  = 0;
            if(manager)manager.AddActivity(this);            
        }

        /// <summary>
        /// Removes this activity from the execution pool.
        /// </summary>
        public void Stop() { if(manager)manager.RemoveActivity(this); }

        /// <summary>
        /// Auxiliary method to validate if starting is allowed.
        /// </summary>
        /// <returns></returns>
        virtual protected bool IsReady() { return true; }

        /// <summary>
        /// Handler for when the activity was removed.
        /// </summary>
        internal void OnStop() { 
            if(m_task==null) return;
            m_task_cancel.Cancel();
            m_task=null;
            m_task_cancel=null;
        }

        /// <summary>
        /// Executes one step.
        /// </summary>
        virtual internal void Execute() {
            switch(state) {
                case State.Complete: return;
                case State.Idle:     return;
                case State.Queued:  { 
                    if(!IsReady())   return;
                    OnStart(); 
                    state=State.Running; 
                }
                break;
            }
            switch(state) {
                case State.Running:  { 
                    bool v0 = OnExecute();
                    bool v1 = true;
                    if(OnExecuteEvent!=null) v1 = OnExecuteEvent(this);
                    if(v0 && v1) break;
                    state = State.Complete;
                    OnComplete();
                    if(OnCompleteEvent!=null) OnCompleteEvent(this);
                    if(m_task!=null) m_task.Start();                    
                }
                break;
            }
        }

        #region Virtuals

        /// <summary>
        /// Handler for activity execution start
        /// </summary>
        virtual protected void OnStart() { }

        /// <summary>
        /// Handler for activity execution loop steps.
        /// </summary>
        virtual protected bool OnExecute() { return OnExecuteEvent!=null; }

        /// <summary>
        /// Handler for activity completion.
        /// </summary>
        virtual protected void OnComplete() { }

        #endregion

        #region Async/Await

        /// <summary>
        /// Yields this activity until completion and wait delay seconds before continuying.
        /// </summary>
        /// <param name="p_delay">Extra delay seconds after completion</param>
        /// <returns></returns>
        public Task Yield(float p_delay=0f) {             
            m_yield_ms = (int)(p_delay*1000f);
            return m_task; 
        }

        /// <summary>
        /// Reference to the awaiter.
        /// </summary>
        /// <returns>Current awaiter for 'await' operator.</returns>
        public TaskAwaiter GetAwaiter() { return m_task==null ? new TaskAwaiter() : m_task.GetAwaiter(); }

        /// <summary>
        /// INotification implement.
        /// </summary>
        /// <param name="completed">Coninutation callback.</param>
        public void OnCompleted(System.Action completed) {
            completed();
        }

        #endregion

    }

}
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityExt.Core {

    /// <summary>
    /// Class that implements the management of a set of interfaces and activities.
    /// It can be used for controlling the execution loop in a different way than the standard one.
    /// </summary>
    public class ActivityManager {
    
            #region class List

            /// <summary>
            /// Base class to handle activities.
            /// </summary>
            internal class List {

                /// <summary>
                /// Activities
                /// </summary>
                public System.Collections.Generic.List<Activity> la;

                /// <summary>
                /// Current iterator for activities
                /// </summary>
                public int ia;

                /// <summary>
                /// Activity.Context of execution.
                /// </summary>
                public Activity.Context context;

                /// <summary>
                /// Time slice in ms
                /// </summary>
                public int timeSlice = 4; 

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
                public List(Activity.Context p_context) {
                la= new System.Collections.Generic.List<Activity>(1000);
                    ia = 0;
                    context = p_context;
                    timer   = context == Activity.Context.Async ? new System.Diagnostics.Stopwatch() : null;
                }

                /// <summary>
                /// Clear the lists
                /// </summary>
                virtual public void Clear() {
                    if(la!=null) la.Clear();
                    context = (Activity.Context)(-1);
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
                public System.Collections.Generic.List<Activity> FindAll(string p_id) {
                System.Collections.Generic.List<Activity> res = null;
                    if(la==null) return res = new System.Collections.Generic.List<Activity>();
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
                    bool is_async = context == Activity.Context.Async;
                    //If async prepare timer
                    if(is_async)  timer.Restart();
                    //If not async iterator is back to 0
                    if(!is_async) ia=0;
                    //Iterate across the list bounds
                    for(int i=0;i<la.Count;i++) {
                        //If async check timer slice limit and break out
                        if(is_async) if(timer.ElapsedMilliseconds>=timeSlice) break;
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
            internal class List<T> : List {

                /// <summary>
                /// Interface nodes
                /// </summary>
                public System.Collections.Generic.List<T> li;

                /// <summary>
                /// Current iterator for interfaces
                /// </summary>
                public int ii;

                /// <summary>
                /// CTOR
                /// </summary>
                public List(Activity.Context p_context) : base(p_context) {
                    li = new System.Collections.Generic.List<T>(1000);
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
                    bool is_async = context == Activity.Context.Async;
                    //Same of interfaces
                    if(is_async)  timer.Restart();
                    if(!is_async) ii=0;
                    //Loop
                    for(int i=0;i<li.Count;i++) {
                        //Cast the interface based on the context.
                        switch(context) {
                            case Activity.Context.Update:      { IUpdateable      it = (IUpdateable)     li[ii]; it.OnUpdate();      } break;
                            case Activity.Context.LateUpdate:  { ILateUpdateable  it = (ILateUpdateable) li[ii]; it.OnLateUpdate();  } break;
                            case Activity.Context.Async:       { IAsyncUpdateable it = (IAsyncUpdateable)li[ii]; it.OnAsyncUpdate(); } break;
                            case Activity.Context.FixedUpdate: { IFixedUpdateable it = (IFixedUpdateable)li[ii]; it.OnFixedUpdate(); } break;
                        }
                        //Same as above
                        ii = (ii+1)%li.Count;
                        if(is_async) if(timer.ElapsedMilliseconds>=timeSlice) break;
                    }
                }

                #endregion

            }

            #endregion

            /// <summary>
            /// Internals.
            /// </summary>
            internal System.Threading.Thread thread_loop;
            internal List<IUpdateable>      lu;
            internal List<ILateUpdateable>  llu;
            internal List<IFixedUpdateable> lfu;
            internal List<IAsyncUpdateable> lau;
            internal System.Collections.Generic.List<Activity> lt;
            internal System.Collections.Generic.List<Activity> ltq;            
            internal float  thread_keep_alive_tick;            
            internal bool   thread_kill_flag;

            /// <summary>
            /// CTOR
            /// </summary>
            public void Awake() {
                if(lu==null)  lu  = new List<IUpdateable>(Activity.Context.Update);
                if(llu==null) llu = new List<ILateUpdateable>(Activity.Context.LateUpdate);
                if(lfu==null) lfu = new List<IFixedUpdateable>(Activity.Context.FixedUpdate);
                if(lau==null) lau = new List<IAsyncUpdateable>(Activity.Context.Async);
                if(lt==null)  lt  = new System.Collections.Generic.List<Activity>();                
                if(ltq==null) ltq = new System.Collections.Generic.List<Activity>();
            }

            /// <summary>
            /// CTOR.
            /// </summary>
            public void Start() {
                //Assert thread on each start in case of scene changes and thread suspension or abortion
                thread_keep_alive_tick = 0f;             
                //Kill flag to cleanly stop the thread
                thread_kill_flag       = false;
            }

            /// <summary>
            /// Clears the manager execution.
            /// </summary>
            public void Clear() {
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
            internal List GetActivityList(Activity.Context p_context) {
                switch(p_context) {
                    case Activity.Context.Update:      return lu;
                    case Activity.Context.LateUpdate:  return llu;
                    case Activity.Context.FixedUpdate: return lfu;
                    case Activity.Context.Async:       return lau;
                }
                return null;
            }

            /// <summary>
            /// Adds a new activity node.
            /// </summary>
            /// <param name="p_activity"></param>
            public void AddActivity(Activity p_activity) {
                Activity a = p_activity;
                if(a==null)               return;
                if(a.state != Activity.State.Idle) return;
                a.state = Activity.State.Queued;
                switch(a.context) {
                    case Activity.Context.Update:      if(!lu.la.Contains(a))  lu.la.Add(a);  break;
                    case Activity.Context.LateUpdate:  if(!llu.la.Contains(a)) llu.la.Add(a); break;
                    case Activity.Context.FixedUpdate: if(!lfu.la.Contains(a)) lfu.la.Add(a); break;
                    case Activity.Context.Async:       if(!lau.la.Contains(a)) lau.la.Add(a); break;
                    //Threaded lists its better to enqueue in a secondary list and let the main execution add the list in a synced way
                    case Activity.Context.Thread:    { if(!ltq.Contains(a)) ltq.Add(a); AssertThread(); break; }
                }
            }

            /// <summary>
            /// Removes a executing node.
            /// </summary>
            /// <param name="p_activity"></param>
            public void RemoveActivity(Activity p_activity) {
                Activity a = p_activity;
                if(a==null)               return;                
                if(a.state == Activity.State.Idle) return;                
                int idx;
                switch(a.context) {
                    case Activity.Context.Update:      if(lu.la.Contains(a))  lu.la.Remove(a);  break;
                    case Activity.Context.LateUpdate:  if(llu.la.Contains(a)) llu.la.Remove(a); break;
                    case Activity.Context.FixedUpdate: if(lfu.la.Contains(a)) lfu.la.Remove(a); break;
                    case Activity.Context.Async:       if(lau.la.Contains(a)) lau.la.Remove(a); break;
                    //Threaded lists its better to null the element and allow removal during the synced execution of the thread
                    case Activity.Context.Thread:      { 
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
            public void AddInterface(object p_interface) {
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
            public void RemoveInterface(object p_interface) {
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
            public Activity Find(string p_id,Activity.Context p_context) {
                //Local ref
                Activity res = null;
                //Shortcut bool
                bool all_ctx = ((int)p_context)<0;
                //Search threads if threaded or all ctxs
                bool search_threads = p_context == Activity.Context.Thread ? true : all_ctx;
                //Search thread nodes
                if(p_context == Activity.Context.Thread) {
                    m_id_query = p_id;
                    //Search active nodes
                    res = lt.Find(FindActivityById);
                    //Search queued nodes if no result
                    if(res==null) res = ltq.Find(FindActivityById);
                    //Clear global search query
                    m_id_query = "";
                    //If thread only return
                    if(p_context == Activity.Context.Thread) return res;
                    //If there is a result return
                    if(res!=null) return res;
                }
                //Try fetch the activity list
                List la = GetActivityList(p_context);
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
            public Activity Find(string p_id) { return Find(p_id,(Activity.Context)(-1)); }

            /// <summary>
            /// Finds all activities, matching the id.
            /// </summary>
            /// <param name="p_id"></param>
            /// <param name="p_context"></param>
            /// <returns></returns>
            public System.Collections.Generic.List<Activity> FindAll(string p_id,Activity.Context p_context) {
            //Local ref
            System.Collections.Generic.List<Activity> res = new System.Collections.Generic.List<Activity>();
                //Shortcut bool
                bool all_ctx = ((int)p_context)<0;
                //Search threads if threaded or all ctxs
                bool search_threads = p_context == Activity.Context.Thread ? true : all_ctx;
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
                    if(p_context == Activity.Context.Thread) return res;
                }
                //Try fetch the activity list
                List la = GetActivityList(p_context);
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
            public System.Collections.Generic.List<Activity> FindAll(string p_id) { return FindAll(p_id,(Activity.Context)(-1)); }

            #endregion

            #region Loops

            /// <summary>
            /// Asserts the lifetime of the thread running the thread loop nodes.
            /// </summary>
            protected void AssertThread() {
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
            public void Update() {
                lu.Execute();
                lau.Execute();
                //Check health state of threading each 2s
                if(thread_keep_alive_tick<=0) { AssertThread(); thread_keep_alive_tick=2f; }
                thread_keep_alive_tick-=Time.unscaledDeltaTime;
            }

            /// <summary>
            /// FixedUpdate Loop
            /// </summary>
            public void FixedUpdate() { lfu.Execute();  }

            /// <summary>
            /// LateUpdate Loop
            /// </summary>
            public void LateUpdate()  { llu.Execute();  }

            /// <summary>
            /// ThreadUpdate Loop
            /// </summary>
            protected void ThreadUpdate() {
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

}
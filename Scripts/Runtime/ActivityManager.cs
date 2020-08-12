using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using SCG = System.Collections.Generic;
using System.Threading;

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
                    la = new System.Collections.Generic.List<Activity>(15000);
                    ia = 0;
                    context = p_context;
                    timer   = context == Activity.Context.Async ? new System.Diagnostics.Stopwatch() : null;                    
                }

                /// <summary>
                /// Check if the execution pool contains elements.
                /// </summary>
                /// <returns></returns>
                virtual public bool IsEmpty() {
                    return la.Count<=0;
                }

                /// <summary>
                /// Clear the lists
                /// </summary>
                virtual public void Clear() {
                    //Threads needs to null the element and handle inside thread context
                    if(context == Activity.Context.Thread) {
                        if(la!=null) for(int i=0;i<la.Count;i++) la[i]=null;
                    }
                    else {
                        if(la!=null) {
                            //Notify running activities for stopping
                            for(int i=0;i<la.Count;i++) if(la[i]!=null) la[i].OnStop();
                            la.Clear();
                        }
                    }
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
                    for(int i=0;i<la.Count;i++) { 
                        if(i<0)         break;
                        if(i>=la.Count) break;
                        if(la[i]==null ? true : la[i].completed) {                            
                            if(i>=0)if(i<la.Count)la.RemoveAt(i--);
                        }
                    }
                    //Skip if empty
                    if(la.Count<=0) return;
                    //Shortcut bool
                    bool is_async = context == Activity.Context.Async;
                    //If async prepare timer
                    if(is_async)  timer.Restart();
                    //If not async iterator is back to 0
                    if(!is_async) ia=0;
                    //Iterate across the list bounds
                    for(int i=0;i<la.Count;i++) {
                        //If async check timer slice limit and break out
                        if(is_async) if(timer.ElapsedMilliseconds>=Activity.asyncTimeSlice) break;
                        //Use iterator for async cases
                        Activity it = ia<0 ? null : (ia>=la.Count ? null : la[ia]);
                        //Check if node can be executed
                        bool is_valid = it==null ? false : !it.completed;                        
                        //Steps the activity if valid
                        if(is_valid) it.Execute();        
                        //Range check
                        int c = la.Count;
                        //Increment-loop the iterator (async might be mid iteration)
                        ia = c<=0 ? 0 : ((ia+1)%c);
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
                public SCG.List<T> li;

                /// <summary>
                /// Current iterator for interfaces
                /// </summary>
                public int ii;

                /// <summary>
                /// CTOR
                /// </summary>
                public List(Activity.Context p_context) : base(p_context) {
                    li = new SCG.List<T>(5000);
                    ii = 0;                    
                }

                /// <summary>
                /// Check if this list is totally empty.
                /// </summary>
                /// <returns></returns>
                override public bool IsEmpty() { return base.IsEmpty() ? li.Count<=0 : false; }

                /// <summary>
                /// Clear this execution list.
                /// </summary>
                override public void Clear() {
                    //Clear activities
                    base.Clear(); 
                    //Threads needs to null the element and handle inside thread context
                    if(context == Activity.Context.Thread) {
                        if(li!=null) for(int i=0;i<li.Count;i++) li[i]=default(T);
                    }
                    else {
                        if(li!=null) li.Clear();
                    }                    
                }

                #region Execute

                /// <summary>
                /// Executes the lists in the chosen context.
                /// </summary>
                override public void Execute() {                    
                    //Iterate activities
                    base.Execute();                                        
                    //Prune interfaces
                    for(int i=0;i<li.Count;i++) { 
                        if(i<0) continue;
                        if(i>=li.Count) break;
                        if(li[i]!=null) continue;
                        if(i>=0)if(i<li.Count)li.RemoveAt(i--); 
                    }
                    //Skip if empty
                    if(li.Count<=0) return;
                    //Shortcut bool
                    bool is_async = context == Activity.Context.Async;
                    //Same of interfaces
                    if(is_async)  timer.Restart();
                    if(!is_async) ii=0;
                    //Loop
                    for(int i=0;i<li.Count;i++) {
                        object li_it = ii<0 ? default(T) : (ii>=li.Count ? default(T) : li[ii]);
                        //Skip invalid
                        if(li_it==null) continue;                        
                        //Cast the interface based on the context.
                        switch(context) {
                            case Activity.Context.Update:      { IUpdateable       it = (IUpdateable)      li_it; if(it!=null)it.OnUpdate();       } break;
                            case Activity.Context.LateUpdate:  { ILateUpdateable   it = (ILateUpdateable)  li_it; if(it!=null)it.OnLateUpdate();   } break;
                            case Activity.Context.Async:       { IAsyncUpdateable  it = (IAsyncUpdateable) li_it; if(it!=null)it.OnAsyncUpdate();  } break;
                            case Activity.Context.FixedUpdate: { IFixedUpdateable  it = (IFixedUpdateable) li_it; if(it!=null)it.OnFixedUpdate();  } break;
                            case Activity.Context.Thread:      { IThreadUpdateable it = (IThreadUpdateable)li_it; if(it!=null)it.OnThreadUpdate(); } break;
                        }
                        //Same as above
                        int c = li.Count;
                        ii = c<=0 ? 0 : (ii+1)%c;
                        if(is_async) if(timer.ElapsedMilliseconds>=Activity.asyncTimeSlice) break;
                    }
                }

                #endregion

            }

            #endregion

            /// <summary>
            /// Internals.
            /// </summary>            
            internal List<IUpdateable>         lu;
            internal List<ILateUpdateable>     llu;
            internal List<IFixedUpdateable>    lfu;
            internal List<IAsyncUpdateable>    lau;
            internal List<IThreadUpdateable>[] lt;
            /// <summary>
            /// Internals threading.
            /// </summary>
            internal Thread[] thdl;            
            internal SCG.List<Activity> ltq_a;
            internal SCG.List<IThreadUpdateable> ltq_i;
            internal float  thread_keep_alive_tick;            
            internal bool   thread_kill_flag;
            internal int    thread_queue_target_a;
            internal int    thread_queue_target_i;
            internal int    thread_assert_target;

            /// <summary>
            /// CTOR
            /// </summary>
            public void Awake() {
                if(lu==null)  lu  = new List<IUpdateable>(Activity.Context.Update);
                if(llu==null) llu = new List<ILateUpdateable>(Activity.Context.LateUpdate);
                if(lfu==null) lfu = new List<IFixedUpdateable>(Activity.Context.FixedUpdate);
                if(lau==null) lau = new List<IAsyncUpdateable>(Activity.Context.Async);                
                if(ltq_a==null) ltq_a = new SCG.List<Activity>();                
                if(ltq_i==null) ltq_i = new SCG.List<IThreadUpdateable>();
                //Init threads based on max allowed threads.
                int max_thread = Activity.maxThreadCount;
                if(lt==null)   lt   = new List<IThreadUpdateable>[max_thread];
                for(int i=0;i<lt.Length;i++) lt[i] = new List<IThreadUpdateable>(Activity.Context.Thread);
                if(thdl==null) thdl = new Thread[max_thread];
                //Index of the thread to add nodes next
                thread_queue_target_a = 0;
                thread_queue_target_i = 0;
                thread_assert_target  = 0;
            }

            /// <summary>
            /// CTOR.
            /// </summary>
            public void Start() {
                //Assert thread on each start in case of scene changes and thread suspension or abortion
                thread_keep_alive_tick = 0f;             
                //Kill flag to cleanly stop the thread
                thread_kill_flag = false;
            }

            /// <summary>
            /// Check is this manager is without any node.
            /// </summary>
            /// <returns></returns>
            public bool IsEmpty() {
                if(lu!=null)  if(!lu.IsEmpty())  return false;
                if(llu!=null) if(!llu.IsEmpty()) return false;
                if(lfu!=null) if(!lfu.IsEmpty()) return false;
                if(lau!=null) if(!lau.IsEmpty()) return false;                
                for(int i=0;i<lt.Length;i++) { if(!lt[i].IsEmpty()) return false; }
                if(ltq_a!=null) if(ltq_a.Count>0) return false;
                if(ltq_i!=null) if(ltq_i.Count>0) return false;
                return true;
            }

            /// <summary>
            /// Clears the manager execution.
            /// </summary>
            public void Clear() {
                if(lu!=null)  lu.Clear();
                if(llu!=null) llu.Clear();
                if(lfu!=null) lfu.Clear();
                if(lau!=null) lau.Clear();
                for(int i=0;i<lt.Length;i++) { lt[i].Clear(); }
                if(ltq_a!=null) ltq_a.Clear();
                if(ltq_i!=null) ltq_i.Clear();
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
                    case Activity.Context.Job:
                    case Activity.Context.JobAsync:
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
                //Skip invalid
                if(a==null)               return;
                //Only accept correctly stopped tasks
                if(a.state==Activity.State.Running) { Debug.LogWarning($"ActivityManager> Activity [{p_activity.id}] already running."); return; }
                if(a.state==Activity.State.Queued)  { Debug.LogWarning($"ActivityManager> Activity [{p_activity.id}] already queued.");  return; }
                a.state = Activity.State.Queued;
                switch(a.context) {
                    case Activity.Context.Job:
                    case Activity.Context.JobAsync:
                    case Activity.Context.Update:      if(!lu.la.Contains(a))  lu.la.Add(a);  break;
                    case Activity.Context.LateUpdate:  if(!llu.la.Contains(a)) llu.la.Add(a); break;
                    case Activity.Context.FixedUpdate: if(!lfu.la.Contains(a)) lfu.la.Add(a); break;
                    case Activity.Context.Async:       if(!lau.la.Contains(a)) lau.la.Add(a); break;
                    //Threaded lists its better to enqueue in a secondary list and let the main execution add the list in a synced way
                    case Activity.Context.Thread:    {                        
                        if(ltq_a.Contains(a)) break;
                        //Add and trigger the thread creation
                        ltq_a.Add(a); 
                        AssertThread(); 
                        break; 
                    }
                }
            }

            /// <summary>
            /// Removes a executing node.
            /// </summary>
            /// <param name="p_activity"></param>
            public void RemoveActivity(Activity p_activity) {
                Activity a = p_activity;
                //Skip invalid
                if(a==null) return;                
                //Only accept active/running
                if(a.state==Activity.State.Complete) { Debug.LogWarning($"ActivityManager> Activity [{p_activity.id}] already completed."); return; }
                if(a.state==Activity.State.Stopped)  { Debug.LogWarning($"ActivityManager> Activity [{p_activity.id}] already stopped.");   return; }
                int idx;
                switch(a.context) {
                    case Activity.Context.Job:
                    case Activity.Context.JobAsync:
                    case Activity.Context.Update:      if(lu.la.Contains(a))  lu.la.Remove(a);  break;
                    case Activity.Context.LateUpdate:  if(llu.la.Contains(a)) llu.la.Remove(a); break;
                    case Activity.Context.FixedUpdate: if(lfu.la.Contains(a)) lfu.la.Remove(a); break;
                    case Activity.Context.Async:       if(lau.la.Contains(a)) lau.la.Remove(a); break;
                    //Threaded lists its better to null the element and allow removal during the synced execution of the thread
                    case Activity.Context.Thread:      { 
                        //Search for the activity and null it for next pruning
                        for(int i=0;i<lt.Length;i++) {
                            List it = lt[i];
                            idx = it.la.IndexOf(a); 
                            if(idx<0) continue;
                            it.la[idx] = null;                             
                            break;
                        }                        
                        //Search the queue too
                        idx = ltq_a.IndexOf(a); 
                        if(idx>=0) ltq_a[idx] = null;
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
                if(p_interface is IThreadUpdateable) {
                    IThreadUpdateable itf = (IThreadUpdateable)p_interface;
                    //Threaded lists its better to enqueue in a secondary list and let the main execution add the list in a synced way                    
                    if(ltq_i.Contains(itf)) return;
                    //Add and trigger the thread creation if not created
                    ltq_i.Add(itf); 
                    AssertThread();
                }
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
                if(p_interface is IThreadUpdateable) {
                    IThreadUpdateable itf = (IThreadUpdateable)p_interface;
                    int idx = -1;
                    //Search for the interface and null it for next pruning
                    for(int i=0;i<lt.Length;i++) {
                        List<IThreadUpdateable> it = lt[i];
                        idx = it.li.IndexOf(itf); 
                        if(idx<0) continue;
                        it.li[idx] = null;                             
                        break;
                    }             
                    //Skip if found
                    if(idx>=0) return;
                    //Search the queue too
                    idx = ltq_i.IndexOf(itf); 
                    if(idx>=0) if(idx<ltq_i.Count) ltq_i[idx] = null;
                }
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
                    for(int i=0;i<lt.Length;i++) {
                        List it = lt[i];
                        //Search active node
                        res = it.Find(m_id_query);
                        //If found break
                        if(res!=null) break;
                    }                    
                    //Search queued nodes if no result
                    if(res==null) res = ltq_a.Find(FindActivityById);
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
                    for(int i=0;i<lt.Length;i++) {
                        List it = lt[i];
                        //Search active nodes
                        res.AddRange(it.FindAll(m_id_query));                        
                    }                      
                    //Search queued nodes
                    if(ltq_a!=null)res.AddRange(ltq_a.FindAll(FindActivityById));
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
            public SCG.List<Activity> FindAll(string p_id) { return FindAll(p_id,(Activity.Context)(-1)); }

            #endregion

            #region Loops

            /// <summary>
            /// Assert all threads.
            /// </summary>
            protected void AssertThread() {                 
                //Assert threads a bit per frame
                AssertThread(thread_assert_target);
                thread_assert_target = (thread_assert_target+1)%lt.Length;
            }

            /// <summary>
            /// Asserts the lifetime of the thread running the thread loop nodes.
            /// </summary>
            protected void AssertThread(int p_index) {
                //If kill switch skip
                if(thread_kill_flag) return;
                //Thread index
                int idx = p_index;
                List<IThreadUpdateable> l = lt[idx];
                //If no nodes executing and no nodes queued skip
                if(l.IsEmpty()) if(ltq_a.Count<=0) if(ltq_i.Count<=0) return;
                //Fetch thread and its state
                Thread  thd   = thdl[idx];                
                int     thd_s = thd==null ? -1 : (int)thd.ThreadState;
                //If thread active/sleep skips
                if(thd!=null) {
                    if(thd_s == 0)  return; //Running 
                    if(thd_s == 32) return; //WaitSleepJoin
                }
                //Create and start the thread
                thd = new System.Threading.Thread(
                delegate() { 
                    while(true) {
                        //If kill thread clear all and break out, reset the flag
                        if(thread_kill_flag) break;
                        //If current queuing slot
                        bool will_queue = idx == thread_queue_target_a;
                        //Execute all nodes
                        ThreadUpdate(l,idx);
                        //Sleep 0 to yield CPU if possible
                        System.Threading.Thread.Sleep(0);
                        //Stop the thread if no nodes
                        if(l.IsEmpty()) if(ltq_a.Count<=0) if(ltq_i.Count<=0) break;
                    }
                    thdl[idx]=null;                    
                });
                thd.Name = "activity-thread-"+idx;
                thdl[idx] = thd;
                thd.Start();                                
            }

            /// <summary>
            /// Update Loop
            /// </summary>
            public void Update() {
                lu.Execute();
                lau.Execute();
                //Check health state of threading each 0.1s
                if(thread_keep_alive_tick<=0) { AssertThread(); thread_keep_alive_tick=0.1f; }
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
            internal void ThreadUpdate(List<IThreadUpdateable> p_list,int p_index) {
                //Fetch the update list
                List<IThreadUpdateable> l = p_list;
                //Move new queued activity into the main list
                if(p_index == thread_queue_target_a) {
                    //Insert activities from queue
                    while(ltq_a.Count>0) {
                        //Dequeue elements until insertion
                        Activity a = ltq_a[0];
                        ltq_a.RemoveAt(0);
                        //Skip invalid
                        if(a==null) continue;
                        //Skip invalid
                        if(l.la.Contains(a)) continue;
                        //Add the element and increment-loop the next thread
                        l.la.Add(a);
                        thread_queue_target_a = (thread_queue_target_a+1)%lt.Length;
                        break;
                    }
                }
                //Move new queued interfaces into the main list
                if(p_index == thread_queue_target_i) {
                    //Insert interfaces from queue
                    while(ltq_i.Count>0) {
                        //Dequeue elements until insertion
                        IThreadUpdateable itf = ltq_i[0];
                        ltq_i.RemoveAt(0);
                        //Skip invalid
                        if(itf==null) continue;
                        //Skip invalid
                        if(l.li.Contains(itf)) continue;
                        //Add the element and increment-loop the next thread
                        l.li.Add(itf);
                        thread_queue_target_i = (thread_queue_target_i+1)%lt.Length;
                        break;
                    }
                }                                
                //Executes threaded nodes.
                l.Execute();                
            }

            #endregion

        }

}
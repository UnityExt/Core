using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using SCG = System.Collections.Generic;
using System.Threading;
using System;

namespace UnityExt.Core {

    /// <summary>
    /// Class that implements the management of a set of interfaces and activities.
    /// It can be used for controlling the execution loop in a different way than the standard one.
    /// </summary>
    public class ActivityManager {

            #region static

            /// <summary>
            /// Profile Clock
            /// </summary>
            static public System.Diagnostics.Stopwatch profilerClock;

            /// <summary>
            /// Elapsed Ms of the Profiler Clock
            /// </summary>
            static public float profilerClockMs { 
                get { 
                    long   ms    = profilerClock==null ? 0 : profilerClock.ElapsedMilliseconds; 
                    double cf    = System.Diagnostics.Stopwatch.Frequency;
                    float  ms_f  = cf<=0f ? 0f : (float)(((double)profilerClockCycles / cf)*1000.0);
                    return ms_f;
                }
            }

            /// <summary>
            /// Elapsed cycles of the profiler clock
            /// </summary>
            static public long profilerClockCycles { get { return profilerClock==null ? 0 : profilerClock.ElapsedTicks; } }

            /// <summary>
            /// Flag that tells profiling will be made.
            /// </summary>
            static public bool profilerClockEnabled;

            /// <summary>
            /// CTOR
            /// </summary>
            static ActivityManager() {
                if(profilerClock==null) { profilerClock = new System.Diagnostics.Stopwatch(); profilerClock.Start(); }
                profilerClockEnabled = Debug.isDebugBuild ? true : Application.isEditor;
            }

            #endregion

    
            #region class List

            /// <summary>
            /// Base class to handle activities.
            /// </summary>
            internal class List {

                /// <summary>
                /// Activities
                /// </summary>
                public SCG.List<Activity> la;

                /// <summary>
                /// Current iterator for activities
                /// </summary>
                public int ia;

                /// <summary>
                /// Activity.Context of execution.
                /// </summary>
                public ActivityContext context;

                /// <summary>
                /// Timer for async loops.
                /// </summary>
                public System.Diagnostics.Stopwatch timer;

                /// <summary>
                /// Last execution ms.
                /// </summary>
                public float profilerMs;

                /// <summary>
                /// Last execution ns
                /// </summary>
                public long  profilerUs { get { return (long)(Mathf.Round(profilerMs*10f)*100f); } }

                /// <summary>
                /// Returns a formatted string telling the profiled time.
                /// </summary>
                public string profilerTimeStr { get { long ut = profilerUs; return profilerMs<1f ? (ut<=0 ? "0 ms" : $"{ut} us") : $"{Mathf.RoundToInt(profilerMs)} ms"; }  }

                /// <summary>
                /// Returns the number of activities.
                /// </summary>
                public int ActivityCount { get { return la==null ? 0 : la.Count; } }

                /// <summary>
                /// Returns the count for all node types.
                /// </summary>
                virtual public int Count { get { return ActivityCount; } }

                /// <summary>
                /// CTOR.
                /// </summary>
                public List(ActivityContext p_context) {
                    la = new SCG.List<Activity>(15000);
                    ia = 0;
                    context = p_context;
                    timer   = context == ActivityContext.Async  ? new System.Diagnostics.Stopwatch() : null;                    
                }

                /// <summary>
                /// Check if the execution pool contains elements.
                /// </summary>
                /// <returns></returns>
                virtual public bool IsEmpty() {
                    return la==null ? true : la.Count<=0;
                }

                /// <summary>
                /// Clear the lists
                /// </summary>
                virtual public void Clear() {
                    //Stop running timer
                    if(timer!=null) timer.Stop();
                    //Skip invalid
                    if(la==null) return;
                    switch(context) {
                        case ActivityContext.Thread: {
                            lock(this) { 
                                //Threads needs to null the element and handle inside thread context
                                for(int i=0;i<la.Count;i++) if(la[i]!=null) { la[i].OnManagerRemoveInternal(); la[i]=null; }
                            }
                        }
                        break;
                        default:  {
                            for(int i=0;i<la.Count;i++) if(la[i]!=null) la[i].OnManagerRemoveInternal();
                            la.Clear();                        
                        }
                        break;
                    }                    
                }

                /// <summary>
                /// Given an activity invalidates it.
                /// </summary>
                /// <param name="p_activity"></param>
                internal bool SafeInvalidate(Activity p_activity) {
                    int idx = -1;
                    lock(this) { 
                        idx = la.IndexOf(p_activity); 
                        if(idx>=0) la[idx] = null;
                    }
                    return idx>=0;
                }

                /// <summary>
                /// Safe Add a new element.
                /// </summary>
                /// <param name="p_activity"></param>
                internal void SafeAdd(Activity p_activity) {
                    if(p_activity==null) return;
                    lock(this) { 
                        if(!la.Contains(p_activity)) la.Add(p_activity);
                    }
                }

                /// <summary>
                /// Searches for an activity by its id.
                /// </summary>
                /// <param name="p_id"></param>
                /// <returns></returns>
                public T Find<T>(string p_id) where T : Activity {
                    T res = null;
                    if(la==null) return res;                    
                    for(int i=0;i<la.Count;i++) if(QueryMatch<T>(la[i],p_id)) { res = (T)la[i]; break; }                    
                    return res;
                }

                /// <summary>
                /// Finds all activities matching the id.
                /// </summary>
                /// <param name="p_id"></param>
                /// <returns></returns>
                public SCG.List<T> FindAll<T>(string p_id) where T : Activity {
                    SCG.List<T> res = new SCG.List<T>();
                    if(la==null) return res;                    
                    for(int i=0;i<la.Count;i++) {                        
                        if(QueryMatch<T>(la[i],p_id)) res.Add((T)la[i]);
                    }
                    return res;
                }

                /// <summary>
                /// Helper method to search activities.
                /// </summary>
                /// <param name="it"></param>
                /// <returns></returns>
                internal bool QueryMatch<T>(Activity it,string p_id) where T : Activity { 
                    if(it==null) return false;
                    if(p_id!="") if(it.id!=p_id) return false;
                    if(!(it is T)) return false;                    
                    return true;
                }
                
                #region Execute

                /// <summary>
                /// Executes all activities.
                /// </summary>
                virtual public void Execute() {
                    
                    lock(this) { 
                        //Prune activities
                        for(int i=0;i<la.Count;i++) {
                            if(la[i]==null ? true : la[i].completed) {
                                la.RemoveAt(i--);
                            }
                        }                    
                    }

                    //Skip if empty
                    if(la.Count<=0) return;
                    //Shortcut bool
                    bool is_async = context == ActivityContext.Async;
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
                        bool can_execute = it==null ? false : (it.completed ? false : it.enabled);                                               
                        float t0 = profilerClockEnabled ? profilerClockMs : 0;
                        //Steps the activity if valid
                        if(can_execute) it.Execute();        
                        float t1 = profilerClockEnabled ? profilerClockMs : 0;
                        //Store profile ms
                        it.profilerMs = profilerClockEnabled ? (t1-t0) : 0;
                        //Accumulate profile time
                        profilerMs += it.profilerMs;
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
                /// Returns the number of interfaces running.
                /// </summary>
                public int InterfaceCount { get { return li==null ? 0 : li.Count; } }

                /// <summary>
                /// Returns the count for all node types.
                /// </summary>
                override public int Count { get { return base.Count + InterfaceCount; } }

                /// <summary>
                /// CTOR
                /// </summary>
                public List(ActivityContext p_context) : base(p_context) {
                    li = new SCG.List<T>(15000);
                    ii = 0;                    
                }

                /// <summary>
                /// Check if this list is totally empty.
                /// </summary>
                /// <returns></returns>
                override public bool IsEmpty() { return base.IsEmpty() ? (li==null ? true : li.Count<=0) : false; }

                /// <summary>
                /// Safe Add a new element.
                /// </summary>
                /// <param name="p_interface"></param>
                internal void SafeAdd(T p_interface) {
                    if(p_interface==null) return;
                    lock(this) { 
                        if(!li.Contains(p_interface)) li.Add(p_interface);
                    }
                }

                /// <summary>
                /// Given an activity invalidates it.
                /// </summary>
                /// <param name="p_activity"></param>
                internal bool Invalidate(T p_interface) {
                    int idx = -1;
                    lock(this) {                         
                        idx = li.IndexOf(p_interface); 
                        if(idx>=0) li[idx] = default(T);
                    }
                    return idx>=0;
                }

                /// <summary>
                /// Clear this execution list.
                /// </summary>
                override public void Clear() {
                    //Clear activities
                    base.Clear(); 
                    //Skip invalid
                    if(li==null) return;
                    switch(context) {
                        case ActivityContext.Thread: {
                            lock(this) { 
                                //Threads needs to null the element and handle inside thread context
                                for(int i=0;i<li.Count;i++) li[i]=default(T);
                            }
                        }
                        break;
                        default: {
                            li.Clear();
                        }
                        break;
                    }    
                }

                #region Execute

                /// <summary>
                /// Executes the lists in the chosen context.
                /// </summary>
                override public void Execute() {                
                
                    //Reset ms
                    profilerMs=0;
                    
                    //Iterate activities
                    base.Execute();                                        

                    lock(this) { 
                        //Prune interfaces
                        for(int i=0;i<li.Count;i++) { 
                            if(li[i]==null)li.RemoveAt(i--); 
                        }
                    }
                    //Skip if empty
                    if(li.Count<=0) {                        
                        return;
                    }
                    
                    //Shortcut bool
                    bool is_async = context == ActivityContext.Async;
                    //Same of interfaces
                    if(is_async)  timer.Restart();
                    if(!is_async) ii=0;
                    //Loop
                    for(int i=0;i<li.Count;i++) {
                        object li_it = ii>=li.Count ? default(T) : li[ii];
                        //Skip invalid
                        if(li_it==null) continue;                        
                        ActivityBehaviour b = li_it is ActivityBehaviour ? (ActivityBehaviour)li_it : null;
                        float t0 = profilerClockEnabled ? profilerClockMs : 0;
                        //Cast the interface based on the context.
                        switch(context) {
                            case ActivityContext.Update:      { IUpdateable       it = (IUpdateable)      li_it; if(it!=null)it.OnUpdate();       } break;
                            case ActivityContext.LateUpdate:  { ILateUpdateable   it = (ILateUpdateable)  li_it; if(it!=null)it.OnLateUpdate();   } break;
                            case ActivityContext.Async:       { IAsyncUpdateable  it = (IAsyncUpdateable) li_it; if(it!=null)it.OnAsyncUpdate();  } break;
                            case ActivityContext.FixedUpdate: { IFixedUpdateable  it = (IFixedUpdateable) li_it; if(it!=null)it.OnFixedUpdate();  } break;
                            case ActivityContext.Thread:      { IThreadUpdateable it = (IThreadUpdateable)li_it; if(it!=null)it.OnThreadUpdate(); } break;
                        }
                        //Same as above
                        int c = li.Count;
                        ii = c<=0 ? 0 : (ii+1)%c;
                        float t1 = profilerClockEnabled ? profilerClockMs : 0;
                        float dt = t1-t0;
                        if(b) b.profilerMs = dt;
                        //Accumulate profile time
                        profilerMs += dt;
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
            internal SCG.List<Action> ltq_jobs;            
            internal float  thread_keep_alive_tick;            
            internal bool   thread_kill_flag;            
            internal int    thread_queue_target_a;
            internal int    thread_queue_target_i;
            internal int    thread_assert_target;
            
            /// <summary>
            /// Returns the count based on interface type.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <returns></returns>
            public int GetCount<T>() {
                if(typeof(T) == typeof(IUpdateable))       return lu.ActivityCount  + lu.InterfaceCount;
                if(typeof(T) == typeof(ILateUpdateable))   return llu.ActivityCount + llu.InterfaceCount;
                if(typeof(T) == typeof(IFixedUpdateable))  return lfu.ActivityCount + lfu.InterfaceCount;
                if(typeof(T) == typeof(IAsyncUpdateable))  return lau.ActivityCount + lau.InterfaceCount;
                if(typeof(T) == typeof(IThreadUpdateable)) { int c=0; for(int i=0;i<lt.Length;i++) { c+= lt[i].ActivityCount + lt[i].InterfaceCount; } return c; }                
                return 0;
            }

            /// <summary>
            /// Returns the number of all executing nodes.
            /// </summary>
            /// <returns></returns>
            public int GetCountTotal() {
                int c=0;
                c+= lu.ActivityCount  + lu.InterfaceCount;
                c+= llu.ActivityCount + llu.InterfaceCount;
                c+= lfu.ActivityCount + lfu.InterfaceCount;
                c+= lau.ActivityCount + lau.InterfaceCount;
                for(int i=0;i<lt.Length;i++) { c+= lt[i].ActivityCount + lt[i].InterfaceCount; }
                return c;
            }

            /// <summary>
            /// CTOR
            /// </summary>
            public void Awake() {                                
                AssertLists();
                //Index of the thread to add nodes next
                thread_queue_target_a = 0;
                thread_queue_target_i = 0;
                thread_assert_target  = 0;                
            }

            /// <summary>
            /// Asserts the existance of the needed lists.
            /// </summary>
            protected void AssertLists() {
                if(lu==null)  lu  = new List<IUpdateable>(ActivityContext.Update);
                if(llu==null) llu = new List<ILateUpdateable>(ActivityContext.LateUpdate);
                if(lfu==null) lfu = new List<IFixedUpdateable>(ActivityContext.FixedUpdate);
                if(lau==null) lau = new List<IAsyncUpdateable>(ActivityContext.Async);                
                if(ltq_a==null)      ltq_a    = new SCG.List<Activity>();                
                if(ltq_i==null)      ltq_i    = new SCG.List<IThreadUpdateable>();                
                if(ltq_jobs == null) ltq_jobs = new SCG.List<Action>();                
                //Init threads based on max allowed threads.
                int max_thread = Activity.maxThreadCount;
                if(lt==null) {
                    lt   = new List<IThreadUpdateable>[max_thread];
                    for(int i=0;i<lt.Length;i++) lt[i] = new List<IThreadUpdateable>(ActivityContext.Thread);
                }                
                if(thdl==null) thdl = new Thread[max_thread];
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
                thread_kill_flag = true;
                lock(this) { 
                    for(int i=0;i<lt.Length;i++) { lt[i].Clear(); }
                    if(ltq_a!=null) ltq_a.Clear();
                    if(ltq_i!=null) ltq_i.Clear();
                }                
            }

            #region Add/Remove

            /// <summary>
            /// Returns the desired activity list by context
            /// </summary>
            /// <param name="p_context"></param>
            /// <returns></returns>
            internal List GetActivityList(ActivityContext p_context) {
                switch(p_context) {
                    case ActivityContext.Job:
                    case ActivityContext.JobAsync:
                    case ActivityContext.Update:      return lu;
                    case ActivityContext.LateUpdate:  return llu;
                    case ActivityContext.FixedUpdate: return lfu;
                    case ActivityContext.Async:       return lau;                    
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
                if(a.state==ActivityState.Running) { /*Debug.LogWarning($"ActivityManager> Activity [{p_activity.id}] already running.");*/ return; }
                if(a.state==ActivityState.Queued)  { /*Debug.LogWarning($"ActivityManager> Activity [{p_activity.id}] already queued.");*/  return; }
                a.state = ActivityState.Queued;
                switch(a.context) {                    
                    case ActivityContext.Job:
                    case ActivityContext.JobAsync:
                    case ActivityContext.Update:      if(!lu.la.Contains(a))  lu.la.Add(a);  break;
                    case ActivityContext.LateUpdate:  if(!llu.la.Contains(a)) llu.la.Add(a); break;
                    case ActivityContext.FixedUpdate: if(!lfu.la.Contains(a)) lfu.la.Add(a); break;
                    case ActivityContext.Async:       if(!lau.la.Contains(a)) lau.la.Add(a); break;
                    #if !UNITY_WEBGL
                    //Threaded lists its better to enqueue in a secondary list and let the main execution add the list in a synced way
                    case ActivityContext.Thread:    {       
                        //Safely add to queue
                        SafeAdd(ltq_a,a);
                        //Assert for thread creation
                        AssertThread(); 
                        break; 
                    }
                    #else
                    case ActivityContext.Thread:     if(!lt[0].la.Contains(a))  lt[0].la.Add(a);  break;
                    #endif
                }
                //Call added handler.
                a.OnManagerAddInternal();
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
                //if(a.state==Activity.State.Complete) { Debug.LogWarning($"ActivityManager> Activity [{p_activity.id}] already completed."); return; }
                //if(a.state==Activity.State.Stopped)  { Debug.LogWarning($"ActivityManager> Activity [{p_activity.id}] already stopped.");   return; }                
                switch(a.context) {
                    case ActivityContext.Job:
                    case ActivityContext.JobAsync:
                    case ActivityContext.Update:      if(lu.la.Contains(a))  lu.la.Remove(a);  break;
                    case ActivityContext.LateUpdate:  if(llu.la.Contains(a)) llu.la.Remove(a); break;
                    case ActivityContext.FixedUpdate: if(lfu.la.Contains(a)) lfu.la.Remove(a); break;
                    case ActivityContext.Async:       if(lau.la.Contains(a)) lau.la.Remove(a); break;
                    //Threaded lists its better to null the element and allow removal during the synced execution of the thread
                    #if !UNITY_WEBGL
                    case ActivityContext.Thread: { 
                        //Search for the activity and null it for next pruning
                        for(int i=0;i<lt.Length;i++) if(lt[i].SafeInvalidate(a)) break;             
                        //Safely invalidate for removal
                        SafeInvalidate(ltq_a,a);                        
                    }
                    break;
                    #else
                    case ActivityContext.Thread:     if(lt[0].la.Contains(a))  lt[0].la.Remove(a);  break;
                    #endif
                }
                a.OnManagerRemoveInternal();
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
                #if !UNITY_WEBGL
                if(p_interface is IThreadUpdateable) {
                    IThreadUpdateable itf = (IThreadUpdateable)p_interface;                    
                    //Safely add the element
                    SafeAdd(ltq_i,itf);
                    //Assert for thread creation
                    AssertThread();
                }
                #else
                if(p_interface is IThreadUpdateable) { IThreadUpdateable itf = (IThreadUpdateable) p_interface; if(!lt[0].li.Contains(itf))  lt[0].li.Add(itf);  }
                #endif
            }

            /// <summary>
            /// Removes an executing node
            /// </summary>
            /// <param name="p_interface"></param>
            public void RemoveInterface(object p_interface) {
                if(p_interface==null) return;
                if(p_interface is IUpdateable)      { IUpdateable      itf = (IUpdateable)      p_interface; if(lu.li.Contains(itf))  lu.li.Remove(itf);  }
                if(p_interface is ILateUpdateable)  { ILateUpdateable  itf = (ILateUpdateable)  p_interface; if(llu.li.Contains(itf)) llu.li.Remove(itf); }
                if(p_interface is IFixedUpdateable) { IFixedUpdateable itf = (IFixedUpdateable) p_interface; if(lfu.li.Contains(itf)) lfu.li.Remove(itf); }
                if(p_interface is IAsyncUpdateable) { IAsyncUpdateable itf = (IAsyncUpdateable) p_interface; if(lau.li.Contains(itf)) lau.li.Remove(itf); }
                #if !UNITY_WEBGL
                if(p_interface is IThreadUpdateable) {
                    IThreadUpdateable itf = (IThreadUpdateable)p_interface;
                    //Search for the interface and null it for next pruning
                    for(int i=0;i<lt.Length;i++) {
                        List<IThreadUpdateable> it = lt[i];
                        if(it.Invalidate(itf)) break;
                    }                     
                    //Also clear in queue
                    SafeInvalidate(ltq_i,itf);                    
                }
                #else
                if(p_interface is IThreadUpdateable) { IThreadUpdateable itf = (IThreadUpdateable) p_interface; if(lt[0].li.Contains(itf))  lt[0].li.Remove(itf);  }
                #endif
            }

            #endregion

            #region Find

            /// <summary>
            /// Searches an activity by id.
            /// </summary>
            /// <param name="p_id"></param>
            /// <returns></returns>
            public T Find<T>(string p_id,ActivityContext p_context) where T : Activity {
                //Local ref
                T res = null;
                //Shortcut bool
                bool all_ctx = ((int)p_context)<0;
                //Search threads if threaded or all ctxs
                bool search_threads = p_context == ActivityContext.Thread ? true : all_ctx;
                //Search thread nodes
                if(p_context == ActivityContext.Thread) {                    
                    for(int i=0;i<lt.Length;i++) {
                        List it = lt[i];
                        //Search active node
                        res = it.Find<T>(p_id);
                        //If found break
                        if(res!=null) break;
                    }                    
                    //Search queued nodes if no result
                    if(res==null) { res = FindByQuery<T>(ltq_a,p_id); }                    
                    //If thread only return
                    if(p_context == ActivityContext.Thread) return res;
                    //If there is a result return
                    if(res!=null) return res;
                }
                //Try fetch the activity list
                List la = GetActivityList(p_context);
                //If single context find in the single list
                if(la!=null) return la.Find<T>(p_id);
                //If not search in all
                res = lu.Find<T>(p_id);  if(res!=null) return res;
                res = llu.Find<T>(p_id); if(res!=null) return res;
                res = lfu.Find<T>(p_id); if(res!=null) return res;
                res = lau.Find<T>(p_id); if(res!=null) return res;
                //Return whatever
                return res;
            }

            /// <summary>
            /// Finds an activity searching all contexts.
            /// </summary>
            /// <param name="p_id"></param>
            /// <returns></returns>
            public T Find<T>(string p_id) where T : Activity { return Find<T>(p_id,(ActivityContext)(-1)); }

            /// <summary>
            /// Finds all activities, matching the id.
            /// </summary>
            /// <param name="p_id"></param>
            /// <param name="p_context"></param>
            /// <returns></returns>
            public SCG.List<T> FindAll<T>(string p_id,ActivityContext p_context) where T : Activity {
                //Local ref
                SCG.List<T> res = new SCG.List<T>();
                //Shortcut bool
                bool all_ctx = ((int)p_context)<0;
                //Search threads if threaded or all ctxs
                bool search_threads = p_context == ActivityContext.Thread ? true : all_ctx;
                //Search thread nodes
                if(search_threads) {                    
                    //Search active nodes
                    for(int i=0;i<lt.Length;i++) {
                        List it = lt[i];
                        //Search active nodes
                        res.AddRange(it.FindAll<T>(p_id));                        
                    }
                //Search queued nodes
                    if(ltq_a!=null) { res.AddRange(FindAllByQuery<T>(ltq_a,p_id)); }                    
                    //If thread only return
                    if(p_context == ActivityContext.Thread) return res;
                }
                //Try fetch the activity list
                List la = GetActivityList(p_context);
                //If single context find in the single list
                if(la!=null) { 
                    SCG.List<T> l = la.FindAll<T>(p_id);
                    res.AddRange(l);
                    return res; 
                }
                //If not search in all
                res.AddRange(lu.FindAll<T>(p_id));  
                res.AddRange(llu.FindAll<T>(p_id)); 
                res.AddRange(lfu.FindAll<T>(p_id)); 
                res.AddRange(lau.FindAll<T>(p_id)); 
                //Return results
                return res;
            }

            /// <summary>
            /// Searches all activities in all contexts.
            /// </summary>
            /// <param name="p_id"></param>
            /// <returns></returns>
            public SCG.List<T> FindAll<T>(string p_id) where T : Activity { return FindAll<T>(p_id,ActivityContext.All); }

            #region Find Helpers
            internal bool QueryMatch<T>(Activity it,string p_id) where T : Activity {
                //Skip invalid
                if(it==null) return false;
                //If there is an id query and it doesnt match skip
                if(p_id!="") if(it.id!=p_id) return false;
                //Check the type
                if(!(it is T)) return false;
                //All matches
                return true;
            }            
            internal T FindByQuery<T>(SCG.List<Activity> l,string p_id) where T : Activity { for(int i=0;i<l.Count;i++) if(QueryMatch<T>(l[i],p_id)) return (T)l[i]; return null; }
            internal SCG.List<T> FindAllByQuery<T>(SCG.List<Activity> l,string p_id) where T : Activity { SCG.List<T> res = new SCG.List<T>(); for(int i=0;i<l.Count;i++) if(QueryMatch<T>(l[i],p_id)) { res.Add((T)l[i]); } return res; }
            #endregion

            #endregion

            #region Loops

            /// <summary>
            /// Assert all threads.
            /// </summary>
            protected void AssertThread() {           
                #if UNITY_WEBGL
                //Don't use threads in WebGL
                return;
                #else
                //Assert threads a bit per frame
                AssertThread(thread_assert_target);
                thread_assert_target = (thread_assert_target+1)%lt.Length;
                #endif
            }

            /// <summary>
            /// Asserts the lifetime of the thread running the thread loop nodes.
            /// </summary>
            protected void AssertThread(int p_index) {
                //If kill switch skip
                if(thread_kill_flag) return;
                //Thread index
                int idx = p_index;                
                //If no nodes executing and no nodes queued skip
                if(AssertThreadEmpty(idx)) return;
                //Fetch thread and its state
                Thread  thd   = thdl[idx];                
                int     thd_s = thd==null ? -1 : (int)thd.ThreadState;
                //If thread active/sleep skips
                if(thd!=null) {
                    if(thd_s == 0)  return; //Running 
                    if(thd_s == 32) return; //WaitSleepJoin
                }
                //Create and start the thread
                thd = new Thread(ThreadLoop);
                thd.Name = "activity-thread-"+idx;
                thdl[idx] = thd;
                thd.Start(p_index);
            }

            /// <summary>
            /// Thread main method.
            /// </summary>
            /// <param name="p_index">Index of the thread</param>
            internal void ThreadLoop(object p_index) {
                int idx = (int)p_index;
                List<IThreadUpdateable> l = lt[idx];
                while(true) {
                    //If kill thread clear all and break out, reset the flag
                    if(thread_kill_flag) break;                        
                    //Execute all nodes
                    ThreadUpdate(l,idx);
                    //Sleep 0 to yield CPU if possible
                    Thread.Sleep(0);
                    //Stop the thread if no nodes                        
                    if(AssertThreadEmpty(idx)) break;                        
                }
                thdl[idx]=null;
            }

            /// <summary>
            /// Assert all thread lists and queues for being empty.
            /// </summary>
            /// <param name="p_index"></param>
            /// <returns></returns>
            protected bool AssertThreadEmpty(int p_index) {
                List<IThreadUpdateable> l = lt[p_index];
                bool is_empty = false;                
                int ltq_ac = ltq_a==null    ? 0 : ltq_a.Count;
                int ltq_ic = ltq_i==null    ? 0 : ltq_i.Count;
                int ltq_jc = ltq_jobs==null ? 0 : ltq_jobs.Count;
                if(l.IsEmpty()) if(ltq_jc<=0) if(ltq_ac<=0) if(ltq_ic<=0) is_empty=true;                
                return is_empty;
            }

            /// <summary>
            /// Update Loop
            /// </summary>
            public void Update() {
                #if UNITY_EDITOR
                if(lu  == null) AssertLists();                
                #endif
                lu.Execute();
                lau.Execute();

                #if UNITY_WEBGL
                //Thread execution is skipped and fallback into 'Update'
                lt[0].Execute();
                #endif

                #if !UNITY_WEBGL
                //Check health state of threading each 0.1s
                if(thread_keep_alive_tick<=0) { AssertThread(); thread_keep_alive_tick=0.1f; }
                thread_keep_alive_tick-=Time.unscaledDeltaTime;
                #endif                
            }

            /// <summary>
            /// FixedUpdate Loop
            /// </summary>
            public void FixedUpdate() { 
                #if UNITY_EDITOR    
                if(lfu==null) AssertLists();
                #endif
                lfu.Execute();  
            }

            /// <summary>
            /// LateUpdate Loop
            /// </summary>
            public void LateUpdate()  { 
                #if UNITY_EDITOR    
                if(llu==null) AssertLists();
                #endif
                llu.Execute();  
            }

            /// <summary>
            /// ThreadUpdate Loop
            /// </summary>
            internal void ThreadUpdate(List<IThreadUpdateable> p_list,int p_index) {

                List<IThreadUpdateable> l = null;
                
                //All 'lock' operands will help prevent race conditions during list manipulation

                //Execute jobs for queueing and invalidating in general
                lock(this) {
                    for(int i = 0; i<ltq_jobs.Count; i++) if(ltq_jobs[i]!=null)ltq_jobs[i]();
                    ltq_jobs.Clear();
                }
    
                lock(this) { 
                    //Insert activities from queue
                    while(ltq_a.Count>0) {            
                        //Fetch the target list for queueing
                        l = lt[thread_queue_target_a];
                        //Dequeue elements until insertion
                        Activity a = SafeDequeue(ltq_a);                        
                        //Safely add the activity
                        l.SafeAdd(a);
                        //Iterate next element
                        thread_queue_target_a = (thread_queue_target_a+1)%lt.Length;                            
                    }
                }
                    
                //Move new queued interfaces into the main list                    
                lock(this) { 
                    //Insert interfaces from queue
                    while(ltq_i.Count>0) {                  
                        //Fetch the target list for queueing
                        l = lt[thread_queue_target_i];
                        //Dequeue elements until insertion
                        IThreadUpdateable itf = SafeDequeue(ltq_i);
                        //Safely add the new element
                        l.SafeAdd(itf);
                        //Iterate next element
                        thread_queue_target_i = (thread_queue_target_i+1)%lt.Length;
                        break;
                    }
                }
                    
                                             
                //Executes threaded nodes.
                p_list.Execute();
            }

            /// <summary>
            /// Safely dequeue the first element.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="p_list"></param>
            /// <returns></returns>
            internal T SafeDequeue<T>(SCG.List<T> p_list) {
                T a = default(T);
                a = p_list.Count<=0 ? default(T) : p_list[0];
                if(p_list.Count>0) p_list.RemoveAt(0);                    
                return a;
            }

            /// <summary>
            /// Safely invalidate an item to null
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="p_list"></param>
            /// <param name="p_item"></param>
            /// <returns></returns>
            internal bool SafeInvalidate<T>(SCG.List<T> p_list,T p_item) {
                int idx = p_list.IndexOf(p_item);
                ltq_jobs.Add(delegate() {                     
                    //Search by index
                    idx = p_list.IndexOf(p_item); 
                    //Invalidate at index
                    if(idx>=0) p_list[idx] = default(T);                                    
                });
                return idx>=0;
            }

            /// <summary>
            /// Safely adds an element.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="p_list"></param>
            /// <param name="p_item"></param>
            /// <returns></returns>
            internal T SafeAdd<T>(SCG.List<T> p_list,T p_item) {
                ltq_jobs.Add(delegate() { 
                    if(!p_list.Contains(p_item)) p_list.Add(p_item);
                });                
                return p_item;
            }

            #endregion

        }

}
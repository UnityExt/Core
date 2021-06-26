using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityExt.Core {

    #region enum TimerType

    /// <summary>
    /// Timer running mode.
    /// </summary>
    public enum TimerType {
        /// <summary>
        /// Runs using unity's time tracking.
        /// </summary>
        Unity,
        /// <summary>
        /// Runs using a stopwatch class and track time out if the main thread.
        /// </summary>
        System,
        #if UNITY_EDITOR
        /// <summary>
        /// Flag that tells the timer uses Editor only time tracking.
        /// </summary>
        Editor=255
        #endif
    }

    #endregion

    /// <summary>
    /// Class that extends an Activity to give support of time tracking, laps and delayed execution start.
    /// </summary>
    public class Timer : Activity, IProgressProvider {

        #region static

        /// <summary>
        /// Static CTOR
        /// </summary>
        static Timer() {
            //Start the thread based timer upon first usage.
            if(m_clock_sys == null) { m_clock_sys = new System.Diagnostics.Stopwatch(); m_clock_sys.Start(); }
        }  
        /// <summary>
        /// Single system time instance
        /// </summary>
        static System.Diagnostics.Stopwatch m_clock_sys;

        #region Atomic Clock

        /// <summary>
        /// Path to be used on the creation of an atomic timer.
        /// </summary>
        static public string AtomicClockRoot = $"{Application.persistentDataPath}/unityex/{Application.platform.ToString().ToLower()}/timer/";

        /// <summary>
        /// Default atomic clock file name
        /// </summary>
        static public string AtomicClockfile = "ue-atomic-clock";

        /// <summary>
        /// Returns the folder where the atomic clock references are stored.
        /// </summary>
        static public string AtomicClockPath {
            get {
                string path = AtomicClockRoot.Replace('\\','/').Replace("//","/");                                
                if(!m_path_checked) if(!Directory.Exists(path)) Directory.CreateDirectory(path);
                m_path_checked = true;
                return path;                
            }
        }
        static private bool m_path_checked = false;

        /// <summary>
        /// Creates a temporary file and sample its current timestamp.
        /// </summary>
        /// <param name="p_id">Atomic Clock Id</param>
        /// <returns>DateTime stamp of the atomic clock</returns>
        static public DateTime GetAtomicTimestamp(string p_id="") {            
            bool is_default = string.IsNullOrEmpty(p_id);
            //File Name
            string     fn = p_id;            
            //Default file name
            if(is_default) { fn = AtomicClockfile; }            
            //File Path
            string fp = AtomicClockPath+fn;
            //Fetch file info
            FileInfo fi = new FileInfo(fp);            
            if(!fi.Exists) {
                FileStream fs = File.Open(fp, FileMode.Create, FileAccess.ReadWrite); 
                //Write a byte and close
                fs.WriteByte(1);                
                fs.FlushAsync();
                fs.Close();
                fs.Dispose();
            }            
            fi.Refresh();
            //Return last access
            return fi.CreationTime;
        }        
        
        /// <summary>
        /// Clears the atomic clock of a given id.
        /// </summary>
        /// <param name="p_id">Atomic Clock Id</param>
        static public void ClearAtomicTimestamp(string p_id="") {
            bool is_default = string.IsNullOrEmpty(p_id);
            //Default file name
            string fn = p_id;
            if(is_default) { fn = AtomicClockfile; }
            //File Path
            string fp = AtomicClockPath+fn;
            FileInfo fi = new FileInfo(fp);
            if(!fi.Exists) return;            
            File.SetCreationTimeUtc(fp,DateTime.UtcNow);
            fi.Delete();               
            fi.Refresh();
        }

        /// <summary>
        /// Returns the elapsed timespan of an atomic clock, returns Timespan(0) if not created yet.
        /// </summary>
        /// <param name="p_id">Atomic Clock Id</param>
        /// <returns>Elapsed timespan of a given atomic block</returns>
        static public TimeSpan GetAtomicClockElapsed(string p_id="") {
            //Fetch the tmp file information
            string tmp_fp = AtomicClockPath+tmp_clock_fn;
            if(tmp_fi == null) { tmp_fi = new FileInfo(tmp_fp); tmp_fs = File.Open(tmp_fp,FileMode.OpenOrCreate); }
            tmp_fs.WriteByte(1);
            tmp_fs.Flush();
            tmp_fi.Refresh();
            //File Path            
            DateTime t1 = tmp_fi.LastAccessTime;
            DateTime t0 = GetAtomicTimestamp(p_id);            
            return t1-t0;
        }
        static private string tmp_clock_fn = "$sys-clock";
        static private FileInfo   tmp_fi  = null;
        static private FileStream tmp_fs  = null;
        
        #endregion
        
        #region Run/Loop

        /// <summary>
        /// Helper
        /// </summary>        
        static internal Timer Create(string p_id,float p_duration,int p_count,TimerType p_type,System.Predicate<Timer> p_on_execute,System.Action<Timer> p_on_complete,System.Predicate<Timer> p_on_step) {
            Timer n = new Timer(p_id,p_type);
            n.duration = p_duration;
            n.count    = p_count;
            n.OnCompleteEvent = p_on_complete;
            n.OnExecuteEvent  = p_on_execute;
            n.OnStepEvent     = p_on_step;            
            return n;
        }

        #region Reflection

        /// <summary>
        /// Reflection Fields Flags
        /// </summary>
        static System.Reflection.BindingFlags m_rfl_flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.InvokeMethod;

        /// <summary>
        /// Creates a Timer and Invokes by name the method of the given target using Reflection.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_delay">Delay before calling the method</param>
        /// <param name="p_target">Object containing the method</param>
        /// <param name="p_method">Method name</param>
        /// <param name="p_args">Method args</param>
        /// <returns>Timer running the delay</returns>
        static public Timer Invoke(string p_id,float p_delay,object p_target,string p_method,params object[] p_args) {
            if(p_target == null)               { Debug.LogWarning("Timer> Invoke Failed / Null Target");    return Delay(p_delay); } 
            if(string.IsNullOrEmpty(p_method)) { Debug.LogWarning("Timer> Invoke Failed / Invalid Method"); return Delay(p_delay); }
            Type type = p_target is Type ? (Type)p_target : p_target.GetType();
            System.Reflection.MethodInfo m = type.GetMethod(p_method,m_rfl_flags);
            if(m==null) { Debug.LogWarning($"Timer> Invoke Failed / Method [{p_method}] not found!"); return Delay(p_delay); }
            object target = p_target is Type ? null : p_target;
            return Create(p_id,p_delay,1, TimerType.Unity,null,delegate(Timer t) { m.Invoke(t,p_args); },null);
        }

        /// <summary>
        /// Creates a Timer and Invokes by name the method of the given target using Reflection.
        /// </summary>        
        /// <param name="p_delay">Delay before calling the method</param>
        /// <param name="p_target">Object containing the method</param>
        /// <param name="p_method">Method name</param>
        /// <param name="p_args">Method args</param>
        /// <returns>Timer running the delay</returns>
        static public Timer Invoke(float p_delay,object p_target,string p_method,params object[] p_args) { return Invoke("timer-invoke",p_delay,p_target,p_method,p_args); }

        /// <summary>
        /// Creates a Timer and Invokes by name the static method of the given type.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_delay">Delay before calling the method</param>        
        /// <param name="p_method">Method name</param>
        /// <param name="p_args">Method args</param>
        /// <returns>Timer running the delay</returns>
        static public Timer Invoke<T>(string p_id,float p_delay,string p_method,params object[] p_args) { return Invoke(p_id,p_delay,typeof(T),p_method,p_args); }

        /// <summary>
        /// Creates a Timer and Invokes by name the static method of the given type.
        /// </summary>        
        /// <param name="p_delay">Delay before calling the method</param>        
        /// <param name="p_method">Method name</param>
        /// <param name="p_args">Method args</param>
        /// <returns>Timer running the delay</returns>
        static public Timer Invoke<T>(float p_delay,string p_method,params object[] p_args) { return Invoke<T>("timer-invoke-static",p_delay,p_method,p_args); }

        /// <summary>
        /// Creates a Timer and Invokes the passed delegate
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_delay">Delay before calling the method</param>        
        /// <param name="p_delegate">Delegate Function to be called</param>
        /// <param name="p_args">Delegate arguments</param>
        /// <returns>Timer running the delay</returns>
        static public Timer Invoke(string p_id,float p_delay,Delegate p_delegate,params object[] p_args) {
            if(p_delegate == null) { Debug.LogWarning("Timer> Invoke Failed / Null Delegate"); return Delay(p_delay); }                        
            return Create(p_id,p_delay,1, TimerType.Unity,null,delegate(Timer t) { p_delegate.DynamicInvoke(p_args); },null);
        }

        /// <summary>
        /// Creates a Timer and Invokes the passed delegate
        /// </summary>        
        /// <param name="p_delay">Delay before calling the method</param>        
        /// <param name="p_delegate">Delegate Function to be called</param>
        /// <param name="p_args">Delegate arguments</param>
        /// <returns>Timer running the delay</returns>
        static public Timer Invoke(float p_delay,Delegate p_delegate,params object[] p_args) {
            if(p_delegate == null) { Debug.LogWarning("Timer> Invoke Failed / Null Delegate"); return Delay(p_delay); }                        
            return Create($"timer-invoke-delegate",p_delay,1, TimerType.Unity,null,delegate(Timer t) { p_delegate.DynamicInvoke(p_args); },null);
        }

        /// <summary>
        /// Creates a timer that set a given property of an object after the delay passed.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_delay">Delay before changing the property</param>
        /// <param name="p_target">Target object</param>
        /// <param name="p_property">Property name</param>
        /// <param name="p_value">Property value</param>
        /// <returns>Running timer</returns>
        static public Timer Set(string p_id,float p_delay,object p_target,string p_property,object p_value) {
            if(p_target == null)                 { Debug.LogWarning("Timer> Set Failed / Null Target");      return Delay(p_delay); } 
            if(string.IsNullOrEmpty(p_property)) { Debug.LogWarning("Timer> Set Failed / Invalid Property"); return Delay(p_delay); }
            Type type = p_target is Type ? (Type)p_target : p_target.GetType();
            System.Reflection.MemberInfo[] ml = type.GetMember(p_property,m_rfl_flags);
            if(ml.Length<=0) { Debug.LogWarning($"Timer> Set Failed / Property [{p_property}] not found!"); return Delay(p_delay); }
            System.Reflection.MemberInfo m = ml[0];
            object target = p_target is Type ? null : p_target;
            System.Reflection.FieldInfo    fi = m as System.Reflection.FieldInfo;
            System.Reflection.PropertyInfo pi = m as System.Reflection.PropertyInfo;            
            return 
            Create(p_id,p_delay,1, TimerType.Unity,null,
            delegate(Timer t) { 
                if(fi!=null){ fi.SetValue(t,p_value); return; }
                if(pi!=null){ pi.SetValue(t,p_value); return; }
            },null);
        }

        /// <summary>
        /// Creates a timer that set a given property of an object after the delay passed.
        /// </summary>        
        /// <param name="p_delay">Delay before changing the property</param>
        /// <param name="p_target">Target object</param>
        /// <param name="p_property">Property name</param>
        /// <param name="p_value">Property value</param>
        /// <returns>Running timer</returns>
        static public Timer Set(float p_delay,object p_target,string p_property,object p_value) { return Set("timer-set",p_delay,p_target,p_property,p_value); }        

        /// <summary>
        /// Creates a timer that set a given static property of a Type.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_delay">Delay before changing the property</param>        
        /// <param name="p_property">Property name</param>
        /// <param name="p_value">Property value</param>
        /// <returns>Running timer</returns>
        static public Timer Set<T>(string p_id,float p_delay,string p_property,object p_value) { return Set(p_id,p_delay,typeof(T),p_property,p_value); }

        /// <summary>
        /// Creates a timer that set a given static property of a Type.
        /// </summary>        
        /// <param name="p_delay">Delay before changing the property</param>        
        /// <param name="p_property">Property name</param>
        /// <param name="p_value">Property value</param>
        /// <returns>Running timer</returns>
        static public Timer Set<T>(float p_delay,string p_property,object p_value) { return Set("timer-set-static",p_delay,typeof(T),p_property,p_value); }

        #endregion

        #region Delay

        /// <summary>
        /// Creates an executes a timer for 'await' scenarios.
        /// </summary>
        /// <param name="p_id">Id of the timer</param>
        /// <param name="p_duration">How much time to wait</param>
        /// <returns>Executing Timer</returns>
        static public Timer Delay(string p_id,float p_duration) { Timer n = Create(p_id,p_duration,1, TimerType.Unity,null,null,null); n.Start(0f); return n; }

        /// <summary>
        /// Creates an executes a timer for 'await' scenarios.
        /// </summary>        
        /// <param name="p_duration">How much time to wait</param>
        /// <returns>Executing Timer</returns>
        static public Timer Delay(float p_duration) { Timer n = Create("timer-delay",p_duration,1, TimerType.Unity,null,null,null); n.Start(0f); return n; }

        #endregion

        #region Run

        /// <summary>
        /// Creates and executes a Timer with a per-execution callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_delay">Delay before starting in seconds.</param>
        /// <param name="p_duration">Duration of the Timer, if 0.0 runs forever.</param>
        /// <param name="p_count">Number of steps, if 0 repeats 'duration' forever.</param>
        /// <param name="p_callback">Handler for each execution.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(string p_id,float p_delay,float p_duration,int p_count,System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(p_id,p_duration,p_count,p_type,p_callback,null,null); n.Start(p_delay); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-execution callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_delay">Delay before starting in seconds.</param>
        /// <param name="p_duration">Duration of the Timer, if 0.0 runs forever.</param>        
        /// <param name="p_callback">Handler for each execution.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(string p_id,float p_delay,float p_duration,            System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(p_id,p_duration,1      ,p_type,p_callback,null,null); n.Start(p_delay); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-execution callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_delay">Delay before starting in seconds.</param>        
        /// <param name="p_callback">Handler for each execution.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(string p_id,float p_delay,                             System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(p_id,0f        ,1      ,p_type,p_callback,null,null); n.Start(p_delay); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-execution callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id</param>        
        /// <param name="p_duration">Duration of the Timer, if 0.0 runs forever.</param>
        /// <param name="p_count">Number of steps, if 0 repeats 'duration' forever.</param>
        /// <param name="p_callback">Handler for each execution.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(string p_id,              float p_duration,int p_count,System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(p_id,p_duration,p_count,p_type,p_callback,null,null); n.Start();        return n; }        
        /// <summary>
        /// Creates and executes a Timer with a per-execution callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_callback">Handler for each execution.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(string p_id,                                           System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(p_id,0f        ,1      ,p_type,p_callback,null,null); n.Start();        return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-execution callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>        
        /// <param name="p_delay">Delay before starting in seconds.</param>
        /// <param name="p_duration">Duration of the Timer, if 0.0 runs forever.</param>
        /// <param name="p_count">Number of steps, if 0 repeats 'duration' forever.</param>
        /// <param name="p_callback">Handler for each execution.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(            float p_delay,float p_duration,int p_count,System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(""  ,p_duration,p_count,p_type,p_callback,null,null); n.Start(p_delay); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-execution callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>        
        /// <param name="p_delay">Delay before starting in seconds.</param>
        /// <param name="p_duration">Duration of the Timer, if 0.0 runs forever.</param>        
        /// <param name="p_callback">Handler for each execution.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(            float p_delay,float p_duration,            System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(""  ,p_duration,1      ,p_type,p_callback,null,null); n.Start(p_delay); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-execution callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>        
        /// <param name="p_delay">Delay before starting in seconds.</param>        
        /// <param name="p_callback">Handler for each execution.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(            float p_delay,                             System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(""  ,0f        ,1      ,p_type,p_callback,null,null); n.Start(p_delay); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-execution callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>        
        /// <param name="p_duration">Duration of the Timer, if 0.0 runs forever.</param>
        /// <param name="p_count">Number of steps, if 0 repeats 'duration' forever.</param>
        /// <param name="p_callback">Handler for each execution.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(                          float p_duration,int p_count,System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(""  ,p_duration,p_count,p_type,p_callback,null,null); n.Start();        return n; }        
        /// <summary>
        /// Creates and executes a Timer with a per-execution callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_callback">Handler for each execution.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(                                                       System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(""  ,0f        ,1      ,p_type,p_callback,null,null); n.Start();        return n; }
        
        #endregion

        #region RunOnce
        
        /// <summary>
        /// Creates and executes a Timer with a single execution callback upon completion. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id.</param>
        /// <param name="p_duration">Duration before completion.</param>
        /// <param name="p_callback">Callback called after completion.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance already running.</returns>
        static public Timer Run(string p_id,              float p_duration,System.Action<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(p_id,p_duration,1,p_type,null,p_callback,null); n.Start();        return n; }                
        /// <summary>
        /// Creates and executes a Timer with a single execution callback upon completion. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>        
        /// <param name="p_duration">Duration before completion.</param>
        /// <param name="p_callback">Callback called after completion.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance already running.</returns>
        static public Timer Run(                          float p_duration,System.Action<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(""  ,p_duration,1,p_type,null,p_callback,null); n.Start();        return n; }                
        
        #endregion

        #region Step

        /// <summary>
        /// Creates and executes a Timer with a per-loop-step callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_delay">Delay before running.</param>
        /// <param name="p_duration">Step duration.</param>
        /// <param name="p_count">Number of steps</param>
        /// <param name="p_callback">Callback called per step.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance already running.</returns>
        static public Timer Step(string p_id,float p_delay,float p_duration,int p_count,System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(p_id,p_duration,p_count,p_type,null,null,p_callback); n.Start(p_delay); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-loop-step callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_delay">Delay before running.</param>
        /// <param name="p_duration">Step duration.</param>        
        /// <param name="p_callback">Callback called per step.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance already running.</returns>
        static public Timer Step(string p_id,float p_delay,float p_duration,            System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(p_id,p_duration,1      ,p_type,null,null,p_callback); n.Start(p_delay); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-loop-step callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_delay">Delay before running.</param>        
        /// <param name="p_callback">Callback called per step.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance already running.</returns>
        static public Timer Step(string p_id,float p_delay,                             System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(p_id,0f        ,1      ,p_type,null,null,p_callback); n.Start(p_delay); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-loop-step callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id</param>        
        /// <param name="p_duration">Step duration.</param>
        /// <param name="p_count">Number of steps</param>
        /// <param name="p_callback">Callback called per step.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance already running.</returns>
        static public Timer Step(string p_id,              float p_duration,int p_count,System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(p_id,p_duration,p_count,p_type,null,null,p_callback); n.Start();        return n; }        
        /// <summary>
        /// Creates and executes a Timer with a per-loop-step callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_callback">Callback called per step.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance already running.</returns>
        static public Timer Step(string p_id,                                           System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(p_id,0f        ,1      ,p_type,null,null,p_callback); n.Start();        return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-loop-step callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>        
        /// <param name="p_delay">Delay before running.</param>
        /// <param name="p_duration">Step duration.</param>
        /// <param name="p_count">Number of steps</param>
        /// <param name="p_callback">Callback called per step.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance already running.</returns>
        static public Timer Step(            float p_delay,float p_duration,int p_count,System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(""  ,p_duration,p_count,p_type,null,null,p_callback); n.Start(p_delay); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-loop-step callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>        
        /// <param name="p_delay">Delay before running.</param>
        /// <param name="p_duration">Step duration.</param>        
        /// <param name="p_callback">Callback called per step.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance already running.</returns>
        static public Timer Step(            float p_delay,float p_duration,            System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(""  ,p_duration,1      ,p_type,null,null,p_callback); n.Start(p_delay); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-loop-step callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>        
        /// <param name="p_delay">Delay before running.</param>
        /// <param name="p_callback">Callback called per step.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance already running.</returns>
        static public Timer Step(            float p_delay,                             System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(""  ,0f        ,1      ,p_type,null,null,p_callback); n.Start(p_delay); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-loop-step callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_duration">Step duration.</param>
        /// <param name="p_count">Number of steps</param>
        /// <param name="p_callback">Callback called per step.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance already running.</returns>
        static public Timer Step(                          float p_duration,int p_count,System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(""  ,p_duration,p_count,p_type,null,null,p_callback); n.Start();        return n; }        
        /// <summary>
        /// Creates and executes a Timer with a per-loop-step callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_callback">Callback called per step.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance already running.</returns>
        static public Timer Step(                                                       System.Predicate<Timer> p_callback=null,TimerType p_type = TimerType.Unity) { Timer n = Create(""  ,0f        ,1      ,p_type,null,null,p_callback); n.Start();        return n; }

        #endregion
        
        #endregion
        
        #endregion

        /// <summary>
        /// Timer clock type.
        /// </summary>
        public TimerType type { get; set; }

        /// <summary>
        /// Number of loop steps to perform, if '0' runs undefinetely.
        /// </summary>
        public int count { get; set; }

        /// <summary>
        /// Current step.
        /// </summary>
        public int step { get; set; }

        /// <summary>
        /// Current elapsed time.
        /// </summary>
        public float elapsed { 
            get {
                switch(state) {
                    case ActivityState.Idle:   return 0f;
                    case ActivityState.Queued: return 0f;
                }
                return m_elapsed;
            }
            set { 
                m_elapsed = value; 
            }
        }
        private float m_elapsed;

        /// <summary>
        /// Duration per execution loop. If '0.0' the timer will not perform any execution step.
        /// </summary>
        public float duration { get; set; }

        /// <summary>
        /// Delay before starting execution the loop.
        /// </summary>
        public float delay { get; set; }

        /// <summary>
        /// Flag that tells if the timer will suffer time scale or not. Defaults to true.
        /// </summary>
        public bool unscaled { get; set; }

        /// <summary>
        /// Flag that tells if the timer will keep updating or not.
        /// </summary>
        public bool paused { get; set; }

        /// <summary>
        /// Returns the execution progress of the timer in the [0,1] range.
        /// </summary>
        public float progress { get { return duration<=0f ? 0f : Mathf.Clamp01(elapsed/duration); } }

        /// <summary>
        /// Speed adjustment for other features.
        /// </summary>
        internal float m_speed_internal = 1f;

        #region Events

        /// <summary>
        /// Event called on each step complete.
        /// </summary>
        public Predicate<Timer> OnStepEvent {
            get { return (Predicate<Timer>)m_on_step_event; }
            set { m_on_step_event = value;                  }                
        }
        protected Delegate m_on_step_event;

        /// <summary>
        /// Event called upon timer completion
        /// </summary>
        new public Action<Timer> OnCompleteEvent {
            get { return (Action<Timer>)m_on_complete_event; }
            set { m_on_complete_event = value;               }
        }

        /// <summary>
        /// Event called upon timer execution step.
        /// </summary>
        new public Predicate<Timer> OnExecuteEvent {
            get { return (Predicate<Timer>)m_on_execute_event; }
            set { m_on_execute_event = value;               }
        }

        /// <summary>
        /// Auxiliary class to method invoke.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="p_event"></param>
        /// <param name="p_arg"></param>
        /// <returns></returns>
        override internal bool InvokeEvent(Delegate p_event,Activity p_arg,bool p_default) {
            bool res = p_default;
            if(p_event==null) return res;
            if(p_event is Action<Timer>)    { Action<Timer>    cb = (Action<Timer>)   p_event;       cb(this); } else
            if(p_event is Predicate<Timer>) { Predicate<Timer> cb = (Predicate<Timer>)p_event; res = cb(this); }
            return res;
        }

        #endregion
        
        #region CTOR

        /// <summary>
        /// Creates a new timer instance.
        /// </summary>
        /// <param name="p_id">Id of the Timer.</param>
        /// <param name="p_delay">Delay in seconds before execution start.</param>
        /// <param name="p_duration">Duration in seconds per execution step.</param>
        /// <param name="p_count">Max number of steps before completion.</param>
        /// <param name="p_type">Clock time tracking mode.</param>
        /// <param name="p_context">Activity execution context.</param>
        public Timer(string p_id,float p_duration,int p_count=1,TimerType p_type = TimerType.Unity,ActivityContext p_context = ActivityContext.Update) : base(p_id,p_context) { CreateTimer(p_duration,p_count,p_type); }
        /// <summary>
        /// Creates a new Timer instance.
        /// </summary>        
        /// <param name="p_delay">Delay in seconds before execution start.</param>
        /// <param name="p_duration">Duration in seconds per execution step.</param>
        /// <param name="p_count">Max number of steps before completion.</param>
        /// <param name="p_type">Clock time tracking mode.</param>
        /// <param name="p_context">Activity execution context.</param>
        public Timer(float p_duration=0f,int p_count=1,TimerType p_type = TimerType.Unity,ActivityContext p_context = ActivityContext.Update) : base("",p_context) { CreateTimer(p_duration,p_count,p_type); }
        /// <summary>
        /// Creates a new Timer instance.
        /// </summary>
        /// <param name="p_id">Id of the Timer.</param>
        /// <param name="p_type">Clock time tracking mode.</param>
        public Timer(string p_id,TimerType p_type = TimerType.System) : base(p_id, p_type == TimerType.System ? ActivityContext.Thread : ActivityContext.Update) { CreateTimer(0f,0,p_type); }
        /// <summary>
        /// Creates a new Timer instance.
        /// </summary>        
        /// <param name="p_type">Clock time tracking mode.</param>
        public Timer(TimerType p_type = TimerType.System) : base("",p_type == TimerType.System ? ActivityContext.Thread : ActivityContext.Update) { CreateTimer(0f,0,p_type); }                

        #endregion

        #region CRUD

        /// <summary>
        /// Helper to create the time.
        /// </summary>
        /// <param name="p_delay"></param>
        /// <param name="p_duration"></param>
        /// <param name="p_count"></param>
        /// <param name="p_mode"></param>
        internal void CreateTimer(float p_duration,int p_count,TimerType p_mode) {
            delay    = 0f;
            duration = p_duration;
            count    = p_count;
            type     = p_mode;            
            unscaled = true;
            paused   = false; 
            m_clock_time = 0f;
            m_elapsed    = 0f;
            switch(type) {
                #if UNITY_EDITOR
                case TimerType.Editor:
                #endif
                case TimerType.Unity: {
                    if(context == ActivityContext.Thread) {
                        Debug.LogWarning("Timer> Using Unity based Time in Thread context will throw an error. Fallback to 'System'");
                        type = TimerType.System;
                    }                    
                }
                break;
            }
        }

        #endregion

        #region Operation

        /// <summary>
        /// Starts the timer immediately.
        /// </summary>
        new public void Start() { Start(0f); }

        /// <summary>
        /// Starts the timer after a delay in seconds
        /// </summary>
        /// <param name="p_delay">Delay before start in seconds.</param>
        public void Start(float p_delay) {
            delay = p_delay;
            base.Start();
        }

        /// <summary>
        /// Resets the Timer entirely, and keep it  running.
        /// </summary>
        public void Restart() {            
            paused = false;
            switch(state) {
                case ActivityState.Queued:
                case ActivityState.Running: {
                    m_elapsed = 0f;
                    step      = 0;                    
                }
                break;

                default: {
                    Start();
                }
                break;
            }
        }

        /// <summary>
        /// Restarts only the current step, and keep it running.
        /// </summary>
        public void RestartStep() {
            paused = false;
            switch(state) {
                case ActivityState.Queued:
                case ActivityState.Running: {
                    m_elapsed = 0f;                                        
                }
                break;
                default: {
                    Start();
                }
                break;
            }
        }

        /// <summary>
        /// Get current clock based on the time tracking methods.
        /// Unity  = Time.time / Time.timeScale
        /// System = clock.EllapsedMilliseconds
        /// </summary>
        /// <returns>Elapsed time in seconds since the last timestamp.</returns>
        public float GetClock() {
            float t = 0f;              
            switch(type) {                
                case TimerType.Unity:  { 
                    //Time.unscaledTime isn't affected by editor's pause/step
                    float ivts = Time.timeScale<=0f ? 1f : (1f/Time.timeScale);
                    t = Time.time * (unscaled ? ivts : 1f);
                }
                break;
                #if UNITY_EDITOR
                case TimerType.Editor: {
                    //If creating the timer for inspectors, window, menus,...
                    t = (float)UnityEditor.EditorApplication.timeSinceStartup;
                }
                break;
                #endif
                case TimerType.System: { double ms = (double)m_clock_sys.ElapsedMilliseconds; t = (float)(ms*0.001); } break;
            }            
            return t - m_clock_stamp;
        }
        private float    m_clock_time;
        private float    m_clock_stamp;
        private float    m_clock_delta_time;

        #endregion

        #region Internal Operation

        /// <summary>
        /// Handler for enabling
        /// </summary>
        protected override void OnEnable() {
            //Trigger clock stamp reset;
            m_clock_stamp=0f;
        }

        /// <summary>
        /// Resets the time stamps.
        /// </summary>
        internal void ResetClock() {
            m_clock_stamp      = 0f;
            m_clock_stamp      = GetClock();
            m_clock_time       = 0f;
        }

        /// <summary>
        /// Timer was just added for queueing and execution.
        /// </summary>
        internal override void OnManagerAddInternal() {            
            m_elapsed          = 0f;                        
            m_clock_delta_time = 0f;            
            m_clock_stamp      = 0f;
            m_clock_time       = 0f;
        }

        /// <summary>
        /// Called when the time has stopped.
        /// </summary>
        internal override void OnManagerRemoveInternal() {            
            step               = 0;
            m_elapsed          = 0f;                        
            m_clock_delta_time = 0f;            
            m_clock_stamp      = 0f;
            m_clock_time       = 0f;
            paused             = false;
            base.OnManagerRemoveInternal();
        }

        /// <summary>
        /// Updates the elapsed time and return true when the delay has passed.
        /// </summary>
        /// <returns></returns>
        internal override bool CanStartInternal() {
            //If delay is reached, reset elapsed and keep going            
            if(m_elapsed>=delay) { 
                m_elapsed = 0f; 
                return true; 
            }
            //Increment 'elapsed' until 'delay'            
            m_elapsed += m_clock_delta_time;
            return false;
        }

        /// <summary>
        /// Executes the timer and early initialize data for execution.
        /// </summary>
        internal override void Execute() {            
            //Keep going the execution
            base.Execute();
            //In the first loop init the clock stamp
            if(m_clock_stamp<=0f) { 
                ResetClock(); 
            }
            //Computes the timer own delta-time based on the timestamps
            float t = GetClock();
            //Sample speed only after 'delay'
            float spd = state == ActivityState.Running ? m_speed_internal : 1f;
            m_clock_delta_time = paused ? 0f : ((t - m_clock_time)*spd);
            m_clock_time = t;
            //If 'running' start updating 'elapsed' past the delay
            switch(state) {
                case ActivityState.Running: {
                    float dt = m_clock_delta_time;
                    //TODO: Study applying dt = reverse ? -dt : dt
                    //Delay lasts the same
                    //Elapsed<=0 == Step--
                    //Elapsed>=duration == Step++
                    //Elapsed always >=0
                    //Update Elapsed
                    m_elapsed += dt;                    
                    //Fix elapsed against duration and 0
                    if(m_elapsed < 0f) m_elapsed = 0f;
                    if(duration  > 0f) m_elapsed = Mathf.Min(duration,m_elapsed);
                }
                break;
            }            
        }

        /// <summary>
        /// Execute loop.
        /// </summary>
        /// <returns></returns>
        protected override bool OnExecute() {                        
            //If duration is '0.0' step counting will never occur, otherwise if elapsed>=duration its completed
            return duration<=0f ? true : (m_elapsed<duration);
        }

        /// <summary>
        /// Executed before actually completing and validate steps
        /// </summary>
        /// <returns></returns>
        internal override bool CanCompleteInternal() {
            //Check if user event orders the completion
            bool v1 = InvokeEvent(m_on_step_event,this,true);
            //If 'false' return 'completed'
            if(!v1) return true;
            //Increment step
            step++;                            
            //If 'steps' reached 'count' and 'count>0' complete the timer.
            if(count>0)if(step>=count) { step=count-1; return true; }
            //Reset elapsed slightly behind in time
            m_elapsed=-0.1f;
            //Keep going
            return false;
        }

        #endregion

        #region IProgressProvider

        /// <summary>
        /// Provides timer execution progress.
        /// </summary>
        /// <returns>Execution progress in the range [0,1]</returns>
        override public float GetProgress() {
            switch(state) {
                case ActivityState.Running: return progress;
            }
            return base.GetProgress();
        }

        #endregion

    }

}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityExt.Core {

    #region enum TweenWrap

    /// <summary>
    /// Enumeration that describes how the tween animation should wrap around.
    /// </summary>
    public enum TweenWrap {
        /// <summary>
        /// Animations run once and stops.
        /// </summary>
        Clamp=0,
        /// <summary>
        /// Animation runs in loop and wraps back to first value.
        /// </summary>
        Repeat,
        /// <summary>
        /// Animation runs in loop and go back and forth following the easing results.
        /// </summary>
        Pinpong
    }

    #endregion

    #region class Tween

    /// <summary>
    /// Tweens are timer extensions that upon receiving a 'target' object and its 'property' interpolates it during the timer execution.
    /// It will blend the 'from' 'to' values using an EasingFunction or AnimationCurve that maps the [0,1] progress to a new [0,1] actually applying the interpolation.
    /// </summary>
    public class Tween : Timer {

        #region static

        /// <summary>
        /// Default constant to add tweens.
        /// </summary>
        const ActivityContext DefaultContext = ActivityContext.Update; 

        /// <summary>
        /// Default tween duration if none is specified.
        /// </summary>
        static public float DefaultDuration = 0.2f;

        /// <summary>
        /// Searches for a single tween by id in one or all contexts.
        /// </summary>
        /// <param name="p_id">Tween id to search.</param>        
        /// <param name="p_context">Context to search.</param>
        /// <returns>Tween found or null</returns>        
        static public Tween Find(string p_id,ActivityContext p_context = DefaultContext) { return (Tween)Activity.Find<Tween>(p_id,p_context); }

        #region Find

        /// <summary>
        /// Searches all tweens matching the search criteria.
        /// </summary>
        /// <param name="p_id">Tween Id</param>
        /// <param name="p_target">Target object</param>
        /// <param name="p_property">Property being animated.</param>
        /// <returns>List of tweens or empty list.</returns>
        static public List<Tween> FindAll(string p_id,object p_target,string p_property,ActivityContext p_context = DefaultContext) {            
            List<Tween> tl = Activity.FindAll<Tween>(p_id,p_context);
            for(int i=0;i<tl.Count;i++) {
                Tween it = tl[i];
                if(p_target!=null) if(it.target   != p_target)   { tl.RemoveAt(i--); continue; }
                if(p_property!="") if(it.property != p_property) { tl.RemoveAt(i--); continue; }
            }
            return tl;
        }

        /// <summary>
        /// Searches all tweens matching the search criteria.
        /// </summary>
        /// <param name="p_id">Tween Id</param>
        /// <param name="p_target">Target object</param>        
        /// <returns>List of tweens or empty list.</returns>
        static public List<Tween> FindAll(string p_id,object p_target,ActivityContext p_context = DefaultContext) { return FindAll(p_id,p_target,"",p_context); }

        /// <summary>
        /// Searches all tweens matching the search criteria.
        /// </summary>
        /// <param name="p_id">Tween Id</param>        
        /// <param name="p_property">Property being animated.</param>
        /// <returns>List of tweens or empty list.</returns>
        static public List<Tween> FindAll(string p_id,string p_property,ActivityContext p_context = DefaultContext) { return FindAll(p_id,null,p_property,p_context); }

        /// <summary>
        /// Searches all tweens matching the search criteria.
        /// </summary>
        /// <param name="p_id">Tween Id</param>
        /// <param name="p_target">Target object</param>
        /// <param name="p_property">Property being animated.</param>
        /// <returns>List of tweens or empty list.</returns>
        static public List<Tween> FindAll(string p_id,ActivityContext p_context = DefaultContext) { return Activity.FindAll<Tween>(p_id,p_context); }

        /// <summary>
        /// Searches all tweens matching the search criteria.
        /// </summary>        
        /// <param name="p_target">Target object</param>
        /// <param name="p_property">Property being animated.</param>
        /// <returns>List of tweens or empty list.</returns>
        static public List<Tween> FindAll(object p_target,string p_property,ActivityContext p_context = DefaultContext) { return FindAll("",p_target,p_property,p_context); }

        /// <summary>
        /// Searches all tweens matching the search criteria.
        /// </summary>        
        /// <param name="p_target">Target object</param>
        /// <param name="p_property">Property being animated.</param>
        /// <returns>List of tweens or empty list.</returns>
        static public List<Tween> FindAll(object p_target,ActivityContext p_context = DefaultContext) { return FindAll("",p_target,"",p_context); }

        #endregion

        #region Clear

        /// <summary>
        /// Stops all tweens matching the criteria
        /// </summary>
        /// <param name="p_id">Tween Id</param>
        /// <param name="p_target">Target object</param>
        /// <param name="p_property">Property being animated</param>
        /// <param name="p_context">Context to search at.</param>
        static public void Clear(string p_id,object p_target,string p_property,ActivityContext p_context = DefaultContext) { ClearAll(FindAll(p_id,p_target,p_property,p_context)); }

        /// <summary>
        /// Stops all tweens matching the criteria
        /// </summary>
        /// <param name="p_id">Tween Id</param>
        /// <param name="p_target">Target object</param>        
        /// <param name="p_context">Context to search at.</param>
        static public void Clear(string p_id,object p_target,ActivityContext p_context = DefaultContext) { Clear(p_id,p_target,"",p_context); }

        /// <summary>
        /// Stops all tweens matching the criteria
        /// </summary>
        /// <param name="p_id">Tween Id</param>        
        /// <param name="p_property">Property being animated</param>
        static public void Clear(string p_id,string p_property,ActivityContext p_context = DefaultContext) { Clear(p_id,null,p_property,p_context); }

        /// <summary>
        /// Stops all tweens matching the criteria
        /// </summary>
        /// <param name="p_id">Tween Id</param>                
        /// <param name="p_context">Context to search at.</param>
        static public void Clear(string p_id,ActivityContext p_context = DefaultContext) { Clear(p_id,null,"",p_context); }

        /// <summary>
        /// Stops all tweens matching the criteria
        /// </summary>
        /// <param name="p_target">Target object</param>
        /// <param name="p_property">Property being animated</param>
        /// <param name="p_context">Context to search at.</param>
        static public void Clear(object p_target,string p_property,ActivityContext p_context = DefaultContext) { ClearAll(FindAll("",p_target,p_property,p_context)); }

        /// <summary>
        /// Stops all tweens matching the criteria
        /// </summary>        
        /// <param name="p_target">Target object</param>        
        /// <param name="p_context">Context to search at.</param>
        static public void Clear(object p_target,ActivityContext p_context = DefaultContext) { Clear("",p_target,"",p_context); }

        /// <summary>
        /// Stops all tweens matching the criteria
        /// </summary>                
        /// <param name="p_context">Context to search at.</param>
        static public void Clear(ActivityContext p_context = DefaultContext) { Clear("",null,"",p_context); }

        /// <summary>
        /// Helper
        /// </summary>
        /// <param name="p_list"></param>
        static internal void ClearAll(List<Tween> p_list) { for(int i=0;i<p_list.Count;i++) p_list[i].Stop(); }

        #endregion
        /*
        #region Run/Loop

        /// <summary>
        /// Helper
        /// </summary>        
        static internal Tween<T> Create<T>(string p_id,object p_target,string p_property,T p_from,T p_to,bool p_has_from,float p_duration,int p_count,object p_easing,TweenWrap p_wrap,System.Predicate<Timer> p_on_execute,System.Action<Timer> p_on_complete,System.Predicate<Timer> p_on_step) {
            Tween<T> n = new Tween<T>(p_id,p_duration,p_count);
            n.CreateTween(p_target,p_property,p_from,p_to,p_has_from,p_easing,p_wrap);
            n.OnCompleteEvent = p_on_complete;
            n.OnExecuteEvent  = p_on_execute;
            n.OnStepEvent     = p_on_step;            
            return n;
        }

        #region Run

        /// <summary>
        /// Creates and executes a Timer with a per-execution callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_callback">Handler for each execution.</param>
        /// <param name="p_duration">Duration of the Timer, if 0.0 runs forever.</param>
        /// <param name="p_count">Number of steps, if 0 repeats 'duration' forever.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,float p_delay=0f,int p_count=1,Func<float,float> p_easing=null,TweenWrap p_wrap = TweenWrap.Clamp,System.Predicate<Tween> p_callback=null) { Tween n = Create<T>(p_id,); n.Start(); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-execution callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_callback">Handler for each execution.</param>        
        /// <param name="p_count">Number of steps, if 0 repeats 'duration' forever.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(string p_id,System.Predicate<Timer> p_callback,int p_count=1,TimerType p_type = TimerType.Unity)                  { Timer n = Create(p_id,p_type,p_callback,null,null,0f,        p_count); n.Start(); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-execution callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>        
        /// <param name="p_callback">Handler for each execution.</param>
        /// <param name="p_duration">Duration of the Timer, if 0.0 runs forever.</param>
        /// <param name="p_count">Number of steps, if 0 repeats 'duration' forever.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(System.Predicate<Timer> p_callback,float p_duration,int p_count=1,TimerType p_type = TimerType.Unity)             { Timer n = Create("",  p_type,p_callback,null,null,p_duration,p_count); n.Start(); return n; }
        /// <summary>
        /// Creates and executes a Timer with a per-execution callback. If no 'duration' is specified the Timer runs forever, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>        
        /// <param name="p_callback">Handler for each execution.</param>        
        /// <param name="p_count">Number of steps, if 0 repeats 'duration' forever.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(System.Predicate<Timer> p_callback,int p_count=1,TimerType p_type = TimerType.Unity)                              { Timer n = Create("",  p_type,p_callback,null,null,0f,        p_count); n.Start(); return n; }

        #endregion

        #region RunOnce

        /// <summary>
        /// Creates and executes a Timer with a once upon completion callback. If no 'duration' is specified the Timer runs for a single frame, if no 'count' is specified the Timer runs for single step.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_callback">Handler for completion.</param>
        /// <param name="p_duration">Duration of the Timer, if 0.0 runs for a frame.</param>
        /// <param name="p_count">Number of steps, will be forced to >=1.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(string p_id,System.Action<Timer> p_callback,float p_duration,int p_count=1,TimerType p_type = TimerType.Unity) { Timer n = Create(p_id,p_type,null,p_callback,null,Mathf.Max(p_duration,0.0001f),Mathf.Max(1,p_count)); n.Start(); return n; }
        /// <summary>
        /// Creates and executes a Timer with a once upon completion callback. If no 'duration' is specified the Timer runs for a single frame, if no 'count' is specified the Timer runs for single step.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_callback">Handler for completion.</param>        
        /// <param name="p_count">Number of steps, will be forced to >=1.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(string p_id,System.Action<Timer> p_callback,int p_count=1,TimerType p_type = TimerType.Unity)                  { Timer n = Create(p_id,p_type,null,p_callback,null,0.0001f,                      Mathf.Max(1,p_count)); n.Start(); return n; }
        /// <summary>
        /// Creates and executes a Timer with a once upon completion callback. If no 'duration' is specified the Timer runs for a single frame, if no 'count' is specified the Timer runs for single step.
        /// </summary>        
        /// <param name="p_callback">Handler for completion.</param>
        /// <param name="p_duration">Duration of the Timer, if 0.0 runs for a frame.</param>
        /// <param name="p_count">Number of steps, will be forced to >=1.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(System.Action<Timer> p_callback,float p_duration,int p_count=1,TimerType p_type = TimerType.Unity)             { Timer n = Create("",  p_type,null,p_callback,null,Mathf.Max(p_duration,0.0001f),Mathf.Max(1,p_count)); n.Start(); return n; }
        /// <summary>
        /// Creates and executes a Timer with a once upon completion callback. If no 'duration' is specified the Timer runs for a single frame, if no 'count' is specified the Timer runs for single step.
        /// </summary>        
        /// <param name="p_callback">Handler for completion.</param>        
        /// <param name="p_count">Number of steps, will be forced to >=1.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Run(System.Action<Timer> p_callback,int p_count=1,TimerType p_type = TimerType.Unity)                              { Timer n = Create("",  p_type,null,p_callback,null,0.0001f,                      Mathf.Max(1,p_count)); n.Start(); return n; }
        
        #endregion

        #region Step

        /// <summary>
        /// Creates and executes a Timer with a per-step callback. If no 'duration' is specified the Timer should run one step per frame, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_callback">Handler for each execution.</param>
        /// <param name="p_duration">Duration of the Timer, if 0.0 runs per frame.</param>
        /// <param name="p_count">Number of steps, if 0 repeats 'duration' forever.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Step(string p_id,System.Predicate<Timer> p_callback,float p_duration,int p_count=1,TimerType p_type = TimerType.Unity)  { Timer n = Create(p_id,p_type,null,null,p_callback,Mathf.Max(p_duration,0.0001f),p_count); n.Start(); return n; }        
        /// <summary>
        /// Creates and executes a Timer with a per-step callback. If no 'duration' is specified the Timer should run one step per frame, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>
        /// <param name="p_id">Timer Id</param>
        /// <param name="p_callback">Handler for each execution.</param>        
        /// <param name="p_count">Number of steps, if 0 repeats 'duration' forever.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Step(string p_id,System.Predicate<Activity> p_callback,int p_count=1,TimerType p_type = TimerType.Unity)                { Timer n = Create(p_id,p_type,null,null,p_callback,0.0001f,                      p_count); n.Start(); return n; }        
        /// <summary>
        /// Creates and executes a Timer with a per-step callback. If no 'duration' is specified the Timer should run one step per frame, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>        
        /// <param name="p_callback">Handler for each execution.</param>
        /// <param name="p_duration">Duration of the Timer, if 0.0 runs per frame.</param>
        /// <param name="p_count">Number of steps, if 0 repeats 'duration' forever.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Step(System.Predicate<Activity> p_callback,float p_duration,int p_count=1,TimerType p_type = TimerType.Unity)           { Timer n = Create("",  p_type,null,null,p_callback,Mathf.Max(p_duration,0.0001f),p_count); n.Start(); return n; }        
        /// <summary>
        /// Creates and executes a Timer with a per-step callback. If no 'duration' is specified the Timer should run one step per frame, if no 'count' is specified the Timer loops 'duration' forever.
        /// </summary>        
        /// <param name="p_callback">Handler for each execution.</param>        
        /// <param name="p_count">Number of steps, if 0 repeats 'duration' forever.</param>
        /// <param name="p_type">Timer type.</param>
        /// <returns>Timer instance, already running.</returns>
        static public Timer Step(System.Predicate<Activity> p_callback,int p_count=1,TimerType p_type = TimerType.Unity)                            { Timer n = Create("",  p_type,null,null,p_callback,0.0001f,                      p_count); n.Start(); return n; }

        #endregion

        #endregion

        //*/

        #endregion

        /// <summary>
        /// Target of the tween.
        /// </summary>
        public object target { get { return m_interpolator==null ? null : m_interpolator.target; } set { m_interpolator.target = value; } }

        /// <summary>
        /// Property to be animated.
        /// </summary>
        public string property { get { return m_interpolator==null ? "" : m_interpolator.property; } set { m_interpolator.property = value; } }

        /// <summary>
        /// Returns the interpolator cast to the desired data type manipulation. If a not matching type is used, null is returned.
        /// </summary>
        /// <typeparam name="T">Type of the value interpolated.</typeparam>
        /// <returns>The tween's interpolator cast to the desired type.</returns>
        public Interpolator interpolator { get { return m_interpolator; } set { m_interpolator = value; } }
        private Interpolator m_interpolator;

        /// <summary>
        /// Tween animation wrapping.
        /// </summary>
        public TweenWrap wrap {
            get { return m_wrap; }
            set {
                m_wrap = value;
                switch(m_wrap) {
                    case TweenWrap.Clamp: count=1; break;
                    case TweenWrap.Pinpong:
                    case TweenWrap.Repeat: if(count==1) count=0; break;
                }
            }
        }
        private TweenWrap m_wrap;

        /// <summary>
        /// Get/Set the tween animation speed.
        /// </summary>
        public float speed { get { return m_speed_internal; } set { m_speed_internal = Mathf.Max(value,0f); } }

        #region CTOR

        /// <summary>
        /// Creates a new Tween to animate 'property' of a 'target' object.
        /// </summary>
        /// <param name="p_id">Tween Id for querying and/or cancelling.</param>
        /// <param name="p_target">Object to animate.</param>
        /// <param name="p_property">Property to apply animation.</param>
        /// <param name="p_from">Initial value, if not specified the interpolator will sample the property in the first iteration.</param>
        /// <param name="p_to">End value applied in the property.</param>
        /// <param name="p_duration">Duration of the Tween. Defaults to 'DefaultDuration'</param>
        /// <param name="p_count">Number of repetitions, defaults to 1</param>
        /// <param name="p_easing">Easing Function/Curve. Its possible to provide either a Func<float,float> or AnimationCurve to interpolate the property.</param>
        public Tween(string p_id,object p_target,string p_property,float p_duration,TweenWrap p_wrap,Func<float,float> p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_easing,p_wrap         ); }
        public Tween(string p_id,object p_target,string p_property,float p_duration,TweenWrap p_wrap,AnimationCurve    p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_easing,p_wrap         ); }                      
        public Tween(string p_id,object p_target,string p_property,                 TweenWrap p_wrap,Func<float,float> p_easing=null) : base(p_id,DefaultDuration,1)  { CreateTween(p_target,p_property,p_easing,p_wrap         ); }
        public Tween(string p_id,object p_target,string p_property,                 TweenWrap p_wrap,AnimationCurve    p_easing=null) : base(p_id,DefaultDuration,1)  { CreateTween(p_target,p_property,p_easing,p_wrap         ); }                        
        public Tween(string p_id,object p_target,string p_property,float p_duration,                 Func<float,float> p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_easing,TweenWrap.Clamp); }
        public Tween(string p_id,object p_target,string p_property,float p_duration,                 AnimationCurve    p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_easing,TweenWrap.Clamp); }
        public Tween(string p_id,object p_target,string p_property,                                  Func<float,float> p_easing=null) : base(p_id,DefaultDuration,1)  { CreateTween(p_target,p_property,p_easing,TweenWrap.Clamp); }        
        public Tween(string p_id,object p_target,string p_property,                                  AnimationCurve    p_easing=null) : base(p_id,DefaultDuration,1)  { CreateTween(p_target,p_property,p_easing,TweenWrap.Clamp); }                        

        public Tween(            object p_target,string p_property,float p_duration,TweenWrap p_wrap,Func<float,float> p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_easing,p_wrap         ); }
        public Tween(            object p_target,string p_property,float p_duration,TweenWrap p_wrap,AnimationCurve    p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_easing,p_wrap         ); }                        
        public Tween(            object p_target,string p_property,                 TweenWrap p_wrap,Func<float,float> p_easing=null) : base("",DefaultDuration,1)  { CreateTween(p_target,p_property,p_easing,p_wrap         ); }
        public Tween(            object p_target,string p_property,                 TweenWrap p_wrap,AnimationCurve    p_easing=null) : base("",DefaultDuration,1)  { CreateTween(p_target,p_property,p_easing,p_wrap         ); }                        
        public Tween(            object p_target,string p_property,float p_duration,                 Func<float,float> p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_easing,TweenWrap.Clamp); }
        public Tween(            object p_target,string p_property,float p_duration,                 AnimationCurve    p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_easing,TweenWrap.Clamp); }
        public Tween(            object p_target,string p_property,                                  Func<float,float> p_easing=null) : base("",DefaultDuration,1)  { CreateTween(p_target,p_property,p_easing,TweenWrap.Clamp); }        
        public Tween(            object p_target,string p_property,                                  AnimationCurve    p_easing=null) : base("",DefaultDuration,1)  { CreateTween(p_target,p_property,p_easing,TweenWrap.Clamp); }                        
        
        /// <summary>
        /// Helper
        /// </summary>        
        internal Tween(string p_id,float p_duration,int p_count) : base(p_id,p_duration,p_count) { }
        
        /// <summary>
        /// Creates the tween internal structure.
        /// </summary>        
        internal void CreateTween(object p_target,string p_property,object p_easing,TweenWrap p_wrap) {
            //TODO: Tweens should use the async context to save up resources. (async timer use a thread timer thus fail on Pause/Play)
            context = DefaultContext;
            wrap    = p_wrap;
            Interpolator itp = m_interpolator;
            if(interpolator!=null) { interpolator.Create(p_target,p_property,p_easing); }
        }

        #endregion

        /// <summary>
        /// Updates the tween interpolation during execution.
        /// </summary>
        internal override void Execute() {
            switch(state) {
                case ActivityState.Running: {
                    if(m_interpolator==null) break;                    
                    float r = duration<=0f ? elapsed : progress;
                    //Transform the ratio
                    switch(wrap) {
                        case TweenWrap.Clamp: {
                            //After first step clamp the ratio
                            r=step>0 ? 1f : r;                            
                        }
                        break;

                        case TweenWrap.Pinpong:
                        case TweenWrap.Repeat: {
                            bool  is_pong = wrap == TweenWrap.Repeat ? false : ((step&1)==1);
                            if(is_pong) r = 1f - r;
                            m_interpolator.Lerp(r);
                        }
                        break;
                    }                    
                    //Apply the ratio
                    m_interpolator.Lerp(r);
                }
                break;
            }
            base.Execute();            
        }

    }

    #endregion

    #region class Tween<T>

    /// <summary>
    /// Extension of the tween class allowing specifying the type of the property's value ranges.
    /// </summary>
    /// <typeparam name="T">Type of the target's property.</typeparam>
    public class Tween<T> : Tween {
    
        #region Get/Set

        /// <summary>
        /// Returns the interpolator cast to the desired data type manipulation. If a not matching type is used, null is returned.
        /// </summary>
        /// <typeparam name="T">Type of the value interpolated.</typeparam>
        /// <returns>The tween's interpolator cast to the desired type.</returns>
        new public Interpolator<T> interpolator { get { return (Interpolator<T>)base.interpolator; } set { base.interpolator = value; } }

        /// <summary>
        /// Get/Set the final value of the animation.
        /// </summary>
        public T to   { get { return interpolator==null ? default : interpolator.to;   } set { if(interpolator!=null) interpolator.to = value; } }

        /// <summary>
        /// Get/Set the starting value of the animation.
        /// </summary>
        public T from { get { return interpolator==null ? default : interpolator.from; } set { if(interpolator!=null) { interpolator.m_capture_from=false; interpolator.from=value; } } }

        #endregion

        #region CTOR

        /// <summary>
        /// Creates a new Tween to animate 'property' of a 'target' object.
        /// </summary>
        /// <param name="p_id">Tween Id for querying and/or cancelling.</param>
        /// <param name="p_target">Object to animate.</param>
        /// <param name="p_property">Property to apply animation.</param>
        /// <param name="p_from">Initial value, if not specified the interpolator will sample the property in the first iteration.</param>
        /// <param name="p_to">End value applied in the property.</param>
        /// <param name="p_duration">Duration of the Tween. Defaults to 'DefaultDuration'</param>
        /// <param name="p_count">Number of repetitions, defaults to 1</param>
        /// <param name="p_easing">Easing Function/Curve. Its possible to provide either a Func<float,float> or AnimationCurve to interpolate the property.</param>
        public Tween(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,TweenWrap p_wrap,Func<float,float> p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,p_wrap         ); }
        public Tween(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,TweenWrap p_wrap,AnimationCurve    p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,p_wrap         ); }        
        public Tween(string p_id,object p_target,string p_property,         T p_to,float p_duration,TweenWrap p_wrap,Func<float,float> p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,p_wrap         ); }        
        public Tween(string p_id,object p_target,string p_property,         T p_to,float p_duration,TweenWrap p_wrap,AnimationCurve    p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,p_wrap         ); }
        public Tween(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,                 Func<float,float> p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,TweenWrap.Clamp); }
        public Tween(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,                 AnimationCurve    p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,TweenWrap.Clamp); }
        public Tween(string p_id,object p_target,string p_property,T p_from,T p_to,                                  Func<float,float> p_easing=null) : base(p_id,DefaultDuration,1)  { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,TweenWrap.Clamp); }        
        public Tween(string p_id,object p_target,string p_property,T p_from,T p_to,                                  AnimationCurve    p_easing=null) : base(p_id,DefaultDuration,1)  { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,TweenWrap.Clamp); }        
        public Tween(string p_id,object p_target,string p_property,         T p_to,float p_duration,                 Func<float,float> p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,TweenWrap.Clamp); }                
        public Tween(string p_id,object p_target,string p_property,         T p_to,                                  Func<float,float> p_easing=null) : base(p_id,DefaultDuration,1)  { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,TweenWrap.Clamp); }
        public Tween(string p_id,object p_target,string p_property,         T p_to,float p_duration,                 AnimationCurve    p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,TweenWrap.Clamp); }        
        public Tween(string p_id,object p_target,string p_property,         T p_to,                                  AnimationCurve    p_easing=null) : base(p_id,DefaultDuration,1)  { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,TweenWrap.Clamp); }

        public Tween(            object p_target,string p_property,T p_from,T p_to,float p_duration,TweenWrap p_wrap,Func<float,float> p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,p_wrap         ); }
        public Tween(            object p_target,string p_property,T p_from,T p_to,float p_duration,TweenWrap p_wrap,AnimationCurve    p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,p_wrap         ); }        
        public Tween(            object p_target,string p_property,         T p_to,float p_duration,TweenWrap p_wrap,Func<float,float> p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,p_wrap         ); }        
        public Tween(            object p_target,string p_property,         T p_to,float p_duration,TweenWrap p_wrap,AnimationCurve    p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,p_wrap         ); }
        public Tween(            object p_target,string p_property,T p_from,T p_to,float p_duration,                 Func<float,float> p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,TweenWrap.Clamp); }
        public Tween(            object p_target,string p_property,T p_from,T p_to,float p_duration,                 AnimationCurve    p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,TweenWrap.Clamp); }
        public Tween(            object p_target,string p_property,T p_from,T p_to,                                  Func<float,float> p_easing=null) : base("",DefaultDuration,1)  { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,TweenWrap.Clamp); }        
        public Tween(            object p_target,string p_property,T p_from,T p_to,                                  AnimationCurve    p_easing=null) : base("",DefaultDuration,1)  { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,TweenWrap.Clamp); }        
        public Tween(            object p_target,string p_property,         T p_to,float p_duration,                 Func<float,float> p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,TweenWrap.Clamp); }                
        public Tween(            object p_target,string p_property,         T p_to,                                  Func<float,float> p_easing=null) : base("",DefaultDuration,1)  { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,TweenWrap.Clamp); }
        public Tween(            object p_target,string p_property,         T p_to,float p_duration,                 AnimationCurve    p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,TweenWrap.Clamp); }        
        public Tween(            object p_target,string p_property,         T p_to,                                  AnimationCurve    p_easing=null) : base("",DefaultDuration,1)  { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,TweenWrap.Clamp); }
        
        /// <summary>
        /// Helper
        /// </summary>        
        internal Tween(string p_id,float p_duration,int p_count) : base(p_id,p_duration,p_count) { }

        /// <summary>
        /// Creates the tween internal structure.
        /// </summary>        
        internal void CreateTween(object p_target,string p_property,T p_from,T p_to,bool p_has_from,object p_easing,TweenWrap p_wrap) {                        
            //First create the interpolator
            interpolator = Interpolator.Get<T>();
            interpolator.to = p_to;
            if(p_has_from) interpolator.from = p_from;
            //Then keep going with init.
            CreateTween(p_target,p_property,p_easing,p_wrap);
        }

        #endregion

        #region Restart

        /// <summary>
        /// Restarts the tween from scratch.
        /// </summary>
        new public void Restart() { Restart(false); }

        /// <summary>
        /// Restarts the tween continuing from the current property value.
        /// </summary>
        /// <param name="p_continue">Flag that tells if the property will keep going from its current value.</param>
        public void Restart(bool p_continue) {
            if(p_continue)if(interpolator!=null) { interpolator.m_capture_from=true; interpolator.m_first_iteration=true; }
            base.Restart();
        }

        /// <summary>
        /// Restarts the current execution step.
        /// </summary>
        new public void RestartStep() {
            RestartStep(false);
        }

        /// <summary>
        /// Restarts the current execution step.
        /// </summary>
        /// <param name="p_continue">Flag that tells if the property will keep going from its current value.</param>
        public void RestartStep(bool p_continue) {
            if(p_continue)if(interpolator!=null) { interpolator.m_capture_from=true; interpolator.m_first_iteration=true; }
            base.RestartStep();
        }

        #endregion
    }

    #endregion

}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityExt.Core {

    #region enum AnimationWrapMode

    /// <summary>
    /// Enumeration that describes how the tween animation should wrap around.
    /// </summary>
    public enum AnimationWrapMode {
        /// <summary>
        /// Animations run once and stops.
        /// </summary>
        Clamp = 0,
        /// <summary>
        /// Animation runs in loop and wraps back to first value.
        /// </summary>
        Repeat,
        /// <summary>
        /// Animation runs in loop and go back and forth following the easing results.
        /// </summary>
        Pingpong
    }

    #endregion

    #region class Tween

    #region struct TweenStart
    /// <summary>
    /// Struct that initializes a Timer Start
    /// </summary>
    public struct TweenStart {

        #region static
        /// <summary>
        /// Returns a populated start struct using the input instance
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        static internal TweenStart From(Tween t) {
            return new TweenStart {
                tween    = t.tween,
                delay    = t.delay,
                duration = t.duration,
                count    = t.count,
                speed    = t.speed,
                wrap     = t.wrap
            };
        }
        #endregion

        /// <summary>
        /// Reference to the interpolator
        /// </summary>
        public IInterpolator tween;

        /// <summary>
        /// Delay before starting
        /// </summary>
        public float delay { get { return m_delay ?? 0f; } set { m_delay = value; } }
        float? m_delay;

        /// <summary>
        /// Duration of the timer
        /// </summary>
        public float duration { get { return m_duration ?? Tween.DefaultDuration; } set { m_duration = value; } }
        float? m_duration;

        /// <summary>
        /// Number of steps
        /// </summary>
        public int count { get { return m_count ?? 1; } set { m_count = value; } }
        int? m_count;

        /// <summary>
        /// Speed of time increments
        /// </summary>
        public float speed { get { return m_speed ?? 1f; } set { m_speed = value; } }
        float? m_speed;

        /// <summary>
        /// Speed of time increments
        /// </summary>
        public AnimationWrapMode wrap { get { return m_wrap ?? AnimationWrapMode.Clamp; } set { m_wrap = value; } }
        AnimationWrapMode? m_wrap;

        /// <summary>
        /// Helper to populate a target instance
        /// </summary>        
        internal void To(Tween t) {
            if(m_delay    != null) { t.delay    = delay;    }
            if(m_duration != null) { t.duration = duration; }
            if(m_count    != null) { t.count    = count;    }
            if(m_speed    != null) { t.speed    = speed;    }
            if(m_wrap     != null) { t.wrap     = wrap;     }
        }

    }
    #endregion

    /// <summary>
    /// Tweens are timer extensions that upon receiving a 'target' object and its 'property' interpolates it during the timer execution.
    /// It will blend the 'from' 'to' values using an EasingFunction or AnimationCurve that maps the [0,1] progress to a new [0,1] actually applying the interpolation.
    /// </summary>
    public class Tween : Timer {

        #region Easings

        /// <summary>
        /// Easing Equation y = x, basic linear interpolation
        /// </summary>
        /// <param name="p_r"></param>
        /// <returns></returns>
        static public float Linear(float p_r) { return p_r; }

        #region Quad
        /// <summary>
        /// Class that handles quadratic form equations.
        /// </summary>
        public class Quad {
            /// <summary>
            /// Easing equation: 'y = x*x'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float In(float p_r) { return p_r * p_r; }
            /// <summary>
            /// Easing equation: 'y = x*(-x+2)'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float Out(float p_r) { return p_r * (-p_r + 2f); }
            /// <summary>
            /// Easing equation: 'y = x*(-3*x + 4)'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float OutBack(float p_r) { return p_r * (-3f * p_r + 4f); }
            /// <summary>
            /// Easing equation: 'y = x*(3*x - 2)'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float BackIn(float p_r) { return p_r * (3f * p_r - 2f); }
        }
        #endregion

        #region Cubic
        /// <summary>
        /// Class that handles cubic form equations.
        /// </summary>
        public class Cubic {
            /// <summary>
            /// Easing equation: 'y = x*x*x'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float In(float p_r) { return p_r * p_r * p_r; }
            /// <summary>
            /// Easing equation: 'y = x*(x*(x-3)+3)'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float Out(float p_r) { return p_r * (p_r * (p_r - 3f) + 3f); }
            /// <summary>
            /// Easing equation: 'y = -2*x*(x*(x-1.5))'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float InOut(float p_r) { return -2f * p_r * (p_r * (p_r - 1.5f)); }
            /// <summary>
            /// Easing equation: 'y = x*(x*(4*x -6)+3)'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float OutIn(float p_r) { return p_r * (p_r * (4f * p_r - 6f) + 3f); }
            /// <summary>
            /// Easing equation: 'y = x*(x*(4*x-3))'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float BackIn(float p_r) { return p_r * (p_r * (4f * p_r - 3f)); }
            /// <summary>
            /// Easing equation: 'y = x*(x*(4*x -9) +6)'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float OutBack(float p_r) { return p_r * (p_r * (4f * p_r - 9f) + 6f); }
        }
        #endregion

        #region Quartic
        /// <summary>
        /// Class that handles quartic form equations.
        /// </summary>
        public class Quartic {
            /// <summary>
            /// Easing equation: 'y = x*x*x*x'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float In(float p_r) { return p_r * p_r * p_r * p_r; }
            /// <summary>
            /// Easing equation: 'y = x*(x*(x*(-x+4)-6)+4)'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float Out(float p_r) { return p_r * (p_r * (p_r * (-p_r + 4f) - 6f) + 4f); }
            /// <summary>
            /// Easing equation: 'y = x*(x*(x*(x+2)-4)+2)'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float OutIn(float p_r) { return p_r * (p_r * (p_r * (p_r + 2f) - 4f) + 2f); }
            /// <summary>
            /// Easing equation: 'y = x*(x*(x*(x+2)+1)-3)'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float BackIn(float p_r) { return p_r * (p_r * (p_r * (p_r + 2f) + 1f) - 3f); }
            /// <summary>
            /// Easing equation: 'y = x*(x*(x*(-2*x+10)-15)+8)'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float OutBack(float p_r) { return p_r * (p_r * (p_r * (-2f * p_r + 10f) - 15f) + 8f); }
        }
        #endregion

        #region Quintic
        /// <summary>
        /// Class that handles quintic form equations.
        /// </summary>
        public class Quintic {
            /// <summary>
            /// Easing equation: 'y = x*x*x*x*x'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float In(float p_r) { return p_r * p_r * p_r * p_r * p_r; }
            /// <summary>
            /// Easing equation: 'y = x*(x*(x*(x*(x-5)+10)-10)+5)'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float Out(float p_r) { return p_r * (p_r * (p_r * (p_r * (p_r - 5f) + 10f) - 10f) + 5f); }
        }
        #endregion

        #region Elastic
        /// <summary>
        /// Class that handles elastic effect interpolation.
        /// </summary>
        public class Elastic {
            /// <summary>
            /// Easing equation: 'y = x*(x*(x*(x*(56*x -175) + 200) -100) + 20)'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float OutBig(float p_r) { return p_r * (p_r * (p_r * (p_r * ((56f) * p_r + (-175f)) + (200f)) + (-100f)) + (20f)); }
            /// <summary>
            /// Easing equation: 'y = x*(x*(x*(x*(33*x -106) + 126) -67) + 15)'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float OutSmall(float p_r) { return p_r * (p_r * (p_r * (p_r * ((33f) * p_r + (-106f)) + (126f)) + (-67f)) + (15f)); }
            /// <summary>
            /// Easing equation: 'y = x*(x*(x*(x*(33*x -59)+32)-5))'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float InBig(float p_r) { return p_r * (p_r * (p_r * (p_r * ((33f) * p_r + (-59f)) + (32f)) + (-5f))); }
            /// <summary>
            /// Easing equation: 'y = x*(x*(x*(x*(56*x-105)+60)-10))'.
            /// </summary>
            /// <param name="p_r">Ratio input</param>
            /// <returns>Ratio output.</returns>
            static public float InSmall(float p_r) { return p_r * (p_r * (p_r * (p_r * ((56f) * p_r + (-105f)) + (60f)) + (-10f))); }
        }
        #endregion

        #endregion

        #region static

        /// <summary>
        /// Default tween duration if none is specified.
        /// </summary>
        static public float DefaultDuration = 0.2f;

        /// <summary>
        /// Default Easing Method
        /// </summary>
        static public Func<float,float> DefaultEasing = Quad.Out;

        /// <summary>
        /// Searches for a single tween by id in one or all contexts.
        /// </summary>
        /// <param name="p_id">Tween id to search.</param>        
        /// <param name="p_context">Context to search.</param>
        /// <returns>Tween found or null</returns>        
        //static public Tween Find(string p_id,ActivityContext p_context = DefaultContext) { return (Tween)Activity.Find<Tween>(p_id,p_context); }

        #region Find
        /*
        /// <summary>
        /// Searches all tweens matching the search criteria.
        /// </summary>
        /// <param name="p_id">Tween Id</param>
        /// <param name="p_target">Target object</param>
        /// <param name="p_property">Property being animated.</param>
        /// <returns>List of tweens or empty list.</returns>
        static public List<Tween> FindAll(string p_id,object p_target,string p_property,ActivityContext p_context = DefaultContext) {            
            List<Tween> tl = Activity.FindAll<Tween>(p_id,p_context);
            bool will_search_objects = (p_target!=null) || (!string.IsNullOrEmpty(p_property));
            for(int i=0;i<tl.Count;i++) {
                Tween it = tl[i];
                PropertyInterpolator it_pi = (it.tween is PropertyInterpolator) ? ((PropertyInterpolator)it.tween) : null;                
                if(will_search_objects) if(it_pi==null) continue;
                if(p_target  != null)                 if(it_pi.target   != p_target)   { tl.RemoveAt(i--); continue; }
                if(!string.IsNullOrEmpty(p_property)) if(it_pi.property != p_property) { tl.RemoveAt(i--); continue; }
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
        //*/
        #endregion

        #region Clear
        /*
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
        //*/
        #endregion
        
        #region Run/Loop
        /*
        /// <summary>
        /// Helper
        /// </summary>        
        static internal Tween<T> Create<T>(string p_id,object p_target,string p_property,T p_from,T p_to,bool p_has_from,float p_duration,float p_delay,int p_count,object p_easing,AnimationWrapMode p_wrap,System.Predicate<Tween> p_on_execute,System.Action<Tween> p_on_complete,System.Predicate<Tween> p_on_step) {
            Tween<T> n = new Tween<T>(p_id,p_duration,p_count);            
            n.CreateTween(p_target,p_property,p_from,p_to,p_has_from,p_easing,p_wrap);
            n.OnCompleteEvent = p_on_complete;
            n.OnExecuteEvent  = p_on_execute;
            n.OnStepEvent     = p_on_step;            
            return n;
        }
        //*/
        #region Run

        #region Complete Callback

        #region Easing Method
        /*
        /// <summary>
        /// Creates and execute a tween animation, applying the property change in the target object. 
        /// A 'from' value can be specified and will be snaped upon tween start, otherwise the tween will sample the current property value.
        /// The tween can have a delay before it animates along 'duration'.
        /// Count will define how much cycles the tween will repeat. The wrap mode will determine if the property will snap to the last value reached or loop/pingpong it.
        /// </summary>
        /// <typeparam name="T">Type of the property to be animated.</typeparam>
        /// <param name="p_id">Tween Id</param>
        /// <param name="p_target">Target object. If a 'static' variable, a System.Type must be passed.</param>
        /// <param name="p_property">Target's property.</param>
        /// <param name="p_from">Start Value.</param>
        /// <param name="p_to">End Value.</param>
        /// <param name="p_duration">Tween duration.</param>
        /// <param name="p_delay">Delay before start.</param>
        /// <param name="p_count">Tween repeats.</param>
        /// <param name="p_easing">Easing equation or curve.</param>
        /// <param name="p_wrap">Tween animation wrap.</param>
        /// <param name="p_callback">Completion Handler.</param>
        /// <returns>Tween already running.</returns>
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,float p_delay=0f,int p_count=1,Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_from,p_to,true, p_duration,      p_delay,p_count,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,float p_delay=0f,              Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_from,p_to,true, p_duration,      p_delay,      1,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,                               Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_from,p_to,true, p_duration,           0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,T p_from,T p_to,                                                Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_from,p_to,true, DefaultDuration,      0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,         T p_to,float p_duration,float p_delay=0f,int p_count=1,Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_to,  p_to,false,p_duration,      p_delay,p_count,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,         T p_to,float p_duration,float p_delay=0f,              Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_to,  p_to,false,p_duration,      p_delay,      1,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,         T p_to,float p_duration,                               Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_to,  p_to,false,p_duration,           0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,         T p_to,                                                Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_to,  p_to,false,DefaultDuration,      0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,T p_from,T p_to,float p_duration,float p_delay=0f,int p_count=1,Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_from,p_to,true, p_duration,      p_delay,p_count,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,T p_from,T p_to,float p_duration,float p_delay=0f,              Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_from,p_to,true, p_duration,      p_delay,      1,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,T p_from,T p_to,float p_duration,                               Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_from,p_to,true, p_duration,           0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,T p_from,T p_to,                                                Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_from,p_to,true, DefaultDuration,      0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,         T p_to,float p_duration,float p_delay=0f,int p_count=1,Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_to,  p_to,false,p_duration,      p_delay,p_count,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,         T p_to,float p_duration,float p_delay=0f,              Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_to,  p_to,false,p_duration,      p_delay,      1,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,         T p_to,float p_duration,                               Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_to,  p_to,false,p_duration,           0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,         T p_to,                                                Func<float,float> p_easing=null,AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_to,  p_to,false,DefaultDuration,      0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        //*/
        #endregion

        #region Easing Curve
        /*
        /// <summary>
        /// Creates and execute a tween animation, applying the property change in the target object. 
        /// A 'from' value can be specified and will be snaped upon tween start, otherwise the tween will sample the current property value.
        /// The tween can have a delay before it animates along 'duration'.
        /// Count will define how much cycles the tween will repeat. The wrap mode will determine if the property will snap to the last value reached or loop/pingpong it.
        /// </summary>
        /// <typeparam name="T">Type of the property to be animated.</typeparam>
        /// <param name="p_id">Tween Id</param>
        /// <param name="p_target">Target object. If a 'static' variable, a System.Type must be passed.</param>
        /// <param name="p_property">Target's property.</param>
        /// <param name="p_from">Start Value.</param>
        /// <param name="p_to">End Value.</param>
        /// <param name="p_duration">Tween duration.</param>
        /// <param name="p_delay">Delay before start.</param>
        /// <param name="p_count">Tween repeats.</param>
        /// <param name="p_easing">Easing equation or curve.</param>
        /// <param name="p_wrap">Tween animation wrap.</param>
        /// <param name="p_callback">Completion Handler.</param>
        /// <returns>Tween already running.</returns>
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,float p_delay=0f,int p_count=1,AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_from,p_to,true, p_duration,      p_delay,p_count,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,float p_delay=0f,              AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_from,p_to,true, p_duration,      p_delay,      1,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,                               AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_from,p_to,true, p_duration,           0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,T p_from,T p_to,                                                AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_from,p_to,true, DefaultDuration,      0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,         T p_to,float p_duration,float p_delay=0f,int p_count=1,AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_to,  p_to,false,p_duration,      p_delay,p_count,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,         T p_to,float p_duration,float p_delay=0f,              AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_to,  p_to,false,p_duration,      p_delay,      1,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,         T p_to,float p_duration,                               AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_to,  p_to,false,p_duration,           0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        static public Tween<T> Run<T>(string p_id,object p_target,string p_property,         T p_to,                                                AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>(p_id,p_target,p_property,p_to,  p_to,false,DefaultDuration,      0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,T p_from,T p_to,float p_duration,float p_delay=0f,int p_count=1,AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_from,p_to,true, p_duration,      p_delay,p_count,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,T p_from,T p_to,float p_duration,float p_delay=0f,              AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_from,p_to,true, p_duration,      p_delay,      1,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,T p_from,T p_to,float p_duration,                               AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_from,p_to,true, p_duration,           0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,T p_from,T p_to,                                                AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_from,p_to,true, DefaultDuration,      0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,         T p_to,float p_duration,float p_delay=0f,int p_count=1,AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_to,  p_to,false,p_duration,      p_delay,p_count,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,         T p_to,float p_duration,float p_delay=0f,              AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_to,  p_to,false,p_duration,      p_delay,      1,p_easing,p_wrap,null,p_callback,null); n.Start(p_delay); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,         T p_to,float p_duration,                               AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_to,  p_to,false,p_duration,           0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        static public Tween<T> Run<T>(            object p_target,string p_property,         T p_to,                                                AnimationCurve p_easing=null,   AnimationWrapMode p_wrap = AnimationWrapMode.Clamp,System.Action<Tween> p_callback=null) { Tween<T> n = Create<T>("",  p_target,p_property,p_to,  p_to,false,DefaultDuration,      0f,      1,p_easing,p_wrap,null,p_callback,null); n.Start(       ); return n; }
        //*/
        #endregion

        #endregion
        
        #endregion

        #endregion

        #endregion

        /// <summary>
        /// Interpolator associated with this tween.
        /// </summary>
        public IInterpolator tween { get { return m_tween; } set { m_tween = value; } }
        private IInterpolator m_tween;

        /// <summary>
        /// Tween animation wrapping.
        /// </summary>
        public AnimationWrapMode wrap {
            get { return m_wrap; }
            set {
                m_wrap = value;
                switch(m_wrap) {
                    case AnimationWrapMode.Clamp: count=1; break;
                    case AnimationWrapMode.Pingpong:
                    case AnimationWrapMode.Repeat: if(count==1) count=0; break;
                }
            }
        }
        private AnimationWrapMode m_wrap = AnimationWrapMode.Clamp;

        #region CTOR

        /// <summary>
        /// Creates a new Tween to animate 'property' of a 'target' object.
        /// </summary>
        /// <param name="p_id">Tween Id for querying and/or cancelling.</param>        
        public Tween(string p_id="") : base(p_id) { }

        #endregion

        #region Operation

        /// <summary>
        /// Sets this tween parameters
        /// </summary>
        /// <param name="p_args"></param>
        public void Set(TweenStart p_args) { p_args.To(this); }

        /// <summary>
        /// Starts the tween with the passed arguments
        /// </summary>
        /// <returns></returns>
        public Tween Start(ProcessContext p_context,TweenStart p_args) { Set(p_args); return (Tween)Start(p_context); }

        /// <summary>
        /// Starts the tween with the passed arguments
        /// </summary>
        /// <returns></returns>
        public Tween Start(TweenStart p_args) { return Start(DefaultContext,p_args); }

        /// <summary>
        /// Starts the tween with the passed arguments
        /// </summary>
        /// <returns></returns>
        new public Tween Start() { return Start(DefaultContext,TweenStart.From(this)); }

        #if UNITY_EDITOR
        /// <summary>
        /// Starts the tween with the passed arguments
        /// </summary>
        /// <returns></returns>
        public Tween StartEditor(TweenStart p_args) { return Start(ProcessContext.Editor,p_args); }

        /// <summary>
        /// Starts the tween with the passed arguments
        /// </summary>
        /// <returns></returns>
        new public Tween StartEditor() { return Start(TweenStart.From(this)); }
        #endif

        #endregion

        #region Events

        /// <summary>
        /// Handler for execution loop
        /// </summary>
        new public Action<Tween> OnExecuteEvent;

        /// <summary>
        /// Handler for state changes
        /// </summary>
        new public Action<Tween,TimerState,TimerState> OnChangeEvent;

        /// <summary>
        /// Auxiliary Event Calling
        /// </summary>        
        protected override void InternalExecuteEvent(TimerState p_state               ) { if(OnExecuteEvent != null) OnExecuteEvent(this           ); }
        protected override void InternalChangeEvent (TimerState p_from,TimerState p_to) { if(OnChangeEvent  != null) OnChangeEvent(this,p_from,p_to); }

        #endregion

        #region Callbacks

        /// <summary>
        /// Method to check running possibility when starting the tween.
        /// </summary>
        /// <returns></returns>
        virtual protected bool AssertTweenStart() { return true; }

        /// <summary>
        /// Updates the tween interpolation during execution.
        /// </summary>
        protected override void OnStateUpdate(TimerState p_state) {

            base.OnStateUpdate(p_state);

            switch (p_state) {
                case TimerState.Run: {
                    if (m_tween == null) break;
                    float r = duration <= 0f ? elapsed : GetProgress(false);
                    //Transform the ratio
                    switch (wrap) {
                        case AnimationWrapMode.Clamp: {
                            //After first step clamp the ratio
                            r = step > 0 ? 1f : r;
                        }
                        break;

                        case AnimationWrapMode.Pingpong:
                        case AnimationWrapMode.Repeat: {
                            bool is_pong = wrap == AnimationWrapMode.Repeat ? false : ((step & 1) == 1);
                            if (is_pong) r = 1f - r;
                            m_tween.Lerp(r);
                        }
                        break;
                    }
                    //Apply the ratio
                    m_tween.Lerp(r);
                }
                break;
            }
        }

        /// <summary>
        /// Handler for state changes in timer
        /// </summary>        
        protected override void OnStateChange(TimerState p_from,TimerState p_to) {            
            //Sample result for tween
            switch(p_to) {
                case TimerState.Run: {
                    //Handle same property tweens and targets to avoid collision
                    AssertTweenStart();
                }
                break;
            }
            //Base State Change
            base.OnStateChange(p_from,p_to);            
        }

        #endregion

    }
    #endregion

    #region class Tween<T>

    #region struct TweenStart<T>
    /// <summary>
    /// Struct that initializes a Timer Start
    /// </summary>
    public struct TweenStart<T> where T : struct {

        #region static
        /// <summary>
        /// Returns a populated start struct using the input instance
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        static internal TweenStart<T> From(Tween<T> t) {
            TweenStart<T> c = new TweenStart<T>();
            c.m_from    = t.tween.deferredFromValue ? null : t.from;
            c.to        = t.to;            
            c.delay     = t.delay;
            c.duration  = t.duration;
            c.count     = t.count;
            c.speed     = t.speed;
            c.wrap      = t.wrap;
            c.curve     = t.curve;
            c.easing    = t.easing;
            return c;
        }
        #endregion

        /// <summary>
        /// From property
        /// </summary>
        public T from { get { return m_from.GetValueOrDefault(default(T)); } set { m_from = value; } }
        T? m_from;

        /// <summary>
        /// From property
        /// </summary>
        public T to { get { return m_to.GetValueOrDefault(default(T)); } set { m_to = value; } }
        T? m_to;

        /// <summary>
        /// Delay before starting
        /// </summary>
        public float delay { get { return m_delay ?? 0f; } set { m_delay = value; } }
        float? m_delay;

        /// <summary>
        /// Duration of the timer
        /// </summary>
        public float duration { get { return m_duration ?? Tween.DefaultDuration; } set { m_duration = value; } }
        float? m_duration;

        /// <summary>
        /// Number of steps
        /// </summary>
        public int count { get { return m_count ?? 1; } set { m_count = value; } }
        int? m_count;

        /// <summary>
        /// Speed of time increments
        /// </summary>
        public float speed { get { return m_speed ?? 1f; } set { m_speed = value; } }
        float? m_speed;

        /// <summary>
        /// Speed of time increments
        /// </summary>
        public AnimationWrapMode wrap { get { return m_wrap ?? AnimationWrapMode.Clamp; } set { m_wrap = value; } }
        AnimationWrapMode? m_wrap;

        /// <summary>
        /// Reference to the easing curve
        /// </summary>
        public AnimationCurve curve;

        /// <summary>
        /// Easing method
        /// </summary>
        public Func<float,float> easing;

        /// <summary>
        /// Helper to populate a target instance
        /// </summary>        
        internal void To(Tween<T> t) {
            if(m_from     != null) { t.from     = from; t.tween.deferredFromValue = false; }
            if(m_to       != null) { t.to       = to;       }
            if(m_delay    != null) { t.delay    = delay;    }
            if(m_duration != null) { t.duration = duration; }
            if(m_count    != null) { t.count    = count;    }
            if(m_speed    != null) { t.speed    = speed;    }
            if(m_wrap     != null) { t.wrap     = wrap;     }
            if(easing     != null) { t.easing   = easing;   } else
            if(curve      != null) { t.curve    = curve ;   }
        }

    }
    #endregion

    /// <summary>
    /// Extension of the tween class allowing specifying the type of the property's value ranges.
    /// </summary>
    /// <typeparam name="T">Type of the target's property.</typeparam>
    public class Tween<T> : Tween where T : struct {
    
        #region Get/Set

        /// <summary>
        /// Returns the interpolator cast to the desired data type manipulation. If a not matching type is used, null is returned.
        /// </summary>
        /// <typeparam name="T">Type of the value interpolated.</typeparam>
        /// <returns>The tween's interpolator cast to the desired type.</returns>
        new public PropertyInterpolator<T> tween { get { return (PropertyInterpolator<T>)base.tween; } set { base.tween = value; } }

        /// <summary>
        /// Target of the tween.
        /// </summary>
        public object target { get { return tween==null ? null : tween.target; } }

        /// <summary>
        /// Property to be animated.
        /// </summary>
        public string property { get { return tween==null ? "" : tween.property; } }

        /// <summary>
        /// Get/Set the starting value of the animation.
        /// </summary>
        public T? from { get { return tween == null ? default : tween.interpolator.from; } set { if (tween != null) { tween.deferredFromValue = value == null; if (!tween.deferredFromValue) tween.interpolator.from = value.GetValueOrDefault(default(T)); } } }

        /// <summary>
        /// Get/Set the final value of the animation.
        /// </summary>
        public T to   { get { return tween==null ? default : tween.interpolator.to;   } set { if(tween!=null) tween.interpolator.to = value; } }

        /// <summary>
        /// Reference to the easing curve
        /// </summary>
        public AnimationCurve curve {  get { return tween==null ? null : tween.interpolator.curve; } set { if (tween != null) tween.interpolator.curve = value; } }

        /// <summary>
        /// Easing method
        /// </summary>
        public Func<float,float> easing { get { return tween == null ? null : tween.interpolator.easing; } set { if (tween != null) tween.interpolator.easing = value; } }

        #endregion

        #region CTOR
        /// <summary>
        /// Creates a new tween with 'id' and associated with a target object and its property
        /// </summary>
        /// <param name="p_id">Tween Id</param>
        /// <param name="p_target">Target Object</param>
        /// <param name="p_property">Object Property</param>
        public Tween(string p_id,object p_target,string p_property) : base(p_id) {
            //Create the interpolator needed for the target object and property
            tween = new UnityExt.Core.PropertyInterpolator<T>(p_target,p_property,default(T),default(T),Tween.Linear);
        }

        /// <summary>
        /// Creates a new tween associated with a target object and its property
        /// </summary>        
        /// <param name="p_target">Target Object</param>
        /// <param name="p_property">Object Property</param>
        public Tween(object p_target,string p_property) : this("",p_target,p_property) { }
        /*
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
        public Tween(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,AnimationWrapMode p_wrap,Func<float,float> p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,p_wrap         ); }
        public Tween(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,AnimationWrapMode p_wrap,AnimationCurve    p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,p_wrap         ); }        
        public Tween(string p_id,object p_target,string p_property,         T p_to,float p_duration,AnimationWrapMode p_wrap,Func<float,float> p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,p_wrap         ); }        
        public Tween(string p_id,object p_target,string p_property,         T p_to,float p_duration,AnimationWrapMode p_wrap,AnimationCurve    p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,p_wrap         ); }
        public Tween(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,                 Func<float,float> p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,AnimationWrapMode.Clamp); }
        public Tween(string p_id,object p_target,string p_property,T p_from,T p_to,float p_duration,                 AnimationCurve    p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,AnimationWrapMode.Clamp); }
        public Tween(string p_id,object p_target,string p_property,T p_from,T p_to,                                  Func<float,float> p_easing=null) : base(p_id,DefaultDuration,1)  { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,AnimationWrapMode.Clamp); }        
        public Tween(string p_id,object p_target,string p_property,T p_from,T p_to,                                  AnimationCurve    p_easing=null) : base(p_id,DefaultDuration,1)  { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,AnimationWrapMode.Clamp); }        
        public Tween(string p_id,object p_target,string p_property,         T p_to,float p_duration,                 Func<float,float> p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,AnimationWrapMode.Clamp); }                
        public Tween(string p_id,object p_target,string p_property,         T p_to,                                  Func<float,float> p_easing=null) : base(p_id,DefaultDuration,1)  { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,AnimationWrapMode.Clamp); }
        public Tween(string p_id,object p_target,string p_property,         T p_to,float p_duration,                 AnimationCurve    p_easing=null) : base(p_id,p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,AnimationWrapMode.Clamp); }        
        public Tween(string p_id,object p_target,string p_property,         T p_to,                                  AnimationCurve    p_easing=null) : base(p_id,DefaultDuration,1)  { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,AnimationWrapMode.Clamp); }

        public Tween(            object p_target,string p_property,T p_from,T p_to,float p_duration,AnimationWrapMode p_wrap,Func<float,float> p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,p_wrap         ); }
        public Tween(            object p_target,string p_property,T p_from,T p_to,float p_duration,AnimationWrapMode p_wrap,AnimationCurve    p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,p_wrap         ); }        
        public Tween(            object p_target,string p_property,         T p_to,float p_duration,AnimationWrapMode p_wrap,Func<float,float> p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,p_wrap         ); }        
        public Tween(            object p_target,string p_property,         T p_to,float p_duration,AnimationWrapMode p_wrap,AnimationCurve    p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,p_wrap         ); }
        public Tween(            object p_target,string p_property,T p_from,T p_to,float p_duration,                 Func<float,float> p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,AnimationWrapMode.Clamp); }
        public Tween(            object p_target,string p_property,T p_from,T p_to,float p_duration,                 AnimationCurve    p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,AnimationWrapMode.Clamp); }
        public Tween(            object p_target,string p_property,T p_from,T p_to,                                  Func<float,float> p_easing=null) : base("",DefaultDuration,1)  { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,AnimationWrapMode.Clamp); }        
        public Tween(            object p_target,string p_property,T p_from,T p_to,                                  AnimationCurve    p_easing=null) : base("",DefaultDuration,1)  { CreateTween(p_target,p_property,p_from,p_to,true, p_easing,AnimationWrapMode.Clamp); }        
        public Tween(            object p_target,string p_property,         T p_to,float p_duration,                 Func<float,float> p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,AnimationWrapMode.Clamp); }                
        public Tween(            object p_target,string p_property,         T p_to,                                  Func<float,float> p_easing=null) : base("",DefaultDuration,1)  { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,AnimationWrapMode.Clamp); }
        public Tween(            object p_target,string p_property,         T p_to,float p_duration,                 AnimationCurve    p_easing=null) : base("",p_duration,1)       { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,AnimationWrapMode.Clamp); }        
        public Tween(            object p_target,string p_property,         T p_to,                                  AnimationCurve    p_easing=null) : base("",DefaultDuration,1)  { CreateTween(p_target,p_property,p_to,  p_to,false,p_easing,AnimationWrapMode.Clamp); }
        
        /// <summary>
        /// Helper
        /// </summary>        
        internal Tween(string p_id,float p_duration,int p_count) : base(p_id,p_duration,p_count) { }
        //*/        
        #endregion

        #region Operation

        /// <summary>
        /// Sets this tween parameters
        /// </summary>
        /// <param name="p_args"></param>
        public void Set(TweenStart<T> p_args) { p_args.To(this); }

        /// <summary>
        /// Starts the tween with the passed arguments
        /// </summary>
        /// <returns></returns>
        public Tween<T> Start(ProcessContext p_context,TweenStart<T> p_args) { Set(p_args); return (Tween<T>)base.Start(p_context); }

        /// <summary>
        /// Starts the tween with the passed arguments
        /// </summary>
        /// <returns></returns>
        new public Tween<T> Start(ProcessContext p_context) { return (Tween<T>)Start(p_context,TweenStart<T>.From(this)); }

        /// <summary>
        /// Starts the tween with the passed arguments
        /// </summary>
        /// <returns></returns>
        public Tween<T> Start(TweenStart<T> p_args) { return Start(DefaultContext,p_args); }

        /// <summary>
        /// Starts the tween with the passed arguments
        /// </summary>
        /// <returns></returns>
        new public Tween<T> Start() { 
            return Start(DefaultContext,TweenStart<T>.From(this)); 
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Starts the tween with the passed arguments
        /// </summary>
        /// <returns></returns>
        public Tween<T> StartEditor(TweenStart<T> p_args) { return Start(ProcessContext.Editor,p_args); }

        /// <summary>
        /// Starts the tween with the passed arguments
        /// </summary>
        /// <returns></returns>
        new public Tween<T> StartEditor() { return Start(TweenStart<T>.From(this)); }
        #endif

        /// <summary>
        /// Called when tween is ready to start
        /// </summary>
        /// <returns></returns>
        protected override bool AssertTweenStart() {
            //Upon starting clear any ongoing tween besides this one
            //Allow for delayed tweens to overwrite running ones
            /*
            List<Tween> l = Tween.FindAll("",target,property,ActivityContext.All);                
            for(int i=0;i<l.Count;i++) if(l[i]!=this) if(l[i].state == ActivityState.Running) l[i].Stop();
            //*/
            return true;
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
            if(p_continue)if(tween!=null) { tween.deferredFromValue=true; tween.m_from_fetched=false; }
            time = speed < 0f ? duration : 0f;
            step = speed < 0f ? count : 0;
            Stop();
            Start();
        }

        #endregion
    }
    #endregion

}
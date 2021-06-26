using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityExt.Core.Motion {

    #region interface IInterpolator

    /// <summary>
    /// Basic interface for classes able to interpolate data upon a provided [0.0;1.0] ratio.
    /// </summary>
    public interface IInterpolator {
        /// <summary>
        /// Interpolation Method
        /// </summary>
        /// <param name="p_ratio">Ratio of interpolation in the [0.0;1.0] range.</param>
        void Lerp(float p_ratio);
    }

    #endregion

    #region ValueType Extensions

    public class SByteInterpolator  : ValueTypeInterpolator<sbyte > { public SByteInterpolator () : base() { } override protected void OnLerp(float p_ratio) { result = (sbyte )LerpAux((float )from,(float )to,p_ratio); } }
    public class ByteInterpolator   : ValueTypeInterpolator<byte  > { public ByteInterpolator  () : base() { } override protected void OnLerp(float p_ratio) { result = (byte  )LerpAux((float )from,(float )to,p_ratio); } }    
    public class UShortInterpolator : ValueTypeInterpolator<ushort> { public UShortInterpolator() : base() { } override protected void OnLerp(float p_ratio) { result = (ushort)LerpAux((float )from,(float )to,p_ratio); } }
    public class ShortInterpolator  : ValueTypeInterpolator<short > { public ShortInterpolator () : base() { } override protected void OnLerp(float p_ratio) { result = (short )LerpAux((float )from,(float )to,p_ratio); } }    
    public class UIntInterpolator   : ValueTypeInterpolator<uint  > { public UIntInterpolator  () : base() { } override protected void OnLerp(float p_ratio) { result = (uint  )LerpAux((double)from,(double)to,p_ratio); } }    
    public class IntInterpolator    : ValueTypeInterpolator<int   > { public IntInterpolator   () : base() { } override protected void OnLerp(float p_ratio) { result = (int   )LerpAux((double)from,(double)to,p_ratio); } }    
    public class ULongInterpolator  : ValueTypeInterpolator<ulong > { public ULongInterpolator () : base() { } override protected void OnLerp(float p_ratio) { result = (ulong )LerpAux((double)from,(double)to,p_ratio); } }    
    public class LongInterpolator   : ValueTypeInterpolator<long  > { public LongInterpolator  () : base() { } override protected void OnLerp(float p_ratio) { result = (long  )LerpAux((double)from,(double)to,p_ratio); } }    
    public class FloatInterpolator  : ValueTypeInterpolator<float > { public FloatInterpolator () : base() { } override protected void OnLerp(float p_ratio) { result = (float )LerpAux(        from,        to,p_ratio); } }
    public class DoubleInterpolator : ValueTypeInterpolator<double> { public DoubleInterpolator() : base() { } override protected void OnLerp(float p_ratio) { result = (double)LerpAux(        from,        to,p_ratio); } }

    public class Vector2Interpolator    : ValueTypeInterpolator<UnityEngine.Vector2   > { public Vector2Interpolator   () : base() { } override protected void OnLerp(float p_ratio) { result = LerpAux(        from,        to,p_ratio); } }
    public class Vector3Interpolator    : ValueTypeInterpolator<UnityEngine.Vector3   > { public Vector3Interpolator   () : base() { } override protected void OnLerp(float p_ratio) { result = LerpAux(        from,        to,p_ratio); } }
    public class Vector4Interpolator    : ValueTypeInterpolator<UnityEngine.Vector4   > { public Vector4Interpolator   () : base() { } override protected void OnLerp(float p_ratio) { result = LerpAux(        from,        to,p_ratio); } }
    public class ColorInterpolator      : ValueTypeInterpolator<UnityEngine.Color     > { public ColorInterpolator     () : base() { } override protected void OnLerp(float p_ratio) { result = LerpAux(        from,        to,p_ratio); } }
    public class RectInterpolator       : ValueTypeInterpolator<UnityEngine.Rect      > { public RectInterpolator      () : base() { } override protected void OnLerp(float p_ratio) { result = LerpAux(        from,        to,p_ratio); } }
    public class Vector2IntInterpolator : ValueTypeInterpolator<UnityEngine.Vector2Int> { public Vector2IntInterpolator() : base() { } override protected void OnLerp(float p_ratio) { result = LerpAux(        from,        to,p_ratio); } }
    public class Vector3IntInterpolator : ValueTypeInterpolator<UnityEngine.Vector3Int> { public Vector3IntInterpolator() : base() { } override protected void OnLerp(float p_ratio) { result = LerpAux(        from,        to,p_ratio); } }
    public class QuaternionInterpolator : ValueTypeInterpolator<UnityEngine.Quaternion> { public QuaternionInterpolator() : base() { } override protected void OnLerp(float p_ratio) { result = LerpAux(        from,        to,p_ratio); } }
    
    #endregion

    #region abstract class ValueTypeInterpolator<T>

    /// <summary>
    /// Base class for all numeric interpolators.
    /// </summary>
    /// <typeparam name="T">ValueType to be interpolated.</typeparam>
    public abstract class ValueTypeInterpolator<T> : IInterpolator {

        #region static

        /// <summary>
        /// Creates a new interpolator appropriate for the given 'T' type.
        /// </summary>
        /// <returns>Interpolator instance.</returns>
        static public ValueTypeInterpolator<T> Create() {
            //Try ordering by probable usage frequency
            object res = null;            
            if(typeof(T) == typeof(float  ))  res = new FloatInterpolator (); else
            if(typeof(T) == typeof(int    ))  res = new IntInterpolator   (); else
            if(typeof(T) == typeof(UnityEngine.Vector2   ))  res = new Vector2Interpolator    ();   else
            if(typeof(T) == typeof(UnityEngine.Vector3   ))  res = new Vector3Interpolator    ();   else
            if(typeof(T) == typeof(UnityEngine.Quaternion))  res = new QuaternionInterpolator ();   else            
            if(typeof(T) == typeof(UnityEngine.Vector4   ))  res = new Vector4Interpolator    ();   else
            if(typeof(T) == typeof(UnityEngine.Color     ))  res = new ColorInterpolator      ();   else
            if(typeof(T) == typeof(UnityEngine.Rect      ))  res = new RectInterpolator       ();   else
            if(typeof(T) == typeof(UnityEngine.Vector2Int))  res = new Vector2IntInterpolator ();   else
            if(typeof(T) == typeof(UnityEngine.Vector3Int))  res = new Vector3IntInterpolator ();   else
            if(typeof(T) == typeof(double ))  res = new DoubleInterpolator(); else
            if(typeof(T) == typeof(long   ))  res = new LongInterpolator  (); else
            if(typeof(T) == typeof(uint   ))  res = new UIntInterpolator  (); else
            if(typeof(T) == typeof(ulong  ))  res = new ULongInterpolator (); else
            if(typeof(T) == typeof(byte   ))  res = new ByteInterpolator  (); else
            if(typeof(T) == typeof(sbyte  ))  res = new SByteInterpolator (); else        
            if(typeof(T) == typeof(ushort ))  res = new UShortInterpolator(); else
            if(typeof(T) == typeof(short  ))  res = new ShortInterpolator ();                
            return res==null ? null : (ValueTypeInterpolator<T>)res;
        }

        #endregion

        /// <summary>
        /// Starting Value
        /// </summary>
        public T from;
        
        /// <summary>
        /// End Value
        /// </summary>
        public T to; 

        /// <summary>
        /// Resulting Interpolation after Lerp
        /// </summary>
        public T result;

        /// <summary>
        /// Easing Method to apply into the ratio
        /// </summary>
        public Func<float,float> easing;

        /// <summary>
        /// Easing Curve to apply into the ratio
        /// </summary>
        public UnityEngine.AnimationCurve curve;

        /// <summary>
        /// CTOR.
        /// </summary>
        public ValueTypeInterpolator() { Set(default(T),default(T),null,null); }

        #region Set

        /// <summary>
        /// Sets this interpolator properties.
        /// </summary>
        /// <param name="p_from"></param>
        /// <param name="p_to"></param>
        /// <param name="p_easing"></param>
        public void Set(T p_from,T p_to,Func<float,float> p_easing) { Set(p_from,p_to,p_easing==null ? LinearEasing : p_easing,null); }

        /// <summary>
        /// Sets this interpolator properties.
        /// </summary>
        /// <param name="p_from"></param>
        /// <param name="p_easing"></param>
        public void Set(T p_from,Func<float,float> p_easing) { Set(p_from,default(T),p_easing==null ? LinearEasing : p_easing,null); }

        /// <summary>
        /// Sets this interpolator properties.
        /// </summary>
        /// <param name="p_from"></param>
        /// <param name="p_to"></param>
        /// <param name="p_easing"></param>
        public void Set(T p_from,T p_to,UnityEngine.AnimationCurve p_curve) { Set(p_from,p_to,null,p_curve); }

        /// <summary>
        /// Sets this interpolator properties.
        /// </summary>
        /// <param name="p_from"></param>
        /// <param name="p_easing"></param>
        public void Set(T p_from,UnityEngine.AnimationCurve p_curve) { Set(p_from,default(T),null,p_curve); }

        /// <summary>
        /// Sets this interpolator properties.
        /// </summary>
        /// <param name="p_from"></param>
        /// <param name="p_to"></param>        
        public void Set(T p_from,T p_to) { Set(p_from,p_to,(Func<float,float>)null); }

        /// <summary>
        /// Sets this interpolator properties.
        /// </summary>
        /// <param name="p_from"></param>        
        public void Set(T p_from) { Set(p_from,default(T),(Func<float,float>)null); }

        /// <summary>
        /// Internals
        /// </summary>
        /// <param name="p_from"></param>
        /// <param name="p_to"></param>
        /// <param name="p_easing"></param>
        /// <param name="p_curve"></param>
        private void Set(T p_from,T p_to,Func<float,float> p_easing,UnityEngine.AnimationCurve p_curve) {
            from   = p_from;
            to     = p_to;
            easing = p_easing;
            curve  = p_curve;
        }

        #endregion

        /// <summary>
        /// Interpolation Method
        /// </summary>
        /// <param name="p_ratio">Ratio of interpolation</param>
        public void Lerp(float p_ratio) { 
            float r = p_ratio;
            if(easing!=null) r = easing(r); else
            if(curve !=null) r = curve.Evaluate(r);
            OnLerp(r);
        }

        /// <summary>
        /// Virtual to override and implement the specific interpolation.
        /// </summary>
        /// <param name="p_ratio"></param>
        virtual protected void OnLerp(float p_ratio) { }

        #region internals

        /// <summary>
        /// Basic Easing mode.
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        static float LinearEasing(float r) { return r; }

        /// <summary>
        /// Auxiliary method to lerp the primitives
        /// </summary>        
        protected double LerpAux(double p_from,double p_to,double p_ratio) { return p_from + ((p_to-p_from) * p_ratio); }
        protected float  LerpAux(float  p_from,float  p_to,float  p_ratio) { return p_from + ((p_to-p_from) * p_ratio); }

        protected UnityEngine.Vector2    LerpAux(UnityEngine.Vector2    p_from,UnityEngine.Vector2    p_to,float p_ratio) { return UnityEngine.Vector2.LerpUnclamped    (p_from,p_to,p_ratio); }        
        protected UnityEngine.Vector3    LerpAux(UnityEngine.Vector3    p_from,UnityEngine.Vector3    p_to,float p_ratio) { return UnityEngine.Vector3.LerpUnclamped    (p_from,p_to,p_ratio); }
        protected UnityEngine.Vector4    LerpAux(UnityEngine.Vector4    p_from,UnityEngine.Vector4    p_to,float p_ratio) { return UnityEngine.Vector4.LerpUnclamped    (p_from,p_to,p_ratio); }
        protected UnityEngine.Color      LerpAux(UnityEngine.Color      p_from,UnityEngine.Color      p_to,float p_ratio) { return UnityEngine.Color.LerpUnclamped      (p_from,p_to,p_ratio); }
        protected UnityEngine.Quaternion LerpAux(UnityEngine.Quaternion p_from,UnityEngine.Quaternion p_to,float p_ratio) { return UnityEngine.Quaternion.SlerpUnclamped(p_from,p_to,p_ratio); }
        protected UnityEngine.Vector2Int LerpAux(UnityEngine.Vector2Int p_from,UnityEngine.Vector2Int p_to,float p_ratio) { UnityEngine.Vector2 v0 = p_from; UnityEngine.Vector2 v1 = p_to; v0 = UnityEngine.Vector2.LerpUnclamped(v0,v1,p_ratio); return new UnityEngine.Vector2Int((int)v0.x,(int)v0.y          ); }
        protected UnityEngine.Vector3Int LerpAux(UnityEngine.Vector3Int p_from,UnityEngine.Vector3Int p_to,float p_ratio) { UnityEngine.Vector3 v0 = p_from; UnityEngine.Vector3 v1 = p_to; v0 = UnityEngine.Vector3.LerpUnclamped(v0,v1,p_ratio); return new UnityEngine.Vector3Int((int)v0.x,(int)v0.y,(int)v0.z); }        
        protected UnityEngine.Rect       LerpAux(UnityEngine.Rect       p_from,UnityEngine.Rect       p_to,float p_ratio) { UnityEngine.Vector4 v0 = new UnityEngine.Vector4(p_from.x,p_from.y,p_from.width,p_from.height); UnityEngine.Vector4 v1 = new UnityEngine.Vector4(p_to.x,p_to.y,p_to.width,p_to.height); v0 = UnityEngine.Vector4.LerpUnclamped(v0,v1,p_ratio); return new UnityEngine.Rect(v0.x,v0.y,v0.z,v0.w); }

        #endregion

    }

    #endregion

    #region class PropertyInterpolator<T>

    /// <summary>
    /// Base Class that contains the basic information of a property interpolator.
    /// </summary>
    public abstract class PropertyInterpolator : IInterpolator {

        /// <summary>
        /// Target object to interpolate its property.
        /// </summary>
        public object target { get; protected set; }

        /// <summary>
        /// Target object property.
        /// </summary>
        public string property { get; protected set; }

        /// <summary>
        /// Interpolates the property
        /// </summary>
        /// <param name="p_ratio"></param>
        virtual public void Lerp(float p_ratio) { }

    }

    /// <summary>
    /// Class that wraps fetching an object's property and calling the proper reflection methods to interpolate them in the most optimized way possible.
    /// </summary>
    public class PropertyInterpolator<T> : PropertyInterpolator {

        /// <summary>
        /// Flag that tells to collect 'from' upon lerp start
        /// </summary>
        public bool deferredFromValue { get; internal set; }

        /// <summary>
        /// Reference to the chosen interpolator.
        /// </summary>
        public ValueTypeInterpolator<T> interpolator { get; private set; }

        /// <summary>
        /// Reference to the chosen property.
        /// </summary>
        public PropertyReflection<T> accessor { get; private set; }

        /// <summary>
        /// Flag that tell this interpolator was properly initialized.
        /// </summary>
        public bool valid { get; private set; }

        #region CTOR.

        /// <summary>
        /// Creates a new property interpolator that uses reflection to interpolate and modifies dynamic properties.
        /// </summary>
        /// <param name="p_target">Target object.</param>
        /// <param name="p_property">Property to interpolate.</param>
        /// <param name="p_from">Start value</param>
        /// <param name="p_to">End value</param>
        /// <param name="p_easing">Interpolation easing.</param>
        public PropertyInterpolator(object p_target,string p_property,T p_from,T p_to,Func<float,float> p_easing)         : this(p_target,p_property,p_from,p_to,false) { if(valid) interpolator.Set(p_from,p_to,p_easing);   }
        
        /// <summary>
        /// Creates a new property interpolator that uses reflection to interpolate and modifies dynamic properties.
        /// </summary>
        /// <param name="p_target">Target object.</param>
        /// <param name="p_property">Property to interpolate.</param>
        /// <param name="p_from">Start value</param>
        /// <param name="p_to">End value</param>
        /// <param name="p_curve">AnimationCurve easing.</param>
        public PropertyInterpolator(object p_target,string p_property,T p_from,T p_to,UnityEngine.AnimationCurve p_curve) : this(p_target,p_property,p_from,p_to,false) { if(valid) interpolator.Set(p_from,p_to,p_curve );   }
        
        /// <summary>
        /// Creates a new property interpolator that uses reflection to interpolate and modifies dynamic properties. The starting value is only sampled at interpolation start
        /// </summary>
        /// <param name="p_target">Target object.</param>
        /// <param name="p_property">Property to interpolate.</param>        
        /// <param name="p_to">End value</param>
        /// <param name="p_easing">Interpolation easing.</param>
        public PropertyInterpolator(object p_target,string p_property,         T p_to,Func<float,float> p_easing)         : this(p_target,p_property,p_to  ,p_to,true)  { if(valid) interpolator.Set(p_to  ,p_to,p_easing);   }

        /// <summary>
        /// Creates a new property interpolator that uses reflection to interpolate and modifies dynamic properties. The starting value is only sampled at interpolation start
        /// </summary>
        /// <param name="p_target">Target object.</param>
        /// <param name="p_property">Property to interpolate.</param>        
        /// <param name="p_to">End value</param>
        /// <param name="p_curve">AnimationCurve easing.</param>
        public PropertyInterpolator(object p_target,string p_property,         T p_to,UnityEngine.AnimationCurve p_curve) : this(p_target,p_property,p_to  ,p_to,true)  { if(valid) interpolator.Set(p_to  ,p_to,p_curve );   }

        #region Internal CTOR

        /// <summary>
        /// Internal CTOR.
        /// </summary>        
        private PropertyInterpolator(object p_target,string p_property,T p_from,T p_to,bool p_deferred) {
            //Init as invalid
            valid = false;
            //If no target
            if(p_target==null) return;
            //Assign
            target = p_target;
            if(string.IsNullOrEmpty(p_property)) return;
            property = p_property;
            //From is set so no need to sample it later
            deferredFromValue  = p_deferred;
            //Create the interpolator
            interpolator       = ValueTypeInterpolator<T>.Create();
            //Create the accessor
            accessor           = PropertyReflection<T>.Create(target,property);
            //If 'null' invalidate
            if(interpolator==null) return;
            if(accessor==null)     return;
            //Apply 'to' value            
            interpolator.from = p_from;
            interpolator.to   = p_to;
            //All valid
            valid=true;
            m_from_fetched = false;
        }

        #endregion

        #endregion

        /// <summary>
        /// Fetches the current property value and apply to the interpolator 'from'
        /// </summary>
        virtual protected T GetPropertyValue() {
            if(!valid) return default(T);
            return accessor.Get();
        }

        /// <summary>
        /// Updates the interpolator result into the object.
        /// </summary>
        virtual protected void SetPropertyValue(T p_value) {
            if(!valid) return;
            accessor.Set(p_value);
        }

        /// <summary>
        /// Interpolates the property
        /// </summary>
        /// <param name="p_ratio"></param>
        override public void Lerp(float p_ratio) {
            if(!valid) return;
            if(deferredFromValue) if(!m_from_fetched) { interpolator.from = GetPropertyValue(); m_from_fetched = true; }            
            interpolator.Lerp(p_ratio);
            SetPropertyValue(interpolator.result);
        }
        internal bool m_from_fetched;

    }

    #endregion

}

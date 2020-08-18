using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityExt.Core {

    #region class Interpolator

    /// <summary>
    /// Class that implements the basics of interpolating object properties.
    /// </summary>
    public class Interpolator {

        #region delegates    

        #region Getter/Setter Templates

        /// <summary>
        /// Shortcut delegate definitions to speed up reflection.
        /// </summary>        
        internal delegate void       SByteSetter     (sbyte      v);
        internal delegate void       ByteSetter      (byte       v);
        internal delegate void       UShortSetter    (ushort     v);
        internal delegate void       ShortSetter     (short      v);
        internal delegate void       UIntSetter      (uint       v);
        internal delegate void       IntSetter       (int        v);
        internal delegate void       ULongSetter     (ulong      v);
        internal delegate void       LongSetter      (long       v);
        internal delegate void       FloatSetter     (float      v);
        internal delegate void       DoubleSetter    (double     v);
        internal delegate void       Vector2Setter   (Vector2    v);
        internal delegate void       Vector2IntSetter(Vector2Int v);
        internal delegate void       Vector3Setter   (Vector3    v);
        internal delegate void       Vector3IntSetter(Vector3Int v);
        internal delegate void       Vector4Setter   (Vector4    v);
        internal delegate void       RectSetter      (Rect       v);
        internal delegate void       QuaternionSetter(Quaternion v);
        internal delegate void       ColorSetter     (Color      v);
        internal delegate void       Color32Setter   (Color32    v);
        internal delegate void       RangeIntSetter  (RangeInt   v);
        internal delegate void       RaySetter       (Ray        v);
        internal delegate void       Ray2DSetter     (Ray2D      v);

        internal delegate sbyte      SByteGetter     ();
        internal delegate byte       ByteGetter      ();
        internal delegate ushort     UShortGetter    ();
        internal delegate short      ShortGetter     ();
        internal delegate uint       UIntGetter      ();
        internal delegate int        IntGetter       ();
        internal delegate ulong      ULongGetter     ();
        internal delegate long       LongGetter      ();
        internal delegate float      FloatGetter     ();
        internal delegate double     DoubleGetter    ();
        internal delegate Vector2    Vector2Getter   ();
        internal delegate Vector2Int Vector2IntGetter();
        internal delegate Vector3    Vector3Getter   ();
        internal delegate Vector3Int Vector3IntGetter();
        internal delegate Vector4    Vector4Getter   ();
        internal delegate Rect       RectGetter      ();
        internal delegate Quaternion QuaternionGetter();
        internal delegate Color      ColorGetter     ();
        internal delegate Color32    Color32Getter   ();
        internal delegate RangeInt   RangeIntGetter  ();
        internal delegate Ray        RayGetter       ();
        internal delegate Ray2D      Ray2DGetter     ();

        #endregion

        #region Dictionary<Type,Type> m_type_getter_delegate_lut

        /// <summary>
        /// LUT Table to match value types to their get/set methods
        /// </summary>
        static internal Dictionary<Type,Type> m_type_getter_delegate_lut = new Dictionary<Type, Type>() {
            { typeof(sbyte     ),typeof(SByteGetter     ) },
            { typeof(byte      ),typeof(ByteGetter      ) },
            { typeof(ushort    ),typeof(UShortGetter    ) },
            { typeof(short     ),typeof(ShortGetter     ) },
            { typeof(uint      ),typeof(UIntGetter      ) },
            { typeof(int       ),typeof(IntGetter       ) },
            { typeof(ulong     ),typeof(ULongGetter     ) },
            { typeof(long      ),typeof(LongGetter      ) },
            { typeof(float     ),typeof(FloatGetter     ) },
            { typeof(double    ),typeof(DoubleGetter    ) },
            { typeof(Vector2   ),typeof(Vector2Getter   ) },
            { typeof(Vector2Int),typeof(Vector2IntGetter) },
            { typeof(Vector3   ),typeof(Vector3Getter   ) },
            { typeof(Vector3Int),typeof(Vector3IntGetter) },
            { typeof(Vector4   ),typeof(Vector4Getter   ) },
            { typeof(Rect      ),typeof(RectGetter      ) },
            { typeof(Quaternion),typeof(QuaternionGetter) },
            { typeof(Color     ),typeof(ColorGetter     ) },
            { typeof(Color32   ),typeof(Color32Getter   ) },
            { typeof(RangeInt  ),typeof(RangeIntGetter  ) },
            { typeof(Ray       ),typeof(RayGetter       ) },
            { typeof(Ray2D     ),typeof(Ray2DGetter     ) }
        };

        #endregion

        #region Dictionary<Type,Type> m_type_setter_delegate_lut

        /// <summary>
        /// LUT Table to match value types to their get/set methods
        /// </summary>
        static internal Dictionary<Type,Type> m_type_setter_delegate_lut = new Dictionary<Type, Type>() {
            { typeof(sbyte     ),typeof(SByteSetter     ) },
            { typeof(byte      ),typeof(ByteSetter      ) },
            { typeof(ushort    ),typeof(UShortSetter    ) },
            { typeof(short     ),typeof(ShortSetter     ) },
            { typeof(uint      ),typeof(UIntSetter      ) },
            { typeof(int       ),typeof(IntSetter       ) },
            { typeof(ulong     ),typeof(ULongSetter     ) },
            { typeof(long      ),typeof(LongSetter      ) },
            { typeof(float     ),typeof(FloatSetter     ) },
            { typeof(double    ),typeof(DoubleSetter    ) },
            { typeof(Vector2   ),typeof(Vector2Setter   ) },
            { typeof(Vector2Int),typeof(Vector2IntSetter) },
            { typeof(Vector3   ),typeof(Vector3Setter   ) },
            { typeof(Vector3Int),typeof(Vector3IntSetter) },
            { typeof(Vector4   ),typeof(Vector4Setter   ) },
            { typeof(Rect      ),typeof(RectSetter      ) },
            { typeof(Quaternion),typeof(QuaternionSetter) },
            { typeof(Color     ),typeof(ColorSetter     ) },
            { typeof(Color32   ),typeof(Color32Setter   ) },
            { typeof(RangeInt  ),typeof(RangeIntSetter  ) },
            { typeof(Ray       ),typeof(RaySetter       ) },
            { typeof(Ray2D     ),typeof(Ray2DSetter     ) }
        };

        #endregion

        #endregion

        #region static

        /// <summary>
        /// Default animation curve to be used in interpolators missing an easing.
        /// </summary>
        static public AnimationCurve DefaultAnimationCurve { get { return m_default_animation==null ? (m_default_animation = AnimationCurve.Linear(0f,0f,1f,1f)) : m_default_animation; } set { m_default_animation = value; } }
        static private AnimationCurve m_default_animation = AnimationCurve.Linear(0f,0f,1f,1f);

        /// <summary>
        /// Handler called to expand the interpolator search and convertion.
        /// Considering not all data types are supported, expand the functionality by providing your own interpolator type associated with a given type.
        /// </summary>
        static public Func<Type,Type> GetInterpolatorCustom;

        /// <summary>
        /// Searches and return an interpolator instance based on the value type.
        /// </summary>
        /// <param name="p_value_type">Data type to be interpolated.</param>
        /// <returns>Reference to the interpolator or null.</returns>
        static public Interpolator Get(Type p_value_type) {            
            //Target value type
            Type vt = p_value_type;
            //Check if already found
            bool has_key = vt==null ? false : m_type_interpolator_lut_default.ContainsKey(vt);
            //Set the interpolator type if found
            Type it  = has_key ? m_type_interpolator_lut_default[vt] : null;
            //Search custom interpolators so they can override default ones
            Type itc = GetInterpolatorCustom==null ? null : GetInterpolatorCustom(p_value_type);
            //Override default interpolator if type was found
            it = itc==null ? it : itc;            
            //If invalid type return default interpolator
            if(it==null) {
                Debug.LogWarning($"Interpolator> Failed to find interpolator for type [{(vt==null ? "<null>" : vt.FullName)}]");
                return new Interpolator();
            }
            return (Interpolator)it.GetConstructor(m_interpolator_empty_ctor).Invoke(m_interpolator_empty_args);
        }
        static Type[]   m_interpolator_empty_ctor = new Type[0];
        static object[] m_interpolator_empty_args = new object[0];
        
        /// <summary>
        /// Searches for the available interpolator for a given type.
        /// </summary>
        /// <typeparam name="T">Data Type to be interpolated.</typeparam>        
        /// <returns>Reference to the interpolator or null.</returns>
        static public Interpolator<T> Get<T>() { return Get(typeof(T)) as Interpolator<T>; }

        #region Dictionary<Type,Type> m_type_interpolator_lut_default
        static Dictionary<Type,Type> m_type_interpolator_lut_default = new Dictionary<Type,Type>() { 
            {typeof(byte      ), typeof(ByteInterpolator        )},
            {typeof(sbyte     ), typeof(SByteInterpolator       )},           
            {typeof(ushort    ), typeof(UShortInterpolator      )},
            {typeof(short     ), typeof(ShortInterpolator       )},
            {typeof(uint      ), typeof(UIntInterpolator        )},
            {typeof(int       ), typeof(IntInterpolator         )},
            {typeof(ulong     ), typeof(ULongInterpolator       )},
            {typeof(long      ), typeof(LongInterpolator        )},
            {typeof(float     ), typeof(FloatInterpolator       )},
            {typeof(double    ), typeof(DoubleInterpolator      )},
            {typeof(Vector2   ), typeof(Vector2Interpolator     )},
            {typeof(Vector2Int), typeof(Vector2IntInterpolator  )},
            {typeof(Vector3   ), typeof(Vector3Interpolator     )},
            {typeof(Vector3Int), typeof(Vector3IntInterpolator  )},
            {typeof(Vector4   ), typeof(Vector4Interpolator     )},
            {typeof(Color     ), typeof(ColorInterpolator       )},
            {typeof(Color32   ), typeof(Color32Interpolator     )},
            {typeof(Rect      ), typeof(RectInterpolator        )},
            {typeof(RangeInt  ), typeof(RangeIntInterpolator    )},
            {typeof(Ray       ), typeof(RayInterpolator         )},
            {typeof(Ray2D     ), typeof(Ray2DInterpolator       )},
            {typeof(Quaternion), typeof(QuaternionInterpolator  )}            
        };
        #endregion

        #endregion

        /// <summary>
        /// Target object to interpolate a property.
        /// </summary>
        public object target { 
            get { return m_target; } 
            set { if(value!=m_target) AssertTarget(m_target,m_property); }
        }
        private object m_target;

        /// <summary>
        /// Property to interpolate, either variable or field.
        /// </summary>
        public string property { 
            get { return m_property; } 
            set { if(value!=m_property) AssertTarget(m_target,m_property); }
        }
        private string m_property;
        
        /// <summary>
        /// Reference to the easing function. This method will map a [0,1] ratio into a new [0,1] range to Lerp the property.
        /// </summary>
        public Func<float,float> easingFunction;

        /// <summary>
        /// Reference to the easing curve. This curve should expect a 'time' in the [0,1] range and return values in a close to [0,1] range.
        /// </summary>
        public AnimationCurve easingCurve;

        /// <summary>
        /// Flag that tells this interpolator will interpolate static data.
        /// </summary>
        public bool isStatic { get; internal set; }

        /// <summary>
        /// Check the target object is a Material
        /// </summary>
        public bool isMaterial { get; internal set; }

        /// <summary>
        /// Returns a flag telling if this interpolator is in a state able to execute.
        /// </summary>
        public bool isValid { 
            get {               
                //If not valid at all skip
                if(!m_is_valid) return false;
                //Cast to Object for null check
                UnityEngine.Object uo = target as UnityEngine.Object;
                return uo!=null;
            } 
            internal set { m_is_valid = value; } 
        }
        private bool m_is_valid;

        /// <summary>
        /// Returns a flag telling if this interpolator target is an unity object.
        /// </summary>
        public bool isUnityObject { get; internal set; }

        /// <summary>
        /// Internals.
        /// </summary>
        private MemberInfo m_target_property_accessor;
        private Delegate   m_target_property_getter;
        private Delegate   m_target_property_setter;
        private int        m_material_property_id;
        private bool       m_is_property { get { return isValid ? (isMaterial ? false : (m_target_property_accessor.MemberType == MemberTypes.Property)) : false; } }
        private bool       m_is_field    { get { return isValid ? (isMaterial ? false : (m_target_property_accessor.MemberType == MemberTypes.Field))    : false; } }
        
        #region CTOR.

        /// <summary>
        /// Creates a new interpolator.
        /// </summary>
        public Interpolator() { Create(null,"",null); }

        /// <summary>
        /// Creates the interpolator internal data.
        /// </summary>
        /// <param name="p_target"></param>
        /// <param name="p_property"></param>
        /// <param name="p_easing_function"></param>
        /// <param name="p_easing_curve"></param>
        internal void Create(object p_target,string p_property,object p_easing) {            
            easingFunction = null;
            easingCurve    = null;
            if(p_easing is Func<float,float>) easingFunction = (Func<float,float>)p_easing;
            if(p_easing is AnimationCurve)    easingCurve    = (AnimationCurve)   p_easing;                
            AssertTarget(p_target,p_property);
        }

        /// <summary>
        /// Asserts the interpolation core data of a given target/property.
        /// </summary>
        /// <param name="p_target"></param>
        /// <param name="p_property"></param>
        internal void AssertTarget(object p_target,string p_property) {
            m_target         = p_target;
            m_property       = p_property;
            isStatic         = m_target==null ? false : (target is Type);
            isMaterial       = m_target==null ? false : (target is Material);
            //Init property state data
            m_target_property_accessor   = null;
            m_target_property_getter     = null;
            m_target_property_setter     = null;
            m_material_property_id       = -1;
            //Clear valid
            isValid = false;
            //Skip null target
            if(m_target==null)                   { return; }
            //Skip empty property
            if(string.IsNullOrEmpty(m_property)) { return; }            
            //Set the flag if its an unity object to increase assertion.
            isUnityObject = target is UnityEngine.Object;
            //Capture reflection info.            
            //If 'target' is a Material it needs special steps.
            if(isMaterial) {
                Material mt = target as Material;
                //Assert if shader exists
                if(mt.shader==null) { Debug.LogWarning($"Interpolator> Material [{mt.name}] does not contain a shader."); return; }
                //Assert if property exists
                if(!mt.HasProperty(property)) { Debug.LogWarning($"Interpolator> Failed to find property [{property}] of Material [{mt.name},{mt.shader.name}]"); return; }                    
                //Property exists
                m_material_property_id = Shader.PropertyToID(property);
                //Set valid
                isValid = true;
                //Skip the System.Reflection search phase
                return;
            }                
            Type target_type = isStatic ? ((Type)target) : target.GetType();
            //Fetch field info
            MemberInfo[] mi = target_type.GetMember(property,BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.SetProperty);
            //Assert if property exists
            if(mi.Length<=0) { Debug.LogWarning($"Interpolator> Failed to find{(isStatic ? "static" : "")} property [{property}] of [{target_type.FullName}]"); return; }
            //Store property accessor
            m_target_property_accessor = mi[0];
            //If property generate specific delegates to speedup the property manipulation
            if(m_target_property_accessor.MemberType == MemberTypes.Property) {
                Type   delegate_type = null;
                object invoker       = isStatic ? null : target;
                PropertyInfo pi   = (PropertyInfo)m_target_property_accessor;
                Type         pi_t = pi.PropertyType;                                
                //if(pi.PropertyType == typeof(Vector2)) fni.CreateDelegate(typeof(Vector2Getter),invoker);
                delegate_type = m_type_getter_delegate_lut.ContainsKey(pi_t) ? m_type_getter_delegate_lut[pi_t] : null;
                m_target_property_getter = delegate_type==null ? null : pi.GetGetMethod().CreateDelegate(delegate_type,invoker);
                delegate_type = m_type_setter_delegate_lut.ContainsKey(pi_t) ? m_type_setter_delegate_lut[pi_t] : null;
                m_target_property_setter = delegate_type==null ? null : pi.GetSetMethod().CreateDelegate(delegate_type,invoker);
            }
            //Set valid
            isValid = true;
        }

        #endregion

        #region Get/Set Property

        #region Material Properties 

        internal int        GetMaterialInt()         {  Material mt = target as Material; return mt.GetInt   (m_material_property_id);    }
        internal float      GetMaterialFloat()       {  Material mt = target as Material; return mt.GetFloat (m_material_property_id);    }        
        internal Color      GetMaterialColor()       {  Material mt = target as Material; return mt.GetColor (m_material_property_id);    }
        internal Vector2    GetMaterialVector2()     {  Material mt = target as Material; Vector4 v = mt.GetVector(m_material_property_id); return new Vector2(v.x,v.y);     }
        internal Vector2Int GetMaterialVector2Int()  {  Material mt = target as Material; Vector4 v = mt.GetVector(m_material_property_id); return new Vector2Int((int)v.x,(int)v.y);     }
        internal Vector3    GetMaterialVector3()     {  Material mt = target as Material; Vector4 v = mt.GetVector(m_material_property_id); return new Vector3(v.x,v.y,v.z); }
        internal Vector3Int GetMaterialVector3Int()  {  Material mt = target as Material; Vector4 v = mt.GetVector(m_material_property_id); return new Vector3Int((int)v.x,(int)v.y,(int)v.z); }
        internal Rect       GetMaterialRect()        {  Material mt = target as Material; Vector4 v = mt.GetVector(m_material_property_id); return new Rect(v.x,v.y,v.z,v.w); }
        internal Quaternion GetMaterialQuaternion()  {  Material mt = target as Material; Vector4 v = mt.GetVector(m_material_property_id); return new Quaternion(v.x,v.y,v.z,v.w); }
        internal Vector4    GetMaterialVector4()     {  Material mt = target as Material; return mt.GetVector(m_material_property_id); }        
        
        internal void  SetMaterialInt    (int     v)       {  Material mt = target as Material; mt.SetInt      (m_material_property_id,v);   }
        internal void  SetMaterialFloat  (float   v)       {  Material mt = target as Material; mt.SetFloat    (m_material_property_id,v);   }
        internal void  SetMaterialColor  (Color   v)       {  Material mt = target as Material; mt.SetColor    (m_material_property_id,v);   }
        internal void  SetMaterialVector2(Vector2 v)       {  Material mt = target as Material; mt.SetVector   (m_material_property_id,new Vector4(v.x,v.y,0f,0f));    }
        internal void  SetMaterialVector2Int(Vector2Int v) {  Material mt = target as Material; mt.SetVector   (m_material_property_id,new Vector4(v.x,v.y,0f,0f));    }
        internal void  SetMaterialVector3(Vector3 v)       {  Material mt = target as Material; mt.SetVector   (m_material_property_id,new Vector4(v.x,v.y,v.z,0f));   }
        internal void  SetMaterialVector3Int(Vector3Int v) {  Material mt = target as Material; mt.SetVector   (m_material_property_id,new Vector4(v.x,v.y,v.z,0f));   }
        internal void  SetMaterialRect(Rect v)             {  Material mt = target as Material; mt.SetVector   (m_material_property_id,new Vector4(v.x,v.y,v.width,v.height)); }
        internal void  SetMaterialQuaternion(Quaternion v) {  Material mt = target as Material; mt.SetVector   (m_material_property_id,new Vector4(v.x,v.y,v.z,v.w)); }
        internal void  SetMaterialVector4(Vector4 v)       {  Material mt = target as Material; mt.SetVector   (m_material_property_id,v);   }

        #endregion

        #region MemberInfo Get/Set

        internal sbyte      GetPropertySByte      () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; SByteGetter       fn = (SByteGetter     )m_target_property_getter; if(fn!=null) return fn(); return (sbyte      ) pi.GetValue(isStatic ? null : target); }
        internal byte       GetPropertyByte       () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; ByteGetter        fn = (ByteGetter      )m_target_property_getter; if(fn!=null) return fn(); return (byte       ) pi.GetValue(isStatic ? null : target); }
        internal ushort     GetPropertyUShort     () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; UShortGetter      fn = (UShortGetter    )m_target_property_getter; if(fn!=null) return fn(); return (ushort     ) pi.GetValue(isStatic ? null : target); }
        internal short      GetPropertyShort      () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; ShortGetter       fn = (ShortGetter     )m_target_property_getter; if(fn!=null) return fn(); return (short      ) pi.GetValue(isStatic ? null : target); }
        internal uint       GetPropertyUInt       () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; UIntGetter        fn = (UIntGetter      )m_target_property_getter; if(fn!=null) return fn(); return (uint       ) pi.GetValue(isStatic ? null : target); }
        internal int        GetPropertyInt        () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; IntGetter         fn = (IntGetter       )m_target_property_getter; if(fn!=null) return fn(); return (int        ) pi.GetValue(isStatic ? null : target); }
        internal ulong      GetPropertyULong      () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; ULongGetter       fn = (ULongGetter     )m_target_property_getter; if(fn!=null) return fn(); return (ulong      ) pi.GetValue(isStatic ? null : target); }
        internal long       GetPropertyLong       () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; LongGetter        fn = (LongGetter      )m_target_property_getter; if(fn!=null) return fn(); return (long       ) pi.GetValue(isStatic ? null : target); }
        internal float      GetPropertyFloat      () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; FloatGetter       fn = (FloatGetter     )m_target_property_getter; if(fn!=null) return fn(); return (float      ) pi.GetValue(isStatic ? null : target); }
        internal double     GetPropertyDouble     () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; DoubleGetter      fn = (DoubleGetter    )m_target_property_getter; if(fn!=null) return fn(); return (double     ) pi.GetValue(isStatic ? null : target); }
        internal Vector2    GetPropertyVector2    () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; Vector2Getter     fn = (Vector2Getter   )m_target_property_getter; if(fn!=null) return fn(); return (Vector2    ) pi.GetValue(isStatic ? null : target); }
        internal Vector2Int GetPropertyVector2Int () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; Vector2IntGetter  fn = (Vector2IntGetter)m_target_property_getter; if(fn!=null) return fn(); return (Vector2Int ) pi.GetValue(isStatic ? null : target); }
        internal Vector3    GetPropertyVector3    () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; Vector3Getter     fn = (Vector3Getter   )m_target_property_getter; if(fn!=null) return fn(); return (Vector3    ) pi.GetValue(isStatic ? null : target); }
        internal Vector3Int GetPropertyVector3Int () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; Vector3IntGetter  fn = (Vector3IntGetter)m_target_property_getter; if(fn!=null) return fn(); return (Vector3Int ) pi.GetValue(isStatic ? null : target); }
        internal Vector4    GetPropertyVector4    () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; Vector4Getter     fn = (Vector4Getter   )m_target_property_getter; if(fn!=null) return fn(); return (Vector4    ) pi.GetValue(isStatic ? null : target); }
        internal Rect       GetPropertyRect       () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; RectGetter        fn = (RectGetter      )m_target_property_getter; if(fn!=null) return fn(); return (Rect       ) pi.GetValue(isStatic ? null : target); }
        internal Quaternion GetPropertyQuaternion () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; QuaternionGetter  fn = (QuaternionGetter)m_target_property_getter; if(fn!=null) return fn(); return (Quaternion ) pi.GetValue(isStatic ? null : target); }
        internal Color      GetPropertyColor      () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; ColorGetter       fn = (ColorGetter     )m_target_property_getter; if(fn!=null) return fn(); return (Color      ) pi.GetValue(isStatic ? null : target); }
        internal Color32    GetPropertyColor32    () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; Color32Getter     fn = (Color32Getter   )m_target_property_getter; if(fn!=null) return fn(); return (Color32    ) pi.GetValue(isStatic ? null : target); }
        internal RangeInt   GetPropertyRangeInt   () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; RangeIntGetter    fn = (RangeIntGetter  )m_target_property_getter; if(fn!=null) return fn(); return (RangeInt   ) pi.GetValue(isStatic ? null : target); }
        internal Ray        GetPropertyRay        () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; RayGetter         fn = (RayGetter       )m_target_property_getter; if(fn!=null) return fn(); return (Ray        ) pi.GetValue(isStatic ? null : target); }
        internal Ray2D      GetPropertyRay2D      () { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; Ray2DGetter       fn = (Ray2DGetter     )m_target_property_getter; if(fn!=null) return fn(); return (Ray2D      ) pi.GetValue(isStatic ? null : target); }

        internal void SetPropertySByte      (sbyte      v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; SByteSetter       fn = (SByteSetter     )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyByte       (byte       v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; ByteSetter        fn = (ByteSetter      )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyUShort     (ushort     v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; UShortSetter      fn = (UShortSetter    )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyShort      (short      v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; ShortSetter       fn = (ShortSetter     )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyUInt       (uint       v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; UIntSetter        fn = (UIntSetter      )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyInt        (int        v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; IntSetter         fn = (IntSetter       )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyULong      (ulong      v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; ULongSetter       fn = (ULongSetter     )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyLong       (long       v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; LongSetter        fn = (LongSetter      )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyFloat      (float      v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; FloatSetter       fn = (FloatSetter     )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyDouble     (double     v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; DoubleSetter      fn = (DoubleSetter    )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyVector2    (Vector2    v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; Vector2Setter     fn = (Vector2Setter   )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyVector2Int (Vector2Int v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; Vector2IntSetter  fn = (Vector2IntSetter)m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyVector3    (Vector3    v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; Vector3Setter     fn = (Vector3Setter   )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyVector3Int (Vector3Int v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; Vector3IntSetter  fn = (Vector3IntSetter)m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyVector4    (Vector4    v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; Vector4Setter     fn = (Vector4Setter   )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyRect       (Rect       v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; RectSetter        fn = (RectSetter      )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyQuaternion (Quaternion v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; QuaternionSetter  fn = (QuaternionSetter)m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyColor      (Color      v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; ColorSetter       fn = (ColorSetter     )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyColor32    (Color32    v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; Color32Setter     fn = (Color32Setter   )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyRangeInt   (RangeInt   v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; RangeIntSetter    fn = (RangeIntSetter  )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyRay        (Ray        v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; RaySetter         fn = (RaySetter       )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }
        internal void SetPropertyRay2D      (Ray2D      v) { PropertyInfo pi = (PropertyInfo)m_target_property_accessor; Ray2DSetter       fn = (Ray2DSetter     )m_target_property_setter; if(fn!=null) { fn(v); return; } pi.SetValue(isStatic ? null : target,v,null); }

        internal sbyte      GetFieldSByte      () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (sbyte      ) fi.GetValue(isStatic ? null : target); }
        internal byte       GetFieldByte       () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (byte       ) fi.GetValue(isStatic ? null : target); }
        internal ushort     GetFieldUShort     () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (ushort     ) fi.GetValue(isStatic ? null : target); }
        internal short      GetFieldShort      () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (short      ) fi.GetValue(isStatic ? null : target); }
        internal uint       GetFieldUInt       () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (uint       ) fi.GetValue(isStatic ? null : target); }
        internal int        GetFieldInt        () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (int        ) fi.GetValue(isStatic ? null : target); }
        internal ulong      GetFieldULong      () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (ulong      ) fi.GetValue(isStatic ? null : target); }
        internal long       GetFieldLong       () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (long       ) fi.GetValue(isStatic ? null : target); }
        internal float      GetFieldFloat      () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (float      ) fi.GetValue(isStatic ? null : target); }
        internal double     GetFieldDouble     () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (double     ) fi.GetValue(isStatic ? null : target); }
        internal Vector2    GetFieldVector2    () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (Vector2    ) fi.GetValue(isStatic ? null : target); }
        internal Vector2Int GetFieldVector2Int () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (Vector2Int ) fi.GetValue(isStatic ? null : target); }
        internal Vector3    GetFieldVector3    () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (Vector3    ) fi.GetValue(isStatic ? null : target); }
        internal Vector3Int GetFieldVector3Int () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (Vector3Int ) fi.GetValue(isStatic ? null : target); }
        internal Vector4    GetFieldVector4    () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (Vector4    ) fi.GetValue(isStatic ? null : target); }
        internal Rect       GetFieldRect       () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (Rect       ) fi.GetValue(isStatic ? null : target); }
        internal Quaternion GetFieldQuaternion () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (Quaternion ) fi.GetValue(isStatic ? null : target); }
        internal Color      GetFieldColor      () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (Color      ) fi.GetValue(isStatic ? null : target); }
        internal Color32    GetFieldColor32    () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (Color32    ) fi.GetValue(isStatic ? null : target); }
        internal RangeInt   GetFieldRangeInt   () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (RangeInt   ) fi.GetValue(isStatic ? null : target); }
        internal Ray        GetFieldRay        () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (Ray        ) fi.GetValue(isStatic ? null : target); }
        internal Ray2D      GetFieldRay2D      () { FieldInfo fi = (FieldInfo)m_target_property_accessor; return (Ray2D      ) fi.GetValue(isStatic ? null : target); }

        internal void SetFieldSByte      (sbyte      v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldByte       (byte       v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldUShort     (ushort     v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldShort      (short      v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldUInt       (uint       v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldInt        (int        v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldULong      (ulong      v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldLong       (long       v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldFloat      (float      v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldDouble     (double     v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldVector2    (Vector2    v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldVector2Int (Vector2Int v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldVector3    (Vector3    v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldVector3Int (Vector3Int v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldVector4    (Vector4    v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldRect       (Rect       v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldQuaternion (Quaternion v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldColor      (Color      v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldColor32    (Color32    v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldRangeInt   (RangeInt   v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldRay        (Ray        v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }
        internal void SetFieldRay2D      (Ray2D      v) { FieldInfo fi = (FieldInfo)m_target_property_accessor; fi.SetValue(isStatic ? null : target,v); }

        #endregion

        #region Get/Set

        protected sbyte      GetSByte      () { if(!isValid) return default; if(isMaterial) return (sbyte     )GetMaterialInt();        return m_is_property ? GetPropertySByte     () : (m_is_field ? GetFieldSByte     () : default); }
        protected byte       GetByte       () { if(!isValid) return default; if(isMaterial) return (byte      )GetMaterialInt();        return m_is_property ? GetPropertyByte      () : (m_is_field ? GetFieldByte      () : default); }
        protected ushort     GetUShort     () { if(!isValid) return default; if(isMaterial) return (ushort    )GetMaterialInt();        return m_is_property ? GetPropertyUShort    () : (m_is_field ? GetFieldUShort    () : default); }
        protected short      GetShort      () { if(!isValid) return default; if(isMaterial) return (short     )GetMaterialInt();        return m_is_property ? GetPropertyShort     () : (m_is_field ? GetFieldShort     () : default); }
        protected uint       GetUInt       () { if(!isValid) return default; if(isMaterial) return (uint      )GetMaterialInt();        return m_is_property ? GetPropertyUInt      () : (m_is_field ? GetFieldUInt      () : default); }
        protected int        GetInt        () { if(!isValid) return default; if(isMaterial) return (int       )GetMaterialInt();        return m_is_property ? GetPropertyInt       () : (m_is_field ? GetFieldInt       () : default); }
        protected ulong      GetULong      () { if(!isValid) return default; if(isMaterial) return (ulong     )GetMaterialInt();        return m_is_property ? GetPropertyULong     () : (m_is_field ? GetFieldULong     () : default); }
        protected long       GetLong       () { if(!isValid) return default; if(isMaterial) return (long      )GetMaterialInt();        return m_is_property ? GetPropertyLong      () : (m_is_field ? GetFieldLong      () : default); }
        protected float      GetFloat      () { if(!isValid) return default; if(isMaterial) return (float     )GetMaterialFloat();      return m_is_property ? GetPropertyFloat     () : (m_is_field ? GetFieldFloat     () : default); }
        protected double     GetDouble     () { if(!isValid) return default; if(isMaterial) return (double    )GetMaterialFloat();      return m_is_property ? GetPropertyDouble    () : (m_is_field ? GetFieldDouble    () : default); }
        protected Vector2    GetVector2    () { if(!isValid) return default; if(isMaterial) return (Vector2   )GetMaterialVector2();    return m_is_property ? GetPropertyVector2   () : (m_is_field ? GetFieldVector2   () : default); }
        protected Vector2Int GetVector2Int () { if(!isValid) return default; if(isMaterial) return (Vector2Int)GetMaterialVector2Int(); return m_is_property ? GetPropertyVector2Int() : (m_is_field ? GetFieldVector2Int() : default); }
        protected Vector3    GetVector3    () { if(!isValid) return default; if(isMaterial) return (Vector3   )GetMaterialVector3();    return m_is_property ? GetPropertyVector3   () : (m_is_field ? GetFieldVector3   () : default); }
        protected Vector3Int GetVector3Int () { if(!isValid) return default; if(isMaterial) return (Vector3Int)GetMaterialVector3Int(); return m_is_property ? GetPropertyVector3Int() : (m_is_field ? GetFieldVector3Int() : default); }
        protected Vector4    GetVector4    () { if(!isValid) return default; if(isMaterial) return (Vector4   )GetMaterialVector4();    return m_is_property ? GetPropertyVector4   () : (m_is_field ? GetFieldVector4   () : default); }
        protected Rect       GetRect       () { if(!isValid) return default; if(isMaterial) return (Rect      )GetMaterialRect();       return m_is_property ? GetPropertyRect      () : (m_is_field ? GetFieldRect      () : default); }
        protected Quaternion GetQuaternion () { if(!isValid) return default; if(isMaterial) return (Quaternion)GetMaterialQuaternion(); return m_is_property ? GetPropertyQuaternion() : (m_is_field ? GetFieldQuaternion() : default); }
        protected Color      GetColor      () { if(!isValid) return default; if(isMaterial) return (Color     )GetMaterialColor();      return m_is_property ? GetPropertyColor     () : (m_is_field ? GetFieldColor     () : default); }
        protected Color32    GetColor32    () { if(!isValid) return default; if(isMaterial) return default;                             return m_is_property ? GetPropertyColor32   () : (m_is_field ? GetFieldColor32   () : default); }
        protected RangeInt   GetRangeInt   () { if(!isValid) return default; if(isMaterial) return default;                             return m_is_property ? GetPropertyRangeInt  () : (m_is_field ? GetFieldRangeInt  () : default); }
        protected Ray        GetRay        () { if(!isValid) return default; if(isMaterial) return default;                             return m_is_property ? GetPropertyRay       () : (m_is_field ? GetFieldRay       () : default); }
        protected Ray2D      GetRay2D      () { if(!isValid) return default; if(isMaterial) return default;                             return m_is_property ? GetPropertyRay2D     () : (m_is_field ? GetFieldRay2D     () : default); }

        protected void SetSByte      (sbyte      v) { if(!isValid) return; if(isMaterial) { SetMaterialInt       ((int)v);   return; } if(m_is_property)SetPropertySByte     (v); else if(m_is_field)SetFieldSByte     (v); }
        protected void SetByte       (byte       v) { if(!isValid) return; if(isMaterial) { SetMaterialInt       ((int)v);   return; } if(m_is_property)SetPropertyByte      (v); else if(m_is_field)SetFieldByte      (v); }
        protected void SetUShort     (ushort     v) { if(!isValid) return; if(isMaterial) { SetMaterialInt       ((int)v);   return; } if(m_is_property)SetPropertyUShort    (v); else if(m_is_field)SetFieldUShort    (v); }
        protected void SetShort      (short      v) { if(!isValid) return; if(isMaterial) { SetMaterialInt       ((int)v);   return; } if(m_is_property)SetPropertyShort     (v); else if(m_is_field)SetFieldShort     (v); }
        protected void SetUInt       (uint       v) { if(!isValid) return; if(isMaterial) { SetMaterialInt       ((int)v);   return; } if(m_is_property)SetPropertyUInt      (v); else if(m_is_field)SetFieldUInt      (v); }
        protected void SetInt        (int        v) { if(!isValid) return; if(isMaterial) { SetMaterialInt       ((int)v);   return; } if(m_is_property)SetPropertyInt       (v); else if(m_is_field)SetFieldInt       (v); }
        protected void SetULong      (ulong      v) { if(!isValid) return; if(isMaterial) { SetMaterialInt       ((int)v);   return; } if(m_is_property)SetPropertyULong     (v); else if(m_is_field)SetFieldULong     (v); }
        protected void SetLong       (long       v) { if(!isValid) return; if(isMaterial) { SetMaterialInt       ((int)v);   return; } if(m_is_property)SetPropertyLong      (v); else if(m_is_field)SetFieldLong      (v); }
        protected void SetFloat      (float      v) { if(!isValid) return; if(isMaterial) { SetMaterialFloat     ((float)v); return; } if(m_is_property)SetPropertyFloat     (v); else if(m_is_field)SetFieldFloat     (v); }
        protected void SetDouble     (double     v) { if(!isValid) return; if(isMaterial) { SetMaterialFloat     ((float)v); return; } if(m_is_property)SetPropertyDouble    (v); else if(m_is_field)SetFieldDouble    (v); }
        protected void SetVector2    (Vector2    v) { if(!isValid) return; if(isMaterial) { SetMaterialVector2   (v); return;        } if(m_is_property)SetPropertyVector2   (v); else if(m_is_field)SetFieldVector2   (v); }
        protected void SetVector2Int (Vector2Int v) { if(!isValid) return; if(isMaterial) { SetMaterialVector2Int(v); return;        } if(m_is_property)SetPropertyVector2Int(v); else if(m_is_field)SetFieldVector2Int(v); }
        protected void SetVector3    (Vector3    v) { if(!isValid) return; if(isMaterial) { SetMaterialVector3   (v); return;        } if(m_is_property)SetPropertyVector3   (v); else if(m_is_field)SetFieldVector3   (v); }
        protected void SetVector3Int (Vector3Int v) { if(!isValid) return; if(isMaterial) { SetMaterialVector3Int(v); return;        } if(m_is_property)SetPropertyVector3Int(v); else if(m_is_field)SetFieldVector3Int(v); }
        protected void SetVector4    (Vector4    v) { if(!isValid) return; if(isMaterial) { SetMaterialVector4   (v); return;        } if(m_is_property)SetPropertyVector4   (v); else if(m_is_field)SetFieldVector4   (v); }
        protected void SetRect       (Rect       v) { if(!isValid) return; if(isMaterial) { SetMaterialRect      (v); return;        } if(m_is_property)SetPropertyRect      (v); else if(m_is_field)SetFieldRect      (v); }
        protected void SetQuaternion (Quaternion v) { if(!isValid) return; if(isMaterial) { SetMaterialQuaternion(v); return;        } if(m_is_property)SetPropertyQuaternion(v); else if(m_is_field)SetFieldQuaternion(v); }
        protected void SetColor      (Color      v) { if(!isValid) return; if(isMaterial) { SetMaterialColor     (v); return;        } if(m_is_property)SetPropertyColor     (v); else if(m_is_field)SetFieldColor     (v); }
        protected void SetColor32    (Color32    v) { if(!isValid) return; if(isMaterial) return;                                      if(m_is_property)SetPropertyColor32   (v); else if(m_is_field)SetFieldColor32   (v); }
        protected void SetRangeInt   (RangeInt   v) { if(!isValid) return; if(isMaterial) return;                                      if(m_is_property)SetPropertyRangeInt  (v); else if(m_is_field)SetFieldRangeInt  (v); }
        protected void SetRay        (Ray        v) { if(!isValid) return; if(isMaterial) return;                                      if(m_is_property)SetPropertyRay       (v); else if(m_is_field)SetFieldRay       (v); }
        protected void SetRay2D      (Ray2D      v) { if(!isValid) return; if(isMaterial) return;                                      if(m_is_property)SetPropertyRay2D     (v); else if(m_is_field)SetFieldRay2D     (v); }

        #endregion
        
        #region Set

        /// <summary>
        /// Set this interpolator target and properties.
        /// </summary>
        /// <param name="p_target">Target object.</param>
        /// <param name="p_property">Property to interpolate.</param>
        /// <param name="p_easing">Easing Function.</param>
        public void Set(object p_target,string p_property,Func<float,float> p_easing) { Create(p_target,p_property,p_easing); }

        /// <summary>
        /// Set this interpolator target and properties.
        /// </summary>
        /// <param name="p_target">Target object.</param>
        /// <param name="p_property">Property to interpolate.</param>
        /// <param name="p_easing">Easing Curve.</param>
        public void Set(object p_target,string p_property,AnimationCurve p_easing) { Create(p_target,p_property,p_easing); }

        /// <summary>
        /// Set this interpolator target and properties.
        /// </summary>
        /// <param name="p_target">Target object.</param>
        /// <param name="p_property">Property to interpolate.</param>
        /// <param name="p_easing">Easing Curve.</param>
        public void Set(object p_target,string p_property) { Create(p_target,p_property,null); }

        #endregion

        #endregion

        /// <summary>
        /// Interpolates the properties of the target object.
        /// </summary>
        /// <param name="p_ratio">Ratio to interpolate the data. This value will be transformed using either the Easing function or curve.</param>
        public void Lerp(float p_ratio) {
            float r = p_ratio;
            //Select either curve or method, prioritizing method
            AnimationCurve    ec = easingCurve;
            Func<float,float> em = easingFunction;
            if(em==null)if(ec==null) ec = DefaultAnimationCurve;
            //Apply easing
            r = em==null ? ec.Evaluate(r) : em(r);            
            //Calls the handler to actually interpolate the data
            OnLerp(r);
        }

        #region Virtuals

        /// <summary>
        /// Handler called for the actual data interpolation.
        /// </summary>
        /// <param name="p_ratio"></param>
        virtual protected void OnLerp(float p_ratio) { }

        /// <summary>
        /// Helper for the typed version.
        /// </summary>
        /// <param name="p_from"></param>
        /// <param name="p_to"></param>
        /// <param name="p_has_from"></param>
        virtual internal void Set(object p_from,object p_to,bool p_has_from) { }

        #endregion

    }

    #endregion

    #region class Interpolator<T>

    /// <summary>
    /// Extension of the interpolator class to support 'from' 'to' ranges to be applied in the target's property.
    /// </summary>
    /// <typeparam name="T">Property type.</typeparam>
    public class Interpolator<T> : Interpolator {

        /// <summary>
        /// Start value.
        /// </summary>
        public T from;

        /// <summary>
        /// End Value
        /// </summary>
        public T to;

        /// <summary>
        /// Flag that tells the 'from' value must be captured early on.
        /// </summary>
        internal bool m_capture_from;
        internal bool m_first_iteration;
        internal Interpolator<U> Cast<U>()    { return this as Interpolator<U>; }
        internal bool            CanCast<U>() { return this is Interpolator<U>; }

        #region Set

        /// <summary>
        /// Set this interpolator target, property and value range.
        /// </summary>
        /// <param name="p_target">Target to interpolate.</param>
        /// <param name="p_property">Property to change.</param>
        /// <param name="p_from">Initial Value.</param>
        /// <param name="p_to">End Value</param>
        public void Set(object p_target,string p_property,T p_from,T p_to) { Create(p_target,p_property,p_from,p_to,true,null); }

        /// <summary>
        /// Set this interpolator target, property and value range. 'From' isn't specified so it will be sampled in the first iteration.
        /// </summary>
        /// <param name="p_target">Target to interpolate.</param>
        /// <param name="p_property">Property to change.</param>        
        /// <param name="p_to">End Value</param>
        public void Set(object p_target,string p_property,T p_to) { Create(p_target,p_property,p_to,p_to,false,null); }

        /// <summary>
        /// Set this interpolator target, property and value range.
        /// </summary>
        /// <param name="p_target">Target to interpolate.</param>
        /// <param name="p_property">Property to change.</param>
        /// <param name="p_from">Initial Value.</param>
        /// <param name="p_to">End Value</param>
        /// <param name="p_easing">Easing Function.</param>
        public void Set(object p_target,string p_property,T p_from,T p_to,Func<float,float> p_easing) { Create(p_target,p_property,p_from,p_to,true,p_easing); }

        /// <summary>
        /// Set this interpolator target, property and value range.
        /// </summary>
        /// <param name="p_target">Target to interpolate.</param>
        /// <param name="p_property">Property to change.</param>
        /// <param name="p_from">Initial Value.</param>
        /// <param name="p_to">End Value</param>
        /// <param name="p_easing">Easing Curve.</param>
        public void Set(object p_target,string p_property,T p_from,T p_to,AnimationCurve p_easing) { Create(p_target,p_property,p_from,p_to,true,p_easing); }

        /// <summary>
        /// Set this interpolator target, property and value range. 'From' isn't specified so it will be sampled in the first iteration.
        /// </summary>
        /// <param name="p_target">Target to interpolate.</param>
        /// <param name="p_property">Property to change.</param>
        /// <param name="p_to">Target Value.</param>        
        /// <param name="p_easing">Easing Function.</param>
        public void Set(object p_target,string p_property,T p_to,Func<float,float> p_easing) { Create(p_target,p_property,p_to,p_to,false,p_easing); }

        /// <summary>
        /// Set this interpolator target, property and value range. 'From' isn't specified so it will be sampled in the first iteration.
        /// </summary>
        /// <param name="p_target">Target to interpolate.</param>
        /// <param name="p_property">Property to change.</param>
        /// <param name="p_to">Target Value.</param>        
        /// <param name="p_easing">Easing Curve.</param>
        public void Set(object p_target,string p_property,T p_to,AnimationCurve p_easing) { Create(p_target,p_property,p_to,p_to,false,p_easing); }

        /// <summary>
        /// Helper method.
        /// </summary>        
        internal void Create(object p_target,string p_property,T p_from,T p_to,bool p_has_from,object p_easing) {
            Create(p_target,p_property,p_easing);                        
            Set(p_from,p_to,p_has_from);
        }

        /// <summary>
        /// Helper
        /// </summary>
        /// <param name="p_from"></param>
        /// <param name="p_to"></param>
        /// <param name="p_has_from"></param>
        override internal void Set(object p_from,object p_to,bool p_has_from) {
            to = (T)p_to;
            if(p_has_from) from = (T)p_from;            
            m_first_iteration = true;
            m_capture_from    = p_has_from;
        }

        #endregion

        /// <summary>
        /// Method called when the 'from' property must be set.
        /// </summary>
        virtual protected void SetFromField() { }

        /// <summary>
        /// Method called to apply the property value.
        /// </summary>
        /// <param name="p_value"></param>
        virtual protected void SetProperty(T p_value) { }

        /// <summary>
        /// Handler to manipulate the data and returns its next value.
        /// </summary>
        /// <param name="p_ratio">Interpolation ratio.</param>
        /// <returns>Interpolated value.</returns>
        virtual protected T LerpValue(float p_ratio) { return p_ratio > 0.5f ? to : from; }

        /// <summary>
        /// Overrides the virtual 'OnLerp' and actually apply the interpolation.
        /// </summary>
        /// <param name="p_ratio"></param>
        protected override void OnLerp(float p_ratio) {
            //If no 'from' value is given, capture it in the first iteration.
            if(m_capture_from)if(m_first_iteration) { m_first_iteration = false; SetFromField(); }
            T next_value = LerpValue(p_ratio);
            SetProperty(next_value);
        }

    }

    #endregion

    #region C# Types

    /// <summary>
    /// Class Extension to interpolate 'sbyte'
    /// </summary>
    internal class SByteInterpolator   : Interpolator<sbyte>   { 
        protected override void  SetFromField()          { from = GetSByte(); }
        protected override void  SetProperty(sbyte p_value) { SetSByte(p_value); }
        protected override sbyte LerpValue(float p_ratio) { float dv  = (float)(to-from); sbyte   off = (sbyte)(dv*p_ratio);  return (sbyte)(from + off); } 
    }
    /// <summary>
    /// Class Extension to interpolate 'byte'
    /// </summary>
    internal class ByteInterpolator    : Interpolator<byte>    { 
        protected override void SetFromField()          { from = GetByte(); }
        protected override void SetProperty(byte p_value) { SetByte(p_value); }
        protected override byte LerpValue(float p_ratio) { float dv  = (float)(to-from); byte    off = (byte)(dv*p_ratio);   return (byte)(from + off); } 
    }
    /// <summary>
    /// Class Extension to interpolate 'ushort'
    /// </summary>
    internal class UShortInterpolator  : Interpolator<ushort>  { 
        protected override void SetFromField()           { from = GetUShort(); }
        protected override void SetProperty(ushort p_value) { SetUShort(p_value); }
        protected override ushort LerpValue(float p_ratio) { float dv  = (float)(to-from); ushort  off = (ushort)(dv*p_ratio); return (ushort)(from + off); } 
    }
    /// <summary>
    /// Class Extension to interpolate 'short'
    /// </summary>
    internal class ShortInterpolator   : Interpolator<short>   { 
        protected override void  SetFromField()          { from = GetShort(); }
        protected override void  SetProperty(short p_value) { SetShort(p_value); }
        protected override short LerpValue(float p_ratio) { float dv  = (float)(to-from); short   off = (short)(dv*p_ratio);  return (short)(from + off); } 
    }    
    /// <summary>
    /// Class Extension to interpolate 'uint'
    /// </summary>
    internal class UIntInterpolator    : Interpolator<uint>    { 
        protected override void SetFromField()           { from = GetUInt(); }
        protected override void SetProperty(uint p_value)   { SetUInt(p_value); }
        protected override uint LerpValue(float p_ratio) { float dv  = (float)(to-from);  uint   off = (uint)(dv*p_ratio);   return from + off; } 
    }
    /// <summary>
    /// Class Extension to interpolate 'int'
    /// </summary>
    internal class IntInterpolator     : Interpolator<int>     { 
        protected override void SetFromField()          { from = GetInt(); }
        protected override void SetProperty(int p_value)   { SetInt(p_value); }
        protected override int  LerpValue(float p_ratio) { float dv  = (float)(to-from);  int    off = (int)(dv*p_ratio);    return from + off; } 
    }
    /// <summary>
    /// Class Extension to interpolate 'ulong'
    /// </summary>
    internal class ULongInterpolator   : Interpolator<ulong>   { 
        protected override void SetFromField()          { from = GetULong(); }
        protected override void SetProperty(ulong p_value) { SetULong(p_value); }
        protected override ulong  LerpValue(float p_ratio) { double dv = (double)(to-from); ulong  off = (ulong)(dv*p_ratio);  return from + off; } 
    }
    /// <summary>
    /// Class Extension to interpolate 'ulong'
    /// </summary>
    internal class LongInterpolator    : Interpolator<long>    { 
        protected override void SetFromField()         { from = GetLong(); }
        protected override void SetProperty(long p_value) { SetLong(p_value); }
        protected override long LerpValue(float p_ratio) { double dv  = (double)(to-from); long  off = (long)(dv*p_ratio);   return from + off; } 
    }
    /// <summary>
    /// Class Extension to interpolate 'float'
    /// </summary>
    internal class FloatInterpolator   : Interpolator<float>   { 
        protected override void  SetFromField()          { from = GetFloat(); }
        protected override void  SetProperty(float p_value) { SetFloat(p_value); }
        protected override float LerpValue(float p_ratio) { float dv  = (float)(to-from);  float  off = (float)(dv*p_ratio);  return from + off; } 
    }
    /// <summary>
    /// Class Extension to interpolate 'double'
    /// </summary>
    internal class DoubleInterpolator  : Interpolator<double>  { 
        protected override void   SetFromField()           { from = GetDouble(); }
        protected override void   SetProperty(double p_value) { SetDouble(p_value); }
        protected override double LerpValue(float p_ratio)    { float dv  = (float)(to-from);  double off = (double)(dv*p_ratio); return from + off; } 
    }

    #endregion

    #region Unity Types

    #region enum VectorInterpolatorMask

    /// <summary>
    /// Swizzle Mask to selectively change vector components when they are available.
    /// </summary>
    public enum VectorInterpolatorMask : uint {
        /// <summary>
        /// Neither component
        /// </summary>
        None   = 0,
        /// <summary>
        /// X Component
        /// </summary>
        X      = (1<<0),        
        /// <summary>
        /// Y Component
        /// </summary>        
        Y      = (1<<1),
        /// <summary>
        /// Z Component
        /// </summary>
        Z      = (1<<2),
        /// <summary>
        /// W Component
        /// </summary>
        W      = (1<<3),
        /// <summary>
        /// XYZ Component
        /// </summary>
        XYZW   = X|Y|Z|W,
        /// <summary>
        /// XYZ Component
        /// </summary>
        XYZ    = X|Y|Z,
        /// <summary>
        /// XY Component
        /// </summary>
        XY     = X|Y,
        /// <summary>
        /// XZ Component
        /// </summary>
        XZ     = X|Z,
        /// <summary>
        /// YZ Component
        /// </summary>
        YZ     = Y|Z,
        /// <summary>
        /// Red Component
        /// </summary>
        R      = (1<<0),        
        /// <summary>
        /// Green Component
        /// </summary>        
        G      = (1<<1),
        /// <summary>
        /// Blue Component
        /// </summary>
        B      = (1<<2),
        /// <summary>
        /// Alpha Component
        /// </summary>
        A      = (1<<3),
        /// <summary>
        /// RGBA Component
        /// </summary>
        RGBA   = R|G|B,
        /// <summary>
        /// RGB Component
        /// </summary>
        RGB    = R|G|B,
        /// <summary>
        /// RG Component
        /// </summary>
        RG     = R|G,
        /// <summary>
        /// RB Component
        /// </summary>
        RB     = R|B,
        /// <summary>
        /// GB Component
        /// </summary>
        GB     = G|B,
        /// <summary>
        /// Width
        /// </summary>
        Width  = (1<<4),
        /// <summary>
        /// Height
        /// </summary>
        Height = (1<<5),  
        /// <summary>
        /// Range Start Component
        /// </summary>
        RangeStart  = (1<<0),        
        /// <summary>
        /// Range End Component
        /// </summary>
        RangeEnd  = (1<<1),        
        /// <summary>
        /// Range Length Component
        /// </summary>
        RangeLength  = (1<<2),        
        /// <summary>
        /// Origin (usually Rays)
        /// </summary>
        Origin = (1<<6),  
        /// <summary>
        /// Direction (usually Rays)
        /// </summary>
        Direction = (1<<7),
        /// <summary>
        /// All available components
        /// </summary>
        All = 0xffffffff
    }

    #endregion

    #region class UnityVectorInterpolator<T>

    internal class UnityVectorInterpolator<T> : Interpolator<T> {
        
        /// <summary>
        /// Component selection mask.
        /// </summary>
        public VectorInterpolatorMask mask = VectorInterpolatorMask.All;

        /// <summary>
        /// Internals.
        /// </summary>
        internal float GMV(VectorInterpolatorMask m,float v) { return (mask & m)==0 ? 0f : v; }
        internal int   GMV(VectorInterpolatorMask m,int   v) { return (mask & m)==0 ? 0  : v; }
        internal Vector2 ApplyMask(Vector2 v)       { v.Set(GMV(VectorInterpolatorMask.X,v.x),GMV(VectorInterpolatorMask.Y,v.y)); return v;  }
        internal Vector2Int ApplyMask(Vector2Int v) { v.Set(GMV(VectorInterpolatorMask.X,v.x),GMV(VectorInterpolatorMask.Y,v.y)); return v;  }
        internal Vector3 ApplyMask(Vector3 v)       { v.Set(GMV(VectorInterpolatorMask.X,v.x),GMV(VectorInterpolatorMask.Y,v.y),GMV(VectorInterpolatorMask.Z,v.z)); return v; }
        internal Vector3Int ApplyMask(Vector3Int v) { v.Set(GMV(VectorInterpolatorMask.X,v.x),GMV(VectorInterpolatorMask.Y,v.y),GMV(VectorInterpolatorMask.Z,v.z)); return v; }
        internal Vector4 ApplyMask(Vector4 v)       { v.Set(GMV(VectorInterpolatorMask.X,v.x),GMV(VectorInterpolatorMask.Y,v.y),GMV(VectorInterpolatorMask.Z,v.z),GMV(VectorInterpolatorMask.W,v.w));          return v; }                
        internal Vector4 ApplyMaskRect(Vector4 v)   { v.Set(GMV(VectorInterpolatorMask.X,v.x),GMV(VectorInterpolatorMask.Y,v.y),GMV(VectorInterpolatorMask.Width,v.z),GMV(VectorInterpolatorMask.Height,v.w)); return v; }
        
        
    }

    #endregion

    /// <summary>
    /// Class Extension to interpolate 'Vector2'
    /// </summary>
    internal class Vector2Interpolator    : UnityVectorInterpolator<Vector2>     { 
        protected override void  SetFromField()            { from = GetVector2(); }
        protected override void  SetProperty(Vector2 p_value) { SetVector2(p_value); }
        protected override Vector2    LerpValue(float p_ratio) { Vector2   dv  = (Vector2)(to-from);      Vector2    off = ApplyMask(dv*p_ratio); return from + off; } 
    }
    /// <summary>
    /// Class Extension to interpolate 'Vector2Int'
    /// </summary>
    internal class Vector2IntInterpolator : UnityVectorInterpolator<Vector2Int>  { 
        protected override void  SetFromField()            { from = GetVector2Int(); }
        protected override void  SetProperty(Vector2Int p_value) { SetVector2Int(p_value); }
        protected override Vector2Int LerpValue(float p_ratio) { Vector2   dv  = (Vector2)(to-from);      Vector2    off = ApplyMask(dv*p_ratio); return from + new Vector2Int((int)off.x,(int)off.y); } 
    }
    /// <summary>
    /// Class Extension to interpolate 'Vector3'
    /// </summary>
    internal class Vector3Interpolator    : UnityVectorInterpolator<Vector3>     { 
        protected override void  SetFromField()            { from = GetVector3(); }
        protected override void  SetProperty(Vector3 p_value) { SetVector3(p_value); }
        protected override Vector3    LerpValue(float p_ratio) { Vector3    dv  = (Vector3)(to-from);     Vector3    off = ApplyMask(dv*p_ratio);  return from + off; } 
    }
    /// <summary>
    /// Class Extension to interpolate 'Vector3Int'
    /// </summary>
    internal class Vector3IntInterpolator : UnityVectorInterpolator<Vector3Int>  { 
        protected override void  SetFromField()               { from = GetVector3Int(); }
        protected override void  SetProperty(Vector3Int p_value) { SetVector3Int(p_value); }
        protected override Vector3Int LerpValue(float p_ratio) { Vector3   dv  = (Vector3)(to-from);      Vector3    off = ApplyMask(dv*p_ratio); return from + new Vector3Int((int)off.x,(int)off.y,(int)off.z); } 
    }
    /// <summary>
    /// Class Extension to interpolate 'Vector4'
    /// </summary>
    internal class Vector4Interpolator    : UnityVectorInterpolator<Vector4>     { 
        protected override void  SetFromField()             { from = GetVector4(); }
        protected override void  SetProperty(Vector4 p_value)  { SetVector4(p_value); }
        protected override Vector4    LerpValue(float p_ratio) { Vector4    dv  = (Vector4)(to-from);     Vector4    off = ApplyMask(dv*p_ratio); return from + off; } 
    }
    /// <summary>
    /// Class Extension to interpolate 'Color'
    /// </summary>
    internal class ColorInterpolator    : UnityVectorInterpolator<Color>         { 
        protected override void  SetFromField()           { from = GetColor(); }
        protected override void  SetProperty(Color p_value)  { SetColor(p_value); }
        protected override Color      LerpValue(float p_ratio) { 
            Vector4 v0 = (Vector4)from; 
            Vector4 v1 = (Vector4)to;             
            Vector4 off = ApplyMask((Vector4)((v1-v0)*p_ratio)); 
            return (Color)(v0 + off); 
        } 
    }
    /// <summary>
    /// Class Extension to interpolate 'Quaternion'
    /// </summary>
    internal class QuaternionInterpolator : UnityVectorInterpolator<Quaternion>  { 
        protected override void  SetFromField()               { from = GetQuaternion(); }
        protected override void  SetProperty(Quaternion p_value) { SetQuaternion(p_value); }
        protected override Quaternion LerpValue(float p_ratio) {             
            Vector4 v0  = new Vector4(from.x,from.y,from.z,from.w);
            Vector4 v1  = new Vector4(to.x,  to.y,  to.z,  to.w);
            Vector4 off = (v1-v0)*p_ratio;
            v0 += off;
            Quaternion nv = new Quaternion(v0.x,v0.y,v0.z,v0.w);
            nv.Normalize();
            return nv;
        } 
    }
    /// <summary>
    /// Class Extension to interpolate 'Rect'
    /// </summary>
    internal class RectInterpolator : UnityVectorInterpolator<Rect> {
        protected override void  SetFromField()          { from = GetRect(); }
        protected override void  SetProperty(Rect p_value)  { SetRect(p_value); }
        protected override Rect LerpValue(float p_ratio) {             
            Vector4 v0 = new Vector4(from.x,from.y,from.width,from.height);
            Vector4 v1 = new Vector4(to.x,  to.y,  to.width,  to.height);                        
            v0 += ApplyMaskRect((v1-v0)*p_ratio);
            return new Rect(v0.x,v0.y,v0.z,v0.w);
        }
    }
    /// <summary>
    /// Class Extension to interpolate 'Color32'
    /// </summary>
    internal class Color32Interpolator  : UnityVectorInterpolator<Color32> { 
        protected override void  SetFromField()           { from = GetColor32(); }
        protected override void  SetProperty(Color32 p_value)  { SetColor32(p_value); }
        protected override Color32 LerpValue(float p_ratio) {             
            float dv  = 0;            
            Color32 nv = from;
            if((mask & VectorInterpolatorMask.R)!=0) { dv = (float)(to.r - from.r); nv.r = (byte)(from.r + (byte)(dv * p_ratio)); }
            if((mask & VectorInterpolatorMask.G)!=0) { dv = (float)(to.g - from.g); nv.g = (byte)(from.g + (byte)(dv * p_ratio)); }
            if((mask & VectorInterpolatorMask.B)!=0) { dv = (float)(to.b - from.b); nv.b = (byte)(from.b + (byte)(dv * p_ratio)); }
            if((mask & VectorInterpolatorMask.A)!=0) { dv = (float)(to.b - from.b); nv.b = (byte)(from.b + (byte)(dv * p_ratio)); }
            return nv;
        }
    }
    /// <summary>
    /// Class Extension to interpolate 'RangeInt'
    /// </summary>
    internal class RangeIntInterpolator  : UnityVectorInterpolator<RangeInt> { 
        protected override void  SetFromField()              { from = GetRangeInt(); }
        protected override void  SetProperty(RangeInt p_value)  { SetRangeInt(p_value); }
        protected override RangeInt LerpValue(float p_ratio) {             
            float dv  = 0;            
            RangeInt nv = from;
            if((mask & VectorInterpolatorMask.RangeStart) !=0) { dv = (float)(to.start  - from.start ); nv.start  = (int)(from.start  + (int)(dv * p_ratio)); }
            if((mask & VectorInterpolatorMask.RangeLength)!=0) { dv = (float)(to.length - from.length); nv.length = (int)(from.length + (int)(dv * p_ratio)); }
            return nv;
        }
    }
    /// <summary>
    /// Class Extension to interpolate 'Ray'
    /// </summary>
    internal class RayInterpolator : UnityVectorInterpolator<Ray> {
        protected override void  SetFromField()         { from = GetRay(); }
        protected override void  SetProperty(Ray p_value)  { SetRay(p_value); }
        protected override Ray LerpValue(float p_ratio) {             
            Ray nv = from;
            if((mask & VectorInterpolatorMask.Origin)   !=0) { nv.origin    += ApplyMask((to.origin-from.origin)*p_ratio); }
            if((mask & VectorInterpolatorMask.Direction)!=0) { nv.direction += ApplyMask((to.direction-from.direction)*p_ratio); }                
            return nv;
        }
    }
    /// <summary>
    /// Class Extension to interpolate 'Ray2D'
    /// </summary>
    internal class Ray2DInterpolator : UnityVectorInterpolator<Ray2D> {
        protected override void  SetFromField()         { from = GetRay2D(); }
        protected override void  SetProperty(Ray2D p_value)  { SetRay2D(p_value); }
        protected override Ray2D LerpValue(float p_ratio) {             
            Ray2D nv = from;
            if((mask & VectorInterpolatorMask.Origin)   !=0) { nv.origin    += ApplyMask((to.origin-from.origin)*p_ratio); }
            if((mask & VectorInterpolatorMask.Direction)!=0) { nv.direction += ApplyMask((to.direction-from.direction)*p_ratio); }                
            return nv;
        }
    }

    #endregion

}

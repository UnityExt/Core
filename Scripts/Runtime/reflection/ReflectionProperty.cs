using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace UnityExt.Core {

    #region enum PropertyReflectionAttribs

    /// <summary>
    /// Bit Enumeration that describes the target type/state and its property type/state.
    /// </summary>
    [Flags]
    public enum PropertyReflectionAttribs : ushort {
        /// <summary>
        /// No Flags
        /// </summary>
        None                    = 0,
        /// <summary>
        /// Overall invalid state
        /// </summary>
        Invalid                 = ((1<<0)|(1<<1)), 
        /// <summary>
        /// Target is invalid
        /// </summary>
        InvalidTarget           = (1<<0),
        /// <summary>
        /// Property is Invalid
        /// </summary>
        InvalidProperty         = (1<<1),
        /// <summary>
        /// Default Target
        /// </summary>
        TargetDefault           = (1<<2),
        /// <summary>
        /// Static Target
        /// </summary>
        TargetStatic            = (1<<3),
        /// <summary>
        /// Unity Material Target
        /// </summary>
        TargetMaterial          = (1<<4),
        /// <summary>
        /// Unity Object Target
        /// </summary>
        TargetUnity             = (1<<5),
        /// <summary>
        /// Field Property
        /// </summary>
        PropertyField           = (1<<6),
        /// <summary>
        /// Get/Set Method Property
        /// </summary>
        PropertyGetSet          = (1<<7)
    }

    #endregion

    #region ValueType Extensions

    public class SBytePropertyReflection : PropertyReflection<sbyte > { public SBytePropertyReflection (object p_target,string p_property) : base(p_target,p_property) { }  override protected sbyte  GetMaterialValue() { return (sbyte ) GetMaterialInt  (); } override protected void SetMaterialValue(sbyte  v) { SetMaterialInt  (        v); } }
    public class BytePropertyReflection  : PropertyReflection<byte  > { public BytePropertyReflection  (object p_target,string p_property) : base(p_target,p_property) { }  override protected byte   GetMaterialValue() { return (byte  ) GetMaterialInt  (); } override protected void SetMaterialValue(byte   v) { SetMaterialInt  (        v); } }
    public class UShortPropertyReflection: PropertyReflection<ushort> { public UShortPropertyReflection(object p_target,string p_property) : base(p_target,p_property) { }  override protected ushort GetMaterialValue() { return (ushort) GetMaterialInt  (); } override protected void SetMaterialValue(ushort v) { SetMaterialInt  ((int)   v); } }
    public class ShortPropertyReflection : PropertyReflection<short > { public ShortPropertyReflection (object p_target,string p_property) : base(p_target,p_property) { }  override protected short  GetMaterialValue() { return (short ) GetMaterialInt  (); } override protected void SetMaterialValue(short  v) { SetMaterialInt  (        v); } }
    public class UIntPropertyReflection  : PropertyReflection<uint  > { public UIntPropertyReflection  (object p_target,string p_property) : base(p_target,p_property) { }  override protected uint   GetMaterialValue() { return (uint  ) GetMaterialInt  (); } override protected void SetMaterialValue(uint   v) { SetMaterialInt  ((int)   v); } }
    public class IntPropertyReflection   : PropertyReflection<int   > { public IntPropertyReflection   (object p_target,string p_property) : base(p_target,p_property) { }  override protected int    GetMaterialValue() { return (int   ) GetMaterialInt  (); } override protected void SetMaterialValue(int    v) { SetMaterialInt  (        v); } }
    public class ULongPropertyReflection : PropertyReflection<ulong > { public ULongPropertyReflection (object p_target,string p_property) : base(p_target,p_property) { }  override protected ulong  GetMaterialValue() { return (ulong ) GetMaterialInt  (); } override protected void SetMaterialValue(ulong  v) { SetMaterialInt  ((int)   v); } }
    public class LongPropertyReflection  : PropertyReflection<long  > { public LongPropertyReflection  (object p_target,string p_property) : base(p_target,p_property) { }  override protected long   GetMaterialValue() { return (long  ) GetMaterialInt  (); } override protected void SetMaterialValue(long   v) { SetMaterialInt  ((int)   v); } }
    public class FloatPropertyReflection : PropertyReflection<float > { public FloatPropertyReflection (object p_target,string p_property) : base(p_target,p_property) { }  override protected float  GetMaterialValue() { return (float ) GetMaterialFloat(); } override protected void SetMaterialValue(float  v) { SetMaterialFloat(        v); } }
    public class DoublePropertyReflection: PropertyReflection<double> { public DoublePropertyReflection(object p_target,string p_property) : base(p_target,p_property) { }  override protected double GetMaterialValue() { return (double) GetMaterialFloat(); } override protected void SetMaterialValue(double v) { SetMaterialFloat((float) v); } }

    public class Vector2PropertyReflection    : PropertyReflection<UnityEngine.Vector2   > { public Vector2PropertyReflection    (object p_target,string p_property) : base(p_target,p_property) { }  override protected UnityEngine.Vector2    GetMaterialValue() { return GetMaterialVector2   (); } override protected void SetMaterialValue(UnityEngine.Vector2     v) { SetMaterialVector2    (        v); } }
    public class Vector2IntPropertyReflection : PropertyReflection<UnityEngine.Vector2Int> { public Vector2IntPropertyReflection (object p_target,string p_property) : base(p_target,p_property) { }  override protected UnityEngine.Vector2Int GetMaterialValue() { return GetMaterialVector2Int(); } override protected void SetMaterialValue(UnityEngine.Vector2Int  v) { SetMaterialVector2Int (        v); } }
    public class Vector3PropertyReflection    : PropertyReflection<UnityEngine.Vector3   > { public Vector3PropertyReflection    (object p_target,string p_property) : base(p_target,p_property) { }  override protected UnityEngine.Vector3    GetMaterialValue() { return GetMaterialVector3   (); } override protected void SetMaterialValue(UnityEngine.Vector3     v) { SetMaterialVector3    (        v); } }
    public class Vector3IntPropertyReflection : PropertyReflection<UnityEngine.Vector3Int> { public Vector3IntPropertyReflection (object p_target,string p_property) : base(p_target,p_property) { }  override protected UnityEngine.Vector3Int GetMaterialValue() { return GetMaterialVector3Int(); } override protected void SetMaterialValue(UnityEngine.Vector3Int  v) { SetMaterialVector3Int (        v); } }
    public class Vector4PropertyReflection    : PropertyReflection<UnityEngine.Vector4   > { public Vector4PropertyReflection    (object p_target,string p_property) : base(p_target,p_property) { }  override protected UnityEngine.Vector4    GetMaterialValue() { return GetMaterialVector4   (); } override protected void SetMaterialValue(UnityEngine.Vector4     v) { SetMaterialVector4    (        v); } }
    public class ColorPropertyReflection      : PropertyReflection<UnityEngine.Color     > { public ColorPropertyReflection      (object p_target,string p_property) : base(p_target,p_property) { }  override protected UnityEngine.Color      GetMaterialValue() { return GetMaterialColor     (); } override protected void SetMaterialValue(UnityEngine.Color       v) { SetMaterialColor      (        v); } }
    public class QuaternionPropertyReflection : PropertyReflection<UnityEngine.Quaternion> { public QuaternionPropertyReflection (object p_target,string p_property) : base(p_target,p_property) { }  override protected UnityEngine.Quaternion GetMaterialValue() { return GetMaterialQuaternion(); } override protected void SetMaterialValue(UnityEngine.Quaternion  v) { SetMaterialQuaternion (        v); } }
    public class RectPropertyReflection       : PropertyReflection<UnityEngine.Rect      > { public RectPropertyReflection       (object p_target,string p_property) : base(p_target,p_property) { }  override protected UnityEngine.Rect       GetMaterialValue() { return GetMaterialRect      (); } override protected void SetMaterialValue(UnityEngine.Rect        v) { SetMaterialRect       (        v); } }

    #endregion

    /// <summary>
    /// Class that wraps System.Reflection actions to get/set properties of objects, without the memory footprint.
    /// </summary>
    /// <typeparam name="T">Type of the property being sampled</typeparam>
    public class PropertyReflection<T> {

        internal delegate void  MethodSet (T v);
        internal delegate T     MethodGet ();

        private const BindingFlags ReflectionAllBindings = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.SetProperty;

        #region static 

        /// <summary>
        /// Creates a new property reflection appropriate for the given 'T' type.
        /// </summary>
        /// <returns>Created Property Reflection</returns>
        static public PropertyReflection<T> Create(object p_target,string p_property) {
            //Try ordering by probable usage frequency
            object res = null;            
            if(typeof(T) == typeof(float  ))  res = new FloatPropertyReflection (p_target,p_property);   else            
            if(typeof(T) == typeof(int    ))  res = new IntPropertyReflection   (p_target,p_property);   else
            if(typeof(T) == typeof(UnityEngine.Vector2   ))  res = new Vector2PropertyReflection    (p_target,p_property);   else
            if(typeof(T) == typeof(UnityEngine.Vector3   ))  res = new Vector3PropertyReflection    (p_target,p_property);   else
            if(typeof(T) == typeof(UnityEngine.Color     ))  res = new ColorPropertyReflection      (p_target,p_property);   else
            if(typeof(T) == typeof(UnityEngine.Quaternion))  res = new QuaternionPropertyReflection (p_target,p_property);   else            
            if(typeof(T) == typeof(UnityEngine.Vector4   ))  res = new Vector4PropertyReflection    (p_target,p_property);   else
            if(typeof(T) == typeof(UnityEngine.Rect      ))  res = new RectPropertyReflection       (p_target,p_property);   else
            if(typeof(T) == typeof(UnityEngine.Vector2Int))  res = new Vector2IntPropertyReflection (p_target,p_property);   else
            if(typeof(T) == typeof(UnityEngine.Vector3Int))  res = new Vector3IntPropertyReflection (p_target,p_property);   else                        
            if(typeof(T) == typeof(double ))  res = new DoublePropertyReflection(p_target,p_property);   else
            if(typeof(T) == typeof(long   ))  res = new LongPropertyReflection  (p_target,p_property);   else
            if(typeof(T) == typeof(uint   ))  res = new UIntPropertyReflection  (p_target,p_property);   else
            if(typeof(T) == typeof(ulong  ))  res = new ULongPropertyReflection (p_target,p_property);   else
            if(typeof(T) == typeof(byte   ))  res = new BytePropertyReflection  (p_target,p_property);   else
            if(typeof(T) == typeof(sbyte  ))  res = new SBytePropertyReflection (p_target,p_property);   else        
            if(typeof(T) == typeof(ushort ))  res = new UShortPropertyReflection(p_target,p_property);   else
            if(typeof(T) == typeof(short  ))  res = new ShortPropertyReflection (p_target,p_property);                
            //If <null> try the base property reflection otherwise return results
            return res==null ? new PropertyReflection<T>(p_target,p_property)  : (PropertyReflection<T>)res;
        }

        #region static Reflection LUT
        static private Dictionary<Type,Dictionary<string,MemberInfo[]>> m_lut_type_accessor = new Dictionary<Type, Dictionary<string, MemberInfo[]>>();
        static private Dictionary<Type,Dictionary<object,Delegate>>     m_lut_object_set    = new Dictionary<Type, Dictionary<object, Delegate    >>();
        static private Dictionary<Type,Dictionary<object,Delegate>>     m_lut_object_get    = new Dictionary<Type, Dictionary<object, Delegate    >>();
        static private Delegate GetCachedDelegate(Dictionary<Type,Dictionary<object,Delegate>>p_lut,object p_target,Type p_delegate_type,MethodInfo p_method) {            
            if(p_delegate_type == null) return null;
            if(p_method        == null) return null;            
            Dictionary<object,Delegate> tdl = null;
            Delegate                    res = null;
            bool has_key = false;
            has_key = p_lut.ContainsKey(p_delegate_type);
            if(has_key)   tdl = p_lut[p_delegate_type];
            if(tdl==null) tdl = new Dictionary<object, Delegate>();
            if(!has_key) p_lut[p_delegate_type] = tdl;
            has_key = tdl.ContainsKey(p_target);
            if(has_key) res = tdl[p_target];
            if(res == null) res = p_method.CreateDelegate(p_delegate_type,p_target is Type ? null : p_target);
            if(res != null) if(!has_key) tdl[p_target] = res;
            return res;
        }
        #endregion

        #endregion

        /// <summary>
        /// Reference to the object.
        /// </summary>
        public object target { get; private set; }

        /// <summary>
        /// Property to be accessed
        /// </summary>
        public string property { get; private set; }

        /// <summary>
        /// General Type of the target
        /// </summary>
        public PropertyReflectionAttribs flags { get; private set; }

        /// <summary>
        /// Returns a flag telling this property accessor was initialized correctly.
        /// </summary>
        public bool valid { 
            get { 
                bool f;
                f = (flags & PropertyReflectionAttribs.Invalid) != 0;
                if(f) return false;
                f = target is UnityEngine.Object;
                if(f) if(((UnityEngine.Object)target)==null) return false;
                return true;
            } 
        }

        /// <summary>
        /// Internals.
        /// </summary>
        protected MemberInfo m_rfl_accessor;
        protected Delegate   m_rfl_get;
        protected Delegate   m_rfl_set;
        protected int        m_mat_prop_id;
        protected PropertyReflectionAttribs m_target_bit;
        protected PropertyReflectionAttribs m_property_bit;

        #region CTOR

        /// <summary>
        /// CTOR.
        /// </summary>
        /// <param name="p_target"></param>
        /// <param name="p_property"></param>
        public PropertyReflection(object p_target,string p_property) {
            //Init with invalid
            flags = PropertyReflectionAttribs.InvalidTarget;
            //Skip if <null>
            if(p_target==null) return;
            //Skip if unity bound <null> check
            if(p_target is UnityEngine.Object) 
            if(((UnityEngine.Object)p_target)==null) return;
            //Assign
            target = p_target;
            //Target is valid now assume property is invalid
            flags = PropertyReflectionAttribs.InvalidProperty;
            //Assert property basics
            if(string.IsNullOrEmpty(p_property)) return;
            //Assign
            property = p_property;
            //Check target type
            //Init with default
            flags = PropertyReflectionAttribs.TargetDefault;
            if(target is Type)                 flags = PropertyReflectionAttribs.TargetStatic;   else
            if(target is UnityEngine.Material) flags = PropertyReflectionAttribs.TargetMaterial; else            
            if(target is UnityEngine.Object)   flags = PropertyReflectionAttribs.TargetUnity; 
            //Init internals
            m_rfl_accessor = null;
            m_rfl_get      = null;
            m_rfl_set      = null;
            m_mat_prop_id  = -1;

            #region Assert Material Target
            //Assert Material
            if((flags & PropertyReflectionAttribs.TargetMaterial)!=0) {
                UnityEngine.Material mt = target as UnityEngine.Material;
                //Assert if shader exists
                if(mt.shader==null)           { flags = PropertyReflectionAttribs.InvalidTarget;   UnityEngine.Debug.LogWarning($"PropertyReflection> Material [{mt.name}] does not contain a shader."); return; }
                //Assert if property exists
                if(!mt.HasProperty(property)) { flags = PropertyReflectionAttribs.InvalidProperty; UnityEngine.Debug.LogWarning($"PropertyReflection> Failed to find property [{property}] of Material [{mt.name},{mt.shader.name}]"); return; }                    
                //Property exists
                m_mat_prop_id = UnityEngine.Shader.PropertyToID(property);
                //Init shortcut bits
                SetFlagsBit();
                //Skip the System.Reflection search phase
                return;
            }
            #endregion

            //Fetch target object type if static ops the target is the Type
            bool is_static = (flags & PropertyReflectionAttribs.TargetStatic) != 0;
            Type   target_type = is_static ? ((Type)target) : target.GetType();
            //Found Member Info
            MemberInfo[] mi = null;
            //Assert property x member-info tables
            bool has_key = m_lut_type_accessor.ContainsKey(target_type);
            if(!has_key) m_lut_type_accessor[target_type] = new Dictionary<string, MemberInfo[]>();
            //Shortcut its ref
            Dictionary<string, MemberInfo[]> lut_type_property = m_lut_type_accessor[target_type];
            //Check if property has a cached member-info otherwise fetch and cache
            has_key = lut_type_property.ContainsKey(property);                                
            mi = has_key ? 
                lut_type_property[property] : 
                lut_type_property[property] = mi = target_type.GetMember(property,ReflectionAllBindings);
            
            //Assert if property exists
            bool is_accessor_valid = mi==null ? false : mi.Length>0;
            if(!is_accessor_valid) { flags = PropertyReflectionAttribs.InvalidProperty; UnityEngine.Debug.LogWarning($"PropertyReflection> Failed to find {(is_static ? "static " : "")}property [{property}] of [{target_type.FullName}]"); return; }            
            //Store property accessor
            m_rfl_accessor = mi[0];
            //Default as field property
            PropertyReflectionAttribs property_flag = PropertyReflectionAttribs.PropertyField;
            
            //If property generate specific delegates to speedup the property manipulation
            if(m_rfl_accessor.MemberType == MemberTypes.Property) {
                Type   delegate_type = null;
                object invoker     = is_static ? null : target;
                PropertyInfo pi    = (PropertyInfo)m_rfl_accessor;                
                Type         pi_t  = pi.PropertyType;                
                object invoker_obj = invoker==null ? target_type : invoker;                
                //delegate_type = m_lut_type_getter.ContainsKey(pi_t) ? m_lut_type_getter[pi_t] : null;
                delegate_type = typeof(PropertyReflection<T>.MethodGet);
                m_rfl_get = GetCachedDelegate(m_lut_object_get,invoker_obj,delegate_type,pi.GetGetMethod());                
                //delegate_type = m_lut_type_setter.ContainsKey(pi_t) ? m_lut_type_setter[pi_t] : null;                
                delegate_type = typeof(PropertyReflection<T>.MethodSet);
                m_rfl_set = GetCachedDelegate(m_lut_object_set,invoker_obj,delegate_type,pi.GetSetMethod());
                //Set the property get/set bit
                property_flag = PropertyReflectionAttribs.PropertyGetSet;
            }
            //If 'Field' then all good and if 'Property' check if get/set exists
            bool is_valid = property_flag == PropertyReflectionAttribs.PropertyField ? true : ((m_rfl_get!=null) && (m_rfl_set!=null));            
            //Skip if invalid
            if(!is_valid) {
                //Set invalid flag
                flags = PropertyReflectionAttribs.InvalidProperty;
                string log = $"PropertyReflection> Failed to fetch get/set methods from {(is_static ? "static " : "")}property [{property}] of [{target_type.FullName}]";
                #if ENABLE_IL2CPP
                log += ". IL2CPP Might have Stripped this property/field.";
                #endif
                UnityEngine.Debug.LogWarning(log);
                return;
            }
            //Set the property bit of 'flags'
            flags = flags | property_flag;            
            //Init shortcut bits
            SetFlagsBit();
        }

        /// <summary>
        /// Helper
        /// </summary>
        private void SetFlagsBit() {
            PropertyReflectionAttribs f;
            f = PropertyReflectionAttribs.TargetDefault;  if((flags & f)!=0) m_target_bit   = f;
            f = PropertyReflectionAttribs.TargetUnity;    if((flags & f)!=0) m_target_bit   = f;
            f = PropertyReflectionAttribs.TargetMaterial; if((flags & f)!=0) m_target_bit   = f;
            f = PropertyReflectionAttribs.TargetStatic;   if((flags & f)!=0) m_target_bit   = f;
            f = PropertyReflectionAttribs.PropertyField;  if((flags & f)!=0) m_property_bit = f;
            f = PropertyReflectionAttribs.PropertyGetSet; if((flags & f)!=0) m_property_bit = f;            
        }

        internal object GetFieldValue()         { FieldInfo fi = (FieldInfo)m_rfl_accessor; return fi.GetValue(m_target_bit == PropertyReflectionAttribs.TargetStatic ? null : target  ); }
        internal void   SetFieldValue(object v) { FieldInfo fi = (FieldInfo)m_rfl_accessor;        fi.SetValue(m_target_bit == PropertyReflectionAttribs.TargetStatic ? null : target,v); }
        internal bool is_field    { get { return m_property_bit == PropertyReflectionAttribs.PropertyField;  } }

        #region Material Properties 

        internal int                    GetMaterialInt()         {  UnityEngine.Material mt = target as UnityEngine.Material; return mt.GetInt   (m_mat_prop_id);    }
        internal float                  GetMaterialFloat()       {  UnityEngine.Material mt = target as UnityEngine.Material; return mt.GetFloat (m_mat_prop_id);    }        
        internal UnityEngine.Color      GetMaterialColor()       {  UnityEngine.Material mt = target as UnityEngine.Material; return mt.GetColor (m_mat_prop_id);    }
        internal UnityEngine.Vector2    GetMaterialVector2()     {  UnityEngine.Material mt = target as UnityEngine.Material; UnityEngine.Vector4 v = mt.GetVector(m_mat_prop_id); return new UnityEngine.Vector2(v.x,v.y);     }
        internal UnityEngine.Vector2Int GetMaterialVector2Int()  {  UnityEngine.Material mt = target as UnityEngine.Material; UnityEngine.Vector4 v = mt.GetVector(m_mat_prop_id); return new UnityEngine.Vector2Int((int)v.x,(int)v.y);     }
        internal UnityEngine.Vector3    GetMaterialVector3()     {  UnityEngine.Material mt = target as UnityEngine.Material; UnityEngine.Vector4 v = mt.GetVector(m_mat_prop_id); return new UnityEngine.Vector3(v.x,v.y,v.z); }
        internal UnityEngine.Vector3Int GetMaterialVector3Int()  {  UnityEngine.Material mt = target as UnityEngine.Material; UnityEngine.Vector4 v = mt.GetVector(m_mat_prop_id); return new UnityEngine.Vector3Int((int)v.x,(int)v.y,(int)v.z); }
        internal UnityEngine.Rect       GetMaterialRect()        {  UnityEngine.Material mt = target as UnityEngine.Material; UnityEngine.Vector4 v = mt.GetVector(m_mat_prop_id); return new UnityEngine.Rect(v.x,v.y,v.z,v.w); }
        internal UnityEngine.Quaternion GetMaterialQuaternion()  {  UnityEngine.Material mt = target as UnityEngine.Material; UnityEngine.Vector4 v = mt.GetVector(m_mat_prop_id); return new UnityEngine.Quaternion(v.x,v.y,v.z,v.w); }
        internal UnityEngine.Vector4    GetMaterialVector4()     {  UnityEngine.Material mt = target as UnityEngine.Material; return mt.GetVector(m_mat_prop_id); }        
        
        internal void  SetMaterialInt       (int                    v) {  UnityEngine.Material mt = target as UnityEngine.Material; mt.SetInt      (m_mat_prop_id,v);   }
        internal void  SetMaterialFloat     (float                  v) {  UnityEngine.Material mt = target as UnityEngine.Material; mt.SetFloat    (m_mat_prop_id,v);   }
        internal void  SetMaterialColor     (UnityEngine.Color      v) {  UnityEngine.Material mt = target as UnityEngine.Material; mt.SetColor    (m_mat_prop_id,v);   }
        internal void  SetMaterialVector2   (UnityEngine.Vector2    v) {  UnityEngine.Material mt = target as UnityEngine.Material; mt.SetVector   (m_mat_prop_id,new UnityEngine.Vector4(v.x,v.y,0f,0f));    }
        internal void  SetMaterialVector2Int(UnityEngine.Vector2Int v) {  UnityEngine.Material mt = target as UnityEngine.Material; mt.SetVector   (m_mat_prop_id,new UnityEngine.Vector4(v.x,v.y,0f,0f));    }
        internal void  SetMaterialVector3   (UnityEngine.Vector3    v) {  UnityEngine.Material mt = target as UnityEngine.Material; mt.SetVector   (m_mat_prop_id,new UnityEngine.Vector4(v.x,v.y,v.z,0f));   }
        internal void  SetMaterialVector3Int(UnityEngine.Vector3Int v) {  UnityEngine.Material mt = target as UnityEngine.Material; mt.SetVector   (m_mat_prop_id,new UnityEngine.Vector4(v.x,v.y,v.z,0f));   }
        internal void  SetMaterialRect      (UnityEngine.Rect       v) {  UnityEngine.Material mt = target as UnityEngine.Material; mt.SetVector   (m_mat_prop_id,new UnityEngine.Vector4(v.x,v.y,v.width,v.height)); }
        internal void  SetMaterialQuaternion(UnityEngine.Quaternion v) {  UnityEngine.Material mt = target as UnityEngine.Material; mt.SetVector   (m_mat_prop_id,new UnityEngine.Vector4(v.x,v.y,v.z,v.w)); }
        internal void  SetMaterialVector4   (UnityEngine.Vector4    v) {  UnityEngine.Material mt = target as UnityEngine.Material; mt.SetVector   (m_mat_prop_id,v);   }

        virtual protected T    GetMaterialValue() { return default(T); }
        virtual protected void SetMaterialValue(T p_value) {  }

        internal bool is_material { get { return m_target_bit   == PropertyReflectionAttribs.TargetMaterial; } }

        #endregion

        #endregion

        /// <summary>
        /// Get the value of the property.
        /// </summary>
        /// <returns>Property Value</returns>
        public T Get() { 
            if(!valid)      return default(T);            
            if(is_field)    return (T)GetFieldValue();
            if(is_material) return GetMaterialValue();
            return ((MethodGet)m_rfl_get)();
        }

        /// <summary>
        /// Set the value of the property.
        /// </summary>
        /// <param name="p_value"></param>
        public void Set(T p_value) { 
            if(!valid)      return;
            if(is_material) { SetMaterialValue(p_value); return; }
            if(is_field)    { SetFieldValue(p_value);    return; }
            ((MethodSet)m_rfl_set)(p_value);
        }

    }

}

using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace UnityExt.Core {

    #region enum ObjectMemberType
    /*
    /// <summary>
    /// Enumeration that tells which fields to sample when parsing
    /// </summary>
    [Flags]
    public enum ObjectMemberType : byte {        
        /// <summary>
        /// Public Members
        /// </summary>
        Public    = (1<<0),
        /// <summary>
        /// Private | Protected | Internal
        /// </summary>
        NonPublic = (1<<1),
        /// <summary>
        /// Field
        /// </summary>
        Field     = (1<<2),
        /// <summary>
        /// Get/Set
        /// </summary>
        GetSet  = (1<<3),
        /// <summary>
        /// All Members
        /// </summary>
        All       = Public | NonPublic | Field | GetSet
    }
    //*/
    #endregion

    #region class ParseSettings
    /// <summary>
    /// Parsing settings to guide the object parser.
    /// </summary>
    public class ParseSettings {

        #region UnitySettings
        /// <summary>
        /// Specific Parsing Settings for Unity3D context.
        /// </summary>
        static public ParseSettings UnitySettings {
            get {
                if(m_unity_settings!=null) return m_unity_settings;
                ParseSettings ps = m_unity_settings = new ParseSettings();                
                ps.ignoreNull=true;
                ps.AddSurrogate<UnityEngine.Vector2          >("x","y");
                ps.AddSurrogate<UnityEngine.Vector3          >("x","y","z");
                ps.AddSurrogate<UnityEngine.Vector4          >("x","y","z","w");
                ps.AddSurrogate<UnityEngine.Vector2Int       >("x","y");
                ps.AddSurrogate<UnityEngine.Vector3Int       >("x","y","z");
                ps.AddSurrogate<UnityEngine.Quaternion       >("x","y","z","w");
                ps.AddSurrogate<UnityEngine.Color            >("r","g","b","a");
                ps.AddSurrogate<UnityEngine.Color32          >("r","g","b","a");
                ps.AddSurrogate<UnityEngine.Rect             >("x","y","width","height");
                ps.AddSurrogate<UnityEngine.RectInt          >("x","y","width","height");
                ps.AddSurrogate<UnityEngine.RectOffset       >("left","right","top","bottom");
                ps.AddSurrogate<UnityEngine.Ray              >("origin","direction");
                ps.AddSurrogate<UnityEngine.Ray2D            >("origin","direction");
                ps.AddSurrogate<UnityEngine.Bounds           >("min","max");
                ps.AddSurrogate<UnityEngine.BoundsInt        >("min","max");
                ps.AddSurrogate<UnityEngine.BoundingSphere   >("position","radius");
                ps.AddSurrogate<UnityEngine.GradientAlphaKey >("alpha","time");
                ps.AddSurrogate<UnityEngine.GradientColorKey >("color","time");
                ps.AddSurrogate<UnityEngine.Keyframe         >("time","value","inTangent","outTangent","inWeight","outWeight","weightedMode", "tangentMode");
                ps.AddSurrogate<UnityEngine.LayerMask        >("value");
                ps.AddSurrogate<UnityEngine.Matrix4x4        >("m00","m33","m23","m13","m03","m32","m22","m02","m12","m21","m11","m01","m30","m20","m10","m31");
                ps.AddSurrogate<UnityEngine.Plane            >("normal","distance");
                ps.AddSurrogate<UnityEngine.RangeInt         >("start","length");
                ps.AddSurrogate<UnityEngine.Resolution       >("width","height","refreshRate");
                ps.AddSurrogate<UnityEngine.AnimationCurve   >("keys","preWrapMode","postWrapMode");                
                ps.AddSurrogate<UnityEngine.Gradient         >("colorKeys","alphaKeys","mode");
                return ps;
            }
        }
        static private ParseSettings m_unity_settings;
        #endregion

        /// <summary>
        /// Default settings
        /// </summary>
        static internal ParseSettings Default = new ParseSettings();
        static List<MemberInfo> m_empty_sl = new List<MemberInfo>();

        /// <summary>
        /// Reflection flags to tell which field/properties to navigate
        /// </summary>
        //public ObjectMemberType members =  ObjectMemberType.Public | ObjectMemberType.Field;

        /// <summary>
        /// Flag that tells the parser to skip evaluating fields when their value is null
        /// </summary>
        public bool ignoreNull;

        /// <summary>
        /// Internals.
        /// </summary>
        private Dictionary<Type,List<MemberInfo>> m_surrogates;

        /// <summary>
        /// CTOR.
        /// </summary>
        public ParseSettings() {
            m_surrogates = new Dictionary<Type, List<MemberInfo>>();
        }

        #region Surrogates
        /*
        public bool IsSurrogateAllowed(MemberInfo p_member) {
            MemberInfo mi = p_member;
            if(mi==null) return false;            
            FieldInfo    fi = (mi is FieldInfo   ) ? (FieldInfo   )mi : null;
            PropertyInfo pi = (mi is PropertyInfo) ? (PropertyInfo)mi : null;            
            //bool is_public       = (pi==null) ? (fi==null ? false : fi.IsPublic) : pi.GetGetMethod
            //bool allow_public    = (members & ObjectMemberType.Public   )!=0;
            //bool allow_nonpublic = (members & ObjectMemberType.NonPublic)!=0;
            //bool allow_field     = (members & ObjectMemberType.Field    )!=0;
            //bool allow_getset    = (members & ObjectMemberType.GetSet   )!=0;            
            return true;
        }
        //*/

        /// <summary>
        /// Fetches the list of surrogate members of a given type.
        /// </summary>
        /// <param name="p_type">Type to check</param>
        /// <returns>List of added surrogates</returns>
        public List<MemberInfo> GetSurrogates(Type p_type) {
            List<MemberInfo> sl = m_surrogates.ContainsKey(p_type) ? m_surrogates[p_type] : null;
            return sl==null ? m_empty_sl : sl;
        }

        /// <summary>
        /// Fetches the list of surrogate members of a given type.
        /// </summary>
        /// <typeparam name="T">Type to check</param>
        /// <returns>List of added surrogates</returns>        
        public List<MemberInfo> GetSurrogates<T>() { return GetSurrogates(typeof(T)); }

        /// <summary>
        /// Adds a surrogate field to be sampled when the struct/class isn't accessible by the code.
        /// </summary>
        /// <param name="p_type">Type to be parsed</param>
        /// <param name="p_fields">Field/Property names</param>
        public void AddSurrogate(Type p_type,params string[] p_fields) {
            if(p_type==null) return;
            List<MemberInfo> sl = m_surrogates.ContainsKey(p_type) ? m_surrogates[p_type] : null;            
            for(int i=0;i<p_fields.Length;i++) {
                MemberInfo[] ml = p_type.GetMember(p_fields[i],ObjectParser.reflection_flags);
                if(ml.Length<=0) continue;
                if(sl==null) sl = new List<MemberInfo>();
                sl.Add(ml[0]);
            }            
            if(sl!=null)m_surrogates[p_type] = sl;
        }

        /// <summary>
        /// Adds a surrogate field to be sampled when the struct/class isn't accessible by the code.
        /// </summary>
        /// <typeparam name="T">Type to be parsed</typeparam>
        /// <param name="p_fields">Field/Property names</param>                
        public void AddSurrogate<T>(params string[] p_fields) { AddSurrogate(typeof(T),p_fields); }

        /// <summary>
        /// Removes a surrogated field.
        /// </summary>
        /// <param name="p_type">Type being parsed</param>
        /// <param name="p_fields">Field names</param>
        public void RemoveSurrogate(Type p_type,params string[] p_fields) {
            List<MemberInfo> sl = m_surrogates.ContainsKey(p_type) ? m_surrogates[p_type] : null;
            if(sl==null) return;
            for(int i=0;i<p_fields.Length;i++) {
                for(int j=0;j<sl.Count;j++) if(sl[j].Name == p_fields[i]) { sl.RemoveAt(j--); break; }
            }            
            m_surrogates[p_type] = sl.Count<=0 ? null : sl;
            if(sl.Count<=0) m_surrogates.Remove(p_type);
        }

        /// <summary>
        /// Removes a surrogated field.
        /// </summary>
        /// <typeparam name="T">Type being parsed</typeparam>
        /// <param name="p_fields">Field/Property names</param>                
        public void RemoveSurrogate<T>(params string[] p_fields) { RemoveSurrogate(typeof(T),p_fields); }

        /// <summary>
        /// Removes all surrogates from the given type.
        /// </summary>
        /// <param name="p_type">Type to be cleared</param>
        public void RemoveSurrogate(Type p_type) {
            List<MemberInfo> sl = m_surrogates.ContainsKey(p_type) ? m_surrogates[p_type] : null;
            if(sl==null) return;
            sl.Clear();
            m_surrogates[p_type] = null;
            m_surrogates.Remove(p_type);
        }

        /// <summary>
        /// Removes all surrogates from the given type.
        /// </summary>
        /// <typeparam name="T">Type to be cleared</typeparam>
        public void RemoveSurrogate<T>() { RemoveSurrogate(typeof(T)); }

        /// <summary>
        /// Removes all added surrogates.
        /// </summary>
        public void ClearSurrogates() {
            //Clear all lists
            foreach(List<MemberInfo> itl in m_surrogates.Values) itl.Clear();
            //Clear the lut
            m_surrogates.Clear();
        }
        #endregion
    }
    #endregion

    #region class ObjectParserTokenLog
    /// <summary>
    /// Auxiliary class that outputs human readable information about an object being parsed.
    /// </summary>
    public class ObjectParserTokenLog : IObjectParserTokenHandler {

        /// <summary>
        /// Writer of the log.
        /// </summary>
        public TextWriter writer { get; private set; }

        /// <summary>
        /// Show new type tokens
        /// </summary>
        public bool showNewType;

        /// <summary>
        /// Show new name tokens
        /// </summary>
        public bool showNewName;

        /// <summary>
        /// Show new references tokens
        /// </summary>
        public bool showNewRef;

        /// <summary>
        /// Adds left padding at the line for depth check
        /// </summary>
        /// <param name="p_depth"></param>
        private void WriteSpacing(int p_depth) {
            for(int i=0;i<p_depth;i++) writer.Write(' ');
        }

        /// <summary>
        /// CTOR.
        /// </summary>
        /// <param name="p_writer">Writer to receive the logging.</param>
        public ObjectParserTokenLog(TextWriter p_writer) {
            writer = p_writer;
        }

        public void OnNewName(ObjectParser p_parser,int p_name_idx) {
            if(showNewName)if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("N  "); writer.Write(' '); writer.Write(p_name_idx); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.WriteLine();  }
        }

        public void OnNewReference(ObjectParser p_parser,int p_type_idx,int p_ref_idx) {
            if(showNewRef)if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("R  "); writer.Write(' '); writer.Write(p_ref_idx); writer.Write(' '); Type t = p_parser.GetType(p_type_idx);  writer.Write(t==null ? "??" : t.FullName); writer.WriteLine();  }
        }

        public void OnNewType(ObjectParser p_parser,int p_type_idx) {
            if(showNewType)if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("T  "); writer.Write(' '); writer.Write(p_type_idx); writer.Write(' '); Type t = p_parser.GetType(p_type_idx);  writer.Write(t==null ? "??" : t.FullName); writer.WriteLine();  }
        }

        public void OnPush(ObjectParser p_parser,int p_type_index,int p_name_idx,int p_ref_idx,object p_ref,bool p_struct) {
            if(writer!=null) { WriteSpacing(p_parser.depth-1); writer.Write("P  "); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx));  writer.Write(' '); writer.Write(p_ref_idx); writer.Write(' '); writer.Write(p_struct ? p_ref : p_parser.GetReference(p_ref_idx)); writer.WriteLine();  }
        }

        public void OnPop(ObjectParser p_parser,int p_type_index,int p_name_idx,int p_ref_idx,object p_ref,bool p_struct) {
            if(writer!=null) { WriteSpacing(p_parser.depth); writer.Write("p  "); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx));  writer.Write(' '); writer.Write(p_ref_idx); writer.Write(' '); writer.Write(p_struct ? p_ref : p_parser.GetReference(p_ref_idx)); writer.WriteLine();  }
        }

        public void OnReference(ObjectParser p_parser,int p_type_idx,int p_name_idx,int p_ref_idx) {
            if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("r  "); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_ref_idx); writer.Write(' '); writer.Write(p_parser.GetReference(p_ref_idx)); writer.WriteLine();  }
        }

        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,Enum     p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("E  " ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,DateTime p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("DT " ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,TimeSpan p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("TS " ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,Type     p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("TP " ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,string   p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("S  " ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,bool     p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("B  " ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,char     p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("C  " ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,sbyte    p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("I8 " ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,byte     p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("U8 " ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,short    p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("I16" ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,ushort   p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("U16" ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,long     p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("I64" ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,ulong    p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("U64" ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,int      p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("I32" ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,uint     p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("U32" ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,float    p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("F16" ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,double   p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("F32" ); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,decimal  p_value) { if(writer!=null) { WriteSpacing(p_parser.depth+1); writer.Write("I128"); writer.Write(' '); writer.Write(p_parser.GetName(p_name_idx)); writer.Write(' '); writer.Write(p_value); writer.WriteLine();  } }
    }
    #endregion

    #region interface IObjectParseTokenHandler
    /// <summary>
    /// Interface that describes an object to handle each node visit during an object graph traversal.
    /// </summary>
    public interface IObjectParserTokenHandler {

        /// <summary>
        /// Method called upon traversal start of a reference type value.
        /// </summary>
        /// <param name="p_parser">Reference to the grammar in execution.</param>
        /// <param name="p_type_idx">Type index in cache of the object</param>
        /// <param name="p_name_idx">Name index in cache of the object</param>
        /// <param name="p_ref_idx">Reference index in cache of the object</param>
        void OnPush(ObjectParser p_parser,int p_type_idx,int p_name_idx,int p_ref_idx,object p_ref,bool p_struct);

        /// <summary>
        /// Method called upon traversal completion of a reference type value.
        /// </summary>
        /// <param name="p_parser">Reference to the grammar in execution.</param>
        /// <param name="p_type_idx">Type index in cache of the object</param>
        /// <param name="p_name_idx">Name index in cache of the object</param>
        /// <param name="p_ref_idx">Reference index in cache of the object</param>
        void OnPop(ObjectParser p_parser,int p_type_idx,int p_name_idx,int p_ref_idx,object p_ref,bool p_struct);

        /// <summary>
        /// Handler called when a new type is found during traversal.
        /// </summary>
        /// <param name="p_parser">Reference to the grammar in execution.</param>
        /// <param name="p_type">Type found during traversal.</param>
        void OnNewType(ObjectParser p_parser,int p_type_idx);

        /// <summary>
        /// Handler called when a new property/field name is found.
        /// </summary>
        /// <param name="p_parser">Reference to the grammar in execution.</param>
        /// <param name="p_name">Property/Field name found.</param>
        void OnNewName(ObjectParser p_parser,int p_name_idx);

        /// <summary>
        /// Handler called when a new object instance is found during traversal. Further occurrences will be treated as reference index.
        /// </summary>
        /// <param name="p_type">Type of the object</param>
        /// <param name="p_reference">Reference of the object.</param>
        void OnNewReference(ObjectParser p_parser,int p_type_idx,int p_ref_idx);

        /// <summary>
        /// Handler called when a primitive value is hit which aren't branchable.
        /// </summary>
        /// <param name="p_parser">Reference to the grammar in execution.</param>
        /// <param name="p_type"></param>
        /// <param name="p_name"></param>
        /// <param name="p_value"></param>
        //void OnValue(ObjectGrammar p_parser,int p_type_idx,int p_name_idx,object   p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,Enum     p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,DateTime p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,TimeSpan p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,Type     p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,string   p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,bool     p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,char     p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,sbyte    p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,byte     p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,short    p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,ushort   p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,long     p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,ulong    p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,int      p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,uint     p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,float    p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,double   p_value);
        void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,decimal  p_value);

        /// <summary>
        /// Handler called when a cached reference is visited thus no need to branch and only its already existing value should be used.
        /// </summary>
        /// <param name="p_parser">Reference to the grammar in execution.</param>
        /// <param name="p_type"></param>
        /// <param name="p_name"></param>
        /// <param name="p_value"></param>
        void OnReference(ObjectParser p_parser,int p_type_idx,int p_name_idx,int p_ref_idx);

    }
    #endregion

    /// <summary>
    /// Attribute to tag a field/property as serializable
    /// </summary>
    public class SerializableField : Attribute { }

    /// <summary>
    /// Class that implements C# object's internal structure navigation.
    /// </summary>
    public class ObjectParser : IObjectParserTokenHandler {

        #region enum TypeCategory
        /// <summary>
        /// Enumeration describing the general classification of a type
        /// </summary>
        [Flags]        
        internal enum TypeCategory : byte {
            Invalid   = 0x0,
            Primitive = 0x1,
            Struct    = 0x2,
            Reference = 0x4,
            ReferenceMask = 0x2|0x4
        }
        #endregion

        #region class StackBuffer
        /// <summary>
        /// Helper class to store values to assign into field/property/collections.
        /// </summary>
        internal class StackBuffer {
            internal object[] values;
            internal int      valueCount;
            internal long     iterator;
            internal StackBuffer() {
                values      = new object[2];
                valueCount  = 0;
                iterator    = 0;
            }
            internal void Clear      () { valueCount=0; iterator=0; }
            internal void ClearValues() { valueCount=0; }
            internal void Add(object p_value) { values[valueCount]=p_value;valueCount++; }
        }
        #endregion

        #region static 

        /// <summary>
        /// Default settings applied to all parsers.
        /// </summary>
        static public ParseSettings DefaultSettings = new ParseSettings();

        /// <summary>
        /// CTOR.
        /// </summary>
        static ObjectParser() { 
            InitializeReflectionCache();
        }

        #region Reflection Cache
        /// <summary>
        /// Internals
        /// </summary>        
        internal const BindingFlags                             reflection_flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.GetProperty;
        static MemberInfo[]                                     m_type_members_empty;
        static Dictionary<Type,TypeCategory>                    m_type_mode_lut;        
        static Dictionary<Type,bool>                            m_type_enum_lut;        
        static Dictionary<Type,MemberInfo[]>                    m_type_fields_lut;        
        static Dictionary<Type,MemberInfo[]>                    m_type_properties_lut;        
        static Dictionary<PropertyInfo,int>                     m_property_param_length_lut;    
        static Dictionary<Type,bool>                            m_type_attrib_field_defined_lut;
        static Dictionary<Type,bool>                            m_type_attrib_data_defined_lut;
        static Dictionary<Type,Dictionary<string,MemberInfo[]>> m_type_name_member_lut;
        static Dictionary<Type,MethodInfo[]>                    m_type_addmethod_lut;
        static Dictionary<Type,ConstructorInfo>                 m_type_ctor_lut;
        static Dictionary<MemberInfo,SerializableField>         m_member_attrib_lut;
        static Dictionary<MemberInfo,bool>                      m_member_attrib_field_defined_lut;        
        static Type m_attrib_field_type;

        /// <summary>
        /// Static CTOR
        /// </summary>
        static private void InitializeReflectionCache() {
            m_type_members_empty      = new MemberInfo[0];
            m_type_mode_lut           = new Dictionary<Type, TypeCategory>();
            m_type_enum_lut           = new Dictionary<Type, bool>();
            m_member_attrib_lut       = new Dictionary<MemberInfo, SerializableField>();
            m_type_fields_lut         = new Dictionary<Type, MemberInfo[]>();
            m_type_properties_lut     = new Dictionary<Type, MemberInfo[]>();
            m_type_addmethod_lut      = new Dictionary<Type, MethodInfo[]>();
            m_property_param_length_lut = new Dictionary<PropertyInfo, int>();
            m_member_attrib_field_defined_lut = new Dictionary<MemberInfo, bool>();
            m_type_name_member_lut    = new Dictionary<Type, Dictionary<string, MemberInfo[]>>();
            m_type_attrib_data_defined_lut  = new Dictionary<Type, bool>();
            m_type_attrib_field_defined_lut = new Dictionary<Type, bool>();     
            m_type_ctor_lut           = new Dictionary<Type, ConstructorInfo>();
            m_attrib_field_type  = typeof(SerializableField); 
        }

        /// <summary>
        /// Returns the list of members of given type
        /// </summary>
        /// <param name="p_target"></param>
        /// <returns></returns>
        static MemberInfo[] GetMembersCached(Type p_target,MemberTypes p_type) {                        
            Dictionary<Type,MemberInfo[]> d = p_type == MemberTypes.Field ? m_type_fields_lut : m_type_properties_lut;                
            if(d.ContainsKey(p_target)) return d[p_target];
            MemberInfo[] v = m_type_members_empty;
            if(p_type == MemberTypes.Field)    v = p_target.GetFields    (reflection_flags); else
            if(p_type == MemberTypes.Property) v = p_target.GetProperties(reflection_flags);            
            d[p_target] = v;
            return v;
        }

        /// <summary>
        /// Returns a parameterless constructor for the given type or null.
        /// </summary>
        /// <param name="p_target">Type to be instantiated</param>
        /// <returns>Constructor with no parameters</returns>
        static ConstructorInfo GetConstructorCached(Type p_target) {
            Dictionary<Type,ConstructorInfo> d = m_type_ctor_lut;
            if(d.ContainsKey(p_target)) return d[p_target];
            ConstructorInfo[] ctor_list = p_target.GetConstructors(reflection_flags);
            ConstructorInfo   ctor      = null;
            for(int j=0;j<ctor_list.Length;j++) {
                ParameterInfo[] ctor_pl = ctor_list[j].GetParameters();
                if(ctor_pl.Length>0) continue;
                ctor = ctor_list[j];
                break;
            }
            d[p_target] = ctor;
            return ctor;
        }

        /// <summary>
        /// Returns if a given attribute is defined in a member
        /// </summary>
        /// <param name="p_target"></param>
        /// <returns></returns>
        static private SerializableField GetAttribCached(MemberInfo p_target) {                        
            Dictionary<MemberInfo,SerializableField> d = m_member_attrib_lut;
            if(d.ContainsKey(p_target)) return d[p_target];
            SerializableField v = (SerializableField)p_target.GetCustomAttribute(m_attrib_field_type);
            d[p_target] = v;
            return v;            
        }        
        
        /// <summary>
        /// Returns if a given attribute instance is defined in the type
        /// </summary>
        /// <param name="p_target"></param>
        /// <returns></returns>
        static private bool IsSerializableDefined(Type p_target) {                        
            Dictionary<Type,bool> d = m_type_attrib_field_defined_lut;
            if(d.ContainsKey(p_target)) return d[p_target];
            bool f = p_target.IsDefined(m_attrib_field_type,true);
            d[p_target] = f;
            return f;            
        }

        /// <summary>
        /// Returns if a given attribute instance is defined in the type member
        /// </summary>
        /// <param name="p_target"></param>
        /// <returns></returns>
        static private bool IsSerializableDefined(MemberInfo p_target) {                        
            Dictionary<MemberInfo,bool> d = m_member_attrib_field_defined_lut;
            if(d.ContainsKey(p_target)) return d[p_target];
            bool f = p_target.IsDefined(m_attrib_field_type,true);
            d[p_target] = f;
            return f;            
        }

        /// <summary>
        /// Returns the list of members for a field defined by the name passed
        /// </summary>
        /// <param name="p_target"></param>
        /// <param name="p_name"></param>
        /// <returns></returns>
        static MemberInfo[] GetMembersByNameCached(Type p_target,string p_name) {
            Dictionary<Type,Dictionary<string,MemberInfo[]>> d0 = m_type_name_member_lut;
            if(!d0.ContainsKey(p_target)) d0[p_target] = new Dictionary<string, MemberInfo[]>();
            Dictionary<string,MemberInfo[]> d1 = d0[p_target];
            if(d1.ContainsKey(p_name)) return d1[p_name];
            MemberInfo[] v = p_target.GetMember(p_name, MemberTypes.Field | MemberTypes.Property,reflection_flags);
            d1[p_name] = v;
            return v;
        }

        /// <summary>
        /// Returns access to the 'Add' method of possibles ICollection<T>
        /// </summary>
        /// <param name="p_target"></param>
        /// <param name="p_name"></param>
        /// <returns></returns>
        static MethodInfo[] GetCollectionAddMethodCached(Type p_target) {
            Dictionary<Type,MethodInfo[]> d0 = m_type_addmethod_lut;            
            if(d0.ContainsKey(p_target)) return d0[p_target];
            MethodInfo[] add_methods = new MethodInfo[3];            
            add_methods[0] = p_target.GetMethod("Enqueue");
            add_methods[1] = p_target.GetMethod("Push"); 
            add_methods[2] = p_target.GetMethod("Add");
            d0[p_target] = add_methods;
            return add_methods;
        }
        

        /// <summary>
        /// Returns the general mode classification of the type, relevant to the serialization
        /// </summary>
        /// <param name="p_type"></param>
        /// <returns></returns>
        static TypeCategory GetTypeCategoryCached(Type p_type) {
            Type type = p_type;
            //invalid
            if(type==null) return TypeCategory.Invalid;            
            Dictionary<Type,TypeCategory> d = m_type_mode_lut;
            if(d.ContainsKey(p_type)) return d[type];
            TypeCategory f = TypeCategory.Invalid;
            //enum
            if(IsEnumCached(type))      f = TypeCategory.Primitive; else
            //struct or value-type
            if(type.IsValueType)        f = type.IsPrimitive ? TypeCategory.Primitive : TypeCategory.Struct; else            
            //strings
            if(type == typeof(string))  f = TypeCategory.Primitive; else            
            //class reference
                                        f = TypeCategory.Reference;

            //Special cases where we read/write them as primitive types            
            if(p_type == typeof(DateTime   )) f = TypeCategory.Primitive; else            
            if(p_type == typeof(TimeSpan   )) f = TypeCategory.Primitive; else
            if(p_type == typeof(decimal    )) f = TypeCategory.Primitive; else
            if(p_type.Name == "RuntimeType")  f = TypeCategory.Primitive;

            d[type] = f;
            return f;        
        }

        /// <summary>
        /// Returns if a type is an enum and cache the result.
        /// </summary>
        /// <param name="p_type"></param>
        /// <returns></returns>
        static bool IsEnumCached(Type p_type) {
            Dictionary<Type,bool> d =  m_type_enum_lut;
            if(d.ContainsKey(p_type)) return d[p_type];
            bool f = p_type.IsEnum;
            d[p_type] = f;
            return f;            
        }

        /// <summary>
        /// Helper to check if a type is a reference based one.
        /// </summary>                
        static bool IsReferenceType(TypeCategory p_type_cat) {            
            return (p_type_cat & TypeCategory.ReferenceMask)!=0;
        }

        /// <summary>
        /// Returns the number of indexed parameters length cached.
        /// </summary>
        /// <param name="p_target"></param>
        /// <returns></returns>
        static int GetIndexParametersCached(PropertyInfo p_target) {
            Dictionary<PropertyInfo,int> d = m_property_param_length_lut;
            if(d.ContainsKey(p_target)) return d[p_target];
            ParameterInfo[] pil = p_target.GetIndexParameters();
            d[p_target] = pil.Length;
            return pil.Length;
        }
        #endregion

        #endregion

        /// <summary>
        /// Currently stacked object.
        /// </summary>
        public object current { get; private set; }

        /// <summary>
        /// Parent of currently stacked object.
        /// </summary>
        public object parent { get; private set; }

        /// <summary>
        /// Traversal depth on current exection.
        /// </summary>
        public int depth { get { return m_stack==null ? 0 : m_stack.Count; } }

        /// <summary>
        /// Number of tokens processed.
        /// </summary>
        public int tokens { get; private set; }

        /// <summary>
        /// Reference to the parse settings.
        /// </summary>
        public ParseSettings settings {
            get { return m_settings; }
            set { m_settings = value; }
        }
        private ParseSettings m_settings;

        /// <summary>
        /// Internals
        /// </summary>        
        private ParseSettings parse_settings { get { return m_settings==null ? (DefaultSettings==null ? ParseSettings.Default : DefaultSettings) : m_settings; } }
        private Stack<object>               m_stack;        
        private List<StackBuffer>           m_stack_buffer;
        private int                         m_stack_buffer_top;
        private List<Type>                  m_types;
        private List<string>                m_names;
        private List<object>                m_refs;
        private Dictionary<object,int>      m_refs_index;          
        private IObjectParserTokenHandler   m_handler;
        private object                      m_read_result;

        #region CTOR
        /// <summary>
        /// CTOR.
        /// </summary>
        /// <param name="p_target">Object to traverse.</param>
        public ObjectParser() {            
        }
        #endregion

        #region Auxiliary        
        /// <summary>
        /// Helper to check is a given type is an enum
        /// </summary>
        /// <param name="p_type_idx">Type index to check</param>
        /// <returns></returns>
        public bool IsEnum(int p_type_idx) {
            Type vt = GetType(p_type_idx);
            return IsEnumCached(vt);
        }
        #endregion

        #region Write
        /// <summary>
        /// Reads the target object and executes the parsing steps for all property/field and children in hierarchy.
        /// </summary>
        /// <param name="p_object">Object to read</param>
        /// <param name="p_capacity">Internal buffers capacity</param>
        /// <param name="p_handler">Object to handle the traversal events.</param>
        public void Write(object p_object,int p_capacity,IObjectParserTokenHandler p_handler=null) {
            if(p_object==null) return;            
            //Temp lists for caching information
            m_stack = new Stack<object>(p_capacity/2);
            m_types = new List<Type>   (p_capacity/10);
            m_names = new List<string> (p_capacity/10);
            m_refs  = new List<object> (p_capacity);
            m_refs_index = new Dictionary<object, int>(p_capacity);
            //Initialize internals
            m_types.Add(null);            
            m_names.Add(null);
            m_names.Add("");
            m_refs.Add(null);            
            m_handler = p_handler;            
            Evaluate(p_object.GetType(),"$",p_object);
            //Clear things up
            Dispose();           
        }

        /// <summary>
        /// Reads the target object and executes the parsing steps for all property/field and children in hierarchy.
        /// </summary>
        /// <param name="p_object">Object to read</param>        
        /// <param name="p_handler">Object to handle the traversal events.</param>
        public void Write(object p_object,IObjectParserTokenHandler p_handler = null) { Write(p_object,200,p_handler); }
        #endregion

        #region Read
        /// <summary>
        /// Begins a read procedure and waits for the tokens containing the object's composition.
        /// </summary>
        public void ReadBegin(int p_capacity=200) {
            //Temp lists for caching information
            m_stack        = new Stack<object>(p_capacity/2);            
            m_stack_buffer = new List<StackBuffer>(p_capacity/2);
            m_types        = new List<Type>   (p_capacity/10);
            m_names        = new List<string> (p_capacity/10);
            m_refs         = new List<object> (p_capacity);
            //When reading we assume references are all defined beforehand
            m_refs_index   = null;//new Dictionary<object, int>(p_capacity);            
            m_stack_buffer_top = 0;
            m_read_result  = null;
            //Initialize internals
            m_types.Add(null);            
            m_names.Add(null);
            m_names.Add("");
            m_refs.Add(null);
        }

        /// <summary>
        /// Completes the read procedure and returns the result.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T ReadEnd<T>() {
            T res = (T)m_read_result;
            Dispose();
            return res;
        }
        #endregion

        #region Stack
        /// <summary>
        /// Pushes an object for traversal and field/property/collection analysis.
        /// </summary>        
        private void Push(int p_type_idx,int p_name_idx,int p_ref_idx,object p_ref,bool p_struct) {
            //Locals
            Type   v_type = GetType(p_type_idx);
            string v_name = GetName(p_name_idx);
            object v      = p_struct ? p_ref : GetReference(p_ref_idx);
            //Stack management
            parent  = m_stack.Count<=0 ? null : m_stack.Peek();
            current = v;
            m_stack.Push(v);
            OnPush(this,p_type_idx,p_name_idx,p_ref_idx,v,p_struct);
            if(m_handler!=null) m_handler.OnPush(this,p_type_idx,p_name_idx,p_ref_idx,v,p_struct);
            //Evaluate Members
            MemberInfo[]     mia = null;
            List<MemberInfo> mil = null;
            //Evaluate Fields
            mia = GetMembersCached(v_type, MemberTypes.Field);
            for(int i=0;i<mia.Length;i++) EvaluateMember(mia[i],true,p_ref_idx,p_ref,p_struct);            
            //Evaluate Properties
            mia = GetMembersCached(v_type, MemberTypes.Property);
            for(int i=0;i<mia.Length;i++) EvaluateMember(mia[i],true,p_ref_idx,p_ref,p_struct);
            //Evaluate Surrogates
            mil = parse_settings.GetSurrogates(v_type);
            for(int i=0;i<mil.Count; i++) EvaluateMember(mil[i],false,p_ref_idx,p_ref,p_struct);
            //Evaluate if collection
            if(v is IDictionary) EvaluateCollection((IDictionary)v,p_type_idx,p_name_idx,p_ref_idx); else
            if(v is IEnumerable) EvaluateCollection((IEnumerable)v,p_type_idx,p_name_idx,p_ref_idx);                        
        }
        

        /// <summary>
        /// Returns to the previous processing state.
        /// </summary>
        /// <param name="p_depth">Depth currently returning.</param>
        private void Pop(int p_type_idx,int p_name_idx,int p_ref_idx,bool p_struct) {                        
            //Stack management
            current = m_stack.Pop();
            parent  = m_stack.Count<=0 ? null : m_stack.Peek();
            OnPop(this,p_type_idx,p_name_idx,p_ref_idx,current,p_struct);
            if(m_handler!=null) m_handler.OnPop(this,p_type_idx,p_name_idx,p_ref_idx,current,p_struct);
        }
        
        /// <summary>
        /// Stack push a cached instance
        /// </summary>
        /// <param name="p_ref_idx"></param>
        public void Push(int p_ref_idx) {
            object v = GetReference(p_ref_idx);
            if(v == null) return;            
            //Stack the reference
            m_stack.Push(current);
            //Stack management
            parent  = current;
            current = v;         
            //If result is null first push is the container object
            if(m_read_result==null)m_read_result = v;
            //Stack the value buffer to accumulate and apply
            //StackBuffer data is also memory pooled
            if(m_stack_buffer_top >= m_stack_buffer.Count) m_stack_buffer.Add(new StackBuffer());            
            m_stack_buffer[m_stack_buffer_top].Clear();            
            m_stack_buffer_top++;
        }
        
        /// <summary>
        /// Pops the current state and apply the result on its parent.
        /// </summary>
        /// <param name="p_name_idx"></param>
        public void Pop(int p_name_idx) {
            //If stack is empty should not proceed
            if(m_stack.Count <= 0) { return; }
            //Result
            object res = current;
            //Stack management
            current = m_stack.Pop();           
            parent  = m_stack.Count<=0 ? null : m_stack.Peek();            
            //Pop the buffer stack
            m_stack_buffer_top = Math.Max(m_stack_buffer_top-1,0);
            //Assign the result into parent container (pop -> current)
            Assign(p_name_idx,res);
        }        
        #endregion

        #region Assign
        /// <summary>
        /// Assigns the a value to the current stack state and apply into the associated name.
        /// </summary>
        /// <param name="p_target"></param>
        /// <param name="p_name_idx"></param>
        public void Assign(int p_name_idx,object p_value) {
            object      v        = current;
            StackBuffer v_buffer = m_stack_buffer_top<=0 ? null : m_stack_buffer[m_stack_buffer_top-1];
            string      v_name   = GetName(p_name_idx);            
            //Adds the result into the stack buffer
            if(v_buffer!=null)v_buffer.Add(p_value);
            //Keep adding until 2 for dictionaries
            if(v is IDictionary) if(v_buffer.valueCount<2) return;
            //If no name 'current' is a Collection
            if(string.IsNullOrEmpty(v_name)) {
                if(v is IDictionary) { AssignDictionary();  } else
                if(v is IEnumerable) { AssignCollection();  }                
            }
            //If 'name' then its a field/property
            else 
            if(v!=null) {                                
                Type ctn_type = v.GetType();
                MemberInfo[] mi_list = GetMembersByNameCached(ctn_type,v_name);
                if(mi_list.Length>0) { 
                    MemberInfo mi = mi_list[0];
                    if(mi is PropertyInfo) { PropertyInfo f = (PropertyInfo)mi; f.SetValue(v,v_buffer.values[0]);  }
                    if(mi is FieldInfo   ) { FieldInfo    f = (FieldInfo   )mi; f.SetValue(v,v_buffer.values[0]);  }                
                }
                else {
                    //could be an error but if the type changed overtime and the field isn't necessary it will be skipped safely
                    //throw new Exception($"Current Target of Type {ctn_type.Name} has no property [{v_name}] | Block {tokens}");                     
                }
            }
            //Clear for next operations
            if(v_buffer!=null)v_buffer.ClearValues();
            //If result is null there was no first push, thus data is a primitive
            if(m_read_result==null)m_read_result = p_value;
        }

        /// <summary>
        /// Assigns the a value to the current stack state.
        /// </summary>
        /// <param name="p_value"></param>
        public void Assign(object p_value) { Assign(0,p_value); }

        /// <summary>
        /// Assigns the buffer information in the active dictionary
        /// </summary>
        /// <param name="p_collection"></param>
        private void AssignCollection() {
            //Locals            
            IEnumerable ctn = (IEnumerable)current;
            if(ctn==null) return;      
            StackBuffer stack = m_stack_buffer[m_stack_buffer_top-1];
            int ctn_index = (int)stack.iterator;
            //Increment the iterator
            stack.iterator++;
            //Value to assign
            object v = stack.values[0];
            //Flag to handle either Array or List
            bool   is_array = ctn is Array;            
            //Cast the array to speed up iteration
            if(is_array) {
                if(ctn is byte     []) {byte     [] arr = (byte     [])ctn; arr[ctn_index] = (byte     )v;} else
                if(ctn is bool     []) {bool     [] arr = (bool     [])ctn; arr[ctn_index] = (bool     )v;} else                
                if(ctn is int      []) {int      [] arr = (int      [])ctn; arr[ctn_index] = (int      )v;} else
                if(ctn is uint     []) {uint     [] arr = (uint     [])ctn; arr[ctn_index] = (uint     )v;} else
                if(ctn is float    []) {float    [] arr = (float    [])ctn; arr[ctn_index] = (float    )v;} else
                if(ctn is string   []) {string   [] arr = (string   [])ctn; arr[ctn_index] = (string   )v;} else
                if(ctn is object   []) {object   [] arr = (object   [])ctn; arr[ctn_index] = (object   )v;} else
                if(ctn is long     []) {long     [] arr = (long     [])ctn; arr[ctn_index] = (long     )v;} else
                if(ctn is short    []) {short    [] arr = (short    [])ctn; arr[ctn_index] = (short    )v;} else
                if(ctn is char     []) {char     [] arr = (char     [])ctn; arr[ctn_index] = (char     )v;} else
                if(ctn is sbyte    []) {sbyte    [] arr = (sbyte    [])ctn; arr[ctn_index] = (sbyte    )v;} else
                if(ctn is ushort   []) {ushort   [] arr = (ushort   [])ctn; arr[ctn_index] = (ushort   )v;} else                
                if(ctn is ulong    []) {ulong    [] arr = (ulong    [])ctn; arr[ctn_index] = (ulong    )v;} else
                if(ctn is DateTime []) {DateTime [] arr = (DateTime [])ctn; arr[ctn_index] = (DateTime )v;} else
                if(ctn is TimeSpan []) {TimeSpan [] arr = (TimeSpan [])ctn; arr[ctn_index] = (TimeSpan )v;} else                                
                if(ctn is Type     []) {Type     [] arr = (Type     [])ctn; arr[ctn_index] = (Type     )v;} else                                
                if(ctn is decimal  []) {decimal  [] arr = (decimal  [])ctn; arr[ctn_index] = (decimal  )v;} else
                                       {Array       arr = (Array)ctn;       arr.SetValue(v,ctn_index);    }
            }
            else
            if(ctn is IList) {
                if(ctn is List<byte     >) {List<byte     > l = (List<byte     >)ctn; l.Add((byte     )v);} else
                if(ctn is List<bool     >) {List<bool     > l = (List<bool     >)ctn; l.Add((bool     )v);} else                
                if(ctn is List<int      >) {List<int      > l = (List<int      >)ctn; l.Add((int      )v);} else
                if(ctn is List<uint     >) {List<uint     > l = (List<uint     >)ctn; l.Add((uint     )v);} else
                if(ctn is List<float    >) {List<float    > l = (List<float    >)ctn; l.Add((float    )v);} else
                if(ctn is List<string   >) {List<string   > l = (List<string   >)ctn; l.Add((string   )v);} else
                if(ctn is List<object   >) {List<object   > l = (List<object   >)ctn; l.Add((object   )v);} else
                if(ctn is List<long     >) {List<long     > l = (List<long     >)ctn; l.Add((long     )v);} else
                if(ctn is List<short    >) {List<short    > l = (List<short    >)ctn; l.Add((short    )v);} else
                if(ctn is List<char     >) {List<char     > l = (List<char     >)ctn; l.Add((char     )v);} else
                if(ctn is List<sbyte    >) {List<sbyte    > l = (List<sbyte    >)ctn; l.Add((sbyte    )v);} else
                if(ctn is List<ushort   >) {List<ushort   > l = (List<ushort   >)ctn; l.Add((ushort   )v);} else                
                if(ctn is List<ulong    >) {List<ulong    > l = (List<ulong    >)ctn; l.Add((ulong    )v);} else
                if(ctn is List<DateTime >) {List<DateTime > l = (List<DateTime >)ctn; l.Add((DateTime )v);} else
                if(ctn is List<TimeSpan >) {List<TimeSpan > l = (List<TimeSpan >)ctn; l.Add((TimeSpan )v);} else                                
                if(ctn is List<Type     >) {List<Type     > l = (List<Type     >)ctn; l.Add((Type     )v);} else                                
                if(ctn is List<decimal  >) {List<decimal  > l = (List<decimal  >)ctn; l.Add((decimal  )v);} else
                                           {IList           l = (IList)ctn;           l.Add(v);           }
            }
            else {                
                if(ctn is Queue) {
                    Queue ctn_q = (Queue)ctn;
                    ctn_q.Enqueue(v);
                }
                else
                if(ctn is Stack) {
                    Stack ctn_s = (Stack)ctn;
                    ctn_s.Push(v);
                }
                else {
                    Type ctn_type = ctn.GetType();                    
                    //Fetch the 3 most probable 'adding' methods for ICollection<T> -> Add,Enqueue,Push
                    MethodInfo[] add_ml = GetCollectionAddMethodCached(ctn_type);
                    MethodInfo mi=null;
                    //Use whichever is found first
                    if(add_ml[0]!=null) mi = add_ml[0]; else
                    if(add_ml[1]!=null) mi = add_ml[1]; else
                    if(add_ml[2]!=null) mi = add_ml[2]; 
                    if(mi!=null) { m_add_method_params[0] = v; mi.Invoke(ctn,m_add_method_params); }
                }                
            }
            //Increment the number of processed tokens
            tokens++;
        }
        private object[] m_add_method_params = new object[1];


        /// <summary>
        /// Assigns the buffer information in the active dictionary
        /// </summary>
        private void AssignDictionary() {
            //Locals            
            IDictionary ctn = (IDictionary)current;
            if(ctn==null) return;      
            StackBuffer stack = m_stack_buffer[m_stack_buffer_top-1];                        
            //Value to assign
            object vk = stack.values[0];
            object vo = stack.values[1];
            //Assign
            ctn[vk] = vo;
            //Increment the number of processed tokens
            tokens+=2;
        }
        #endregion

        #region Evaluation
        /// <summary>
        /// Analyses a value and handle it either its a ValueType or a ReferenceType
        /// </summary>
        /// <param name="p_type"></param>
        /// <param name="p_name"></param>
        /// <param name="p_value"></param>
        private void Evaluate(Type p_type,string p_name,object p_value) {
            //Locals
            object       v          = p_value;
            Type         v_type     = v==null ? p_type : v.GetType();
            string       v_name     = p_name;
            TypeCategory v_type_cat = GetTypeCategoryCached(v_type);
            //Assert name/type caches and fetch their indexes
            int t_type_idx = NewType(v_type,v);
            int t_name_idx = NewName(v_name);
            
            //Increment the number of processed tokens
            tokens++;

            switch(v_type_cat) {
                //If reference check if visited otherwise its an indexed reference
                case TypeCategory.Reference:
                case TypeCategory.Struct: {
                    //Assert reference for first visit (structs are always 'new')
                    int t_ref_idx = v_type_cat == TypeCategory.Reference ? GetReferenceIndex(v) : -1;
                    //If existing reference just notify                    
                    if(t_ref_idx>=0) { 
                        //If ignore null values skip
                        if(t_ref_idx==0)if(parse_settings.ignoreNull) break;
                        OnReference(this,t_type_idx,t_name_idx,t_ref_idx);
                        if(m_handler!=null) m_handler.OnReference(this,t_type_idx,t_name_idx,t_ref_idx);
                        break;
                    }
                    //If non existing then cache
                    bool is_struct = v_type_cat == TypeCategory.Struct;
                    t_ref_idx = NewReference(v_type,v,is_struct);
                    //Push and evaluate contents
                    Push(t_type_idx,t_name_idx,t_ref_idx,v,is_struct);
                    //Pop and complete traversal of the object
                    Pop (t_type_idx,t_name_idx,t_ref_idx,  is_struct);
                }
                break;
                //If primitive types (non brancheable) just evaluate its data
                case TypeCategory.Primitive: {                        
                    if(v_type == typeof(bool      ))Evaluate(t_type_idx,t_name_idx,(bool      )v); else
                    if(v_type == typeof(string    ))Evaluate(t_type_idx,t_name_idx,(string    )v); else
                    if(v_type == typeof(int       ))Evaluate(t_type_idx,t_name_idx,(int       )v); else
                    if(v_type == typeof(uint      ))Evaluate(t_type_idx,t_name_idx,(uint      )v); else
                    if(v_type == typeof(float     ))Evaluate(t_type_idx,t_name_idx,(float     )v); else
                    if(v_type == typeof(byte      ))Evaluate(t_type_idx,t_name_idx,(byte      )v); else
                    if(v_type == typeof(short     ))Evaluate(t_type_idx,t_name_idx,(short     )v); else
                    if(v_type == typeof(long      ))Evaluate(t_type_idx,t_name_idx,(long      )v); else
                    if(IsEnumCached(v_type)        )Evaluate(t_type_idx,t_name_idx,(Enum      )v); else
                    if(v_type == typeof(double    ))Evaluate(t_type_idx,t_name_idx,(double    )v); else
                    if(v_type == typeof(char      ))Evaluate(t_type_idx,t_name_idx,(char      )v); else
                    if(v_type == typeof(sbyte     ))Evaluate(t_type_idx,t_name_idx,(sbyte     )v); else
                    if(v_type == typeof(ushort    ))Evaluate(t_type_idx,t_name_idx,(ushort    )v); else
                    if(v_type == typeof(ulong     ))Evaluate(t_type_idx,t_name_idx,(ulong     )v); else
                    if(v_type == typeof(DateTime  ))Evaluate(t_type_idx,t_name_idx,(DateTime  )v); else
                    if(v_type == typeof(TimeSpan  ))Evaluate(t_type_idx,t_name_idx,(TimeSpan  )v); else
                    if(v_type == typeof(decimal   ))Evaluate(t_type_idx,t_name_idx,(decimal   )v); else 
                    if(v_type.Name == "RuntimeType")Evaluate(t_type_idx,t_name_idx,(Type      )v);
                }
                break;
            }
        }

        private void Evaluate(int p_type_idx,int p_name_idx,bool      p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }        
        private void Evaluate(int p_type_idx,int p_name_idx,int       p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }
        private void Evaluate(int p_type_idx,int p_name_idx,float     p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }
        private void Evaluate(int p_type_idx,int p_name_idx,byte      p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }
        private void Evaluate(int p_type_idx,int p_name_idx,short     p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }
        private void Evaluate(int p_type_idx,int p_name_idx,long      p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }
        private void Evaluate(int p_type_idx,int p_name_idx,Enum      p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }
        private void Evaluate(int p_type_idx,int p_name_idx,double    p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }
        private void Evaluate(int p_type_idx,int p_name_idx,char      p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }
        private void Evaluate(int p_type_idx,int p_name_idx,sbyte     p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }
        private void Evaluate(int p_type_idx,int p_name_idx,ushort    p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }
        private void Evaluate(int p_type_idx,int p_name_idx,ulong     p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }
        private void Evaluate(int p_type_idx,int p_name_idx,DateTime  p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }
        private void Evaluate(int p_type_idx,int p_name_idx,TimeSpan  p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }        
        private void Evaluate(int p_type_idx,int p_name_idx,decimal   p_value) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); }
        private void Evaluate(int p_type_idx,int p_name_idx,string    p_value) {
            //non null handle its contents
            if(p_value != null) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); return; }
            //If ignore null values skip
            if(parse_settings.ignoreNull) return;
            //if null treat as reference pointing to null
            OnReference(this,p_type_idx,p_name_idx,0); //0 == null
            if(m_handler!=null) m_handler.OnReference(this,p_type_idx,p_name_idx,0);
        }
        private void Evaluate(int p_type_idx,int p_name_idx,Type      p_value) { 
            if(p_value != null) { OnValue(this,p_type_idx,p_name_idx,p_value); if(m_handler!=null) m_handler.OnValue(this,p_type_idx,p_name_idx,p_value); return; }
            //If ignore null values skip
            if(parse_settings.ignoreNull) return;
            //if null treat as reference pointing to null
            OnReference(this,p_type_idx,p_name_idx,0); //0 == null
            if(m_handler!=null) m_handler.OnReference(this,p_type_idx,p_name_idx,0);
        }

        #region Evaluate List|Array|Collection
        /// <summary>
        /// Analyses a list and its contents.
        /// </summary>
        /// <param name="p_collection"></param>
        private void EvaluateCollection(IEnumerable p_collection,int p_type_idx,int p_name_idx,int p_ref_idx) {
            //Locals
            IEnumerable  ctn = p_collection;
            Array arr      = null;
            bool  is_array = false; 
            if(ctn is Array) {
                arr      = (Array)ctn;
                is_array = true;
                if(arr.Rank>1) { throw new Exception("Multidimensional Arrays are not supported!"); }
            }                                    
            Type v_type = GetType(p_type_idx);
            Type e_type = null;
            if(is_array) { 
                e_type = v_type.GetElementType(); 
            }
            else {
                Type[] arr_arg_types = v_type.GetGenericArguments();
                e_type = arr_arg_types.Length>0 ? arr_arg_types[0] : typeof(object);
            }    
            
            //Assert element type cache
            int e_type_idx = NewType(e_type);
            //Cast the array to speed up iteration
            if(is_array) {
                if(ctn is byte     []) InternalEvaluateArray((byte     [])ctn,e_type_idx); else
                if(ctn is bool     []) InternalEvaluateArray((bool     [])ctn,e_type_idx); else                
                if(ctn is int      []) InternalEvaluateArray((int      [])ctn,e_type_idx); else
                if(ctn is uint     []) InternalEvaluateArray((uint     [])ctn,e_type_idx); else
                if(ctn is float    []) InternalEvaluateArray((float    [])ctn,e_type_idx); else
                if(ctn is string   []) InternalEvaluateArray((string   [])ctn,e_type_idx); else
                if(ctn is object   []) InternalEvaluateArray((object   [])ctn,e_type    ); else
                if(ctn is long     []) InternalEvaluateArray((long     [])ctn,e_type_idx); else
                if(ctn is short    []) InternalEvaluateArray((short    [])ctn,e_type_idx); else
                if(ctn is char     []) InternalEvaluateArray((char     [])ctn,e_type_idx); else
                if(ctn is sbyte    []) InternalEvaluateArray((sbyte    [])ctn,e_type_idx); else
                if(ctn is ushort   []) InternalEvaluateArray((ushort   [])ctn,e_type_idx); else                
                if(ctn is ulong    []) InternalEvaluateArray((ulong    [])ctn,e_type_idx); else
                if(ctn is DateTime []) InternalEvaluateArray((DateTime [])ctn,e_type_idx); else
                if(ctn is TimeSpan []) InternalEvaluateArray((TimeSpan [])ctn,e_type_idx); else                                
                if(ctn is Type     []) InternalEvaluateArray((Type     [])ctn,e_type_idx); else                                
                if(ctn is decimal  []) InternalEvaluateArray((decimal  [])ctn,e_type_idx); else
                                       InternalEvaluateArray((Array)ctn,e_type);
            }
            else
            if(ctn is IList) {
                if(ctn is List<byte    >) InternalEvaluateList((List<byte    >)ctn,e_type_idx); else
                if(ctn is List<bool    >) InternalEvaluateList((List<bool    >)ctn,e_type_idx); else                
                if(ctn is List<int     >) InternalEvaluateList((List<int     >)ctn,e_type_idx); else
                if(ctn is List<uint    >) InternalEvaluateList((List<uint    >)ctn,e_type_idx); else
                if(ctn is List<float   >) InternalEvaluateList((List<float   >)ctn,e_type_idx); else
                if(ctn is List<string  >) InternalEvaluateList((List<string  >)ctn,e_type_idx); else
                if(ctn is List<object  >) InternalEvaluateList((List<object  >)ctn,e_type    ); else
                if(ctn is List<long    >) InternalEvaluateList((List<long    >)ctn,e_type_idx); else
                if(ctn is List<short   >) InternalEvaluateList((List<short   >)ctn,e_type_idx); else
                if(ctn is List<char    >) InternalEvaluateList((List<char    >)ctn,e_type_idx); else
                if(ctn is List<sbyte   >) InternalEvaluateList((List<sbyte   >)ctn,e_type_idx); else
                if(ctn is List<ushort  >) InternalEvaluateList((List<ushort  >)ctn,e_type_idx); else                
                if(ctn is List<ulong   >) InternalEvaluateList((List<ulong   >)ctn,e_type_idx); else
                if(ctn is List<DateTime>) InternalEvaluateList((List<DateTime>)ctn,e_type_idx); else
                if(ctn is List<TimeSpan>) InternalEvaluateList((List<TimeSpan>)ctn,e_type_idx); else                                
                if(ctn is List<Type    >) InternalEvaluateList((List<Type    >)ctn,e_type_idx); else                                
                if(ctn is List<decimal >) InternalEvaluateList((List<decimal >)ctn,e_type_idx); else
                                          InternalEvaluateList((IList)ctn,e_type);
            } 
            else {                                
                bool is_stack = (ctn is Stack) ? true : v_type.FullName.StartsWith("System.Collections.Generic.Stack");                
                InternalEvaluateCollection(ctn,e_type,is_stack);
            }
            //Increment the number of processed tokens
            
        }

        private void InternalEvaluateItem(Type p_element_type,object p_item) { object e = p_item; Type e_type = e==null ? p_element_type : e.GetType(); Evaluate(e_type,null,e); tokens++; }
        private void InternalEvaluateList      (IList ctn,Type p_element_type) { for(int i=0;i<ctn.Count; i++) { InternalEvaluateItem(p_element_type,ctn[i]         ); } }
        private void InternalEvaluateArray     (Array ctn,Type p_element_type) { for(int i=0;i<ctn.Length;i++) { InternalEvaluateItem(p_element_type,ctn.GetValue(i)); } }        
        private void InternalEvaluateCollection(IEnumerable ctn,Type p_element_type,bool p_reverse) {                  
            if(p_reverse)
            if(m_reverse_stack==null) m_reverse_stack = new Stack();
            foreach(object e in ctn) { if(p_reverse) m_reverse_stack.Push(e); else InternalEvaluateItem(p_element_type,e); }
            if(p_reverse)
            while(m_reverse_stack.Count>0) { object e = m_reverse_stack.Pop(); InternalEvaluateItem(p_element_type,e); }            
        }
        private Stack m_reverse_stack;

        #region Evaluate Array
        private void InternalEvaluateArray(object     [] ctn,Type p_element_type    ) { for(int i=0;i<ctn.Length;i++) { object   e = ctn[i]; Type e_type = e==null ? p_element_type : e.GetType(); Evaluate(p_element_type,null,e); } }
        private void InternalEvaluateArray(byte       [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { byte     e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateArray(bool       [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { bool     e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateArray(int        [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { int      e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateArray(uint       [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { uint     e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateArray(float      [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { float    e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateArray(string     [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { string   e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }        
        private void InternalEvaluateArray(long       [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { long     e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateArray(short      [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { short    e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateArray(char       [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { char     e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateArray(sbyte      [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { sbyte    e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateArray(ushort     [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { ushort   e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateArray(ulong      [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { ulong    e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateArray(DateTime   [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { DateTime e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateArray(TimeSpan   [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { TimeSpan e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateArray(Type       [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { Type     e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateArray(decimal    [] ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Length;i++) { decimal  e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        #endregion

        #region Evaluate List<>
        private void InternalEvaluateList(List<object   > ctn,Type p_element_type    ) { for(int i=0;i<ctn.Count;i++) { object   e = ctn[i]; Type e_type = e==null ? p_element_type : e.GetType(); Evaluate(p_element_type,null,e); } }
        private void InternalEvaluateList(List<byte     > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { byte     e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateList(List<bool     > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { bool     e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateList(List<int      > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { int      e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateList(List<uint     > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { uint     e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateList(List<float    > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { float    e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateList(List<string   > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { string   e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }        
        private void InternalEvaluateList(List<long     > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { long     e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateList(List<short    > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { short    e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateList(List<char     > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { char     e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateList(List<sbyte    > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { sbyte    e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateList(List<ushort   > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { ushort   e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateList(List<ulong    > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { ulong    e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateList(List<DateTime > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { DateTime e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateList(List<TimeSpan > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { TimeSpan e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateList(List<Type     > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { Type     e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        private void InternalEvaluateList(List<decimal  > ctn,int  p_element_type_idx) { for(int i=0;i<ctn.Count;i++) { decimal  e = ctn[i]; Evaluate(p_element_type_idx,0,e); } }
        #endregion

        #endregion

        #region Evaluate Dictionary
        /// <summary>
        /// Analyses a dictionary and its contents.
        /// </summary>
        /// <param name="p_collection"></param>
        private void EvaluateCollection(IDictionary p_collection,int p_type_idx,int p_name_idx,int p_ref_idx) {
            //Locals
            Type        v_type = GetType(p_type_idx);
            string      v_name = GetName(p_name_idx);
            IDictionary ctn    = p_collection;
            //List and Element types
            Type   dict_type      = ctn.GetType();            
            Type[] dict_arg_types = dict_type.GetGenericArguments();
            Type   e_key_type     = dict_arg_types.Length > 0 ? dict_arg_types[0] : null;
            Type   e_value_type   = dict_arg_types.Length > 1 ? dict_arg_types[1] : null;                                                
            int    e_key_type_idx   = NewType(e_key_type);
            int    e_value_type_idx = NewType(e_value_type);
            
            ICollection keys   = ctn.Keys;
            
            foreach(object vk in keys) {
                //Type Info (start w/ element type)
                Type   vk_type = e_key_type;
                //If value is not null use its type (in case of object)
                if(vk!=null) vk_type = vk.GetType();
                Evaluate(vk_type,null,vk);                
                //Value                            
                object vo = ctn[vk];
                //Type Info (start w/ element type)
                Type   vo_type = e_value_type;
                //If value is not null use its type (in case of object)
                if(vo!=null) vo_type = vo.GetType();
                Evaluate(vo_type,null,vo);
            }            
            //Increment the number of processed tokens
            tokens += ctn.Count*2;
        }
        #endregion

        #region Evaluate Members        
        /// <summary>
        /// Evaluates a member based on Reflection's MemberInfo
        /// </summary>        
        private void EvaluateMember(MemberInfo p_mi,bool p_need_attrib,int p_ref_idx,object p_ref,bool p_struct) { 
            object       v     = p_struct ? p_ref : GetReference(p_ref_idx);
            MemberInfo   mi    = p_mi;
            FieldInfo    mi_f  = mi is FieldInfo    ? (FieldInfo   )mi : null;
            PropertyInfo mi_p  = mi is PropertyInfo ? (PropertyInfo)mi : null;
            bool         is_rw = mi_f==null ? (mi_p.CanWrite && mi_p.CanRead) : true;
            //Member needs to be RW
            if(!is_rw) return;
            //Flag telling this field can be visited
            bool is_valid = p_need_attrib ? IsSerializableDefined(mi) : true;
            if(!is_valid) return;
            //Fetch type and value based on member type
            Type   mi_type  = null;
            string mi_name  = mi.Name;
            object mi_value = null;                
            if(mi_f!=null) { mi_type = mi_f.FieldType;    mi_value = mi_f.GetValue(v); } else
            if(mi_p!=null) { mi_type = mi_p.PropertyType; mi_value = mi_p.GetValue(v); }
            //Override using the actual object's type for more precise serialization
            if(mi_value!=null) mi_type = mi_value.GetType();
            //Evaluate the member value
            Evaluate(mi_type,mi_name,mi_value);
        }

        #endregion

        #endregion

        #region TypeCache
        /// <summary>
        /// Asserts the Type or the value.GetType() and adds to the cache for index sampling.
        /// </summary>
        /// <param name="p_type">Type of the value</param>
        /// <param name="p_value">Object being checked (will override Type)</param>
        /// <returns>Index of the cached type.</returns>
        public int NewType(Type p_type,object p_value) {
            object v      = p_value;
            Type   v_type = p_type==null ? (v==null ? null : v.GetType()) : p_type;
            if(v_type==null) return 0;
            int idx = m_types.IndexOf(v_type);
            if(idx>=0) return idx;            
            idx = m_types.Count;
            m_types.Add(v_type);                
            OnNewType(this,idx);
            if(m_handler!=null) m_handler.OnNewType(this,idx);
            return idx;
        }

        /// <summary>
        /// Asserts a type by name and throw an error if not available.
        /// </summary>
        /// <param name="p_type_name">Full qualified type name.</param>
        /// <returns>Index of the cached type.</returns>
        public int NewType(string p_type_name) {
            return NewType(Type.GetType(p_type_name,true,false));
        }

        /// <summary>
        /// Asserts the Type or the value.GetType() and adds to the cache for index sampling.
        /// </summary>
        /// <param name="p_type">Type of the value</param>
        /// <returns>Index of the registered type.</returns>
        public int NewType(Type p_type) { return NewType(p_type,null); }

        /// <summary>
        /// Given a type returns its index in the internal cache.
        /// </summary>        
        public int GetTypeIndex(Type p_type) { if(p_type==null) return 0; return m_types.IndexOf(p_type); }

        /// <summary>
        /// Returns the cached type by index.
        /// </summary>
        /// <param name="p_index">Index in the cache list.</param>
        /// <returns>Cached type</returns>
        public Type GetType(int p_index) { return p_index<0 ? null : (p_index>=m_types.Count ? null : m_types[p_index]); }
        #endregion

        #region Name Cache
        /// <summary>
        /// Asserts the property/field name and caches if new.
        /// </summary>
        /// <param name="p_type">Type of the value</param>
        /// <param name="p_value">Object being checked (will override Type)</param>
        /// <returns>Index of the cached name.</returns>
        public int NewName(string p_name) {
            if(p_name == null) return 0;
            if(p_name == ""  ) return 1;        
            int idx = m_names.IndexOf(p_name);
            if(idx>=0) return idx;            
            idx = m_names.Count;
            m_names.Add(p_name);          
            OnNewName(this,idx);
            if(m_handler!=null) m_handler.OnNewName(this,idx);
            return idx;
        }

        /// <summary>
        /// Given a name returns its index in the internal cache.
        /// </summary>        
        /// <returns>Index of the cached name.</returns>
        public int GetNameIndex(string p_name) { if(p_name==null) return 0; return m_names.IndexOf(p_name); }

        /// <summary>
        /// Returns the cached name by index.
        /// </summary>
        /// <param name="p_index">Index in the cache list.</param>
        /// <returns>Cached name</returns>
        public string GetName(int p_index) { return p_index<0 ? null : (p_index>=m_names.Count ? null : m_names[p_index]); }
        #endregion

        #region Reference Cache
        /// <summary>
        /// Analyses the type | name | value of the object and register them in the local cache structure.
        /// </summary>
        /// <param name="p_type">Value type either from Array/Dictionary or value itself.</param>
        /// <param name="p_name">Property/Field name if any</param>
        /// <param name="p_value">Objet reference.</param>
        public int NewReference(Type p_type,object p_value,bool p_struct) {         
            //When reading from a previously parsed dataset all object indexes are defined so no need to update cache
            bool use_cache = m_refs_index!=null;
            object v       = p_value;
            Type   v_type  = p_type==null ? (v==null ? null : v.GetType()) : p_type;            
            int    ref_idx = use_cache ? GetReferenceIndex(v) : -1;
            if(ref_idx >= 0) { return ref_idx; }
            int type_idx = m_types.IndexOf(v_type);
            ref_idx      = m_refs.Count;            
            //Stores the index to avoid reference loop (only when writing)
            if(use_cache) {
                object rk  = p_struct ? v_type : v;                
                m_refs_index[rk] = ref_idx;                
            }            
            //Adds the reference to the pool
            m_refs.Add(v);
            OnNewReference(this,type_idx,ref_idx);
            if(m_handler!=null) m_handler.OnNewReference(this,type_idx,ref_idx);
            return ref_idx;
        }

        /// <summary>
        /// Instantiates a new reference and caches it.
        /// </summary>
        /// <param name="p_type">Type to be instantiated</param>
        /// <param name="p_length">Length in case of collections</param>
        /// <returns>Index of the cached reference</returns>
        public int NewReference(Type p_type,int p_length=0) {
            TypeCategory v_type_cat = GetTypeCategoryCached(p_type);
            Type   v_type = p_type;
            object v_ref  = null;
            //Create the instance based on category
            switch(v_type_cat) {
                //Primitives are not references
                case TypeCategory.Primitive: break;
                //Structs behaves like value-type but are better handled as reference
                case TypeCategory.Struct: {
                    v_ref = Activator.CreateInstance(v_type);
                }
                break;
                //References are created using the constructor info
                case TypeCategory.Reference: {
                    //If array create the appropriate one w/ the provided length
                    if(v_type.IsArray) {
                        Type arr_elem_type = v_type.HasElementType ? v_type.GetElementType() : typeof(object);
                        v_ref = Array.CreateInstance(arr_elem_type,p_length);
                        break;                            
                    } 
                    //Otherwise fetch the constructor and call it
                    ConstructorInfo ctor = GetConstructorCached(v_type);
                    if(ctor==null) { throw new Exception($"Reference of Type {v_type.Name} has no parameterless constructor"); }                            
                    v_ref = ctor.Invoke(m_empty_args);
                }
                break;
            }
            return NewReference(v_type,v_ref,v_type_cat == TypeCategory.Struct);
        }
        static private object[] m_empty_args = new object[0];

        /// <summary>
        /// Instantiates a new reference and caches it.
        /// </summary>
        /// <param name="p_type">Type index to search in cache and instantiate</param>
        /// <param name="p_length">Length in case of collections</param>
        /// <returns>Index of the cached reference</returns>
        public int NewReference(int p_type_idx,int p_length = 0) {
            return NewReference(GetType(p_type_idx),p_length);
        }

        /// <summary>
        /// Give an object it returns the instance index in the reference list cache.
        /// </summary>        
        public int GetReferenceIndex(object p_value) {
            if(p_value==null)       return  0;
            //If ref hashtable is null we always add the reference into the pool
            if(m_refs_index==null)  return -1;
            Type         vt      = p_value.GetType();
            TypeCategory vt_mode = GetTypeCategoryCached(vt);
            if(vt_mode == TypeCategory.Struct) { p_value = vt; }
            object rk = p_value;
            return m_refs_index.ContainsKey(rk) ? m_refs_index[rk] : -1;
        }

        /// <summary>
        /// Returns the cached reference by index.
        /// </summary>
        /// <param name="p_index">Index in the cache list.</param>
        /// <returns>Cached reference</returns>
        public object GetReference(int p_index) { return p_index<0 ? null : (p_index>=m_refs.Count ? null : m_refs[p_index]); }
        #endregion

        #region Virtuals
        /// <summary>
        /// Method called upon traversal start of a reference type value.
        /// </summary>
        /// <param name="p_parser">Reference to the grammar in execution.</param>
        /// <param name="p_type_idx">Type index in cache of the object</param>
        /// <param name="p_name_idx">Name index in cache of the object</param>
        /// <param name="p_ref_idx">Reference index in cache of the object</param>
        virtual public void OnPush(ObjectParser p_parser,int p_type_idx,int p_name_idx,int p_ref_idx,object p_ref,bool p_struct){ }

        /// <summary>
        /// Method called upon traversal completion of a reference type value.
        /// </summary>
        /// <param name="p_parser">Reference to the grammar in execution.</param>
        /// <param name="p_type_idx">Type index in cache of the object</param>
        /// <param name="p_name_idx">Name index in cache of the object</param>
        /// <param name="p_ref_idx">Reference index in cache of the object</param>
        virtual public void OnPop(ObjectParser p_parser,int p_type_idx,int p_name_idx,int p_ref_idx,object p_ref,bool p_struct){ }

        /// <summary>
        /// Handler called when a new type is found during traversal.
        /// </summary>
        /// <param name="p_parser">Reference to the grammar in execution.</param>
        /// <param name="p_type">Type found during traversal.</param>
        virtual public void OnNewType(ObjectParser p_parser,int p_type_idx){ }

        /// <summary>
        /// Handler called when a new property/field name is found.
        /// </summary>
        /// <param name="p_parser">Reference to the grammar in execution.</param>
        /// <param name="p_name">Property/Field name found.</param>
        virtual public void OnNewName(ObjectParser p_parser,int p_name_idx){ }

        /// <summary>
        /// Handler called when a new object instance is found during traversal. Further occurrences will be treated as reference index.
        /// </summary>
        /// <param name="p_type">Type of the object</param>
        /// <param name="p_reference">Reference of the object.</param>
        virtual public void OnNewReference(ObjectParser p_parser,int p_type_idx,int p_ref_idx){ }

        /// <summary>
        /// Handler called when a primitive value is hit which aren't branchable.
        /// </summary>
        /// <param name="p_parser">Reference to the grammar in execution.</param>
        /// <param name="p_type"></param>
        /// <param name="p_name"></param>
        /// <param name="p_value"></param>
        //void OnValue(ObjectGrammar p_parser,int p_type_idx,int p_name_idx,object   p_value);
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,Enum     p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,DateTime p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,TimeSpan p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,Type     p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,string   p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,bool     p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,char     p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,sbyte    p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,byte     p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,short    p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,ushort   p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,long     p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,ulong    p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,int      p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,uint     p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,float    p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,double   p_value){ }
        virtual public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,decimal  p_value){ }

        /// <summary>
        /// Handler called when a cached reference is visited thus no need to branch and only its already existing value should be used.
        /// </summary>
        /// <param name="p_parser">Reference to the grammar in execution.</param>
        /// <param name="p_type"></param>
        /// <param name="p_name"></param>
        /// <param name="p_value"></param>
        virtual public void OnReference(ObjectParser p_parser,int p_type_idx,int p_name_idx,int p_ref_idx) { }
        #endregion

        #region void Dispose
        /// <summary>
        /// Disposes the internal structures.
        /// </summary>
        public void Dispose() {            
            if(m_stack != null) { m_stack.Clear(); m_stack = null; }
            if(m_types != null) { m_types.Clear(); m_types = null; }
            if(m_names != null) { m_names.Clear(); m_names = null; }
            if(m_refs  != null) { m_refs .Clear(); m_refs  = null; }
            if(m_reverse_stack!=null) { m_reverse_stack.Clear(); m_reverse_stack=null; }
            if(m_refs_index != null)  { m_refs_index.Clear(); m_refs_index=null; }            
            current   = parent = null;
            tokens    = 0;
            m_handler = null;
            m_stack_buffer_top = 0;            
        }
        #endregion

    }
}

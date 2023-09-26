using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections;
using System.Text;
using System.Threading.Tasks;

namespace UnityExt.Core {

    #region enum TokenType
    /// <summary>
    /// Enumeration describing the serialization token guiding the IO operation.
    /// </summary>
    [Flags]        
    internal enum TokenType : byte {            
        //No Op
        None      = (byte)'\0',
        //New Type found
        NewType   = (byte)'T',
        //New Name found
        NewName   = (byte)'N',
        //New Reference found
        NewRef    = (byte)'R',
        //Set active Type
        SetType   = (byte)'t',
        //Set active Name
        SetName   = (byte)'n',
        //Push instance into stack
        Push      = (byte)'P',
        //Pop instance from stack
        Pop       = (byte)'p',
        //Primitive Data
        Data      = (byte)'D',
        //String ending character
        StringEnd = (byte)'\0',
        //Zero Ended string
        String    = (byte)'0',
        //Byte sized data  | 8bits
        Byte1     = (byte)'1',
        //Short sized data | 16bits
        Byte2     = (byte)'2',
        //Int sized data | 32bits
        Byte4     = (byte)'4',
        //Long sized data | 64bits
        Byte8     = (byte)'8',
        //Decimal sized data | 128bits
        Byte16    = (byte)'F',
        //Reference
        Reference = (byte)'r',
        //Separator
        Separator = (byte)'\n',
        //Operation Start
        OpStart   = (byte)3
    }
    #endregion

    /// <summary>
    /// Class that implements the read/write of C# objects into streams.
    /// </summary>
    public class ObjectWriter : IObjectParserTokenHandler {

        /// <summary>
        /// Internals
        /// </summary>
        public Stream BaseStream { get; private set; }

        /// <summary>
        /// Reference to the writer object.
        /// </summary>
        public IDisposable Writer { get; private set; }

        /// <summary>
        /// Flushes the writer.
        /// </summary>
        public void Flush() {
            if(Writer is TextWriter  ) { TextWriter   w = (TextWriter  )Writer; w.Flush(); }
            if(Writer is BinaryWriter) { BinaryWriter w = (BinaryWriter)Writer; w.Flush(); }
        }

        #region CTOR
        /// <summary>
        /// Creates an object writer into a Stream as text or binary mode.
        /// </summary>
        /// <param name="p_stream">Stream to write into</param>
        /// <param name="p_text_mode">Flag telling if the objet will be written as text or binary</param>
        public ObjectWriter(Stream p_stream,bool p_text_mode) {
            BaseStream = p_stream;
            if(BaseStream  ==null)   { throw new InvalidDataException     ("Stream is <null>");       }
            if(!BaseStream.CanWrite) { throw new InvalidOperationException("Stream is not writable"); }
            m_is_text = p_text_mode;
            if(m_is_text) {
                Writer = m_writer_txt = new StreamWriter(BaseStream,Encoding.Default,1024 * 16,false);
            }
            else {
                Writer = m_writer_bin = new BinaryWriter(BaseStream);
            }
        }

        /// <summary>
        /// Creates a new object writer with a pre-defined TextWriter
        /// </summary>
        /// <param name="p_writer">Writer instance</param>
        public ObjectWriter(TextWriter p_writer) {            
            Writer = m_writer_txt = p_writer;     
            if(Writer == null)   { throw new InvalidDataException     ("Writer is <null>");       }
            m_is_text    = true;
            if(m_writer_txt is StreamWriter) { StreamWriter sw = (StreamWriter)m_writer_txt; BaseStream = sw.BaseStream; }                        
        }

        /// <summary>
        /// Creates a new object writer with a pre-defined BinaryWriter
        /// </summary>
        /// <param name="p_writer">Writer instance</param>
        public ObjectWriter(BinaryWriter p_writer) {            
            Writer = m_writer_bin = p_writer;
            if(Writer == null)   { throw new InvalidDataException     ("Writer is <null>");       }
            m_is_text    = false;
            BaseStream   = m_writer_bin.BaseStream;
        }
        #endregion

        /// <summary>
        /// Internals
        /// </summary>
        private ObjectParser  m_parser;
        private bool          m_is_text;
        private TextWriter    m_writer_txt;
        private BinaryWriter  m_writer_bin;
        private TokenType     set_operation;
        private int           set_name;
        private int           set_type;        
        private char[] m_cbuffer = new char[128];
        private byte[] m_bbuffer = new byte[8];

        #region Write
        /// <summary>
        /// Writes the contents of the object into the writer.
        /// </summary>        
        /// <param name="p_object">Object to be written</param>
        public void Write(object p_object) {     
            set_type = -1;
            set_name = -1;
            set_operation = TokenType.None;
            m_parser  = new ObjectParser();            
            m_parser.Write(p_object,this);
            m_parser.Dispose();
            Flush();
        }        
        #endregion

        #region Writer
        /// <summary>
        /// Helper for wrapping writing the data
        /// </summary>        
        private void WriteValue(Enum      v) { if(m_is_text) WriteValueStr(v); else WriteValueBin(v); }
        private void WriteValue(char      v) { if(m_is_text) WriteValueStr(v); else m_writer_bin.Write((byte)v); }
        private void WriteValue(bool      v) { if(m_is_text) WriteValueStr(v); else m_writer_bin.Write((byte)(v ? 1 : 0)); }
        private void WriteValue(sbyte     v) { if(m_is_text) WriteValueStr(v); else m_writer_bin.Write(v); }
        private void WriteValue(byte      v) { if(m_is_text) WriteValueStr(v); else m_writer_bin.Write(v); }
        private void WriteValue(short     v) { if(m_is_text) WriteValueStr(v); else m_writer_bin.Write(v); }
        private void WriteValue(ushort    v) { if(m_is_text) WriteValueStr(v); else m_writer_bin.Write(v); }
        private void WriteValue(int       v) { if(m_is_text) WriteValueStr(v); else m_writer_bin.Write(v); }
        private void WriteValue(uint      v) { if(m_is_text) WriteValueStr(v); else m_writer_bin.Write(v); }
        private void WriteValue(long      v) { if(m_is_text) WriteValueStr(v); else m_writer_bin.Write(v); }
        private void WriteValue(ulong     v) { if(m_is_text) WriteValueStr(v); else m_writer_bin.Write(v); }
        private void WriteValue(float     v) { if(m_is_text) WriteValueStr(v); else { int c=Parse.From(m_bbuffer,v); m_writer_bin.Write(m_bbuffer,0,c); } }            
        private void WriteValue(double    v) { if(m_is_text) WriteValueStr(v); else { int c=Parse.From(m_bbuffer,v); m_writer_bin.Write(m_bbuffer,0,c); } }
        private void WriteValue(decimal   v) { if(m_is_text) WriteValueStr(v); else m_writer_bin.Write(v); }
        private void WriteValue(string    v,bool z) { if(m_is_text) WriteValueStr(v,z); else WriteValueBin(v,z); }
        private void WriteValue(DateTime  v) { if(m_is_text) WriteValueStr(v); else m_writer_bin.Write(v.ToBinary()); }
        private void WriteValue(TimeSpan  v) { if(m_is_text) WriteValueStr(v); else m_writer_bin.Write(v.Ticks);      }
        private void WriteValue(Type      v) { WriteValue(v.AssemblyQualifiedName,true); }
        
        /// <summary>
        /// Helper to write the data into char[] and speed up conversion to string
        /// </summary>        
        private void WriteValueStr(Enum     v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false);  }
        private void WriteValueStr(char     v) {                                  if(m_is_text) m_writer_txt.Write(v);             else m_writer_bin.Write((byte)v);  }
        private void WriteValueStr(bool     v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false);  }
        private void WriteValueStr(sbyte    v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false);  }
        private void WriteValueStr(byte     v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false);  }
        private void WriteValueStr(short    v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false);  }
        private void WriteValueStr(ushort   v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false);  }
        private void WriteValueStr(int      v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false);  }
        private void WriteValueStr(uint     v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false);  }        
        private void WriteValueStr(long     v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false);  }
        private void WriteValueStr(ulong    v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false);  }        
        private void WriteValueStr(float    v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false);  }
        private void WriteValueStr(double   v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false);  }        
        private void WriteValueStr(decimal  v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false);  }
        private void WriteValueStr(string   v,bool z) { if(m_is_text) { m_writer_txt.Write(v); if(z) m_writer_txt.Write((char)TokenType.StringEnd); } else WriteValueBin(v,z); }
        private void WriteValueStr(DateTime v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false); }
        private void WriteValueStr(TimeSpan v) { int c = Parse.From(m_cbuffer,v); if(m_is_text) m_writer_txt.Write(m_cbuffer,0,c); else WriteValueBin(m_cbuffer,c,false); }
        
        /// <summary>
        /// Helper to write enum as its numerical value.
        /// </summary>        
        private void WriteValueBin(Enum v) {
            Type e_type = Enum.GetUnderlyingType(v.GetType());
            if(e_type == typeof(sbyte   )) m_writer_bin.Write((sbyte   )(object)v); else
            if(e_type == typeof(byte    )) m_writer_bin.Write((byte    )(object)v); else
            if(e_type == typeof(short   )) m_writer_bin.Write((short   )(object)v); else
            if(e_type == typeof(ushort  )) m_writer_bin.Write((ushort  )(object)v); else
            if(e_type == typeof(int     )) m_writer_bin.Write((int     )(object)v); else
            if(e_type == typeof(uint    )) m_writer_bin.Write((uint    )(object)v); else
            if(e_type == typeof(long    )) m_writer_bin.Write((long    )(object)v); else
            if(e_type == typeof(ulong   )) m_writer_bin.Write((ulong   )(object)v); else
            if(e_type == typeof(decimal )) m_writer_bin.Write((decimal )(object)v); else
            m_writer_bin.Write((byte)0);
        }

        /// <summary>
        /// Helper to write a 0 ended string
        /// </summary>        
        private void WriteValueBin(string v,bool z) {
            if(v!=null) for(int i=0;i<v.Length;i++) m_writer_bin.Write((byte)v[i]);
            if(z)m_writer_bin.Write((byte)TokenType.StringEnd);
        }

        private void WriteValueBin(char[] v,int c,bool z) {
            if(v==null) return;
            for(int i=0;i<c;i++) m_writer_bin.Write((byte)v[i]);            
            if(z)m_writer_bin.Write((byte)TokenType.StringEnd);
        }

        /// <summary>
        /// Write the operation flag
        /// </summary>        
        private void WriteOperation(TokenType p_operation) {            
            bool will_apply = false;
            //In binary mode always write the op token            
            switch(p_operation) {
                case TokenType.Data:
                case TokenType.Reference: {
                    if(m_is_text)if(set_operation == p_operation) break;
                    set_operation = p_operation;
                    will_apply=true;
                }
                break;
                default: will_apply=true; break;                
            }
            if(!will_apply) return;
            if(m_is_text)WriteValue((char)TokenType.OpStart);
            WriteValue((char)p_operation);
        }
        
        /// <summary>
        /// Writes the next active name
        /// </summary>
        /// <param name="p_idx"></param>
        private void WriteName(int p_idx) {
            if(set_name == p_idx) return;            
            set_name = p_idx;
            WriteOperation(TokenType.SetName);
            WriteValueStr(p_idx);
            WriteValue((char)TokenType.Separator);            
        }

        /// <summary>
        /// Writes the next active type
        /// </summary>
        /// <param name="p_idx"></param>
        private void WriteType(int p_idx) {
            if(set_type == p_idx) return;            
            set_type = p_idx;
            WriteOperation(TokenType.SetType);
            WriteValueStr(p_idx);
            WriteValue((char)TokenType.Separator);
        }
        /// <summary>
        /// Writes the header preceding writing binary information
        /// </summary>        
        private void WriteDataOperation(Enum v) {
            WriteOperation(TokenType.Data);
        }
        private void WriteDataOperation(char     v) { WriteOperation(TokenType.Data); }
        private void WriteDataOperation(bool     v) { WriteOperation(TokenType.Data); }
        private void WriteDataOperation(sbyte    v) { WriteOperation(TokenType.Data); }
        private void WriteDataOperation(byte     v) { WriteOperation(TokenType.Data); }
        private void WriteDataOperation(short    v) { WriteOperation(TokenType.Data); }
        private void WriteDataOperation(ushort   v) { WriteOperation(TokenType.Data); }
        private void WriteDataOperation(int      v) { WriteOperation(TokenType.Data); }
        private void WriteDataOperation(uint     v) { WriteOperation(TokenType.Data); }        
        private void WriteDataOperation(long     v) { WriteOperation(TokenType.Data); }
        private void WriteDataOperation(ulong    v) { WriteOperation(TokenType.Data); }        
        private void WriteDataOperation(float    v) { WriteOperation(TokenType.Data); }
        private void WriteDataOperation(double   v) { WriteOperation(TokenType.Data); }
        private void WriteDataOperation(decimal  v) { WriteOperation(TokenType.Data); }
        private void WriteDataOperation(string   v) { WriteOperation(TokenType.Data); }
        private void WriteDataOperation(DateTime v) { WriteOperation(TokenType.Data); }
        private void WriteDataOperation(TimeSpan v) { WriteOperation(TokenType.Data); }
        private void WriteDataOperation(Type     v) { WriteOperation(TokenType.Data); }
        /*
        /// <summary>
        /// Writes the header preceding writing binary information
        /// </summary>        
        private void WriteDataOperation(Enum v) {
            if(m_is_text) { WriteOperation(TokenType.Data); return; }
            Type e_type = Enum.GetUnderlyingType(v.GetType());
            if(e_type == typeof(sbyte   )) m_writer_bin.Write((byte)TokenType.Byte1 ); else
            if(e_type == typeof(byte    )) m_writer_bin.Write((byte)TokenType.Byte1 ); else
            if(e_type == typeof(short   )) m_writer_bin.Write((byte)TokenType.Byte2 ); else
            if(e_type == typeof(ushort  )) m_writer_bin.Write((byte)TokenType.Byte2 ); else
            if(e_type == typeof(int     )) m_writer_bin.Write((byte)TokenType.Byte4 ); else
            if(e_type == typeof(uint    )) m_writer_bin.Write((byte)TokenType.Byte4 ); else
            if(e_type == typeof(long    )) m_writer_bin.Write((byte)TokenType.Byte8 ); else
            if(e_type == typeof(ulong   )) m_writer_bin.Write((byte)TokenType.Byte8 ); else
            if(e_type == typeof(decimal )) m_writer_bin.Write((byte)TokenType.Byte16); else
            m_writer_bin.Write((byte)TokenType.Byte4);
        }
        private void WriteDataOperation(char     v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte1 ); else WriteOperation(TokenType.Data); }
        private void WriteDataOperation(bool     v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte1 ); else WriteOperation(TokenType.Data); }
        private void WriteDataOperation(sbyte    v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte1 ); else WriteOperation(TokenType.Data); }
        private void WriteDataOperation(byte     v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte1 ); else WriteOperation(TokenType.Data); }
        private void WriteDataOperation(short    v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte2 ); else WriteOperation(TokenType.Data); }
        private void WriteDataOperation(ushort   v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte2 ); else WriteOperation(TokenType.Data); }
        private void WriteDataOperation(int      v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte4 ); else WriteOperation(TokenType.Data); }
        private void WriteDataOperation(uint     v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte4 ); else WriteOperation(TokenType.Data); }        
        private void WriteDataOperation(long     v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte4 ); else WriteOperation(TokenType.Data); }
        private void WriteDataOperation(ulong    v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte8 ); else WriteOperation(TokenType.Data); }        
        private void WriteDataOperation(float    v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte4 ); else WriteOperation(TokenType.Data); }
        private void WriteDataOperation(double   v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte8 ); else WriteOperation(TokenType.Data); }
        private void WriteDataOperation(decimal  v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte16); else WriteOperation(TokenType.Data); }
        private void WriteDataOperation(string   v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.String); else WriteOperation(TokenType.Data); }
        private void WriteDataOperation(DateTime v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte4 ); else WriteOperation(TokenType.Data); }
        private void WriteDataOperation(TimeSpan v) { if(!m_is_text) m_writer_bin.Write((byte)TokenType.Byte4 ); else WriteOperation(TokenType.Data); }
        //*/
        #endregion

        #region IObjectParserTokenHandler

        #region void OnNewName
        /// <summary>
        /// Parser found a new property/field name
        /// </summary>        
        public void OnNewName(ObjectParser p_parser,int p_name_idx) {            
            string v_name = p_parser.GetName(p_name_idx);
            WriteOperation(TokenType.NewName);
            WriteValue(v_name,false);
            WriteValue((char)TokenType.Separator);
        }
        #endregion

        #region void OnNewReference
        /// <summary>
        /// Parser found a new reference instance
        /// </summary>
        /// <param name="p_parser"></param>
        /// <param name="p_type_idx"></param>
        /// <param name="p_ref_idx"></param>
        public void OnNewReference(ObjectParser p_parser,int p_type_idx,int p_ref_idx) {
            object v_ref = p_parser.GetReference(p_ref_idx);
            WriteOperation(TokenType.NewRef);                
            int ctn_c=0;
            WriteValueStr(p_type_idx);
            if(v_ref is IList      ) { IList       ctn = (IList)      v_ref; ctn_c = ctn.Count; } else
            if(v_ref is IDictionary) { IDictionary ctn = (IDictionary)v_ref; ctn_c = ctn.Count; }
            WriteValue((char)TokenType.Separator);            
            WriteValueStr(ctn_c);
            WriteValue((char)TokenType.Separator);
        }
        #endregion

        #region void OnNewType
        /// <summary>
        /// Parser found a new type
        /// </summary>
        /// <param name="p_parser"></param>
        /// <param name="p_type_idx"></param>
        public void OnNewType(ObjectParser p_parser,int p_type_idx) { 
            Type v_type = p_parser.GetType(p_type_idx);                
            WriteOperation(TokenType.NewType);
            WriteValue(v_type.AssemblyQualifiedName,false);
            WriteValue((char)TokenType.Separator);
        }
        #endregion

        #region void OnPush
        /// <summary>
        /// Parser found a valid object and will stack it up for processing.
        /// </summary>        
        public void OnPush(ObjectParser p_parser,int p_type_idx,int p_name_idx,int p_ref_idx,object p_ref,bool p_struct) { 
            int ref_idx  = p_ref_idx;
            int name_idx = p_name_idx;
            WriteOperation(TokenType.Push);                                
            WriteValueStr(ref_idx);
            WriteValue((char)TokenType.Separator);
        }
        #endregion

        #region void OnPop
        /// <summary>
        /// Parser finished processing a stacked object
        /// </summary>        
        public void OnPop(ObjectParser p_parser,int p_type_idx,int p_name_idx,int p_ref_idx,object p_ref,bool p_struct) { 
            //int ref_idx  = p_ref_idx;
            int name_idx = p_name_idx;            
            //Pop operation
            WriteOperation(TokenType.Pop);
            WriteValueStr(name_idx);
            WriteValue((char)TokenType.Separator);            
        }
        #endregion

        #region void OnReference
        /// <summary>
        /// Parser found an existing reference with a reference index of the cache.
        /// </summary>        
        public void OnReference(ObjectParser p_parser,int p_type_idx,int p_name_idx,int p_ref_idx) {
            WriteType(p_type_idx);
            WriteName(p_name_idx);            
            WriteOperation(TokenType.Reference);
            WriteValueStr(p_ref_idx);
            WriteValue((char)TokenType.Separator);
        }
        #endregion

        #region void OnValue
        /// <summary>
        /// Parser found a primitive value
        /// </summary>        
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,Enum     p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,DateTime p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,TimeSpan p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,string   p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value,true); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,bool     p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,char     p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,sbyte    p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,byte     p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,short    p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,ushort   p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); } 
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,long     p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,ulong    p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,int      p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,uint     p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,float    p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,double   p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,decimal  p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value);if(m_is_text) WriteValue((char)TokenType.Separator); }
        public void OnValue(ObjectParser p_parser,int p_type_idx,int p_name_idx,Type     p_value) {  WriteType(p_type_idx); WriteName(p_name_idx); WriteDataOperation(p_value); WriteValue(p_value); }
        #endregion

        #endregion
    }
}

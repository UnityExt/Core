using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections;
using System.Text;
using System.Threading.Tasks;

namespace UnityExt.Core {

    /// <summary>
    /// Class that implements the read/write of C# objects into streams.
    /// </summary>
    public class ObjectReader {

        /// <summary>
        /// Internals
        /// </summary>
        public Stream BaseStream { get; private set; }

        /// <summary>
        /// Reference to the writer object.
        /// </summary>
        public IDisposable Reader { get; private set; }

        /// <summary>
        /// Resets the streams/readers to the initial state
        /// </summary>
        public void Reset() {
            if(BaseStream!=null) if(BaseStream.CanSeek) BaseStream.Position=0;
            if(Reader is StreamReader) { StreamReader r = (StreamReader)Reader;  r.DiscardBufferedData(); }            
        }

        #region CTOR
        /// <summary>
        /// Creates an object reader into a Stream as text or binary mode.
        /// </summary>
        /// <param name="p_stream">Stream to write into</param>
        /// <param name="p_text_mode">Flag telling if the objet will be written as text or binary</param>
        public ObjectReader(Stream p_stream,bool p_text_mode) {
            BaseStream = p_stream;
            if(BaseStream  ==null)   { throw new InvalidDataException     ("Stream is <null>");       }
            if(!BaseStream.CanRead)  { throw new InvalidOperationException("Stream is not readable"); }
            m_is_text       = p_text_mode;
            m_can_seek      = BaseStream.CanSeek;            
            if(m_is_text) {
                Reader = m_reader_txt = new StreamReader(BaseStream,Encoding.Default,true,1024 * 16,false);                
            }
            else {
                Reader = m_reader_bin = new BinaryReader(BaseStream);                
            }
        }

        /// <summary>
        /// Creates a new object reader with a pre-defined TextReader
        /// </summary>
        /// <param name="p_reader">Reader instance</param>
        public ObjectReader(TextReader p_reader) {            
            Reader = m_reader_txt = p_reader;     
            if(Reader  == null)   { throw new InvalidDataException     ("Reader  is <null>");       }
            m_is_text  = true;
            m_can_seek = false;
            if(m_reader_txt is StreamReader) { 
                StreamReader sr = (StreamReader)m_reader_txt; 
                BaseStream = sr.BaseStream; 
                m_can_seek = BaseStream.CanSeek;                 
            }            
        }

        /// <summary>
        /// Creates a new object reader with a pre-defined BinaryReader
        /// </summary>
        /// <param name="p_reader">Reader instance</param>
        public ObjectReader(BinaryReader p_reader) {            
            Reader = m_reader_bin = p_reader;
            if(Reader == null)   { throw new InvalidDataException     ("Reader is <null>");       }
            m_is_text    = false;            
            BaseStream   = m_reader_bin.BaseStream;
            m_can_seek   = BaseStream.CanSeek;            
        }
        #endregion

        /// <summary>
        /// Internals
        /// </summary>
        private ObjectParser  m_parser;
        private bool          m_is_text;
        private bool          m_can_seek;
        private TextReader    m_reader_txt;
        private BinaryReader  m_reader_bin;                
        private char[] m_cbuffer   = new char[128];
        private byte[] m_bbuffer1  = new byte[1];
        private byte[] m_bbuffer2  = new byte[2];
        private byte[] m_bbuffer4  = new byte[4];
        private byte[] m_bbuffer8  = new byte[8];
        private byte[] m_bbuffer16 = new byte[16];
        private StringBuilder m_sbuffer = new StringBuilder(1024 * 5);
        private long m_stream_length = -1;

        #region Reader        
        /// <summary>
        /// Reads the contents of the reader/stream and returns the object instance
        /// </summary>        
        /// <typeparam name="T">Type of the object</typeparam>
        /// <returns>Object instance inside the reader/stream.</returns>
        public T Read<T>() {                 
            m_parser  = new ObjectParser();  
            T res = InternalRead<T>();
            m_parser.Dispose();         
            return res;
        }

        /// <summary>
        /// Reads the contents of the reader/stream and returns the object instance
        /// </summary>                
        /// <returns>Object instance inside the reader/stream.</returns>
        public object Read() { return Read<object>(); }    

        /// <summary>
        /// Loops the stream and parse the contents and return the resulting object.
        /// </summary>        
        private T InternalRead<T>() {
            //int k=0;
            ObjectParser op = m_parser;
            op.ReadBegin();
            TokenType op_token = TokenType.None;
            TokenType op_data  = TokenType.None;
            TokenType op_peek  = TokenType.None;
            int    set_name_idx = 0;
            int    set_type_idx = 0;
            string set_name = null;
            Type   set_type = null;
            bool is_txt = m_is_text;
            bool is_bin = !is_txt;

            m_stream_length = 0;
            if(BaseStream!=null) m_stream_length = BaseStream.Length;

            while(true) {                

                //infinite loop helper
                //if(k++ >= 1000000) break;

                //If no more data exit.
                if(IsReaderComplete()) break;

                //Due some tokens being 'set' and staying until new ones, need to peek op tokens on text mode
                if(is_txt) {
                    op_peek = PeekOperation();
                    if(op_peek == TokenType.OpStart) op_token = ReadOperation();
                }

                //FSM the operation tokens
                switch(op_token) {

                    #region Operation
                    //If no token reads an operation byte
                    case TokenType.None:
                    //Except in text mode where it first peeks for the operation then proceed
                    if(is_bin) { op_token = ReadOperation(); continue; }
                    break;
                    //If its announcing an operation start reads the next
                    case TokenType.OpStart: 
                    op_token = ReadOperation(); 
                    continue;
                    #endregion

                    #region NewType
                    //NewType -> TYPE_NAME
                    case TokenType.NewType: {
                        string vs = ReadString(TokenType.Separator);
                        op.NewType(vs);
                        op_token = TokenType.None;
                    }
                    continue;
                    #endregion

                    #region NewName
                    //NewName -> NAME_VALUE
                    case TokenType.NewName: {
                        string vs = ReadString(TokenType.Separator);
                        op.NewName(vs);
                        op_token = TokenType.None;
                    }
                    continue;
                    #endregion

                    #region NewRef
                    //NewReference -> TYPE_INDEX | LENGTH
                    case TokenType.NewRef: {
                        //Read reference index
                        int type_idx   = ReadIntStr();
                        //Ref length (if collection)
                        int ref_length = ReadIntStr();
                        //Adds the reference
                        op.NewReference(type_idx,ref_length);
                        op_token = TokenType.None;
                    }
                    continue;
                    #endregion

                    #region SetName
                    //Set the active name -> NAME_IDX
                    case TokenType.SetName: {                        
                        set_name_idx = ReadIntStr();                        
                        set_name     = op.GetName(set_name_idx);
                        op_token     = TokenType.None;
                    }
                    continue;
                    #endregion

                    #region SetType
                    //Set the active type -> TYPE_IDX
                    case TokenType.SetType: {                        
                        set_type_idx = ReadIntStr();                        
                        set_type     = op.GetType(set_type_idx);
                        op_token     = TokenType.None;
                    }
                    continue;
                    #endregion

                    #region Push
                    //Push new object into stack -> REF_IDX
                    case TokenType.Push: {
                        //int name_idx = ReadIntStr();
                        int ref_idx  = ReadIntStr();
                        op.Push(ref_idx);
                        op_token = TokenType.None;
                    }
                    continue;
                    #endregion

                    #region Pop
                    //Pop the current object from stack -> NAME_IDX
                    case TokenType.Pop: {
                        int name_idx = ReadIntStr();
                        //int ref_idx  = ReadIntStr();
                        op.Pop(name_idx);
                        op_token = TokenType.None;
                    }
                    continue;
                    #endregion

                    #region Data
                    //Read the active data flag
                    case TokenType.Data:       { 
                        op_data = op_token;                         
                        if(is_bin) op_token = TokenType.None;
                    } 
                    break;
                    #endregion

                    #region Reference
                    case TokenType.Reference:  { 
                        op_data = op_token; 
                        if(is_bin) op_token = TokenType.None;
                    } 
                    break;
                    #endregion

                }

                switch(op_data) {

                    case TokenType.Data: {
                        bool is_enum = op.IsEnum(set_type_idx);
                        object v_data = ReadObject(set_type,is_enum);
                        op.Assign(set_name_idx,v_data);                        
                    }
                    break;

                    case TokenType.Reference: {
                        int    ref_idx = ReadIntStr();
                        object v_ref   = op.GetReference(ref_idx);
                        op.Assign(set_name_idx,v_ref);                        
                    }
                    break;

                }

                //text files reactively changes the operations so they can be kept active for a longer time

                //binary files are always tuples of OPERATION | ARGS so need to reset and fetch
                if(is_bin) op_data = TokenType.None;

            }
            return op.ReadEnd<T>();
        }
        #endregion

        #region Reader
        /// <summary>
        /// Try peeking an int in the readers.
        /// </summary>        
        private int PeekInt() {
            TextReader   sr = m_reader_txt;
            if(m_is_text) {
                int vi = sr.Peek();                
                return vi;
            }
            BinaryReader br = m_reader_bin;                        
            Stream       bs = br.BaseStream;                        
            return bs.Position < m_stream_length ? 0 : -1;
        }
        
        /// <summary>
        /// Peeks a char in the readers
        /// </summary>        
        private char PeekChar() { return (char)PeekInt(); }

        /// <summary>
        /// Peeks am operation Token in the readers
        /// </summary>        
        private TokenType PeekOperation() { return (TokenType)PeekChar(); }

        /// <summary>
        /// Helper to check if reading is completed.
        /// </summary>        
        private bool IsReaderComplete() {
            return PeekInt()<0;
        }

        /// <summary>
        /// Read Internals
        /// </summary>        
        private char     ReadChar     ()  { return m_is_text ? (char)m_reader_txt.Read() : (char)m_reader_bin.ReadByte(); }        
        private bool     ReadBool     ()  { byte v = ReadByte(); return v>0; }
        private sbyte    ReadSByte    ()  { if(m_is_text) { int c = ReadEOS((char)TokenType.Separator,m_cbuffer); sbyte    v; Parse.To(m_cbuffer,0,c,out v); return v; } ReadBytes(m_bbuffer1,1); return (sbyte)m_bbuffer1[0]; }
        private byte     ReadByte     ()  { if(m_is_text) { int c = ReadEOS((char)TokenType.Separator,m_cbuffer); byte     v; Parse.To(m_cbuffer,0,c,out v); return v; } ReadBytes(m_bbuffer1,1); return m_bbuffer1[0]; }
        private short    ReadShort    ()  { if(m_is_text) { int c = ReadEOS((char)TokenType.Separator,m_cbuffer); short    v; Parse.To(m_cbuffer,0,c,out v); return v; } ReadBytes(m_bbuffer2,2); return BitConverter.ToInt16  (m_bbuffer2,0); }
        private ushort   ReadUShort   ()  { if(m_is_text) { int c = ReadEOS((char)TokenType.Separator,m_cbuffer); ushort   v; Parse.To(m_cbuffer,0,c,out v); return v; } ReadBytes(m_bbuffer2,2); return BitConverter.ToUInt16 (m_bbuffer2,0); }
        private int      ReadInt      ()  { if(m_is_text) { int c = ReadEOS((char)TokenType.Separator,m_cbuffer); int      v; Parse.To(m_cbuffer,0,c,out v); return v; } ReadBytes(m_bbuffer4,4); return BitConverter.ToInt32  (m_bbuffer4,0); }
        private uint     ReadUInt     ()  { if(m_is_text) { int c = ReadEOS((char)TokenType.Separator,m_cbuffer); uint     v; Parse.To(m_cbuffer,0,c,out v); return v; } ReadBytes(m_bbuffer4,4); return BitConverter.ToUInt32 (m_bbuffer4,0); }
        private long     ReadLong     ()  { if(m_is_text) { int c = ReadEOS((char)TokenType.Separator,m_cbuffer); long     v; Parse.To(m_cbuffer,0,c,out v); return v; } ReadBytes(m_bbuffer8,8); return BitConverter.ToInt64  (m_bbuffer8,0); }
        private ulong    ReadULong    ()  { if(m_is_text) { int c = ReadEOS((char)TokenType.Separator,m_cbuffer); ulong    v; Parse.To(m_cbuffer,0,c,out v); return v; } ReadBytes(m_bbuffer8,8); return BitConverter.ToUInt64 (m_bbuffer8,0); }
        private float    ReadFloat    ()  { if(m_is_text) { int c = ReadEOS((char)TokenType.Separator,m_cbuffer); float    v; Parse.To(m_cbuffer,0,c,out v); return v; } ReadBytes(m_bbuffer4,4); return BitConverter.ToSingle (m_bbuffer4,0); }
        private double   ReadDouble   ()  { if(m_is_text) { int c = ReadEOS((char)TokenType.Separator,m_cbuffer); double   v; Parse.To(m_cbuffer,0,c,out v); return v; } ReadBytes(m_bbuffer8,8); return BitConverter.ToDouble (m_bbuffer8,0); }
        private string   ReadString   (TokenType p_sep)  { ReadEOS((char)p_sep,m_sbuffer); return m_sbuffer.ToString(); }
        private DateTime ReadDateTime ()  { if(m_is_text) { int c = ReadEOS((char)TokenType.Separator,m_cbuffer); DateTime v; Parse.To(m_cbuffer,0,c,out v); return v; } ReadBytes(m_bbuffer8,8); return DateTime.FromBinary(BitConverter.ToInt64 (m_bbuffer8,0)); }
        private TimeSpan ReadTimeSpan ()  { if(m_is_text) { int c = ReadEOS((char)TokenType.Separator,m_cbuffer); TimeSpan v; Parse.To(m_cbuffer,0,c,out v); return v; } ReadBytes(m_bbuffer8,8); return new TimeSpan(BitConverter.ToInt64 (m_bbuffer8,0));        }
        private decimal  ReadDecimal  ()  { 
            if(m_is_text) { int c = ReadEOS((char)TokenType.Separator,m_cbuffer); decimal v; Parse.To(m_cbuffer,0,c,out v); return v; }
            m_dcbuffer[0] = ReadInt();
            m_dcbuffer[1] = ReadInt();
            m_dcbuffer[2] = ReadInt();
            m_dcbuffer[3] = ReadInt();
            return new decimal(m_dcbuffer);
        }
        private int[] m_dcbuffer = new int[4];
        private Enum ReadEnum(Type p_type) {
            Type e_type = Enum.GetUnderlyingType(p_type);
            Enum res = null;
            if(e_type == typeof(sbyte   )) { sbyte   v = ReadSByte  (); res = (Enum)Enum.ToObject(p_type,v); } else 
            if(e_type == typeof(byte    )) { byte    v = ReadByte   (); res = (Enum)Enum.ToObject(p_type,v); } else 
            if(e_type == typeof(short   )) { short   v = ReadShort  (); res = (Enum)Enum.ToObject(p_type,v); } else 
            if(e_type == typeof(ushort  )) { ushort  v = ReadUShort (); res = (Enum)Enum.ToObject(p_type,v); } else 
            if(e_type == typeof(int     )) { int     v = ReadInt    (); res = (Enum)Enum.ToObject(p_type,v); } else 
            if(e_type == typeof(uint    )) { uint    v = ReadUInt   (); res = (Enum)Enum.ToObject(p_type,v); } else 
            if(e_type == typeof(long    )) { long    v = ReadLong   (); res = (Enum)Enum.ToObject(p_type,v); } else 
            if(e_type == typeof(ulong   )) { ulong   v = ReadULong  (); res = (Enum)Enum.ToObject(p_type,v); } else 
            if(e_type == typeof(decimal )) { decimal v = ReadDecimal(); res = (Enum)Enum.ToObject(p_type,v); }
            return res;
        }
        private Type ReadType()  { 
            string vs = ReadString(TokenType.StringEnd);
            return Type.GetType(vs);
        }

        /// <summary>
        /// Reads the next dataset as string and returns the int value.
        /// </summary>
        /// <returns></returns>
        private int ReadIntStr()  { 
            int c = ReadEOS((char)TokenType.Separator,m_cbuffer);
            int v;
            Parse.To(m_cbuffer,0,c,out v); 
            return v;
        }
        /// <summary>
        /// Reads data based on the informed type.
        /// </summary>
        /// <param name="p_type"></param>        
        private object ReadObject(Type p_type,bool p_enum=false) {
            Type vt = p_type;            
            if(p_enum) return ReadEnum(vt);
            if(vt == typeof(char    )) return ReadChar    ();
            if(vt == typeof(bool    )) return ReadBool    ();  
            if(vt == typeof(sbyte   )) return ReadSByte   ();  
            if(vt == typeof(byte    )) return ReadByte    ();  
            if(vt == typeof(short   )) return ReadShort   ();  
            if(vt == typeof(ushort  )) return ReadUShort  ();  
            if(vt == typeof(int     )) return ReadInt     ();  
            if(vt == typeof(uint    )) return ReadUInt    ();  
            if(vt == typeof(long    )) return ReadLong    ();  
            if(vt == typeof(ulong   )) return ReadULong   ();  
            if(vt == typeof(float   )) return ReadFloat   ();  
            if(vt == typeof(double  )) return ReadDouble  ();  
            if(vt == typeof(string  )) return ReadString  (TokenType.StringEnd);  
            if(vt == typeof(decimal )) return ReadDecimal ();
            if(vt == typeof(DateTime)) return ReadDateTime();
            if(vt == typeof(TimeSpan)) return ReadTimeSpan();
            if(vt.Name == "RuntimeType") return ReadType();
            return null;
        }
        /// <summary>
        /// Reads until separator into a stringbuilder
        /// </summary>        
        private void ReadEOS(char p_sep,StringBuilder p_buffer) {
            p_buffer.Clear();
            while(true) {
                char c = ReadChar();
                if(c==p_sep) break;
                p_buffer.Append(c);
            }            
        }
        private int ReadEOS(char p_sep,char[] p_buffer) {            
            int k=0;
            while(true) {
                char c = ReadChar();
                if(c==p_sep) break;
                p_buffer[k++] = c;
            }            
            return k;
        }        
        /// <summary>
        /// Reads a set of bytes from the binary reader
        /// </summary>        
        private void ReadBytes(byte[] p_buffer,int p_count) { for(int i=0;i<p_count;i++)p_buffer[i] = m_reader_bin.ReadByte(); }
        
        /// <summary>
        /// Reads one operation flag
        /// </summary>        
        private TokenType ReadOperation() { return (TokenType)ReadChar(); }
        #endregion

    }
}

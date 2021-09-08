using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace UnityExt.Core.IO {

    #region enum SerializerAttrib
    /// <summary>
    /// Class that wraps attribute bits for serialization.
    /// </summary>
    public enum SerializerAttrib : ushort {
        /*=== General Bits ===*/
        /// <summary>
        /// No Flags
        /// </summary>
        None        = (   0),
        /// <summary>
        /// Debug flags (some extra logging performed)
        /// </summary>
        Debug       = (1<<0),
        /// <summary>
        /// Will run inside try-catch
        /// </summary>
        Safe        = (1<<1),
        /// <summary>
        /// Close the underlying stream
        /// </summary>
        CloseStream = (1<<2),
        /*=== Json Bits ===*/
        /// <summary>
        /// Encode to Base64 during serialization
        /// </summary>
        Base64      = (1<<4),
        /// <summary>
        /// Indent the json output
        /// </summary>
        Indented      = (1<<5),
        /*=== Compression Bits ===*/
        /// <summary>
        /// Compress using GZip Optimal
        /// </summary>
        GZip        = (1<<8),
        /// <summary>
        /// Compress using GZip Fast
        /// </summary>
        GZipFast    = (1<<9),
        /// <summary>
        /// Compress using Deflate Optimal
        /// </summary>
        Deflate     = (1<<10),
        /// <summary>
        /// Compress using Deflate Fast
        /// </summary>
        DeflateFast = (1<<11),
        /*=== Object Bits ===*/
        /// <summary>
        /// Serialize in binary mode
        /// </summary>
        BinaryMode  = (1<<12),
        /// <summary>
        /// Serialize in text mode
        /// </summary>
        TextMode    = (1<<13),
    }
    #endregion

    #region enum SerializerMode
    /// <summary>
    /// Operation Mode
    /// </summary>
    public enum SerializerMode : byte {
        /// <summary>
        /// No action
        /// </summary>
        Idle=0,
        /// <summary>
        /// Serialization Mode
        /// </summary>
        Serialize,
        /// <summary>
        /// Deserialization Mode
        /// </summary>
        Deserialize
    }
    #endregion

    #region class SerializerDesc
    /// <summary>
    /// Class that describes a serialization operation and the needed flags and structures.
    /// </summary>
    internal class SerializerDesc {
        public SerializerAttrib attribs;
        public object           input;        
        public object           output;            
        public object           container;                
        public bool             safe  { get { return (attribs & SerializerAttrib.Safe)        != 0; } }
        public bool             close { get { return (attribs & SerializerAttrib.CloseStream) != 0; } }
        public string           password;
        public Delegate         callback;        
        public void Invoke() { 
            //If no callback ignore
            if(callback == null) return; 
            //Check if input is a type (deserialization)
            Type t = input is Type ? (Type)input : null;
            //Call either the serialization callback or a deserialization one with the converted output
            if(t == null) {
                callback.DynamicInvoke();
            }
            else {
                callback.DynamicInvoke(output);
            }
        }
    }
    #endregion

    /// <summary>
    /// Base Class for all serialization operations using UnityEx features.
    /// Serializers use the 'Activity' system to either run sync or async (using threads).
    /// </summary>
    public abstract class Serializer : Activity {

        #region static 

        #region Activity InternalTask
        /// <summary>
        /// Helper to perform serialization/deserialization on streams.
        /// </summary>        
        static protected Activity InternalTask(bool p_async,SerializerMode p_mode,Stream p_from,Stream p_to,string p_password,SerializerAttrib p_attribs = SerializerAttrib.None,Action p_callback=null) {
            Stream src = p_from;
            Stream dst = p_to;
            if(src == null) { return p_async ? Activity.Run(delegate(Activity a) { return false; }) : null; }
            if(dst == null) { return p_async ? Activity.Run(delegate(Activity a) { return false; }) : null; }
            bool is_srl = p_mode == SerializerMode.Serialize;
            Stream ss = is_srl ? dst : src;
            ss = GetStream(ss,p_password,p_mode,p_attribs);
            if(ss  == null) { return p_async ? Activity.Run(delegate(Activity a) { return false; }) : null; }
            Stream cp_src = is_srl ? src : ss;
            Stream cp_dst = is_srl ? ss  : dst;
            if(p_async) {
                System.Threading.Tasks.Task copy_tsk = cp_src.CopyToAsync(cp_dst);
                Activity task =
                Activity.Run(
                delegate(Activity a) {
                    if(copy_tsk==null)                            { if(p_callback!=null) p_callback(); return false; }
                    if(copy_tsk.IsFaulted || copy_tsk.IsCanceled) { if(p_callback!=null) p_callback(); return false; }
                    if(!copy_tsk.IsCompleted) return true;
                    cp_dst.Flush();
                    cp_dst.Close(); 
                    if((p_attribs & SerializerAttrib.CloseStream)!=0) cp_src.Close();
                    if(p_callback!=null) p_callback();
                    return false;
                });
                task.id = "serializer-task";
                return task;
            }
            else {
                cp_src.CopyTo(cp_dst);
                cp_dst.Flush();
                cp_dst.Close(); 
                if((p_attribs & SerializerAttrib.CloseStream)!=0) cp_src.Close();
            }
            return null;
        }
        /// <summary>
        /// Helper to perform hashes calculations on streams.
        /// </summary>        
        static protected Activity InternalHash(bool p_async,Type p_hash_type,Stream p_from,Stream p_to,bool p_base64,Action p_callback = null,bool p_close=true) {
            Stream src = p_from;
            Stream dst = p_to;
            if(src == null) { return p_async ? Activity.Run(delegate(Activity a) { return false; }) : null; }
            if(dst == null) { return p_async ? Activity.Run(delegate(Activity a) { return false; }) : null; }

            HashAlgorithm h = null;
            if(p_hash_type == typeof(KeyedHashAlgorithm)) h = KeyedHashAlgorithm.Create();
            if(p_hash_type == typeof(RIPEMD160         )) h = RIPEMD160.Create();
            if(p_hash_type == typeof(MD5               )) h = MD5.Create();
            if(p_hash_type == typeof(SHA1              )) h = SHA1.Create();
            if(p_hash_type == typeof(SHA256            )) h = SHA256.Create();
            if(p_hash_type == typeof(SHA384            )) h = SHA384.Create();
            if(p_hash_type == typeof(SHA512            )) h = SHA512.Create();

            if(h == null) { return p_async ? Activity.Run(delegate(Activity a) { return false; }) : null; }

            if(p_base64) dst = PipeBase64Conversion(dst, SerializerMode.Serialize, SerializerAttrib.Base64);

            if(p_async) {                
                Activity task =
                Activity.Run(
                delegate(Activity a1) {
                    //Compute and write
                    byte[] res = h.ComputeHash(src);
                    dst.Write(res,0,res.Length);
                    dst.Flush();
                    if(dst is CryptoStream) { CryptoStream dst_cs = (CryptoStream)dst; dst_cs.FlushFinalBlock(); }
                    if(p_close) dst.Close();
                    //Run in main thread
                    task = 
                    Activity.Run(delegate(Activity a2) { 
                        if(p_callback!=null) p_callback();
                        return false;
                    },ActivityContext.Update);
                    task.id = "serializer-hash-complete";

                    return false;
                }, ActivityContext.Thread);
                task.id = "serializer-hash-create";
                return task;
            }
            else {
                //Compute and write
                byte[] res = h.ComputeHash(src);
                dst.Write(res,0,res.Length);
                dst.Flush();
                if(dst is CryptoStream) { CryptoStream dst_cs = (CryptoStream)dst; dst_cs.FlushFinalBlock(); }
                if(p_close) dst.Close();
            }
            return null;
        }

        #endregion

        #region Serialize
        /// <summary>
        /// Perform a serialization operation between the two streams.
        /// It applies the Compression,Encryption and Base64 steps available in other modes.
        /// </summary>
        /// <param name="p_from">Source Stream</param>
        /// <param name="p_to">Destination Stream</param>
        /// <param name="p_password">Password for encryption</param>
        /// <param name="p_attribs">Serialization attributes</param>
        static public void Serialize(Stream p_from,Stream p_to,string p_password,SerializerAttrib p_attribs = SerializerAttrib.None) { InternalTask(false, SerializerMode.Serialize,p_from,p_to,p_password,p_attribs,null); }
        static public void Serialize(Stream p_from,Stream p_to,                  SerializerAttrib p_attribs = SerializerAttrib.None) { InternalTask(false, SerializerMode.Serialize,p_from,p_to,null      ,p_attribs,null); }
        static public void Serialize(string p_from,string p_to,string p_password,SerializerAttrib p_attribs = SerializerAttrib.None) { FileStream src = File.OpenRead(p_from), dst = File.Create(p_to); InternalTask(false, SerializerMode.Serialize,src,dst,p_password,p_attribs | SerializerAttrib.CloseStream,null); }
        static public void Serialize(string p_from,string p_to,                  SerializerAttrib p_attribs = SerializerAttrib.None) { FileStream src = File.OpenRead(p_from), dst = File.Create(p_to); InternalTask(false, SerializerMode.Serialize,src,dst,null      ,p_attribs | SerializerAttrib.CloseStream,null); }

        /// <summary>
        /// Perform a serialization operation between the two streams.
        /// It applies the Compression,Encryption and Base64 steps available in other modes.
        /// </summary>
        /// <param name="p_from">Source Stream</param>
        /// <param name="p_to">Destination Stream</param>
        /// <param name="p_password">Password for encryption</param>
        /// <param name="p_attribs">Serialization attributes</param>
        /// <param name="p_callback">Callback called upon finishing</param>
        /// <returns>Running Activity performing the serialization (awaitable)</returns>
        static public Activity SerializeAsync(Stream p_from,Stream p_to,string p_password,SerializerAttrib p_attribs = SerializerAttrib.None,Action p_callback=null) { return InternalTask(true, SerializerMode.Serialize,p_from,p_to,p_password,p_attribs,p_callback); }
        static public Activity SerializeAsync(Stream p_from,Stream p_to,                  SerializerAttrib p_attribs = SerializerAttrib.None,Action p_callback=null) { return InternalTask(true, SerializerMode.Serialize,p_from,p_to,null      ,p_attribs,p_callback); }
        static public Activity SerializeAsync(string p_from,string p_to,string p_password,SerializerAttrib p_attribs = SerializerAttrib.None,Action p_callback=null) { FileStream src = File.OpenRead(p_from), dst = File.Create(p_to); return InternalTask(true, SerializerMode.Serialize,src,dst,p_password,p_attribs | SerializerAttrib.CloseStream,null); }
        static public Activity SerializeAsync(string p_from,string p_to,                  SerializerAttrib p_attribs = SerializerAttrib.None,Action p_callback=null) { FileStream src = File.OpenRead(p_from), dst = File.Create(p_to); return InternalTask(true, SerializerMode.Serialize,src,dst,null      ,p_attribs | SerializerAttrib.CloseStream,null); }
        #endregion

        #region Deserialize
        /// <summary>
        /// Perform a deserialization operation between the two streams.
        /// It reversibly applies the Compression,Encryption and Base64 steps available in other modes.
        /// </summary>
        /// <param name="p_from">Source Stream</param>
        /// <param name="p_to">Destination Stream</param>
        /// <param name="p_password">Password for encryption</param>
        /// <param name="p_attribs">Serialization attributes</param>
        static public void Deserialize(Stream p_from,Stream p_to,string p_password,SerializerAttrib p_attribs = SerializerAttrib.None) {  InternalTask(false, SerializerMode.Deserialize,p_from,p_to,p_password,p_attribs,null); }
        static public void Deserialize(Stream p_from,Stream p_to,                  SerializerAttrib p_attribs = SerializerAttrib.None) {  InternalTask(false, SerializerMode.Deserialize,p_from,p_to,null      ,p_attribs,null); }
        static public void Deserialize(string p_from,string p_to,string p_password,SerializerAttrib p_attribs = SerializerAttrib.None) { FileStream src = File.OpenRead(p_from), dst = File.Create(p_to); InternalTask(false, SerializerMode.Deserialize,src,dst,p_password,p_attribs | SerializerAttrib.CloseStream,null); }
        static public void Deserialize(string p_from,string p_to,                  SerializerAttrib p_attribs = SerializerAttrib.None) { FileStream src = File.OpenRead(p_from), dst = File.Create(p_to); InternalTask(false, SerializerMode.Deserialize,src,dst,null      ,p_attribs | SerializerAttrib.CloseStream,null); }

        /// <summary>
        /// Perform a serialization operation between the two streams.
        /// It applies the Compression,Encryption and Base64 steps available in other modes.
        /// </summary>
        /// <param name="p_from">Source Stream</param>
        /// <param name="p_to">Destination Stream</param>
        /// <param name="p_password">Password for encryption</param>
        /// <param name="p_attribs">Serialization attributes</param>
        /// <param name="p_callback">Callback called upon finishing</param>
        /// <returns>Running Activity performing the serialization (awaitable)</returns>
        static public Activity DeserializeAsync(Stream p_from,Stream p_to,string p_password,SerializerAttrib p_attribs = SerializerAttrib.None,Action p_callback=null) {  return InternalTask(true, SerializerMode.Deserialize,p_from,p_to,p_password,p_attribs,p_callback); }
        static public Activity DeserializeAsync(Stream p_from,Stream p_to,                  SerializerAttrib p_attribs = SerializerAttrib.None,Action p_callback=null) {  return InternalTask(true, SerializerMode.Deserialize,p_from,p_to,null      ,p_attribs,p_callback); }
        static public Activity DeserializeAsync(string p_from,string p_to,string p_password,SerializerAttrib p_attribs = SerializerAttrib.None,Action p_callback=null) { FileStream src = File.OpenRead(p_from), dst = File.Create(p_to); return InternalTask(true, SerializerMode.Deserialize,src,dst,p_password,p_attribs | SerializerAttrib.CloseStream,null); }
        static public Activity DeserializeAsync(string p_from,string p_to,                  SerializerAttrib p_attribs = SerializerAttrib.None,Action p_callback=null) { FileStream src = File.OpenRead(p_from), dst = File.Create(p_to); return InternalTask(true, SerializerMode.Deserialize,src,dst,null      ,p_attribs | SerializerAttrib.CloseStream,null); }
        #endregion

        #region Hash        
        /// <summary>
        /// Applies hashing operation from a source stream into a destination stream.
        /// </summary>
        /// <typeparam name="T">HashAlgorithm Type</typeparam>
        /// <param name="p_from">Source information to be hashed</param>
        /// <param name="p_to">Destination of the hashing result.</param>
        /// <param name="p_base64">Flag that tells to convert the hashing result to base64.</param>
        static public void Hash<T>(Stream p_from,Stream p_to,bool p_base64=false) where T : HashAlgorithm { InternalHash(false,typeof(T),p_from,p_to,p_base64,null); }        
        static public void Hash<T>(string p_from,string p_to,bool p_base64=false) where T : HashAlgorithm { FileStream src = File.OpenRead(p_from), dst = File.Create(p_to); InternalHash(false,typeof(T),src,dst,p_base64,null); src.Close(); }
        static public Activity HashAsync<T>(Stream p_from,Stream p_to,bool p_base64=false,Action p_callback=null) where T : HashAlgorithm { return InternalHash(false,typeof(T),p_from,p_to,p_base64,p_callback); }        
        static public Activity HashAsync<T>(string p_from,string p_to,bool p_base64=false,Action p_callback=null) where T : HashAlgorithm { FileStream src = File.OpenRead(p_from), dst = File.Create(p_to); Activity a = InternalHash(false,typeof(T),src,dst,p_base64,p_callback); src.Close(); return a; }
        static public string Hash<T>(string p_input) where T : HashAlgorithm { 
            StreamWriter sw = new StreamWriter(new MemoryStream());
            sw.Write(p_input);
            sw.Flush();
            sw.BaseStream.Position=0; 
            MemoryStream ms = new MemoryStream();                        
            InternalHash(false,typeof(T),sw.BaseStream,ms,true,null,false);
            sw.Close();
            ms.Position=0;
            StreamReader sr = new StreamReader(ms);
            string res =  sr.ReadToEnd();
            sr.Close();
            return res;
        }
        #endregion

        #region Stream Pipeline
        /// <summary>
        /// Returns the final stream to be written
        /// </summary>
        /// <returns></returns>
        static protected Stream GetStream(Stream p_stream,string p_password,SerializerMode p_mode,SerializerAttrib p_attribs) {            
            //Stream Pipeline
            Stream ss = p_stream;                                    
            //If no stream and container isn' then error
            if(ss==null) return null;
            //Next Stream
            Stream ns;
            //Base64 Stream
            ns = PipeBase64Conversion(ss,p_mode,p_attribs ); if(ns != null) ss = ns;
            //Encryption w/ password
            ns = PipeEncryption      (ss,p_mode,p_password); if(ns != null) ss = ns;
            //Compression Streams
            ns = PipeCompression     (ss,p_mode,p_attribs ); if(ns != null) ss = ns;            
            //Return final stream
            return ss;
        }

        #region PipeFile
        /// <summary>
        /// Pipe a FileStream if a valid path is provided
        /// </summary>
        /// <param name="p_path"></param>
        /// <param name="p_attribs"></param>
        /// <returns></returns>
        static protected Stream PipeFile(string p_path,SerializerMode p_mode,SerializerAttrib p_attribs) {                        
            string file_path = p_path;
            if(string.IsNullOrEmpty(file_path)) return null;
            //Create FileStream if file-path
            if(p_mode == SerializerMode.Serialize)   return File.Create  (file_path);
            if(p_mode == SerializerMode.Deserialize) return File.OpenRead(file_path);
            return null;
        }
        #endregion

        #region Stream PipeCompression
        /// <summary>
        /// Pipe a compression stream around the passed stream
        /// </summary>
        /// <param name="p_stream">Stream to be compressed</param>
        /// <returns>Compression Stream</returns>
        static protected Stream PipeCompression(Stream p_stream,SerializerMode p_mode,SerializerAttrib p_attribs) {
            Stream ss = p_stream;
            //Invalid stream
            if(ss==null) return null;            
            //Detect compression level Off | Fast | Optimal
            System.IO.Compression.CompressionLevel compression_level = CompressionLevel.NoCompression;
            //Select the enum
            if((p_attribs & (SerializerAttrib.GZip     | SerializerAttrib.Deflate))     != 0) compression_level = CompressionLevel.Optimal;
            if((p_attribs & (SerializerAttrib.GZipFast | SerializerAttrib.DeflateFast)) != 0) compression_level = CompressionLevel.Fastest;
            //Skip if off
            if(compression_level == CompressionLevel.NoCompression) return null;
            //Check compression mode
            SerializerAttrib compression_type  = SerializerAttrib.None;
            if((p_attribs & (SerializerAttrib.GZip     | SerializerAttrib.GZipFast))     != 0) compression_type  = SerializerAttrib.GZip;
            if((p_attribs & (SerializerAttrib.Deflate  | SerializerAttrib.DeflateFast))  != 0) compression_type  = SerializerAttrib.Deflate;
            //Skip if none
            if(compression_type == SerializerAttrib.None) return null;
            //Check compression mode
            System.IO.Compression.CompressionMode  compression_mode = p_mode == SerializerMode.Serialize ? System.IO.Compression.CompressionMode.Compress : System.IO.Compression.CompressionMode.Decompress;
            //Generate the appropriate stream
            switch(compression_type) {
                case SerializerAttrib.GZip:    return compression_mode == CompressionMode.Compress ? new GZipStream   (ss,compression_level) : new GZipStream   (ss,compression_mode);
                case SerializerAttrib.Deflate: return compression_mode == CompressionMode.Compress ? new DeflateStream(ss,compression_level) : new DeflateStream(ss,compression_mode);
            }
            return null;
        }
        #endregion

        #region Stream PipeEncryption
        /// <summary>
        /// Pipe an encryption stream around the passed stream.
        /// </summary>
        /// <param name="p_stream">Stream to be encrypted.</param>
        /// <returns>Encryption Stream</returns>
        static protected Stream PipeEncryption(Stream p_stream,SerializerMode p_mode,string p_password) {
            Stream ss = p_stream;
            //Invalid input stream
            if(ss==null)   return null;
            string pwd       = p_password;
            bool   encrypted = !string.IsNullOrEmpty(pwd);
            //No password
            if(!encrypted) return null;            
            //Create hashing/encryption settings
            byte[] pwd_str = System.Text.ASCIIEncoding.ASCII.GetBytes(pwd);
            MD5CryptoServiceProvider       md5   = new MD5CryptoServiceProvider(); 
            TripleDESCryptoServiceProvider tdcsp = new TripleDESCryptoServiceProvider();
            tdcsp.Key  = md5.ComputeHash(pwd_str);
            tdcsp.Mode = CipherMode.ECB; //CBC, CFB
            //Create stream
            switch(p_mode) {
                case SerializerMode.Serialize:   return new CryptoStream(ss,tdcsp.CreateEncryptor(), CryptoStreamMode.Write); 
                case SerializerMode.Deserialize: return new CryptoStream(ss,tdcsp.CreateDecryptor(), CryptoStreamMode.Read);  
            }
            return null;
        }
        #endregion

        #region Stream PipeBase64Conversion
        /// <summary>
        /// Pipe a Base64 conversion stream around the input stream.
        /// </summary>
        /// <param name="p_stream">Stream to be encoded in base64</param>
        /// <returns>Base64 Encoding Stream</returns>
        static protected Stream PipeBase64Conversion(Stream p_stream,SerializerMode p_mode,SerializerAttrib p_attribs) {            
            Stream ss = p_stream;
            //Invalid input stream
            if(ss==null)   return null;                        
            //If Base64 not specified
            if((p_attribs & SerializerAttrib.Base64)==0) return null;
            switch(p_mode) {
                case SerializerMode.Serialize:   return new CryptoStream(ss,new ToBase64Transform(),  CryptoStreamMode.Write);
                case SerializerMode.Deserialize: return new CryptoStream(ss,new FromBase64Transform(),CryptoStreamMode.Read);
            }            
            return null;
        }
        #endregion

        #endregion

        #endregion

        /// <summary>
        /// Simple Id for counting serializers.
        /// </summary>
        static private int m_id=0;

        /// <summary>
        /// Mode Flag
        /// </summary>
        public SerializerMode mode { get; protected set; }

        /// <summary>
        /// Flag that tells this serialization is in operation
        /// </summary>
        public bool active { get { return mode != SerializerMode.Idle; } }

        /// <summary>
        /// Event called upon operation completion
        /// </summary>
        new public Action<Serializer> OnCompleteEvent {
            get { return (Action<Serializer>)m_on_complete_event; }
            set { m_on_complete_event = value;               }
        }

        /// <summary>
        /// Returns the result of async operations.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetResult<T>() { return m_output==null ? default(T) : (T)m_output; }

        /// <summary>
        /// Internals.
        /// </summary>
        internal  SerializerDesc descriptor;
        protected Activity       m_task_thread;
        protected object         m_output;
        
        /// <summary>
        /// CTOR.
        /// </summary>
        public Serializer() {
            id = $"{GetType().Name.ToLower()}-{m_id}";
        }

        /// <summary>
        /// Serialize the input data into the target container.
        /// </summary>
        /// <param name="p_input">Data for serialization</param>
        /// <param name="p_password">Password for encryption</param>
        /// <param name="p_attribs">Operation Attributes</param>
        /// <param name="p_container">Target container to read/write the results</param>
        /// <param name="p_callback">Callback for operation complete</param>        
        public void Serialize(object p_input,string p_password,StringBuilder p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,p_attribs,            p_input,p_password,p_container,p_callback);  }
        public void Serialize(object p_input,string p_password,Stream        p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,p_attribs,            p_input,p_password,p_container,p_callback);  }
        public void Serialize(object p_input,string p_password,string        p_file,     SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,p_attribs,            p_input,p_password,p_file,     p_callback);  }        
        public void Serialize(object p_input,                  StringBuilder p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,p_attribs,            p_input,""        ,p_container,p_callback);  }
        public void Serialize(object p_input,                  Stream        p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,p_attribs,            p_input,""        ,p_container,p_callback);  }
        public void Serialize(object p_input,                  string        p_file,     SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,p_attribs,            p_input,""        ,p_file,     p_callback);  }        
        public void Serialize(object p_input,                  StringBuilder p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,SerializerAttrib.None,p_input,""        ,p_container,p_callback);  }
        public void Serialize(object p_input,                  Stream        p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,SerializerAttrib.None,p_input,""        ,p_container,p_callback);  }
        public void Serialize(object p_input,                  string        p_file,                                Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,SerializerAttrib.None,p_input,""        ,p_file,     p_callback);  }
        public void Serialize(object p_input,string p_password,StringBuilder p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,SerializerAttrib.None,p_input,p_password,p_container,p_callback);  }
        public void Serialize(object p_input,string p_password,Stream        p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,SerializerAttrib.None,p_input,p_password,p_container,p_callback);  }
        public void Serialize(object p_input,string p_password,string        p_file,                                Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,SerializerAttrib.None,p_input,p_password,p_file,     p_callback);  }

        /// <summary>
        /// Serialize the input data into the target container using threads. 
        /// The result will arrive in the callback.
        /// </summary>
        /// <param name="p_input">Data for serialization</param>
        /// <param name="p_password">Password for encryption</param>
        /// <param name="p_attribs">Operation Attributes</param>
        /// <param name="p_container">Target container to read/write the results</param>
        /// <param name="p_callback">Callback for operation complete</param>        
        public Serializer SerializeAsync(object p_input,string p_password,StringBuilder p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,p_attribs,            p_input,p_password,p_container,p_callback); return this; }
        public Serializer SerializeAsync(object p_input,string p_password,Stream        p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,p_attribs,            p_input,p_password,p_container,p_callback); return this; }
        public Serializer SerializeAsync(object p_input,string p_password,string        p_file,     SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,p_attribs,            p_input,p_password,p_file,     p_callback); return this; }        
        public Serializer SerializeAsync(object p_input,                  StringBuilder p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,p_attribs,            p_input,""        ,p_container,p_callback); return this; }
        public Serializer SerializeAsync(object p_input,                  Stream        p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,p_attribs,            p_input,""        ,p_container,p_callback); return this; }
        public Serializer SerializeAsync(object p_input,                  string        p_file,     SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,p_attribs,            p_input,""        ,p_file,     p_callback); return this; }
        public Serializer SerializeAsync(object p_input,                  StringBuilder p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,SerializerAttrib.None,p_input,""        ,p_container,p_callback); return this; }
        public Serializer SerializeAsync(object p_input,                  Stream        p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,SerializerAttrib.None,p_input,""        ,p_container,p_callback); return this; }
        public Serializer SerializeAsync(object p_input,                  string        p_file,                                Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,SerializerAttrib.None,p_input,""        ,p_file,     p_callback); return this; }        
        public Serializer SerializeAsync(object p_input,string p_password,StringBuilder p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,SerializerAttrib.None,p_input,p_password,p_container,p_callback); return this; }
        public Serializer SerializeAsync(object p_input,string p_password,Stream        p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,SerializerAttrib.None,p_input,p_password,p_container,p_callback); return this; }
        public Serializer SerializeAsync(object p_input,string p_password,string        p_file,                                Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,SerializerAttrib.None,p_input,p_password,p_file,     p_callback); return this; }

        /// <summary>
        /// Deserialize the data inside the 'container'. 
        /// </summary>        
        /// <param name="p_password">Password for encryption</param>
        /// <param name="p_attribs">Operation Attributes</param>
        /// <param name="p_container">Target container to read/write the results</param>
        /// <param name="p_callback">Callback for operation complete</param>        
        public T Deserialize<T>(string p_password,StringBuilder p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,p_attribs,            typeof(T),p_password,p_container,p_callback);  }
        public T Deserialize<T>(string p_password,Stream        p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,p_attribs,            typeof(T),p_password,p_container,p_callback);  }
        public T Deserialize<T>(string p_password,string        p_file,     SerializerAttrib p_attribs,Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,p_attribs,            typeof(T),p_password,p_file,     p_callback);  }
        public T Deserialize<T>(                  StringBuilder p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,p_attribs,            typeof(T),""        ,p_container,p_callback);  }
        public T Deserialize<T>(                  Stream        p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,p_attribs,            typeof(T),""        ,p_container,p_callback);  }
        public T Deserialize<T>(                  string        p_file,     SerializerAttrib p_attribs,Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,p_attribs,            typeof(T),""        ,p_file,     p_callback);  }
        public T Deserialize<T>(                  StringBuilder p_container,                           Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,SerializerAttrib.None,typeof(T),""        ,p_container,p_callback);  }
        public T Deserialize<T>(                  Stream        p_container,                           Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,SerializerAttrib.None,typeof(T),""        ,p_container,p_callback);  }
        public T Deserialize<T>(                  string        p_file,                                Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,SerializerAttrib.None,typeof(T),""        ,p_file,     p_callback);  }
        public T Deserialize<T>(string p_password,StringBuilder p_container,                           Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,SerializerAttrib.None,typeof(T),p_password,p_container,p_callback);  }
        public T Deserialize<T>(string p_password,Stream        p_container,                           Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,SerializerAttrib.None,typeof(T),p_password,p_container,p_callback);  }
        public T Deserialize<T>(string p_password,string        p_file,                                Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,SerializerAttrib.None,typeof(T),p_password,p_file,     p_callback);  }

        /// <summary>
        /// Deserialize the data inside the 'container' using threads.
        /// The result will arrive in the callback.
        /// </summary>        
        /// <param name="p_password">Password for encryption</param>
        /// <param name="p_attribs">Operation Attributes</param>
        /// <param name="p_container">Target container to read/write the results</param>
        /// <param name="p_callback">Callback for operation complete</param>        
        public Serializer DeserializeAsync<T>(string p_password,StringBuilder p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,p_attribs,            typeof(T),p_password,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(string p_password,Stream        p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,p_attribs,            typeof(T),p_password,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(string p_password,string        p_file,     SerializerAttrib p_attribs,Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,p_attribs,            typeof(T),p_password,p_file,     p_callback); return this; }        
        public Serializer DeserializeAsync<T>(                  StringBuilder p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,p_attribs,            typeof(T),""        ,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(                  Stream        p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,p_attribs,            typeof(T),""        ,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(                  string        p_file,     SerializerAttrib p_attribs,Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,p_attribs,            typeof(T),""        ,p_file,     p_callback); return this; }        
        public Serializer DeserializeAsync<T>(                  StringBuilder p_container,                           Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,SerializerAttrib.None,typeof(T),""        ,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(                  Stream        p_container,                           Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,SerializerAttrib.None,typeof(T),""        ,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(                  string        p_file,                                Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,SerializerAttrib.None,typeof(T),""        ,p_file,     p_callback); return this; }        
        public Serializer DeserializeAsync<T>(string p_password,StringBuilder p_container,                           Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,SerializerAttrib.None,typeof(T),p_password,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(string p_password,Stream        p_container,                           Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,SerializerAttrib.None,typeof(T),p_password,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(string p_password,string        p_file,                                Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,SerializerAttrib.None,typeof(T),p_password,p_file,     p_callback); return this; }

        /// <summary>
        /// Handler for preparing for both Serialization/Deserialization
        /// </summary>
        virtual protected void OnInitialize() { }

        /// <summary>
        /// Handler for the final serialization
        /// </summary>
        virtual protected bool OnSerialize() { return true; }

        /// <summary>
        /// Handler for the final deserialization
        /// </summary>
        virtual protected bool OnDeserialize() { return true; }

        /// <summary>
        /// Initialize the operation
        /// </summary>
        protected void Initialize() {
            SerializerDesc dsc = descriptor;
            //Skip if null descriptor
            if(dsc==null)           return;
            //Skip if not in operation
            if(!active)             return;
            //Skip if no container
            if(dsc.container==null) return;            
            //Call extension initialize
            OnInitialize();            
        }

        /// <summary>
        /// Wrapper for serializing and deserializing
        /// </summary>        
        protected object InternalTask(SerializerMode p_mode,bool p_async,SerializerAttrib p_attribs,object p_input,string p_password,object p_container,Delegate p_callback) {
            SerializerDesc dsc = descriptor;
            //If there is a descriptor something is running
            if(dsc!=null) { UnityEngine.Debug.LogWarning($"{GetType().Name}> {p_mode} / Task in Progress!"); Start(); return null; }
            //Set operation mode
            mode = p_mode;
            //Initialize the descriptor
            dsc = new SerializerDesc() { attribs = p_attribs, input = p_input, password = p_password, container = p_container, callback = p_callback };
            //If compressed need to force close so buffer is emptied
            if((dsc.attribs & (SerializerAttrib.GZip | SerializerAttrib.GZipFast | SerializerAttrib.Deflate | SerializerAttrib.DeflateFast)) != 0) dsc.attribs |= SerializerAttrib.CloseStream;
            //Store descriptor
            descriptor = dsc;
            //If 'async' start activity and return nothing
            if(p_async) { Start(); return null; }
            //If 'sync' run the operations now
            //Operation result
            object res = null;
            //Prepare operations
            Initialize();
            //Operations
            if(mode == SerializerMode.Serialize) OnSerialize(); else OnDeserialize();
            //Fetch result in case of deserialize
            res  = descriptor.output;
            //Set mode to idle
            mode = SerializerMode.Idle;
            //Invalidate descriptor
            descriptor = null;
            //Run activity anyway for standard
            Start();
            //Return result if any
            return res;
        }

        /// <summary>
        /// Handler for the execution loop
        /// </summary>
        /// <returns></returns>
        protected override bool OnExecute() {
            SerializerDesc dsc = descriptor;
            //If no descriptor active stop
            if(dsc==null) return false;
            //If there is a task check its completion or continue
            if(m_task_thread != null) {
                //Check thread completion
                bool is_completed = m_task_thread.completed;
                if(!is_completed) return true;
                //Fetch result
                m_output = dsc.output;
                //Callbacks
                descriptor.Invoke();                    
                //Invalidate descriptor
                descriptor = null;
                return false;
            }
            //Flag to tell its the first loop
            bool first_loop = true;
            //Parsing Thread
            m_task_thread =
            Activity.Run(
            delegate(Activity a) { 
                bool is_complete = false;
                if(dsc.safe) {
                    try {
                        if(first_loop) { Initialize(); first_loop = false; }
                        is_complete = mode == SerializerMode.Serialize ? OnSerialize() : OnDeserialize();
                    }
                    catch(System.Exception p_err) {
                        is_complete=true;
                        UnityEngine.Debug.LogWarning($"{GetType().Name}> {mode} / Task Error\n{p_err.Message}");
                    }
                }
                else {
                    if(first_loop) { Initialize(); first_loop = false; }
                    is_complete = mode == SerializerMode.Serialize ? OnSerialize() : OnDeserialize();
                }
                //Keep running until completion in case the parsing is several loops long
                if(!is_complete) return true;                        
                //Stop the thread and let the activity collect the result
                return false;
            },ActivityContext.Thread);
            m_task_thread.id = id+"$task";            
            //Cycle again and poll the running thread
            return true;
        }

        #region Streams

        /// <summary>
        /// Fetches the starting point stream
        /// </summary>
        /// <returns></returns>
        virtual protected Stream GetBaseStream() { return descriptor==null ? null : ((Stream)descriptor.container); }

        protected Stream GetStream() {
            SerializerDesc dsc = descriptor;
            //File Open/Create to let it run inside thread 
            bool need_file = dsc.container is string;                        
            //Stream Pipeline
            Stream ss = null;                        
            //Check if file_path or if 'StringBuilder' flow or 'container' as Stream
            ss = need_file ? PipeFile((string)dsc.container,mode,dsc.attribs) : GetBaseStream();
            //Pipe all needed streams and return it
            ss = GetStream(ss,dsc.password,mode,dsc.attribs);
            return ss;
        }

        #endregion
    }
}

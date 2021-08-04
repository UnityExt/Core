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

        /// <summary>
        /// Returns the final stream to be written
        /// </summary>
        /// <returns></returns>
        protected Stream GetStream() {
            SerializerDesc dsc = descriptor;
            //File Open/Create to let it run inside thread 
            bool need_file = dsc.container is string;                        
            //Stream Pipeline
            Stream ss = null;                        
            //Check if file_path or if 'StringBuilder' flow or 'container' as Stream
            ss = need_file ? PipeFile((string)dsc.container,dsc.attribs) : GetBaseStream();
            //If no stream and container isn' then error
            if(ss==null) return null;
            //Next Stream
            Stream ns;
            //Encryption w/ password
            ns = PipeEncryption(ss,dsc.password);      if(ns != null) ss = ns;
            //Compression Streams
            ns = PipeCompression(ss,dsc.attribs);      if(ns != null) ss = ns;
            //Base64 Stream
            ns = PipeBase64Conversion(ss,dsc.attribs); if(ns != null) ss = ns;
            //Return final stream
            return ss;
        }

        /// <summary>
        /// Pipe a FileStream if a valid path is provided
        /// </summary>
        /// <param name="p_path"></param>
        /// <param name="p_attribs"></param>
        /// <returns></returns>
        protected Stream PipeFile(string p_path,SerializerAttrib p_attribs) {                        
            string file_path = p_path;
            if(string.IsNullOrEmpty(file_path)) return null;
            //Create FileStream if file-path
            if(mode == SerializerMode.Serialize)   return File.Create  (file_path);
            if(mode == SerializerMode.Deserialize) return File.OpenRead(file_path);
            return null;
        }

        #region Stream PipeCompression

        /// <summary>
        /// Pipe a compression stream around the passed stream
        /// </summary>
        /// <param name="p_stream">Stream to be compressed</param>
        /// <returns>Compression Stream</returns>
        protected Stream PipeCompression(Stream p_stream,SerializerAttrib p_attribs) {
            Stream ss = p_stream;
            //Invalid stream
            if(ss==null) return null;
            //Not operating
            if(!active)  return null;
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
            System.IO.Compression.CompressionMode  compression_mode = mode == SerializerMode.Serialize ? System.IO.Compression.CompressionMode.Compress : System.IO.Compression.CompressionMode.Decompress;
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
        protected Stream PipeEncryption(Stream p_stream,string p_password) {
            Stream ss = p_stream;
            //Invalid input stream
            if(ss==null)   return null;
            string password  = p_password;
            bool   encrypted = !string.IsNullOrEmpty(p_password);
            //No password
            if(!encrypted) return null;
            //Any invalid state
            if(!active)     return null;
            //Create hashing/encryption settings
            byte[] pwd_str = System.Text.ASCIIEncoding.ASCII.GetBytes(password);
            MD5CryptoServiceProvider       md5   = new MD5CryptoServiceProvider(); 
            TripleDESCryptoServiceProvider tdcsp = new TripleDESCryptoServiceProvider();
            tdcsp.Key  = md5.ComputeHash(pwd_str);
            tdcsp.Mode = CipherMode.ECB; //CBC, CFB
            //Create stream
            switch(mode) {
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
        protected Stream PipeBase64Conversion(Stream p_stream,SerializerAttrib p_attribs) {            
            Stream ss = p_stream;
            //Invalid input stream
            if(ss==null)   return null;            
            //Any invalid state
            if(!active)     return null;
            //If Base64 not specified
            if((p_attribs & SerializerAttrib.Base64)==0) return null;
            switch(mode) {
                case SerializerMode.Serialize:   return new CryptoStream(ss,new ToBase64Transform(),  CryptoStreamMode.Write);
                case SerializerMode.Deserialize: return new CryptoStream(ss,new FromBase64Transform(),CryptoStreamMode.Read);
            }            
            return null;
        }

        #endregion

        #endregion
    }
}

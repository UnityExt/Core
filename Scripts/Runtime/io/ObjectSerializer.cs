using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System;

namespace UnityExt.Core.IO {

    /// <summary>
    /// Class that extends the base serializer to handle objects read/write
    /// </summary>
    public class ObjectSerializer : Serializer {

        /// <summary>
        /// Internals
        /// </summary>
        protected IDisposable     m_handler;           
        protected ObjectWriter    m_writer;
        protected ObjectReader    m_reader;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        public void Serialize(object p_input,string p_password,TextWriter     p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,p_attribs,            p_input,p_password,p_container,p_callback);  }
        public void Serialize(object p_input,                  TextWriter     p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,p_attribs,            p_input,""        ,p_container,p_callback);  }
        public void Serialize(object p_input,                  TextWriter     p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,SerializerAttrib.None,p_input,""        ,p_container,p_callback);  }
        public void Serialize(object p_input,string p_password,TextWriter     p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,SerializerAttrib.None,p_input,p_password,p_container,p_callback);  }
        
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        public Serializer SerializeAsync(object p_input,string p_password,TextWriter     p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,p_attribs,            p_input,p_password,p_container,p_callback); return this;  }
        public Serializer SerializeAsync(object p_input,                  TextWriter     p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,p_attribs,            p_input,""        ,p_container,p_callback); return this;  }
        public Serializer SerializeAsync(object p_input,                  TextWriter     p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,SerializerAttrib.None,p_input,""        ,p_container,p_callback); return this;  }
        public Serializer SerializeAsync(object p_input,string p_password,TextWriter     p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,SerializerAttrib.None,p_input,p_password,p_container,p_callback); return this;  }
        
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        public T Deserialize<T>(string p_password,TextReader     p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,p_attribs,            typeof(T),p_password,p_container,p_callback);  }
        public T Deserialize<T>(                  TextReader     p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,p_attribs,            typeof(T),""        ,p_container,p_callback);  }
        public T Deserialize<T>(                  TextReader     p_container,                           Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,SerializerAttrib.None,typeof(T),""        ,p_container,p_callback);  }
        public T Deserialize<T>(string p_password,TextReader     p_container,                           Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,SerializerAttrib.None,typeof(T),p_password,p_container,p_callback);  }
        
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        public Serializer DeserializeAsync<T>(string p_password,TextReader     p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,p_attribs,            typeof(T),p_password,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(                  TextReader     p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,p_attribs,            typeof(T),""        ,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(                  TextReader     p_container,                           Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,SerializerAttrib.None,typeof(T),""        ,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(string p_password,TextReader     p_container,                           Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,SerializerAttrib.None,typeof(T),p_password,p_container,p_callback); return this; }
        
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        protected override void OnInitialize() {
            SerializerDesc dsc = descriptor;            
            //Reset handler
            m_handler=null;

            bool is_bin = (dsc.attribs & SerializerAttrib.BinaryMode)!=0;
            bool is_txt = (dsc.attribs & SerializerAttrib.TextMode  )!=0;
            SerializerAttrib file_type = is_bin ? SerializerAttrib.BinaryMode : (is_txt ? SerializerAttrib.TextMode : SerializerAttrib.TextMode);
            //Handle Final Writer/Reader            
            //Containers are already the 'reader'/'writer' primitives
            if(mode == SerializerMode.Serialize)   if(dsc.container is TextWriter)     { m_handler = (TextWriter) dsc.container;                                    m_writer = new ObjectWriter((TextWriter)m_handler); return; }                        
            if(mode == SerializerMode.Serialize)   if(dsc.container is StringBuilder)  { m_handler = new StringWriter(dsc.container as StringBuilder);              m_writer = new ObjectWriter((TextWriter)m_handler); return; }
            if(mode == SerializerMode.Deserialize) if(dsc.container is TextReader)     { m_handler = (TextReader) dsc.container;                                    m_reader = new ObjectReader((TextReader)m_handler); return; }            
            if(mode == SerializerMode.Deserialize) if(dsc.container is StringBuilder)  { m_handler = new StringReader((dsc.container as StringBuilder).ToString()); m_reader = new ObjectReader((TextReader)m_handler); return; }
            //Fetch target stream            
            Stream ss = GetStream();
            if(ss==null) { UnityEngine.Debug.LogWarning($"{GetType().Name}> Failed to Create Stream"); return; }
            //Apply to handler into next steps
            switch(file_type) {
                case SerializerAttrib.BinaryMode: {
                    if(mode == SerializerMode.Serialize)   { m_handler = new BinaryWriter(ss); m_writer = new ObjectWriter((BinaryWriter)m_handler); }            
                    if(mode == SerializerMode.Deserialize) { m_handler = new BinaryReader(ss); m_reader = new ObjectReader((BinaryReader)m_handler); }            
                }
                break;
                case SerializerAttrib.TextMode: {
                    if(mode == SerializerMode.Serialize)   { m_handler = new StreamWriter(ss); m_writer = new ObjectWriter((StreamWriter)m_handler); }
                    if(mode == SerializerMode.Deserialize) { m_handler = new StreamReader(ss); m_reader = new ObjectReader((StreamReader)m_handler); }
                }
                break;
            }            
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        protected override bool OnSerialize() {
            if(m_writer==null) return true;
            SerializerDesc dsc = descriptor;            
            if(m_handler is TextWriter) {
                TextWriter tw = m_handler as TextWriter;
                m_writer.Write(dsc.input);                
                tw.Flush();                
                if(dsc.close) tw.Close();
            }
            else
            if(m_handler is BinaryWriter) {
                BinaryWriter tw = m_handler as BinaryWriter;
                m_writer.Write(dsc.input);
                tw.Flush();                
                if(dsc.close) tw.Close();
            }
            return true;
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        protected override bool OnDeserialize() {
            if(m_reader==null) return true;
            SerializerDesc dsc = descriptor;
            if(m_handler is TextReader) {
                TextReader tr = m_handler as TextReader;
                dsc.output = m_reader.Read();                
                if(dsc.close) tr.Close();
            }
            else
            if(m_handler is BinaryReader) {
                BinaryReader tr = m_handler as BinaryReader;
                dsc.output = m_reader.Read();                
                if(dsc.close) tr.Close();
            }
            return true;
        }


    }
}

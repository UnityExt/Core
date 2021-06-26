using System;
using System.IO;
using System.Text;
using NJsonWriter             = Newtonsoft.Json.JsonWriter;
using NJsonReader             = Newtonsoft.Json.JsonReader;
using NJsonSerializer         = Newtonsoft.Json.JsonSerializer;
using NJsonSerializerSettings = Newtonsoft.Json.JsonSerializerSettings;
using NFormatting             = Newtonsoft.Json.Formatting;
using NDateParseHandling      = Newtonsoft.Json.DateParseHandling;


namespace UnityExt.Core.IO {


    /// <summary>
    /// Class that extends the base serializer to handle json read/write
    /// </summary>
    public class JSONSerializer : Serializer {

        /// <summary>
        /// Internals
        /// </summary>
        protected IDisposable     m_handler;           
        protected NJsonSerializer m_json;

        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        public void Serialize(object p_input,string p_password,TextWriter     p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,p_attribs,            p_input,p_password,p_container,p_callback);  }
        public void Serialize(object p_input,                  TextWriter     p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,p_attribs,            p_input,""        ,p_container,p_callback);  }
        public void Serialize(object p_input,                  TextWriter     p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,SerializerAttrib.None,p_input,""        ,p_container,p_callback);  }
        public void Serialize(object p_input,string p_password,TextWriter     p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,SerializerAttrib.None,p_input,p_password,p_container,p_callback);  }
        public void Serialize(object p_input,string p_password,NJsonWriter    p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,p_attribs,            p_input,p_password,p_container,p_callback);  }
        public void Serialize(object p_input,                  NJsonWriter    p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,p_attribs,            p_input,""        ,p_container,p_callback);  }
        public void Serialize(object p_input,                  NJsonWriter    p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,SerializerAttrib.None,p_input,""        ,p_container,p_callback);  }
        public void Serialize(object p_input,string p_password,NJsonWriter    p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,false,SerializerAttrib.None,p_input,p_password,p_container,p_callback);  }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        public Serializer SerializeAsync(object p_input,string p_password,TextWriter     p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,p_attribs,            p_input,p_password,p_container,p_callback); return this;  }
        public Serializer SerializeAsync(object p_input,                  TextWriter     p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,p_attribs,            p_input,""        ,p_container,p_callback); return this;  }
        public Serializer SerializeAsync(object p_input,                  TextWriter     p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,SerializerAttrib.None,p_input,""        ,p_container,p_callback); return this;  }
        public Serializer SerializeAsync(object p_input,string p_password,TextWriter     p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,SerializerAttrib.None,p_input,p_password,p_container,p_callback); return this;  }
        public Serializer SerializeAsync(object p_input,string p_password,NJsonWriter    p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,p_attribs,            p_input,p_password,p_container,p_callback); return this;  }
        public Serializer SerializeAsync(object p_input,                  NJsonWriter    p_container,SerializerAttrib p_attribs,Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,p_attribs,            p_input,""        ,p_container,p_callback); return this;  }
        public Serializer SerializeAsync(object p_input,                  NJsonWriter    p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,SerializerAttrib.None,p_input,""        ,p_container,p_callback); return this;  }
        public Serializer SerializeAsync(object p_input,string p_password,NJsonWriter    p_container,                           Action p_callback = null) {  InternalTask(SerializerMode.Serialize,true,SerializerAttrib.None,p_input,p_password,p_container,p_callback); return this;  }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        public T Deserialize<T>(string p_password,TextReader     p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,p_attribs,            typeof(T),p_password,p_container,p_callback);  }
        public T Deserialize<T>(                  TextReader     p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,p_attribs,            typeof(T),""        ,p_container,p_callback);  }
        public T Deserialize<T>(                  TextReader     p_container,                           Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,SerializerAttrib.None,typeof(T),""        ,p_container,p_callback);  }
        public T Deserialize<T>(string p_password,TextReader     p_container,                           Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,SerializerAttrib.None,typeof(T),p_password,p_container,p_callback);  }
        public T Deserialize<T>(string p_password,NJsonReader    p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,p_attribs,            typeof(T),p_password,p_container,p_callback);  }
        public T Deserialize<T>(                  NJsonReader    p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,p_attribs,            typeof(T),""        ,p_container,p_callback);  }
        public T Deserialize<T>(                  NJsonReader    p_container,                           Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,SerializerAttrib.None,typeof(T),""        ,p_container,p_callback);  }
        public T Deserialize<T>(string p_password,NJsonReader    p_container,                           Action<T> p_callback = null) { return (T)InternalTask(SerializerMode.Deserialize,false,SerializerAttrib.None,typeof(T),p_password,p_container,p_callback);  }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        public Serializer DeserializeAsync<T>(string p_password,TextReader     p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,p_attribs,            typeof(T),p_password,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(                  TextReader     p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,p_attribs,            typeof(T),""        ,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(                  TextReader     p_container,                           Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,SerializerAttrib.None,typeof(T),""        ,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(string p_password,TextReader     p_container,                           Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,SerializerAttrib.None,typeof(T),p_password,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(string p_password,NJsonReader    p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,p_attribs,            typeof(T),p_password,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(                  NJsonReader    p_container,SerializerAttrib p_attribs,Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,p_attribs,            typeof(T),""        ,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(                  NJsonReader    p_container,                           Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,SerializerAttrib.None,typeof(T),""        ,p_container,p_callback); return this; }
        public Serializer DeserializeAsync<T>(string p_password,NJsonReader    p_container,                           Action<T> p_callback = null) { InternalTask(SerializerMode.Deserialize,true,SerializerAttrib.None,typeof(T),p_password,p_container,p_callback); return this; }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        protected override void OnInitialize() {
            SerializerDesc dsc = descriptor;            
            //Reset handler
            m_handler=null;
            //Handle Final Writer/Reader            
            //Containers are already the 'reader'/'writer' primitives
            if(mode == SerializerMode.Serialize)   if(dsc.container is TextWriter)     { m_handler = (TextWriter) dsc.container; return; }            
            if(mode == SerializerMode.Serialize)   if(dsc.container is NJsonWriter)    { m_handler = (NJsonWriter)dsc.container; return; }
            if(mode == SerializerMode.Serialize)   if(dsc.container is StringBuilder)  { m_handler = new StringWriter(dsc.container as StringBuilder); return; }
            if(mode == SerializerMode.Deserialize) if(dsc.container is TextReader)     { m_handler = (TextReader) dsc.container; return; }
            if(mode == SerializerMode.Deserialize) if(dsc.container is NJsonReader)    { m_handler = (NJsonReader)dsc.container; return; }
            if(mode == SerializerMode.Deserialize) if(dsc.container is StringBuilder)  { m_handler = new StringReader((dsc.container as StringBuilder).ToString()); return; }
            //Fetch target stream            
            Stream ss = GetStream();
            if(ss==null) { UnityEngine.Debug.LogWarning($"{GetType().Name}> Failed to Create Stream"); return; }
            //Apply to handler into next steps            
            if(mode == SerializerMode.Serialize)   m_handler = new StreamWriter(ss);            
            if(mode == SerializerMode.Deserialize) m_handler = new StreamReader(ss);            
            NJsonSerializerSettings json_s = new NJsonSerializerSettings();
            json_s.Formatting        = ((dsc.attribs & SerializerAttrib.Indented)!=0) ? NFormatting.Indented : NFormatting.None;
            json_s.DateParseHandling = NDateParseHandling.DateTime;
            m_json = NJsonSerializer.Create(json_s);            
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        protected override bool OnSerialize() {
            if(m_json==null) return true;
            SerializerDesc dsc = descriptor;
            if(m_handler is TextWriter) {
                TextWriter tw = m_handler as TextWriter;
                m_json.Serialize(tw,dsc.input);
                tw.Flush();
                if(dsc.close) tw.Close();
            }
            else
            if(m_handler is NJsonWriter) {
                NJsonWriter jw = m_handler as NJsonWriter;
                m_json.Serialize(jw,dsc.input);
                jw.Flush();
                if(dsc.close) jw.Close();
            }
            return true;
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        protected override bool OnDeserialize() {
            if(m_json==null) return true;
            SerializerDesc dsc = descriptor;            
            if(m_handler is TextReader) {
                TextReader tr = m_handler as TextReader;
                dsc.output = m_json.Deserialize(tr,(Type)dsc.input);                
                if(dsc.close) tr.Close();
            }
            else
            if(m_handler is NJsonReader) {
                NJsonReader jr = m_handler as NJsonReader;
                dsc.output = m_json.Deserialize(jr,(Type)dsc.input);
                if(dsc.close) jr.Close();
            }
            return true;
        }


    }
}

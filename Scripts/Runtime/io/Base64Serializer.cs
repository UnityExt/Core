using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UnityExt.Core {


    /// <summary>
    /// Class that extends the base serializer to handle base64 read/write
    /// </summary>
    public class Base64Serializer : Serializer {

        /// <summary>
        /// Internals
        /// </summary>
        protected IDisposable     m_handler;

        protected override Stream GetBaseStream() {
            SerializerDesc dsc = descriptor;
            if(dsc.container is Stream) return (Stream)dsc.container;
            Stream ss = null;
            //Containers are already the 'reader'/'writer' primitives            
            if(dsc.container is StringBuilder) {
                //When StringBuilder 'container' convert to MemoryStream then re-encode bytes into 'string'
                if(mode == SerializerMode.Serialize)   { ss = new MemoryStream(); dsc.output = ss; }            
                if(mode == SerializerMode.Deserialize) { ss = new MemoryStream(Encoding.UTF8.GetBytes(((StringBuilder)dsc.container).ToString())); }                
            }
            return ss;
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        protected override void OnInitialize() {            
            SerializerDesc dsc = descriptor;
            //Reset handler
            m_handler=null;
            //Force Base64
            dsc.attribs |= SerializerAttrib.Base64;
            //Bas64 can only be applied to strings and byte arrays
            if(mode == SerializerMode.Serialize) 
            if(!(dsc.input is string)) if(!(dsc.input is byte[])) { UnityEngine.Debug.LogWarning("Bas64Serializer> Only 'string' and 'byte[]' are supported!"); return; }            
            //Fetch target stream            
            Stream ss = GetStream();
            if(ss==null) { UnityEngine.Debug.LogWarning($"{GetType().Name}> Failed to Create Stream"); return; }
            //Apply to handler into next steps
            m_handler = ss;            
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        protected override bool OnSerialize() {
            if(m_handler==null) return true;
            SerializerDesc dsc = descriptor;
            Stream         ss  = m_handler as Stream;
            bool close_stream = (dsc.attribs & SerializerAttrib.CloseStream)!=0;
            if(dsc.input is string) {
                StreamWriter sw = new StreamWriter(ss);
                sw.Write((string)dsc.input);
                sw.Flush();                    
            }
            else
            if(dsc.input is byte[]) {
                BinaryWriter bw = new BinaryWriter(ss);
                bw.Write((byte[])dsc.input);
                bw.Flush();                                
            }
            if(dsc.container is StringBuilder) { 
                ((CryptoStream)ss).FlushFinalBlock();
                StringBuilder sb = dsc.container as StringBuilder; 
                MemoryStream  ms = dsc.output as MemoryStream;
                StreamReader  sr = new StreamReader(ms);
                ms.Flush();
                ms.Position=0;                
                sb.Clear();                 
                sb.Append(sr.ReadToEnd());
                ss.Close();
            }
            if(close_stream) ss.Close();
            return true;
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        protected override bool OnDeserialize() {
            if(m_handler==null) return true;
            SerializerDesc dsc = descriptor;
            Stream         ss  = m_handler as Stream;
            bool close_stream = (dsc.attribs & SerializerAttrib.CloseStream)!=0;
            Type t = dsc.input as Type;
            if(t == typeof(string)) {
                StreamReader sr = new StreamReader(ss);
                dsc.output = sr.ReadToEnd();                
            }
            else
            if(t == typeof(byte[])) {
                MemoryStream ms = new MemoryStream();
                ss.CopyTo(ms);
                ms.Flush();
                dsc.output = ms.ToArray();
                ms.Close();                
            }                      
            if(close_stream) ss.Close();
            return true;
        }


    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace UnityExt.Core.IO {

    public class ObjectStream {
        /// <summary>
        /// Internals
        /// </summary>
        protected Stream       m_stream;
        protected object       m_instance;
        protected List<string> m_lut_types;
        
        public ObjectStream(object p_instance,Stream p_stream) {
            m_instance = p_instance;
            m_stream   = p_stream;
        }

        public ObjectStream(Stream p_stream) : this(null,p_stream) { }

        public void Write() {
            if(m_instance==null)   { throw new InvalidDataException     ("Instance is <null>");     }
            if(m_stream  ==null)   { throw new InvalidDataException     ("Stream is <null>");       }
            if(!m_stream.CanWrite) { throw new InvalidOperationException("Stream is not writable"); }
            Stream ss = m_stream;
        }

        public T Read<T>() {            
            if(m_stream  ==null)   { throw new InvalidDataException     ("Stream is <null>");       }
            if(!m_stream.CanRead)  { throw new InvalidOperationException("Stream is not readable"); }
            return default(T);
        } 
    }

    /// <summary>
    /// Class that extends the base serializer to handle base64 read/write
    /// </summary>
    public class BinarySerializer : Serializer {

        /// <summary>
        /// Internals
        /// </summary>
        protected IDisposable     m_handler;           
        
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        protected override void OnInitialize() {
            SerializerDesc dsc = descriptor;
            //Reset handler
            m_handler=null;                        
            //Fetch target stream
            Stream ss = GetStream();
            if(ss==null) { UnityEngine.Debug.LogWarning($"{GetType().Name}> Failed to Create Stream"); return; }
            //Apply to handler into next steps
            m_handler = ss;      
            
            //Serializer Field Attributes
            //[SerializerData]
            //[SerializerData(default)]

            //Serialize

            //Header - Inform id size format to prepare parsing and save space
            //Type  Id Format = byte|ushort|ulong
            //Token Id Format = byte|ushort|ulong

            //TypeTable - Prevent several strings for types
            //[Id][Type.FullName]

            //Data - List of tokens regardless topology
            //Token -> [TokenId][Flag][TypeId][Index][Parent][VarName][DataLength][Data|Address]
            //Token0|Token1|Token2|...
            
            //Deserialize
            
            //TokenBytes -> TokenClass[]

            //Topology Assemble
            //Reference tokens assigned in the proper location

            //Topology Table - Avoid circular linking
            //TokenN -> Visited True|False
            
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        protected override bool OnSerialize() {
            if(m_handler==null) return true;
            SerializerDesc dsc = descriptor;
            Stream         ss  = m_handler as Stream;
            bool close_stream = (dsc.attribs & SerializerAttrib.CloseStream)!=0;
            
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
            
            if(close_stream) ss.Close();
            return true;
        }


    }
}

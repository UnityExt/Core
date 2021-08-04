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

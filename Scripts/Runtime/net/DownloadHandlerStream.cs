using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using NetMultipartFormDataContent = System.Net.Http.MultipartFormDataContent;
using NetHttpContent              = System.Net.Http.HttpContent;
using NetByteArrayContent         = System.Net.Http.ByteArrayContent;
using NetStreamContent            = System.Net.Http.StreamContent;
using NetStringContent            = System.Net.Http.StringContent;
using HttpStatusCode              = System.Net.HttpStatusCode;
using System.Security.Cryptography;

namespace UnityExt.Core {
    
        #region class DownloadHandlerStream
        /// <summary>
        /// Class to wraps unity's downloader and give it Stream support
        /// </summary>
        public class DownloadHandlerStream : DownloadHandlerScript {

            /// <summary>
            /// Flag telling this handler is valid.
            /// </summary>
            public bool valid { get { return stream!=null; } }

            /// <summary>
            /// Stream to write into.
            /// </summary>
            public Stream stream { get; private set; }

            /// <summary>
            /// Size in bytes
            /// </summary>
            public int bytesTotal { get; private set; }

            /// <summary>
            /// Bytes downloaded
            /// </summary>
            public int bytesLoaded { get; private set; }

            /// <summary>
            /// Returns the progress
            /// </summary>
            public float progress { get { return GetProgress(); } }

            /// <summary>
            /// CTOR.
            /// </summary>
            /// <param name="p_stream"></param>
            public DownloadHandlerStream(Stream p_stream) : base() {
                m_progress  = 0f;
                bytesLoaded = 0;
                bytesTotal  = 0;
                stream          = p_stream;                                
            }

            /// <summary>
            /// <inheritdoc/>
            /// </summary>
            protected override void CompleteContent() {
                if(stream!=null) stream.Flush();
            }

            /// <summary>
            /// <inheritdoc/>
            /// </summary>
            protected override byte[] GetData() {
                return new byte[0];
            }

            /// <summary>
            /// <inheritdoc/>
            /// </summary>
            protected override float GetProgress() {
                if(isDone) return m_progress = 1f;                
                float t = (float)bytesTotal;
                float c = (float)bytesLoaded;                
                if(t<=0f) return Mathf.Clamp01(m_progress);
                return Mathf.Clamp01(Mathf.Max(m_progress,c/t));
            }
            private float m_progress;

            /// <summary>
            /// <inheritdoc/>
            /// </summary>
            protected override void ReceiveContentLength(int p_content_length) {
                bytesTotal = p_content_length;
                if(stream is FileStream) return;                
                MemoryStream ms = (MemoryStream)stream; 
                ms.Capacity = p_content_length; 
                ms.Position = 0;             
            }

            /// <summary>
            /// <inheritdoc/>
            /// </summary>
            protected override bool ReceiveData(byte[] p_data,int p_length) {
                if(stream==null) return false;
                bytesLoaded += p_length;                                
                //Fake increase progress in case bytes-total is not available yet
                if(bytesTotal<=0) m_progress += (1f-m_progress)*0.01f;                                        
                stream.Write(p_data,0,p_length);
                return true;
            }

            /// <summary>
            /// Disposes this downloaded
            /// </summary>
            new public void Dispose() {
                base.Dispose();
                if(stream != null) { stream.Dispose(); stream.Close(); stream=null; }
            }
        }
        #endregion

}

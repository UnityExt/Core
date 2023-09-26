using System.IO;
using System.Text;
using UnityEngine.Networking;
using NetMultipartFormDataContent = System.Net.Http.MultipartFormDataContent;
using NetHttpContent              = System.Net.Http.HttpContent;
using NetByteArrayContent         = System.Net.Http.ByteArrayContent;
using NetStreamContent            = System.Net.Http.StreamContent;
using NetStringContent            = System.Net.Http.StringContent;
using HttpStatusCode              = System.Net.HttpStatusCode;
using System.Security.Cryptography;

namespace UnityExt.Core {
    
    #region class UploadHandlerStream
    /// <summary>
    /// Class to wraps unity's upload handler and give it Stream support
    /// </summary>
    public class UploadHandlerStream {

        /// <summary>
        /// Helper to cast this handler into unity's upload handler
        /// </summary>
        /// <param name="d"></param>
        public static implicit operator UploadHandler(UploadHandlerStream d) { return d.handler; }
        
        /// <summary>
        /// Flag telling this handler is valid.
        /// </summary>
        public bool valid { get { return (stream!=null) && (handler!=null); } }

        /// <summary>
        /// Reference to the stream
        /// </summary>
        public Stream stream { get; private set; }

        /// <summary>
        /// Internal
        /// </summary>
        public UploadHandler handler { get; private set; }

        /// <summary>
        /// Content Type
        /// </summary>
        public string mime { get { return handler==null ? "" : handler.contentType; } }

        /// <summary>
        /// Upload progress
        /// </summary>
        public float progress { get { return handler==null ? 1f : handler.progress; } }
            
        /// <summary>
        /// CTOR.
        /// </summary>
        /// <param name="p_stream"></param>
        public UploadHandlerStream(Stream p_stream) {
            stream = p_stream;
            if(stream == null)         { handler = new UploadHandlerRaw(m_empty_form)               { contentType = "application/x-www-form-urlencoded" }; }
            if(stream is FileStream)   { handler = new UploadHandlerFile(((FileStream)stream).Name) { contentType = "application/x-www-form-urlencoded" }; }
            if(stream is MemoryStream) { 
                byte[] d = ((MemoryStream)stream).Length<=0 ? m_empty_form : ((MemoryStream)stream).ToArray();                    
                handler = new UploadHandlerRaw(d) { contentType = "application/x-www-form-urlencoded" };
            }
        }
        private byte[] m_empty_form = Encoding.ASCII.GetBytes("--unity-null-form--\r\n\r\n--unity-null-form--\r\n");
            
        /// <summary>
        /// Destroys this upload data
        /// </summary>
        public void Dispose() {
            if(handler!=null) handler.Dispose();
        }

    }
    #endregion

}

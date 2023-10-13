using System;
using System.Collections.Generic;
using System.IO;
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
    
    #region class HttpMethod
    /// <summary>
    /// HTTP defines a set of request methods to indicate the desired action to be performed for a given resource. 
    /// Although they can also be nouns, these request methods are sometimes referred to as HTTP verbs. 
    /// Each of them implements a different semantic, but some common features are shared by a group of them
    /// https://developer.mozilla.org/en-US/docs/Web/HTTP/Methods
    /// </summary>
    public class HttpMethod {
        /// <summary>
        /// No method specified
        /// </summary>
        public const string None = "NONE";
        /// <summary>
        /// The GET method requests a representation of the specified resource. Requests using GET should only retrieve data.
        /// </summary>
        public const string Get     = "GET";
        /// <summary>
        /// The HEAD method asks for a response identical to that of a GET request, but without the response body.
        /// </summary>
        public const string Head    = "HEAD";
        /// <summary>
        /// The POST method is used to submit an entity to the specified resource, often causing a change in state or side effects on the server.
        /// </summary>
        public const string Post    = "POST";
        /// <summary>
        /// The PUT method replaces all current representations of the target resource with the request payload.
        /// </summary>
        public const string Put     = "PUT";
        /// <summary>
        /// The DELETE method deletes the specified resource.
        /// </summary>
        public const string Delete  = "DELETE";
        /// <summary>
        /// The CONNECT method establishes a tunnel to the server identified by the target resource.
        /// </summary>
        public const string Connect = "CONNECT";
        /// <summary>
        /// The OPTIONS method is used to describe the communication options for the target resource.
        /// </summary>
        public const string Options = "OPTIONS";
        /// <summary>
        /// The TRACE method performs a message loop-back test along the path to the target resource.
        /// </summary>
        public const string Trace   = "TRACE";
        /// <summary>
        /// The PATCH method is used to apply partial modifications to a resource.
        /// </summary>
        public const string Patch   = "PATCH";   
        /// <summary>
        /// 
        /// </summary>
        public const string Create   = "CREATE";   
    }
    #endregion

    #region HttpStatusCode
    /// <summary>
    /// HTTP response status codes indicate whether a specific HTTP request has been successfully completed. Responses are grouped in five classes:
    /// Info          (100–199)
    /// Successful    (200–299)
    /// Redirects     (300–399)
    /// Client Errors (400–499)
    /// Server Errors (500–599)
    /// The below status codes are defined by section 10 of RFC 2616. You can find an updated specification in RFC 7231.
    /// https://developer.mozilla.org/en-US/docs/Web/HTTP/Status
    /// </summary>    
    #endregion

    #region class HttpQuery
    /// <summary>
    /// Class that wraps the setup of a query string.
    /// </summary>
    public class HttpQuery {

        /// <summary>
        /// Internal.
        /// </summary>
        private List<string> m_fields;
        private bool m_dirty;

        /// <summary>
        /// CTOR.
        /// </summary>
        public HttpQuery() {
            m_dirty = true;
        }

        /// <summary>
        /// Clears all args.
        /// </summary>
        public void Clear() {
            if(m_fields!=null) m_fields.Clear();
            m_dirty=true;
        }

        #region Add
        /// <summary>
        /// Set the query string variable-value pair with a query-string compliant indexer.
        /// </summary>
        /// <param name="p_indexer">Array indexer, can be an int or string, if an empty string is provided its the auto indexed value '[]'</param>
        /// <param name="p_var">Variable Name</param>
        /// <param name="p_value">Variable Value</param>        
        public void Add(object p_indexer,string p_var,string p_value) { 
            if(m_fields==null) m_fields = new List<string>();
            string k = p_indexer==null ? null : p_indexer.ToString();
            if(k==null) k = ""; else k = $"[{k}]";
            //k==null -> ''
            //k==""   -> '[]'
            //k!=""   -> '[val]'
            string v = $"{p_var}{k}={p_value}";
            if(string.IsNullOrEmpty(v)) return;
            m_fields.Add(v);   
            m_dirty = true;
        }

        /// <summary>
        /// Set the query string variable-value pair without any indexing.
        /// </summary>
        /// <param name="p_var">Variable Name</param>
        /// <param name="p_value">Variable Value</param>        
        public void Add(string p_var,string p_value) { Add((object)null,p_var,p_value); }

        /// <summary>
        /// Set the query string variable-value pair with a query-string compliant indexer.
        /// </summary>
        /// <param name="p_indexer">Array indexer, can be an int or string, if an empty string is provided its the auto indexed value '[]'</param>
        /// <param name="p_var">Variable Name</param>
        /// <param name="p_value">BinaryData to be converted into base64</param>        
        public void AddBase64(object p_indexer,string p_var,object p_value) {
            if(p_value==null) return;
            Base64Serializer s = new Base64Serializer();
            StringBuilder sb = new StringBuilder();
            if(p_value is byte[]) s.Serialize((byte[])p_value,sb);
            else                  s.Serialize(p_value.ToString(),sb);
            Add(p_indexer,p_var,sb.ToString());
        }

        /// <summary>
        /// Set the query string variable-value pair without any indexing.
        /// </summary>        
        /// <param name="p_var">Variable Name</param>
        /// <param name="p_value">BinaryData to be converted into base64</param>        
        public void AddBase64(string p_var,object p_value) { AddBase64(null,p_var,p_value); }

        /// <summary>
        /// Set the query string variable-value pair with a query-string compliant indexer.
        /// </summary>
        /// <param name="p_indexer">Array indexer, can be an int or string, if an empty string is provided its the auto indexed value '[]'</param>
        /// <param name="p_var">Variable Name</param>
        /// <param name="p_value">Object to be converted into json</param>        
        /// <param name="p_base64_encode">Flag that tells to after json parsing it should be encoded in base64</param>        
        public void AddJson(object p_indexer,string p_var,object p_value,bool p_base64_encode=false) {
            JSONSerializer s = new JSONSerializer();
            StringBuilder sb = new StringBuilder();
            s.Serialize(p_value,sb, p_base64_encode ? SerializerAttrib.Base64 : SerializerAttrib.None);
            Add(p_indexer,p_var,sb.ToString());
        }

        /// <summary>
        /// Set the query string variable-value pair without any indexing.
        /// </summary>                
        /// <param name="p_var">Variable Name</param>
        /// <param name="p_value">Object to be converted into json</param>        
        /// <param name="p_base64_encode">Flag that tells to after json parsing it should be encoded in base64</param>        
        public void AddJson(string p_var,byte[] p_value,bool p_base64_encode=false) { AddJson(null,p_var,p_value,p_base64_encode); }

        /// <summary>
        /// Set the query string with all values in the list, properly indexing them.
        /// </summary>
        /// <param name="p_var">Variable Name</param>
        /// <param name="p_values">List of values to compose the query string.</param>
        public void Add(string p_var,System.Collections.IList p_values) {
            if(p_values==null) return;
            for(int i = 0; i < p_values.Count; i++) { Add(i,p_var,p_values[i]==null ? "" : p_values[i].ToString()); }
        }

        /// <summary>
        /// Set the query string with all values in the dictionary, properly indexing them.
        /// </summary>
        /// <param name="p_var">Variable Name</param>
        /// <param name="p_values">Table of key-values to compose the query string</param>
        public void Add(string p_var,System.Collections.IDictionary p_values) {
            if(p_values==null) return;
            foreach(System.Collections.DictionaryEntry it in p_values) {
                object k = it.Key;
                object v = it.Value;
                if(k==null) continue;
                string ks = k.ToString();
                string vs = it.Value==null ? "" : it.Value.ToString();
                Add(ks,p_var,vs);
            }
        }

        #endregion

        /// <summary>
        /// Returns the string representation of the query string.
        /// </summary>
        /// <param name="p_escaped">Flag that tells to apply char escaping.</param>
        /// <returns></returns>
        public string ToString(bool p_escaped) {            
            
            int c = m_fields==null ? 0 : m_fields.Count;
            if(c<=0) return "";

            if(!m_dirty) return p_escaped ? m_to_string_cache : m_to_string_cache_escaped;

            StringBuilder sb = new StringBuilder();
            for(int i=0;i<c;i++) {
                string it = m_fields[i];
                sb.Append(i<=0 ? '?' : '&');
                sb.Append(it);
            }        
            string sb_s = sb.ToString();

            m_to_string_cache         = sb_s;
            m_to_string_cache_escaped = Uri.EscapeUriString(sb_s);

            m_dirty = false;

            return p_escaped ? m_to_string_cache : m_to_string_cache_escaped;
        }
        private string m_to_string_cache;
        private string m_to_string_cache_escaped;

        /// <summary>
        /// Returns the unescaped query string value.
        /// </summary>
        /// <returns></returns>
        public override string ToString() { return ToString(false); }

    }
    #endregion

    #region class HttpHeader
    /// <summary>
    /// Class that wraps header information of a request/response content.
    /// </summary>
    public class HttpHeader : Dictionary<string,object> {

        /// <summary>
        /// Copy all KeyValues of this header.
        /// </summary>
        /// <param name="p_request"></param>
        public void CopyTo(UnityWebRequest p_request) {
            if(p_request==null) return;
            foreach(KeyValuePair<string,object> it in this) {
                string k = it.Key;
                object v = it.Value;
                string vs = v==null ? "" : v.ToString();
                p_request.SetRequestHeader(k,vs);
            }
        }

        /// <summary>
        /// Copy all KeyValues from the input request.
        /// </summary>
        /// <param name="p_response"></param>
        public void CopyFrom(UnityWebRequest p_response) {
            if(p_response==null) return;
            Dictionary<string,string> rh = p_response.GetResponseHeaders();
            if(rh==null) return;
            foreach(KeyValuePair<string,string> it in rh) {
                this[it.Key] = it.Value;
            }
        }

    }
    #endregion

    #region class HttpBody
    /// <summary>
    /// Class that wraps the body of a request/response content.
    /// </summary>
    public class HttpBody {

        #region class Field
        /// <summary>
        /// Field Type
        /// </summary>
        internal enum FieldType {
            String=0,
            File,
            Binary,
            Json,
            Base64,
            ImageJPEG,
            ImagePNG            
        }
        /// <summary>
        /// Middleware data to be serialized before body processing
        /// </summary>
        internal class Field {
            public FieldType type;
            public string name;            
            public object value;
            public string filename;
            public Stream stream;
            public string mime {
                get {
                    switch(type) {
                        case FieldType.String:    return "text/plain";
                        case FieldType.File:      return "application/octet-stream";
                        case FieldType.Binary:    return "application/octet-stream";
                        case FieldType.Json:      return "application/json";
                        case FieldType.ImageJPEG: return "image/jpeg";
                        case FieldType.ImagePNG:  return "image/png";                             
                    }
                    return "text/plain";
                }
            }
            
            /// <summary>
            /// Creates the BaseStream of the field, if file creates a temp file to hold the information.
            /// </summary>
            /// <param name="p_file_mode"></param>
            public void SetBaseStream(bool p_file_mode) {
                if(p_file_mode) { stream = File.Create($"{WebRequest.GetTempFilePath(name)}"); return; }
                stream = new MemoryStream();
            }

            #region Write
            protected void WriteString()         { StreamWriter sw = new StreamWriter(stream); sw.Write(value==null ? ""        : (string)value); sw.Flush(); }
            protected void WriteBytes (byte[] v) { BinaryWriter sw = new BinaryWriter(stream); sw.Write(v==null ? m_empty_bytes : (byte[])v);     sw.Flush(); }
            protected void WriteBytes ()         { WriteBytes((byte[])value); }            
            protected void WriteJson() {
                //If pure json
                if(value is string) { WriteString(); return; }
                JSONSerializer ds = new JSONSerializer();
                ds.Serialize(value,stream);
            }
            protected void WriteBase64() {
                Base64Serializer ds = new Base64Serializer();
                ds.Serialize(value,stream);
            }
            protected void WriteImage() {                
                if(value is byte[]) { WriteBytes(); return;  }
                byte[] v = null;
                Texture2D img = value as Texture2D;
                if(type == FieldType.ImageJPEG)if(img) { v = img.EncodeToJPG(); }
                if(type == FieldType.ImagePNG) if(img) { v = img.EncodeToPNG(); }
                WriteBytes(v);
            }
            #endregion

            /// <summary>
            /// Submit all the local data into the associated streams.
            /// </summary>
            public void Flush() {
                if(stream==null) { Debug.LogWarning($"HttpBody> Flush / Field [{type}.{name}] has no Stream to write into."); return; }
                switch(type) {
                    case FieldType.String:    WriteString(); break;
                    case FieldType.Binary:    WriteBytes (); break;
                    case FieldType.ImageJPEG: WriteImage (); break;
                    case FieldType.ImagePNG:  WriteImage (); break;
                    case FieldType.Base64:    WriteBase64(); break;
                    case FieldType.Json:      WriteJson  (); break;
                }
                //Reset reader position
                stream.Position=0;
            }
            static private byte[] m_empty_bytes = new byte[0];

            /// <summary>
            /// Disposes this field stream and delete the temp file if any
            /// </summary>
            public void Dispose() {
                if(stream==null) return;
                FileInfo fi = null;
                if(stream is FileStream) fi = new FileInfo(((FileStream)stream).Name);
                if(fi!=null) fi.Delete();
                stream.Close();
                stream.Dispose(); 
                stream = null; 
            }

        }
        #endregion

        /// <summary>
        /// Reference to the form content if any.
        /// </summary>
        public NetMultipartFormDataContent form { get; private set; }

        /// <summary>
        /// Reference to the stream containing this body data.
        /// </summary>
        public Stream stream { get; set; }

        /// <summary>
        /// Size of this content in bytes.
        /// </summary>
        public long length { get { return stream==null ? 0 : stream.Length; } }

        /// <summary>
        /// List of added fields.
        /// </summary>
        internal List<Field> m_fields;
        internal List<Field> m_flush_list;

        /// <summary>
        /// CTOR.
        /// </summary>
        public HttpBody() {
            m_fields = new List<Field>();
        }

        #region Add

        public void AddField  (object p_indexer,string p_name,string       p_value                     ) { AddField(FieldType.String,   p_indexer,p_name,p_value,""        ); }
        public void AddField  (object p_indexer,string p_name,MemoryStream p_value,string p_filename="") { AddField(FieldType.Binary,   p_indexer,p_name,p_value,p_filename); }
        public void AddField  (object p_indexer,string p_name,byte[]       p_value,string p_filename="") { AddField(FieldType.Binary,   p_indexer,p_name,p_value,p_filename); }
        public void AddFile   (object p_indexer,string p_name,FileStream   p_value,string p_filename="") { AddField(FieldType.File,     p_indexer,p_name,p_value,p_filename); }
        public void AddFile   (object p_indexer,string p_name,string       p_path ,string p_filename="") { AddField(FieldType.File,     p_indexer,p_name,p_path ,p_filename); }        
        public void AddJson   (object p_indexer,string p_name,string       p_value,string p_filename="") { AddField(FieldType.Json,     p_indexer,p_name,p_value,p_filename); }
        public void AddJson   (object p_indexer,string p_name,Stream       p_value,string p_filename="") { AddField(FieldType.Json,     p_indexer,p_name,p_value,p_filename); }
        public void AddJson   (object p_indexer,string p_name,object       p_value,string p_filename="") { AddField(FieldType.Json,     p_indexer,p_name,p_value,p_filename); }
        public void AddBase64 (object p_indexer,string p_name,string       p_value,string p_filename="") { AddField(FieldType.Base64,   p_indexer,p_name,p_value,p_filename); }
        public void AddBase64 (object p_indexer,string p_name,byte[]       p_value,string p_filename="") { AddField(FieldType.Base64,   p_indexer,p_name,p_value,p_filename); }
        public void AddJPEG   (object p_indexer,string p_name,byte[]       p_value,string p_filename="") { AddField(FieldType.ImageJPEG,p_indexer,p_name,p_value,p_filename); }
        public void AddJPEG   (object p_indexer,string p_name,Texture2D    p_value,string p_filename="") { AddField(FieldType.ImageJPEG,p_indexer,p_name,p_value,p_filename); }
        public void AddPNG    (object p_indexer,string p_name,byte[]       p_value,string p_filename="") { AddField(FieldType.ImagePNG, p_indexer,p_name,p_value,p_filename); }
        public void AddPNG    (object p_indexer,string p_name,Texture2D    p_value,string p_filename="") { AddField(FieldType.ImagePNG, p_indexer,p_name,p_value,p_filename); }

        public void AddField  (string p_name,string       p_value                     ) { AddField(FieldType.String,   null, p_name,p_value,        ""); }        
        public void AddField  (string p_name,MemoryStream p_value,string p_filename="") { AddField(FieldType.Binary,   null, p_name,p_value,p_filename); }        
        public void AddField  (string p_name,byte[]       p_value,string p_filename="") { AddField(FieldType.Binary,   null, p_name,p_value,p_filename); }        
        public void AddFile   (string p_name,FileStream   p_value,string p_filename="") { AddField(FieldType.File,     null, p_name,p_value,p_filename); }        
        public void AddFile   (string p_name,string       p_path ,string p_filename="") { AddField(FieldType.File,     null, p_name,p_path ,p_filename); }       
        public void AddJson   (string p_name,string       p_value,string p_filename="") { AddField(FieldType.Json,     null, p_name,p_value,p_filename); }
        public void AddJson   (string p_name,Stream       p_value,string p_filename="") { AddField(FieldType.Json,     null, p_name,p_value,p_filename); }
        public void AddJson   (string p_name,object       p_value,string p_filename="") { AddField(FieldType.Json,     null, p_name,p_value,p_filename); }
        public void AddBase64 (string p_name,string       p_value,string p_filename="") { AddField(FieldType.Base64,   null, p_name,p_value,p_filename); }
        public void AddBase64 (string p_name,byte[]       p_value,string p_filename="") { AddField(FieldType.Base64,   null, p_name,p_value,p_filename); }
        public void AddBase64 (string p_name,Stream       p_value,string p_filename="") { AddField(FieldType.Base64,   null, p_name,p_value,p_filename); }
        public void AddJPEG   (string p_name,byte[]       p_value,string p_filename="") { AddField(FieldType.ImageJPEG,null, p_name,p_value,p_filename); }        
        public void AddJPEG   (string p_name,Texture2D    p_value,string p_filename="") { AddField(FieldType.ImageJPEG,null, p_name,p_value,p_filename); }        
        public void AddPNG    (string p_name,byte[]       p_value,string p_filename="") { AddField(FieldType.ImagePNG, null, p_name,p_value,p_filename); }        
        public void AddPNG    (string p_name,Texture2D    p_value,string p_filename="") { AddField(FieldType.ImagePNG, null, p_name,p_value,p_filename); }

        public void AddFields(string p_name,System.Collections.IList p_values) {
            if(p_values==null) return;
            int c = p_values.Count;
            if(c<=0)return;
            for(int i=0;i<p_values.Count;i++) {
                object v  = p_values[i];
                string vs = v==null ? "" : v.ToString();
                AddField(i,p_name,vs);
            }
        }

        public void AddFields(string p_name,System.Collections.IDictionary p_values) {
            if(p_values==null) return;
            int c = p_values.Count;
            if(c<=0)return;            
            foreach(System.Collections.DictionaryEntry it in p_values) {
                object k = it.Key;
                object v = it.Value;
                if(k==null) continue;
                string ks = k.ToString();
                string vs = it.Value==null ? "" : it.Value.ToString();
                AddField(ks,p_name,vs);
            }
        }

        /// <summary>
        /// Helper to create and populate a field.
        /// </summary>        
        internal Field AddField(FieldType p_type,object p_indexer,string p_name,object p_value,string p_filename) {
            string k = p_indexer==null ? "" : $"[{p_indexer}]";
            Field f = new Field() { type=p_type,name=$"{p_name}{k}",value=p_value,filename=p_filename};
            m_fields.Add(f);
            return f;
        }

        #endregion

        /// <summary>
        /// Clears all stored data.
        /// </summary>
        public void Dispose() {
            DisposeFields();
            if(form  !=null) { form.Dispose(); form=null; }
            if(stream==null) return;
            stream.Close();
            if(stream is FileStream) {
                FileStream fs = stream as FileStream;
                FileInfo fi = new FileInfo(fs.Name);
                try { if(fi.Exists) fi.Delete(); } catch(Exception) { }
            }             
            stream.Dispose(); 
            stream=null;            
        }

        #region Internals

        /// <summary>
        /// Clears any invalid field.
        /// </summary>
        internal void Initialize(Stream p_stream) {
            stream = p_stream;            
            m_fields.RemoveAll(
            delegate(Field it) { 
                if(it==null) return true; 
                if(string.IsNullOrEmpty(it.name)) return true;
                return false;
            });
            for(int i=0;i<m_fields.Count;i++) { Field it = m_fields[i]; it.SetBaseStream(stream is FileStream); }
            m_flush_list = new List<Field>(m_fields);
            if(m_fields.Count>0) if(form==null) form = new NetMultipartFormDataContent();
        }

        /// <summary>
        /// Flush field items one at a time
        /// </summary>
        /// <returns></returns>
        internal  bool FlushStep(bool p_unity_ctx) {
            if(m_flush_list.Count<=0) return false;
            bool has_flushed = false;
            for(int i=0;i<m_flush_list.Count;i++) {
                Field it = m_flush_list[i];                
                if(!FlushField(it,p_unity_ctx)) continue;                
                m_flush_list.RemoveAt(i--);                
                has_flushed=true;
                break;
            }
            if(!has_flushed) return false;
            return m_fields.Count>0;
        }

        /// <summary>
        /// Parse and register all non unity elements
        /// </summary>
        internal void Flush(bool p_unity_ctx) {
            for(int i=0;i<m_flush_list.Count;i++) { FlushStep(p_unity_ctx); }
        }

        /// <summary>
        /// Creates the internal form content of the field and remove from the processing pool
        /// </summary>        
        internal bool FlushField(Field p_field,bool p_unity_ctx) {
            Field f = p_field;
            if(f==null) return true;
            if(f.stream==null) f.SetBaseStream(stream is FileStream);            
            if(!p_unity_ctx) {
                bool is_image = (f.type == FieldType.ImageJPEG) || (f.type == FieldType.ImagePNG);
                if(is_image)               return false;
                if((f.value is Texture))   return false;                                                
            }
            f.Flush();
            //Create the form content
            NetStreamContent sc = new NetStreamContent(f.stream);
            //Fill the content type header
            sc.Headers.ContentType   = new System.Net.Http.Headers.MediaTypeHeaderValue(f.mime);
            sc.Headers.ContentLength = f.stream.Length;
            //Add the field
            if(string.IsNullOrEmpty(f.filename)) form.Add(sc,f.name); else form.Add(sc,f.name,f.filename);
            //Field processed
            return true;
        }

        /// <summary>
        /// Flush the form information into the stream
        /// </summary>
        /// <returns></returns>
        internal Process FlushFormAsync() {
            //If no data skip a frame and continue            
            if(stream==null) return Process.Delay(1f/60f);
            if(form==null)   return Process.Delay(1f/60f);
            Task tsk = null;
            Process form_flush = 
            Process.Start(delegate(ProcessContext ctx, Process p) {
                if(tsk == null) { tsk = form.CopyToAsync(stream); return true; }
                bool is_completed = tsk.IsCompleted || tsk.IsFaulted || tsk.IsCanceled;
                if(!is_completed) return true;
                p.name = "form-success";
                if(tsk.IsCanceled) p.name = "form-cancel";
                if(tsk.IsFaulted)  p.name = "form-error";
                return false;
            });
            form_flush.name = "web-body-form-flush";
            return form_flush;
        }

        /// <summary>
        /// Disposes the fields only
        /// </summary>
        internal void DisposeFields() {
            for(int i=0;i<m_fields.Count;i++) { Field it = m_fields[i]; if(it!=null) it.Dispose(); }
            m_fields.Clear();
            if(m_flush_list!=null) m_flush_list.Clear();
        }

        #endregion

    }
    #endregion

    #region class HttpRequest
    /// <summary>
    /// Class that wraps the content of a request/response
    /// </summary>
    public class HttpRequest {

        /// <summary>
        /// Web Request Header
        /// </summary>
        public HttpHeader header { get; private set; }

        /// <summary>
        /// Web Request Body
        /// </summary>
        public HttpBody body { get; private set; }

        /// <summary>
        /// Request processing progress.
        /// </summary>
        public float progress { get; internal set; }

        /// <summary>
        /// CTOR.
        /// </summary>
        public HttpRequest() {
            header = new HttpHeader();
            body   = new HttpBody();
        }

        /// <summary>
        /// Disposes the header and body information
        /// </summary>
        public void Dispose() {
            if(header!=null) header.Clear();
            if(body  !=null) body.Dispose();
        }

    }
#endregion


}
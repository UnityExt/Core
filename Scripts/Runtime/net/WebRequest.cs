using System;
using System.IO;
using UnityExt.Core.IO;
using UnityEngine;
using UnityEngine.Networking;
using NetMultipartFormDataContent = System.Net.Http.MultipartFormDataContent;
using NetHttpContent              = System.Net.Http.HttpContent;
using NetByteArrayContent         = System.Net.Http.ByteArrayContent;
using NetStreamContent            = System.Net.Http.StreamContent;
using NetStringContent            = System.Net.Http.StringContent;
using HttpStatusCode              = System.Net.HttpStatusCode;
using System.Security.Cryptography;

namespace UnityExt.Core.Net {

    #region enum WebRequestState
    /// <summary>
    /// WebRequest Execution State
    /// </summary>
    public enum WebRequestState : byte {
        Idle=0,
        Start,
        Create,
        Cached,        
        UploadStart,
        UploadProgress,
        UploadComplete,
        DownloadStart,
        DownloadProgress,
        DownloadComplete,
        Success,
        Timeout,
        Cancel,
        Error
    }
    #endregion

    #region enum WebRequestAttrib
    /// <summary>
    /// Enumeration that define bit flags to configure the web request behavior.
    /// </summary>
    [Flags]
    public enum WebRequestAttrib : byte {
        /// <summary>
        /// No Flags
        /// </summary>
        None          = 0,
        /// <summary>
        /// Uses the global default buffer mode
        /// </summary>
        DefaultBuffer = (1<<1),
        /// <summary>
        /// Uses the FileStream to hold upload/download data.
        /// </summary>
        FileBuffer    = (1<<2),
        /// <summary>
        /// Uses the MemoryStream to hold upload/download data.
        /// </summary>
        MemoryBuffer  = (1<<3),
        /// <summary>
        /// Mask to extract the buffer bits
        /// </summary>
        BufferMask     = DefaultBuffer|FileBuffer|MemoryBuffer,
        /// <summary>
        /// Uses the global default cache mode.
        /// </summary>
        DefaultCache  = (1<<4),
        /// <summary>
        /// Do not cache results
        /// </summary>
        NoCache       = (1<<5),
        /// <summary>
        /// Saves cache in FileStream
        /// </summary>
        FileCache     = (1<<6),
        /// <summary>
        /// Saves the cache in MemoryStream
        /// </summary>
        MemoryCache   = (1<<7),
        /// <summary>
        /// Mask to extract the cache mode bits
        /// </summary>
        CacheMask     = DefaultCache|NoCache|FileCache|MemoryCache
    }
    #endregion

    #region class WebRequest
    /// <summary>
    /// Class that wraps Unity's web request functionality for improved memory management and performance.
    /// </summary>
    public class WebRequest : Activity {

        #region static 

        /// <summary>
        /// CTOR.
        /// </summary>
        static WebRequest() {
            //Store due threads.
            m_persistent_path = Application.persistentDataPath;
        }
        static private string m_persistent_path;

        #region Consts

        /// <summary>
        /// Default option for buffering mode.
        /// </summary>
        static public WebRequestAttrib DefaultBufferMode = WebRequestAttrib.FileBuffer;
        
        /// <summary>
        /// Default option for caching mode.
        /// </summary>
        static public WebRequestAttrib DefaultCacheMode  = WebRequestAttrib.FileCache;

        /// <summary>
        /// Flag telling in which execution context the data must be processed.
        /// </summary>
        static public ActivityContext DataProcessContext = ActivityContext.Thread;

        /// <summary>
        /// Default Cache TTL in Minutes
        /// </summary>
        static public float DefaultCacheTTL = 1f;

        /// <summary>
        /// Default Timeout in seconds
        /// </summary>
        static public float DefaultTimeout = 10f;

        /// <summary>
        /// Returns the folder to write temprary form data.
        /// </summary>
        static public string DataPath {
            get {                
                string path = $"{m_persistent_path}/unityex/{Application.platform.ToString().ToLower()}/web/";
                path.Replace('\\','/').Replace("//","/");
                if(!Directory.Exists(path)) Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>
        /// Returns the folder to write temprary form data.
        /// </summary>
        static public string TempPath {
            get {                
                string path = $"{DataPath}temp/";
                if(!Directory.Exists(path)) Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>
        /// Returns the folder to write cached results
        /// </summary>
        static public string CachePath {
            get {                
                string path = $"{DataPath}cache/"; 
                if(!Directory.Exists(path)) Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>
        /// Helper to return a temp file name in the proper path
        /// </summary>        
        static internal string GetTempFilePath(string p_name) { return $"{TempPath}{GetTempFileName(p_name)}"; }

        /// <summary>
        /// Helper to return a temp file name
        /// </summary>        
        static internal string GetTempFileName(string p_name) {
            if(m_rnd==null) m_rnd = new System.Random();
            m_rnd.Next();
            ulong  hn   = (ulong)(((double)0xffffffff) * m_rnd.NextDouble());
            string h    = hn.ToString("x");
            return $"{h}_{p_name}";
        }
        static private System.Random m_rnd;

        /// <summary>
        /// Flag that tells the machine's filesystem can be used.
        /// </summary>
        static public bool CanUseFileSystem {
            get {
                #if UNITY_WEBGL
                return true;
                #endif
                #if !UNITY_WEBGL
                if(m_can_use_fs!=null) return (bool)m_can_use_fs;
                m_can_use_fs = true;
                try {
                    //Force an exception to confirm the filesystem can be used
                    System.Security.AccessControl.DirectorySecurity ds = Directory.GetAccessControl(DataPath);
                }
                catch (UnauthorizedAccessException) {
                    m_can_use_fs=false;
                }
                return (bool)m_can_use_fs;
                #endif
            }
        }
        static private object m_can_use_fs;

        /// <summary>
        /// Auxiliary method to give control when to trigger the file system cleanup and initialization.
        /// </summary>
        static public void InitDataFileSystem() {
            if(m_fs_init) return;
            m_fs_init=true;
            //Cache FS check result
            bool can_use_fs = CanUseFileSystem;
            WebRequestCache.Load(CachePath);
            DirectoryInfo di = new DirectoryInfo(TempPath);
            FileInfo[] fl = di.GetFiles("*");
            for(int i=0;i<fl.Length;i++) fl[i].Delete();
        }
        static private bool m_fs_init;

        #endregion

        #region Operations

        #region Create
        /// <summary>
        /// Creates and run a request with the informed arguments.
        /// </summary>
        /// <param name="p_method">Http Method</param>
        /// <param name="p_url">Request URL</param>
        /// <param name="p_attribs">Request attribute flags</param>
        /// <param name="p_query">URL query string</param>
        /// <param name="p_request">Request data</param>
        /// <param name="p_callback">Request events handler</param>
        /// <returns>Running web request</returns>
        static public WebRequest Send(string p_method,string p_url,WebRequestAttrib p_attribs,HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback=null) {
            WebRequest req = new WebRequest();
            if(p_query  !=null) req.query   = p_query;
            if(p_request!=null) req.request = p_request;
            req.OnRequestEvent = p_callback;
            req.Send(p_method,p_url,p_attribs);
            return req;
        }
        static public WebRequest Send(string p_method,string p_url,WebRequestAttrib p_attribs,HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(p_method,p_url,p_attribs,p_query,null     ,p_callback); }
        static public WebRequest Send(string p_method,string p_url,WebRequestAttrib p_attribs,                  HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(p_method,p_url,p_attribs,null   ,p_request,p_callback); }
        static public WebRequest Send(string p_method,string p_url,WebRequestAttrib p_attribs,                                        Action<WebRequest> p_callback = null) { return Send(p_method,p_url,p_attribs,null   ,null     ,p_callback); }
        static public WebRequest Send(string p_method,string p_url,                           HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(p_method,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,p_query,p_request,p_callback); }
        static public WebRequest Send(string p_method,string p_url,                           HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(p_method,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,p_query,null     ,p_callback); }
        static public WebRequest Send(string p_method,string p_url,                                                                   Action<WebRequest> p_callback = null) { return Send(p_method,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,null   ,null     ,p_callback); }
        #endregion

        #region Get
        /// <summary>
        /// Creates and run a GET request with the informed arguments.
        /// </summary>        
        /// <param name="p_url">Request URL</param>
        /// <param name="p_attribs">Request attribute flags</param>
        /// <param name="p_query">URL query string</param>
        /// <param name="p_request">Request data (headers only)</param>
        /// <param name="p_callback">Request events handler</param>
        /// <returns>Running web request</returns>
        static public WebRequest Get(string p_url,WebRequestAttrib p_attribs,HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Get,p_url,p_attribs,p_query,p_request,p_callback); }
        static public WebRequest Get(string p_url,WebRequestAttrib p_attribs,HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Get,p_url,p_attribs,p_query,null     ,p_callback); }
        static public WebRequest Get(string p_url,WebRequestAttrib p_attribs,                  HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Get,p_url,p_attribs,null   ,p_request,p_callback); }
        static public WebRequest Get(string p_url,WebRequestAttrib p_attribs,                                        Action<WebRequest> p_callback = null) { return Send(HttpMethod.Get,p_url,p_attribs,null   ,null     ,p_callback); }
        static public WebRequest Get(string p_url,                           HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Get,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,p_query,p_request,p_callback); }
        static public WebRequest Get(string p_url,                           HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Get,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,p_query,null     ,p_callback); }
        static public WebRequest Get(string p_url,                                                                   Action<WebRequest> p_callback = null) { return Send(HttpMethod.Get,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,null   ,null     ,p_callback); }
        #endregion

        #region Post
        /// <summary>
        /// Creates and run a POST request with the informed arguments.
        /// </summary>        
        /// <param name="p_url">Request URL</param>
        /// <param name="p_attribs">Request attribute flags</param>
        /// <param name="p_query">URL query string</param>
        /// <param name="p_request">Request data (headers only)</param>
        /// <param name="p_callback">Request events handler</param>
        /// <returns>Running web request</returns>
        static public WebRequest Post(string p_url,WebRequestAttrib p_attribs,HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Post,p_url,p_attribs,p_query,p_request,p_callback); }
        static public WebRequest Post(string p_url,WebRequestAttrib p_attribs,HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Post,p_url,p_attribs,p_query,null     ,p_callback); }
        static public WebRequest Post(string p_url,WebRequestAttrib p_attribs,                  HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Post,p_url,p_attribs,null   ,p_request,p_callback); }
        static public WebRequest Post(string p_url,WebRequestAttrib p_attribs,                                        Action<WebRequest> p_callback = null) { return Send(HttpMethod.Post,p_url,p_attribs,null   ,null     ,p_callback); }
        static public WebRequest Post(string p_url,                           HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Post,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,p_query,p_request,p_callback); }
        static public WebRequest Post(string p_url,                           HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Post,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,p_query,null     ,p_callback); }
        static public WebRequest Post(string p_url,                                                                   Action<WebRequest> p_callback = null) { return Send(HttpMethod.Post,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,null   ,null     ,p_callback); }
        #endregion

        #region Put
        /// <summary>
        /// Creates and run a PUT request with the informed arguments.
        /// </summary>        
        /// <param name="p_url">Request URL</param>
        /// <param name="p_attribs">Request attribute flags</param>
        /// <param name="p_query">URL query string</param>
        /// <param name="p_request">Request data (headers only)</param>
        /// <param name="p_callback">Request events handler</param>
        /// <returns>Running web request</returns>
        static public WebRequest Put(string p_url,WebRequestAttrib p_attribs,HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Put,p_url,p_attribs,p_query,p_request,p_callback); }
        static public WebRequest Put(string p_url,WebRequestAttrib p_attribs,HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Put,p_url,p_attribs,p_query,null     ,p_callback); }
        static public WebRequest Put(string p_url,WebRequestAttrib p_attribs,                  HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Put,p_url,p_attribs,null   ,p_request,p_callback); }
        static public WebRequest Put(string p_url,WebRequestAttrib p_attribs,                                        Action<WebRequest> p_callback = null) { return Send(HttpMethod.Put,p_url,p_attribs,null   ,null     ,p_callback); }
        static public WebRequest Put(string p_url,                           HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Put,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,p_query,p_request,p_callback); }
        static public WebRequest Put(string p_url,                           HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Put,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,p_query,null     ,p_callback); }
        static public WebRequest Put(string p_url,                                                                   Action<WebRequest> p_callback = null) { return Send(HttpMethod.Put,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,null   ,null     ,p_callback); }
        #endregion

        #region Delete
        /// <summary>
        /// Creates and run a DELETE request with the informed arguments.
        /// </summary>        
        /// <param name="p_url">Request URL</param>
        /// <param name="p_attribs">Request attribute flags</param>
        /// <param name="p_query">URL query string</param>
        /// <param name="p_request">Request data (headers only)</param>
        /// <param name="p_callback">Request events handler</param>
        /// <returns>Running web request</returns>
        static public WebRequest Delete(string p_url,WebRequestAttrib p_attribs,HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Delete,p_url,p_attribs,p_query,p_request,p_callback); }
        static public WebRequest Delete(string p_url,WebRequestAttrib p_attribs,HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Delete,p_url,p_attribs,p_query,null     ,p_callback); }
        static public WebRequest Delete(string p_url,WebRequestAttrib p_attribs,                  HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Delete,p_url,p_attribs,null   ,p_request,p_callback); }
        static public WebRequest Delete(string p_url,WebRequestAttrib p_attribs,                                        Action<WebRequest> p_callback = null) { return Send(HttpMethod.Delete,p_url,p_attribs,null   ,null     ,p_callback); }
        static public WebRequest Delete(string p_url,                           HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Delete,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,p_query,p_request,p_callback); }
        static public WebRequest Delete(string p_url,                           HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Delete,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,p_query,null     ,p_callback); }
        static public WebRequest Delete(string p_url,                                                                   Action<WebRequest> p_callback = null) { return Send(HttpMethod.Delete,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,null   ,null     ,p_callback); }
        #endregion

        #region Head
        /// <summary>
        /// Creates and run a HEAD request with the informed arguments.
        /// </summary>        
        /// <param name="p_url">Request URL</param>
        /// <param name="p_attribs">Request attribute flags</param>
        /// <param name="p_query">URL query string</param>
        /// <param name="p_request">Request data (headers only)</param>
        /// <param name="p_callback">Request events handler</param>
        /// <returns>Running web request</returns>
        static public WebRequest Head(string p_url,WebRequestAttrib p_attribs,HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Head,p_url,p_attribs,p_query,p_request,p_callback); }
        static public WebRequest Head(string p_url,WebRequestAttrib p_attribs,HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Head,p_url,p_attribs,p_query,null     ,p_callback); }
        static public WebRequest Head(string p_url,WebRequestAttrib p_attribs,                  HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Head,p_url,p_attribs,null   ,p_request,p_callback); }
        static public WebRequest Head(string p_url,WebRequestAttrib p_attribs,                                        Action<WebRequest> p_callback = null) { return Send(HttpMethod.Head,p_url,p_attribs,null   ,null     ,p_callback); }
        static public WebRequest Head(string p_url,                           HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Head,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,p_query,p_request,p_callback); }
        static public WebRequest Head(string p_url,                           HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Head,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,p_query,null     ,p_callback); }
        static public WebRequest Head(string p_url,                                                                   Action<WebRequest> p_callback = null) { return Send(HttpMethod.Head,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,null   ,null     ,p_callback); }
        #endregion

        #region Create
        /// <summary>
        /// Creates and run a CREATE request with the informed arguments.
        /// </summary>        
        /// <param name="p_url">Request URL</param>
        /// <param name="p_attribs">Request attribute flags</param>
        /// <param name="p_query">URL query string</param>
        /// <param name="p_request">Request data (headers only)</param>
        /// <param name="p_callback">Request events handler</param>
        /// <returns>Running web request</returns>
        static public WebRequest Create(string p_url,WebRequestAttrib p_attribs,HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Create,p_url,p_attribs,p_query,p_request,p_callback); }
        static public WebRequest Create(string p_url,WebRequestAttrib p_attribs,HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Create,p_url,p_attribs,p_query,null     ,p_callback); }
        static public WebRequest Create(string p_url,WebRequestAttrib p_attribs,                  HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Create,p_url,p_attribs,null   ,p_request,p_callback); }
        static public WebRequest Create(string p_url,WebRequestAttrib p_attribs,                                        Action<WebRequest> p_callback = null) { return Send(HttpMethod.Create,p_url,p_attribs,null   ,null     ,p_callback); }
        static public WebRequest Create(string p_url,                           HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Create,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,p_query,p_request,p_callback); }
        static public WebRequest Create(string p_url,                           HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Create,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,p_query,null     ,p_callback); }
        static public WebRequest Create(string p_url,                                                                   Action<WebRequest> p_callback = null) { return Send(HttpMethod.Create,p_url,WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache,null   ,null     ,p_callback); }
        #endregion

        #endregion

        #endregion

        #region enum State
        /// <summary>
        /// Helper state to run the webrequest fsm
        /// </summary>
        private enum State : byte {
            Idle=0,
            Start,
            Create,
            Cached,
            Processing,            
            CacheRead,            
            RequestSetup,
            CacheHit,
            ResponseComplete,
            Timeout,
            Terminate
        }
        #endregion

        /// <summary>
        /// Flag that tells this web request is currently in operation
        /// </summary>
        public bool active { 
            get { 
                if(completed) return false;
                switch(state) {
                    case WebRequestState.Idle:
                    case WebRequestState.Cancel:
                    case WebRequestState.Success:
                    case WebRequestState.Error:
                    case WebRequestState.Timeout: return false;
                }
                return true;
            } 
        }

        /// <summary>
        /// Request Method Currently running.
        /// </summary>
        public string method { get; private set; }

        /// <summary>
        /// Request URL.
        /// </summary>
        public string url { get; private set; }
        private string m_internal_url;

        /// <summary>
        /// Returns the fully resolved URL.
        /// </summary>
        /// <param name="p_encoded">Flag that tells to encode the query string</param>
        /// <returns>resolved URL</returns>
        public string GetURL(bool p_encoded=false) { return url+query.ToString(p_encoded); }

        /// <summary>
        /// Query String of the Request
        /// </summary>
        public HttpQuery query { get; private set; }

        /// <summary>
        /// Request Data to be sent.
        /// </summary>
        public HttpRequest request  { get; private set; }

        /// <summary>
        /// Request Data received.
        /// </summary>
        public HttpRequest response { get; private set; }

        /// <summary>
        /// Cache TTL in Minutes
        /// </summary>
        public float ttl { get; set; }

        /// <summary>
        /// Timeout in Seconds
        /// </summary>
        public float timeout { get; set; }

        /// <summary>
        /// Flag that tells the request result was cached
        /// </summary>
        public bool cached { get; private set;}

        /// <summary>
        /// WebRequest Current State
        /// </summary>
        new public WebRequestState state { get; private set; }

        /// <summary>
        /// Error Message if Any
        /// </summary>
        public string error { get; private set;}

        /// <summary>
        /// Returns the combined progress of upload/download with a weight applied to prioritize download.
        /// </summary>
        public float progress { get; private set; }

        /// <summary>
        /// Http Code returned after completion.
        /// </summary>        
        public HttpStatusCode code { get; private set; }

        #region WebRequestAttrib attribs
        /// <summary>
        /// Set of modifier attribs of this web request.
        /// </summary>
        public WebRequestAttrib attribs { get;  private set; }
        private WebRequestAttrib m_attrib_filtered;
        
        /// <summary>
        /// Helper to fetch the constrained request attribs, based on FS available and global flags
        /// </summary>        
        private WebRequestAttrib GetAttribs() {
            WebRequestAttrib a = attribs;            
            //If default modes use the global attribs
            if((a & WebRequestAttrib.DefaultBuffer) != 0) a = (a & ~WebRequestAttrib.BufferMask) | DefaultBufferMode;
            if((a & WebRequestAttrib.DefaultCache ) != 0) a = (a & ~WebRequestAttrib.CacheMask)  | DefaultCacheMode ;
            if(CanUseFileSystem) return a;            
            //If no FS force memory buffering
            a  = (a & ~WebRequestAttrib.BufferMask) | WebRequestAttrib.MemoryBuffer;
            //If no FS force memory based cache or keep no-cache if set
            bool is_no_cache = (a & WebRequestAttrib.NoCache)!=0;
            a &= (a & ~WebRequestAttrib.CacheMask) | (is_no_cache ? WebRequestAttrib.NoCache : WebRequestAttrib.MemoryCache );            
            return a;
        }

        /// <summary>
        /// Helper to check if an attrib is set
        /// </summary>        
        private bool IsAttrib(WebRequestAttrib p_flag) { return (m_attrib_filtered & p_flag)!=0; }
        #endregion

        /// <summary>
        /// Handler for request events.
        /// </summary>
        public Action<WebRequest> OnRequestEvent { get { return (Action<WebRequest>)m_on_event; } set { m_on_event = value; } }
        private Delegate m_on_event;

        /// <summary>
        /// Internal
        /// </summary>
        private   State                         m_internal_state;
        protected UnityWebRequest               m_uwr;
        protected UnityWebRequestAsyncOperation m_uwr_op;
        
        /// <summary>
        /// CTOR.
        /// </summary>
        public WebRequest() {
            m_internal_state = State.Idle;
            method  = HttpMethod.None;            
            state   = WebRequestState.Idle;
            attribs = WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache;            
            query   = new HttpQuery();
            request = new HttpRequest();
            //If cache isn't initialized yet, do in the first run
            if(!m_fs_init) InitDataFileSystem();
        }

        #region Operations

        /// <summary>
        /// Starts the SEND process of this web request with the informed method and URL.
        /// </summary>
        /// <param name="p_method">Http Request Method</param>
        /// <param name="p_url">Base URL of the request</param>
        /// /// <param name="p_attribs">Request Configuration attributes</param>
        /// <returns>Running Web Request</returns>
        public WebRequest Send(string p_method,string p_url,WebRequestAttrib p_attribs = WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache) {
            if(active) { Debug.LogWarning($"WebRequest> Already Running."); return this; }
            id        = $"web-{p_method.ToLower()}-{GetHashCode().ToString("x")}";
            method    = p_method;
            url       = p_url;
            m_internal_url     = GetURL(false);                        
            m_internal_state   = State.CacheRead;
            attribs   = p_attribs;
            m_attrib_filtered = GetAttribs();            
            code      = (HttpStatusCode)0; //invalid
            progress  = 0f;
            //Dispatch the event to outside
            DispatchStateEvent(WebRequestState.Start);
            //Add to the execution pool
            base.Start();
            return this;
        }

        /// <summary>
        /// Starts a GET process of this web request with the informed and URL.
        /// </summary>        
        /// <param name="p_url">Base URL of the request</param>
        /// /// <param name="p_attribs">Request Configuration attributes</param>
        /// <returns>Running Web Request</returns>
        public WebRequest Get(string p_url,WebRequestAttrib p_attribs = WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache) { return Send(HttpMethod.Get,p_url,p_attribs); }

        /// <summary>
        /// Starts a POST process of this web request with the informed and URL.
        /// </summary>        
        /// <param name="p_url">Base URL of the request</param>
        /// /// <param name="p_attribs">Request Configuration attributes</param>
        /// <returns>Running Web Request</returns>
        public WebRequest Post(string p_url,WebRequestAttrib p_attribs = WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache) { return Send(HttpMethod.Post,p_url,p_attribs); }

        /// <summary>
        /// Starts a PUT process of this web request with the informed and URL.
        /// </summary>        
        /// <param name="p_url">Base URL of the request</param>
        /// /// <param name="p_attribs">Request Configuration attributes</param>
        /// <returns>Running Web Request</returns>
        public WebRequest Put(string p_url,WebRequestAttrib p_attribs = WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache) { return Send(HttpMethod.Put,p_url,p_attribs); }

        /// <summary>
        /// Starts a DELETE process of this web request with the informed and URL.
        /// </summary>        
        /// <param name="p_url">Base URL of the request</param>
        /// /// <param name="p_attribs">Request Configuration attributes</param>
        /// <returns>Running Web Request</returns>
        public WebRequest Delete(string p_url,WebRequestAttrib p_attribs = WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache) { return Send(HttpMethod.Delete,p_url,p_attribs); }

        /// <summary>
        /// Starts a HEAD process of this web request with the informed and URL.
        /// </summary>        
        /// <param name="p_url">Base URL of the request</param>
        /// /// <param name="p_attribs">Request Configuration attributes</param>
        /// <returns>Running Web Request</returns>
        public WebRequest Head(string p_url,WebRequestAttrib p_attribs = WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache) { return Send(HttpMethod.Head,p_url,p_attribs); }

        /// <summary>
        /// Starts a CREATE process of this web request with the informed and URL.
        /// </summary>        
        /// <param name="p_url">Base URL of the request</param>
        /// /// <param name="p_attribs">Request Configuration attributes</param>
        /// <returns>Running Web Request</returns>
        public WebRequest Create(string p_url,WebRequestAttrib p_attribs = WebRequestAttrib.DefaultBuffer | WebRequestAttrib.DefaultCache) { return Send(HttpMethod.Create,p_url,p_attribs); }

        #endregion

        /// <summary>
        /// Stops the request processing.
        /// </summary>
        public void Cancel() {
            if(m_uwr==null) return;
            InternalDispose();
            DispatchStateEvent(WebRequestState.Cancel);
            base.Stop();
        }

        /// <summary>
        /// Disposes this request.
        /// </summary>
        public void Dispose() {
            InternalDispose();
        }

        #region Internals

        /// <summary>
        /// Helper to dispose the internal structures.
        /// </summary>
        protected void InternalDispose() {
            if(m_uwr!=null) {
                m_uwr.Abort(); 
                if(m_uwr.downloadHandler != null) { m_uwr.downloadHandler.Dispose(); }
                if(m_uwr.uploadHandler   != null) { m_uwr.uploadHandler.Dispose();   }                
                m_uwr.Dispose(); 
                m_uwr=null;
            }            
            if(request  != null) { request.Dispose();  }
            if(response != null) { response.Dispose(); }            
            if(query!=null) query.Clear();
            m_attrib_filtered = WebRequestAttrib.None;            
        }

        /// <summary>
        /// Helper to change state and dispatch events
        /// </summary>        
        protected void DispatchStateEvent(WebRequestState p_state) {
            state = p_state;
            if(OnRequestEvent!=null)OnRequestEvent(this);
        }

        /// <summary>
        /// Helper to change state and dispatch events
        /// </summary>        
        protected void DispatchErrorEvent(string p_error) {
            error    = p_error;
            progress = 1f;
            Debug.LogWarning($"WebRequest> {error}");
            DispatchStateEvent(WebRequestState.Error);
        }

        /// <summary>
        /// Prevent Activity Starting
        /// </summary>
        new private void Start() { Debug.LogWarning("WebRequest> Can't Start Activity / Setup and Send a Request"); }

        /// <summary>
        /// Prevent Activity Stopping
        /// </summary>
        new private void Stop() { Debug.LogWarning("WebRequest> Can't Stop Activity / Use Cancel"); }

        #endregion

        #region Execution FSM

        /// <summary>
        /// Execution Loop to handle the web request state machine.
        /// </summary>
        /// <returns></returns>
        protected override bool OnExecute() {            
            //In case of cancel
            if(state == WebRequestState.Cancel) return false;
            //Process FSM state
            switch(m_internal_state) {
                //Keep looping
                case State.Idle:       return true;
                //Processing another async step
                case State.Processing: return true;
                //Auxiliary state to finish the loop
                case State.Terminate: return false;
                //Request Timed out
                case State.Timeout: {
                    InternalDispose();
                    error    = "Request Timeout";
                    progress = 1f;
                    DispatchStateEvent(WebRequestState.Timeout);
                    //Kill process
                }
                return false;
                //Initializes the cache system and try searching for an entry matching the request URL
                case State.CacheRead: {
                    //If no cache mode skip
                    if(IsAttrib(WebRequestAttrib.NoCache)) { m_internal_state = State.RequestSetup; break; }                    
                    //Sample cache based on URL generated MD5 hash
                    WebCacheEntry e = WebRequestCache.Get(m_internal_url);
                    //If no cache continue request
                    if(e==null)         { m_internal_state = State.RequestSetup; break; }
                    //Fetch TTL
                    float cache_ttl = ttl<=0f ? DefaultCacheTTL : ttl;
                    //If cache is dead dispose and continue request
                    if(!e.IsAlive(cache_ttl)) { WebRequestCache.Dispose(e); m_internal_state = State.RequestSetup; break; }
                    //Retrieve cached stream and proceed to final steps
                    response = new HttpRequest();
                    response.body.stream = e.GetStream($"{TempPath}{e.hash}_response");
                    response.progress = 1f;                    
                    m_internal_state = State.CacheHit;                    
                }
                break;
                //In case there is no cache the request will be setup, filling needed form data and preparing streams.                
                case State.RequestSetup: {                    
                    //Mini state machine 
                    WebRequestAttrib buffer_mode = (m_attrib_filtered & WebRequestAttrib.BufferMask);
                    int      init_step     = 0;
                    Activity request_setup = null;
                    Activity form_copy     = null;
                    string   temp_request_fp  = buffer_mode == WebRequestAttrib.FileBuffer ? GetTempFilePath("request")  : "";
                    string   temp_response_fp = buffer_mode == WebRequestAttrib.FileBuffer ? GetTempFilePath("response") : "";
                    Predicate<Activity> init_task = null;
                    //Initialization loop, will run inside a thread and also unity                    
                    init_task =
                    delegate(Activity a) {
                        //In case of cancel
                        if(state == WebRequestState.Cancel) return false;
                        //Flag telling the stream in the request was made before
                        bool is_custom_stream = true;
                        //State machine step
                        switch(init_step) {
                            //Initialize
                            case 0: {   
                                //Assert Request instance
                                if(request==null) request = new HttpRequest();
                                //If the body doesnt have any stream
                                if(request.body.stream==null) {
                                    is_custom_stream = false;
                                    Stream ss = buffer_mode == WebRequestAttrib.FileBuffer ? (Stream)File.Open(temp_request_fp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite) : (Stream)new MemoryStream();
                                    request.body.Initialize(ss);
                                }                                
                                //Flush non unity
                                init_step = 1;
                            }
                            break;
                            //Flush non-unity fields (per frame)
                            case 1: {
                                //If its a custom stream just skip the form generation
                                if(is_custom_stream) { init_step = 3; form_copy = Timer.Delay(1f / 60f); break; }
                                //Keep consuming fields to flush
                                if(request.body.FlushStep(false)) break;
                                //Set the step to 'unity' flush
                                init_step=2;
                                //Create another task but now in unity main thread
                                request_setup = Activity.Run(init_task,ActivityContext.Update);
                                request_setup.id = id+"$request-setup-unity";
                                //kill the thread mode
                            }
                            return false;
                            //Flush unity data fields (per frame)
                            case 2: {   
                                //Keep consuming fields to flush
                                if(request.body.FlushStep(true)) break;
                                //Flush form information into the stream
                                form_copy = request.body.FlushFormAsync();
                                //Wait form copying into stream
                                init_step=3;
                            }
                            break;
                            //Wait form copy and completes the setup
                            case 3: {                                   
                                //Null check
                                if(form_copy==null)               { DispatchErrorEvent("Form Copy into Stream Failed to Start"); return false; }
                                //Wait copy completion
                                if(!form_copy.completed) break;                                
                                if(form_copy.id == "form-error")  { DispatchErrorEvent("Form Copy into Stream Failed");          return false; }
                                if(form_copy.id == "form-cancel") { DispatchErrorEvent("Form Copy into Stream Cancelled");       return false; }                                
                                //Request is ready to ship, so create response
                                response = new HttpRequest();
                                //Prepare the response stream
                                Stream ss = buffer_mode == WebRequestAttrib.FileBuffer ? (Stream)File.Open(temp_response_fp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite) : (Stream)new MemoryStream();
                                //Set the stream reference
                                response.body.stream = ss;
                                //Mark as created
                                m_internal_state = State.Create;
                                //Kill task
                            }
                            return false;
                        }
                        return true;
                    };
                    //Start the initialization thread
                    request_setup = Activity.Run(init_task,DataProcessContext);
                    request_setup.id = id+"$request-setup-thread";
                    //Keep running until parallel task runs
                    m_internal_state = State.Processing;
                }
                break;
                //All needed data of the request are set, now Unity's web request needs to be setup and started
                case State.Create: {
                    //Create handlers
                    UploadHandlerStream   uh = new UploadHandlerStream(request.body.stream);                    
                    DownloadHandlerStream dh = new DownloadHandlerStream(response.body.stream);
                    if(!dh.valid) { DispatchErrorEvent("Failed to Create Download Handler!"); return false; }                    
                    //Create request
                    m_uwr = new UnityWebRequest(m_internal_url,method,dh,uh);
                    //Flag telling its a GET request so there is no upload
                    bool is_get = method == HttpMethod.Get;
                    //Upload size in mb
                    float upload_mb = ((float)request.body.length)/1024f/1024f;
                    //Balance out progress report the bigger the upload
                    //few kbytes = 20% 5mb+ 60%
                    float upload_progress_w   = is_get ? 0f : Mathf.Lerp(0.05f,0.6f,upload_mb/5f);
                    float download_progress_w = 1f - upload_progress_w;
                    //Fill headers
                    request.header.CopyTo(m_uwr);
                    //Start Request
                    m_uwr_op = m_uwr.SendWebRequest(); 
                    //Dispatch the event to outside
                    DispatchStateEvent(WebRequestState.Create);
                    //Keep the state machine looping
                    m_internal_state  = State.Processing;
                    //Upload/Download state machine vars
                    float  last_upload_progress   = -0.1f;
                    float  last_download_progress = -0.1f;
                    bool is_first_download    = true;
                    bool is_first_upload      = true;                                        
                    bool is_download_complete = false;
                    bool is_upload_complete   = false;                    
                    float  timeout_elapsed    = 0f;
                    //Starts the unity request polling task
                    Activity uwr_task = null;
                    uwr_task = 
                    Activity.Run(
                    delegate(Activity a) {
                        //In case of cancel
                        if(state == WebRequestState.Cancel) return false;
                        float timeout_duration = timeout<=0f ? DefaultTimeout : timeout;
                        //Update Timeout
                        if(timeout_duration > 0f) {
                            timeout_elapsed += Time.unscaledDeltaTime;
                            if(timeout_elapsed >= timeout_duration) {                                
                                m_internal_state = State.Timeout;                                
                                return false;
                            }
                        }
                        //Progress values
                        bool is_done            = m_uwr.isDone;                        
                        float upload_progress   = is_done ? 1f : (is_get ? 1f : uh.progress*0.99f);
                        float download_progress = is_done ? 1f : dh.progress*0.99f;
                        progress = (upload_progress_w*upload_progress) + (download_progress_w*download_progress);
                        //Update the http data
                        request.progress  = upload_progress;
                        response.progress = download_progress;
                        //Process progression (GET will not trigger upload)                        
                        ProcessProgress(true, ref is_first_upload,  ref is_upload_complete,  ref last_upload_progress,  upload_progress);
                        ProcessProgress(false,ref is_first_download,ref is_download_complete,ref last_download_progress,download_progress);
                        //Keep running
                        if(!is_done) return true;
                        //All steps completed                        
                        m_internal_state = State.ResponseComplete;
                        return false;                        
                    });

                }
                break;                
                //Request is cached, either file or memory
                //Request Response Body is ready, either on memory or file
                case State.ResponseComplete:
                case State.CacheHit: {
                    //Check if state is cached and fill cache-only fields                    
                    cached = m_internal_state == State.CacheHit;
                    //Process final resulting state
                    switch(m_internal_state) {
                        case State.CacheHit: {
                            code = HttpStatusCode.NotModified;                                                        
                            //Dispatch the event to outside
                            DispatchStateEvent(WebRequestState.Cached);
                            //End request as it was cache hit
                        }
                        return false;

                        case State.ResponseComplete: {                            
                            code   = (HttpStatusCode)m_uwr.responseCode;
                            bool is_error = m_uwr.isNetworkError || m_uwr.isHttpError;
                            //Error event and finish loop                                                        
                            if(is_error) {              
                                string error_pfx = m_uwr.isNetworkError ? "[network]" : "[http]";
                                //Write response headers
                                response.header.CopyFrom(m_uwr);  
                                DispatchErrorEvent($"{error_pfx} {m_uwr.error}"); 
                                return false; 
                            }
                            //Task to finalize the request                            
                            Activity success_activity = null;
                            //Mini FSM
                            int success_state = 0;
                            System.Predicate<Activity> success_task_fn = null;                            
                            success_task_fn =                            
                            delegate(Activity a) { 
                                switch(success_state) {
                                    //Execute heavy steps | Thread
                                    case 0: {
                                        //Reset Stream position
                                        response.body.stream.Seek(0, SeekOrigin.Begin);                            
                                        WebRequestAttrib cache_mode = (m_attrib_filtered & WebRequestAttrib.CacheMask);
                                        //If no error save cache
                                        if(cache_mode != WebRequestAttrib.NoCache) {
                                            WebRequestCache.Add(m_internal_url,response.body.stream,cache_mode == WebRequestAttrib.FileCache);
                                        }                            
                                        //Restore seek position
                                        response.body.stream.Seek(0, SeekOrigin.Begin);
                                        //next success state
                                        success_state = 1;
                                        //Continue loop but in main thread
                                        success_activity = Run(success_task_fn,DataProcessContext);
                                        success_activity .id = id+"$finalize-success";
                                    }
                                    return false;
                                    //Finalize in unity main thread
                                    case 1: {
                                        //Notify success
                                        DispatchStateEvent(WebRequestState.Success);                    
                                        //Stops the execution
                                        m_internal_state = State.Terminate;
                                    }
                                    return false;
                                }

                                return true;
                            };   
                            //Run finalization as thread to avoid fps drops
                            success_activity = Run(success_task_fn,DataProcessContext);
                            success_activity .id = id+"$finalize";
                            //Wait for finalize 
                            m_internal_state = State.Processing;
                        }
                        break;
                    }                    
                }
                break;
            }
            //Keep FSM looping
            return true;
        }        
        
        /// <summary>
        /// Helper for generating start,progress complete states
        /// </summary>        
        private void ProcessProgress(bool p_is_upload,ref bool p_is_first,ref bool p_is_complete,ref float p_progress,float p_next_progress) {            
            if(p_next_progress>p_progress) {
                p_progress = p_next_progress;
                if(p_is_first) { 
                    p_is_first=false;
                    //Start
                    DispatchStateEvent(p_is_upload ? WebRequestState.UploadStart : WebRequestState.DownloadStart);
                }                
                //Progress                           
                DispatchStateEvent(p_is_upload ? WebRequestState.UploadProgress : WebRequestState.DownloadProgress);
            }           
            if(p_is_complete) return;            
            if(p_progress>=1f) {
                p_is_complete=true;
                //Progress                           
                DispatchStateEvent(p_is_upload ? WebRequestState.UploadComplete : WebRequestState.DownloadComplete);
            }
        }

        #endregion

        #region Json
        /// <summary>
        /// Returns the json parsed object.
        /// </summary>
        /// <typeparam name="T">Class type to be parsed from a json data.</typeparam>
        /// <returns>Class instance</returns>
        public T GetJson<T>(T p_default = default(T)) {
            if(response      == null) return p_default;
            if(response.body == null) return p_default;
            Stream ss = response.body.stream;
            if(ss == null) return p_default;
            ss.Position=0;
            JSONSerializer s = new JSONSerializer();
            T res = s.Deserialize<T>(ss);
            ss.Position=0;
            return res;
        }

        /// <summary>
        /// Parses the json in the response body, asynchronously
        /// </summary>
        /// <typeparam name="T">Class type to be parsed from a json data.</typeparam>
        /// <param name="p_callback">Callback called when finished</param>
        /// <param name="p_default">Default value in case of error.</param>
        /// <returns>Running Serializer</returns>
        public Serializer GetJsonAsync<T>(Action<T> p_callback=null,T p_default = default(T)) {
            if(response == null)      { if(p_callback != null) p_callback(p_default); return null; }
            if(response.body == null) { if(p_callback != null) p_callback(p_default); return null; }
            Stream ss = response.body.stream;
            if(ss == null) { if(p_callback != null) p_callback(p_default); return null; }
            ss.Position=0;
            JSONSerializer s = new JSONSerializer();
            s.DeserializeAsync<T>(ss,p_callback);
            return s;
        }
        #endregion

        #region String
        /// <summary>
        /// Returns the response as string.
        /// </summary>
        /// <param name="p_default">Default value in case of error</param>
        /// <returns>Response String</returns>
        public string GetString(string p_default="") {
            if(response      == null) return p_default;
            if(response.body == null) return p_default;
            Stream ss = response.body.stream;
            if(ss == null) return p_default;
            ss.Position=0;
            StreamReader sr = new StreamReader(ss);
            string res = sr.ReadToEnd();
            ss.Position=0;
            return res;
        }
        #endregion

        #region Bytes
        /// <summary>
        /// Returns the response as raw bytes.
        /// </summary>        
        /// <returns>Response Bytes</returns>
        public byte[] GetBytes() {
            if(response      == null) return null;
            if(response.body == null) return null;
            Stream ss = response.body.stream;
            if(ss == null) return null;
            ss.Position=0;
            MemoryStream ms = new MemoryStream();
            ss.CopyTo(ms);
            ms.Flush();
            byte[] res = ms.ToArray();
            ms.Close();
            ms.Dispose();
            return res;
        }        
        #endregion

        #region Texture
        /// <summary>
        /// Returns a Texture instance from the binary information.
        /// </summary>                
        /// <param name="p_readable">Mark as read enabled</param>
        /// <param name="p_default">Default texture in case of error.</param>
        /// <returns>Texture instance</returns>
        public Texture2D GetTexture(bool p_readable,Texture2D p_default=null) {
            if(response      == null) return p_default;
            if(response.body == null) return p_default;
            Stream ss = response.body.stream;
            if(ss == null) return p_default;
            if(m_texture_ms==null) m_texture_ms = new MemoryStream();
            m_texture_ms.Position=0;
            ss.Position=0;
            MemoryStream ms = m_texture_ms;
            ss.CopyTo(ms);
            ms.Flush();
            byte[] d = ms.GetBuffer();            
            Texture2D tex = new Texture2D(1,1);
            tex.LoadImage(d,p_readable);
            d=null;
            tex.name = id;
            return tex;
        }
        static MemoryStream m_texture_ms;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p_default"></param>
        /// <returns></returns>
        public Texture2D GetTexture(Texture2D p_default = null) { return GetTexture(false,p_default); }
        #endregion

        #region AudioClip
        /// <summary>
        /// Returns an Audio instance from the binary information.
        /// </summary>                
        /// <param name="p_type">Audio Type</param>
        /// <param name="p_default">Default Audio in case of error.</param>
        /// <returns>Texture instance</returns>
        public Activity GetAudioClip(AudioType p_type,System.Action<AudioClip> p_callback,AudioClip p_default=null) {
            if(response == null)      { if(p_callback != null) p_callback(p_default); return null; }
            if(response.body == null) { if(p_callback != null) p_callback(p_default); return null; }
            Stream ss = response.body.stream;
            if(ss == null) { if(p_callback != null) p_callback(p_default); return null; }
            if(!CanUseFileSystem) { Debug.LogWarning("WebRequest> AudioClip needs FileSystem Access."); if(p_callback != null) p_callback(p_default); return null; }
            ss.Position=0;
            int state=0;
            string          temp_audio = GetTempFilePath("audioclip");
            UnityWebRequest temp_req   = null;
            FileStream fs = File.Create(temp_audio);
            AudioClip  c=null;
            ss.CopyTo(fs);
            fs.Flush();
            fs.Close();
            ss.Position=0;
            Activity parser = null;
            parser =
            Activity.Run(delegate(Activity a) {
                switch(state) {
                    //Create the file:// request and run it
                    case 0: {
                        string local_url = $"file:///{temp_audio}";
                        temp_req = UnityWebRequestMultimedia.GetAudioClip(local_url,p_type);
                        temp_req.SendWebRequest();
                        state=1;
                    }
                    break;
                    //Wait for its completion
                    case 1: {
                        if(!temp_req.isDone) return true;
                        File.Delete(temp_audio);
                        c = DownloadHandlerAudioClip.GetContent(temp_req);
                        c.name = id;
                        if(!c.preloadAudioData)c.LoadAudioData();
                        //wait audio loading;
                        state=2;
                    }
                    break;
                    //Wait audio loading
                    case 2: {
                        if(!c) { if(p_callback != null) p_callback(p_default); return false; }                        
                        if(c.loadState == AudioDataLoadState.Failed) { if(p_callback != null) p_callback(p_default); return false; }
                        if(c.loadState != AudioDataLoadState.Loaded) return true;
                        if(p_callback != null) p_callback(c);
                        temp_req.downloadHandler.Dispose();
                        temp_req.Dispose();                        
                    }
                    return false;
                }
                return true;
            });            
            return parser;
        }        
        #endregion

    }
    #endregion

}

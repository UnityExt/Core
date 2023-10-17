using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using NetMultipartFormDataContent = System.Net.Http.MultipartFormDataContent;
using NetHttpContent              = System.Net.Http.HttpContent;
using NetByteArrayContent         = System.Net.Http.ByteArrayContent;
using NetStreamContent            = System.Net.Http.StreamContent;
using NetStringContent            = System.Net.Http.StringContent;
using HttpStatusCode              = System.Net.HttpStatusCode;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.CompilerServices;

namespace UnityExt.Core {

    #region enum WebRequestState
    /// <summary>
    /// WebRequest Execution State
    /// </summary>
    public enum WebRequestState : byte {
        Idle=0,
        Queue,
        Start,
        Processing,
        CacheSearch,
        CacheSuccess,
        Create,
        Send,
        UploadStart,
        Upload,
        UploadProgress,        
        UploadComplete,
        DownloadStart,
        Download,
        DownloadProgress,
        DownloadComplete,
        RequestComplete,
        Stop,
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
    public enum WebRequestFlags : byte {
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
    public class WebRequest : Activity<WebRequestState> {

        #region static 

        /// <summary>
        /// CTOR.
        /// </summary>
        static WebRequest() {
            DirectoryInfo di;
            //Assert the DataPath folder (stores temp files and caching)
            DataPath = $"{m_app_persistent_dp}/unityex/{m_app_platform}/web/";
            di = new DirectoryInfo(DataPath);
            if (!di.Exists) di.Create();
            //Assert the temp files (used for inbetween web operations)
            TempPath = $"{DataPath}temp/";
            di = new DirectoryInfo(TempPath);
            //Assert the cache folder when storing in disk
            if (!di.Exists) di.Create();
            CachePath = $"{DataPath}cache/";
            di = new DirectoryInfo(CachePath);
            if (!di.Exists) di.Create();
            //Assert if FileSystem can be used
            #if UNITY_WEBGL
            CanUseFileSystem = true;
            #else                            
            CanUseFileSystem = true;
            try {
                //Force an exception to confirm the filesystem can be used
                System.Security.AccessControl.DirectorySecurity ds = Directory.GetAccessControl(DataPath);
            }
            catch (UnauthorizedAccessException) {
                CanUseFileSystem = false;
            }            
            #endif
            StringBuilder log = new StringBuilder();
            log.AppendLine($"=== Web Request Paths ===");
            log.AppendLine($"Data:   {DataPath}");
            log.AppendLine($"Temp:   {TempPath}");
            log.AppendLine($"Cache:  {CachePath}");
            log.AppendLine($"Use FS: {CanUseFileSystem}");
            log.AppendLine($"=========================");
            Debug.Log(log.ToString());
        }
        
        #region Consts

        /// <summary>
        /// Default option for buffering mode.
        /// </summary>
        static public WebRequestFlags DefaultBufferMode = WebRequestFlags.FileBuffer;
        
        /// <summary>
        /// Default option for caching mode.
        /// </summary>
        static public WebRequestFlags DefaultCacheMode  = WebRequestFlags.FileCache;

        /// <summary>
        /// Flag telling in which execution context the data must be processed.
        /// </summary>
        static public ProcessContext DataProcessContext = ProcessContext.Thread;

        /// <summary>
        /// Default Cache TTL in Minutes
        /// </summary>
        static public float DefaultCacheTTL = 1f;

        /// <summary>
        /// Default Timeout in seconds
        /// </summary>
        static public float DefaultTimeout = 10f;

        #region File System
        /// <summary>
        /// Returns the folder to write temprary form data.
        /// </summary>
        static public string DataPath { get; private set; } 
        
        /// <summary>
        /// Returns the folder to write temprary form data.
        /// </summary>
        static public string TempPath { get; private set; }

        /// <summary>
        /// Returns the folder to write cached results
        /// </summary>
        static public string CachePath { get; private set; }

        /// <summary>
        /// Flag that tells the machine's filesystem can be used.
        /// </summary>
        static public bool CanUseFileSystem { get; private set; }

        /// <summary>
        /// Helper to return a temp file name in the proper path
        /// </summary>        
        static public string GetTempFilePath(string p_name) { return $"{TempPath}{GetTempFileName(p_name)}"; }

        /// <summary>
        /// Helper to return a temp file name
        /// </summary>        
        static internal string GetTempFileName(string p_name) {
            if(m_rnd==null) m_rnd = new System.Random();
            m_rnd.Next();
            StringBuilder sb = new StringBuilder();
            for(int i=0;i<24;i++) sb.Append(m_hash_lut[m_rnd.Next(0,m_hash_lut.Length-1)]);
            sb.Append("_");
            sb.Append(p_name);
            return sb.ToString();
        }
        static private System.Random m_rnd;
        static private string m_hash_lut = $"0123456789abcdef";
            
        /// <summary>
        /// Auxiliary method to give control when to trigger the file system cleanup and initialization.
        /// </summary>
        static public void InitDataFileSystem(bool p_clear_files=false) {
            if(m_fs_init) return;
            m_fs_init=true;
            //Cache FS check result
            bool can_use_fs = CanUseFileSystem;
            WebRequestCache.Load(CachePath);
            //Delete temp files (not the cache)
            DirectoryInfo di = new DirectoryInfo(TempPath);
            FileInfo[] fl = di.GetFiles("*");
            for(int i=0;i<fl.Length;i++) fl[i].Delete();
        }
        static private bool m_fs_init;

        #endregion

        #endregion

        #region Operations

        #region Create
        /// <summary>
        /// Creates and run a request with the informed arguments.
        /// </summary>
        /// <param name="p_method">Http Method</param>
        /// <param name="p_url">Request URL</param>
        /// <param name="p_flags">Request attribute flags</param>
        /// <param name="p_query">URL query string</param>
        /// <param name="p_request">Request data</param>
        /// <param name="p_callback">Request events handler</param>
        /// <returns>Running web request</returns>
        static public WebRequest Send(string p_method,string p_url,WebRequestFlags p_flags,HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback=null) {
            WebRequest req = new WebRequest();
            if(p_query  !=null) req.query   = p_query;
            if(p_request!=null) req.request = p_request;
            //req.OnRequestEvent = p_callback;
            req.Send(p_method,p_url,p_flags);
            return req;
        }
        static public WebRequest Send(string p_method,string p_url,WebRequestFlags p_flags,HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(p_method,p_url,p_flags,p_query,null     ,p_callback); }
        static public WebRequest Send(string p_method,string p_url,WebRequestFlags p_flags,                  HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(p_method,p_url,p_flags,null   ,p_request,p_callback); }
        static public WebRequest Send(string p_method,string p_url,WebRequestFlags p_flags,                                        Action<WebRequest> p_callback = null) { return Send(p_method,p_url,p_flags,null   ,null     ,p_callback); }
        static public WebRequest Send(string p_method,string p_url,                           HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(p_method,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,p_query,p_request,p_callback); }
        static public WebRequest Send(string p_method,string p_url,                           HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(p_method,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,p_query,null     ,p_callback); }
        static public WebRequest Send(string p_method,string p_url,                                                                   Action<WebRequest> p_callback = null) { return Send(p_method,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,null   ,null     ,p_callback); }
        #endregion

        #region Get
        /// <summary>
        /// Creates and run a GET request with the informed arguments.
        /// </summary>        
        /// <param name="p_url">Request URL</param>
        /// <param name="p_flags">Request attribute flags</param>
        /// <param name="p_query">URL query string</param>
        /// <param name="p_request">Request data (headers only)</param>
        /// <param name="p_callback">Request events handler</param>
        /// <returns>Running web request</returns>
        static public WebRequest Get(string p_url,WebRequestFlags p_flags,HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Get,p_url,p_flags,p_query,p_request,p_callback); }
        static public WebRequest Get(string p_url,WebRequestFlags p_flags,HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Get,p_url,p_flags,p_query,null     ,p_callback); }
        static public WebRequest Get(string p_url,WebRequestFlags p_flags,                  HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Get,p_url,p_flags,null   ,p_request,p_callback); }
        static public WebRequest Get(string p_url,WebRequestFlags p_flags,                                        Action<WebRequest> p_callback = null) { return Send(HttpMethod.Get,p_url,p_flags,null   ,null     ,p_callback); }
        static public WebRequest Get(string p_url,                           HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Get,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,p_query,p_request,p_callback); }
        static public WebRequest Get(string p_url,                           HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Get,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,p_query,null     ,p_callback); }
        static public WebRequest Get(string p_url,                                                                   Action<WebRequest> p_callback = null) { return Send(HttpMethod.Get,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,null   ,null     ,p_callback); }
        #endregion

        #region Post
        /// <summary>
        /// Creates and run a POST request with the informed arguments.
        /// </summary>        
        /// <param name="p_url">Request URL</param>
        /// <param name="p_flags">Request attribute flags</param>
        /// <param name="p_query">URL query string</param>
        /// <param name="p_request">Request data (headers only)</param>
        /// <param name="p_callback">Request events handler</param>
        /// <returns>Running web request</returns>
        static public WebRequest Post(string p_url,WebRequestFlags p_flags,HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Post,p_url,p_flags,p_query,p_request,p_callback); }
        static public WebRequest Post(string p_url,WebRequestFlags p_flags,HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Post,p_url,p_flags,p_query,null     ,p_callback); }
        static public WebRequest Post(string p_url,WebRequestFlags p_flags,                  HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Post,p_url,p_flags,null   ,p_request,p_callback); }
        static public WebRequest Post(string p_url,WebRequestFlags p_flags,                                        Action<WebRequest> p_callback = null) { return Send(HttpMethod.Post,p_url,p_flags,null   ,null     ,p_callback); }
        static public WebRequest Post(string p_url,                           HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Post,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,p_query,p_request,p_callback); }
        static public WebRequest Post(string p_url,                           HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Post,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,p_query,null     ,p_callback); }
        static public WebRequest Post(string p_url,                                                                   Action<WebRequest> p_callback = null) { return Send(HttpMethod.Post,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,null   ,null     ,p_callback); }
        #endregion

        #region Put
        /// <summary>
        /// Creates and run a PUT request with the informed arguments.
        /// </summary>        
        /// <param name="p_url">Request URL</param>
        /// <param name="p_flags">Request attribute flags</param>
        /// <param name="p_query">URL query string</param>
        /// <param name="p_request">Request data (headers only)</param>
        /// <param name="p_callback">Request events handler</param>
        /// <returns>Running web request</returns>
        static public WebRequest Put(string p_url,WebRequestFlags p_flags,HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Put,p_url,p_flags,p_query,p_request,p_callback); }
        static public WebRequest Put(string p_url,WebRequestFlags p_flags,HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Put,p_url,p_flags,p_query,null     ,p_callback); }
        static public WebRequest Put(string p_url,WebRequestFlags p_flags,                  HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Put,p_url,p_flags,null   ,p_request,p_callback); }
        static public WebRequest Put(string p_url,WebRequestFlags p_flags,                                        Action<WebRequest> p_callback = null) { return Send(HttpMethod.Put,p_url,p_flags,null   ,null     ,p_callback); }
        static public WebRequest Put(string p_url,                           HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Put,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,p_query,p_request,p_callback); }
        static public WebRequest Put(string p_url,                           HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Put,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,p_query,null     ,p_callback); }
        static public WebRequest Put(string p_url,                                                                   Action<WebRequest> p_callback = null) { return Send(HttpMethod.Put,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,null   ,null     ,p_callback); }
        #endregion

        #region Delete
        /// <summary>
        /// Creates and run a DELETE request with the informed arguments.
        /// </summary>        
        /// <param name="p_url">Request URL</param>
        /// <param name="p_flags">Request attribute flags</param>
        /// <param name="p_query">URL query string</param>
        /// <param name="p_request">Request data (headers only)</param>
        /// <param name="p_callback">Request events handler</param>
        /// <returns>Running web request</returns>
        static public WebRequest Delete(string p_url,WebRequestFlags p_flags,HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Delete,p_url,p_flags,p_query,p_request,p_callback); }
        static public WebRequest Delete(string p_url,WebRequestFlags p_flags,HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Delete,p_url,p_flags,p_query,null     ,p_callback); }
        static public WebRequest Delete(string p_url,WebRequestFlags p_flags,                  HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Delete,p_url,p_flags,null   ,p_request,p_callback); }
        static public WebRequest Delete(string p_url,WebRequestFlags p_flags,                                        Action<WebRequest> p_callback = null) { return Send(HttpMethod.Delete,p_url,p_flags,null   ,null     ,p_callback); }
        static public WebRequest Delete(string p_url,                           HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Delete,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,p_query,p_request,p_callback); }
        static public WebRequest Delete(string p_url,                           HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Delete,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,p_query,null     ,p_callback); }
        static public WebRequest Delete(string p_url,                                                                   Action<WebRequest> p_callback = null) { return Send(HttpMethod.Delete,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,null   ,null     ,p_callback); }
        #endregion

        #region Head
        /// <summary>
        /// Creates and run a HEAD request with the informed arguments.
        /// </summary>        
        /// <param name="p_url">Request URL</param>
        /// <param name="p_flags">Request attribute flags</param>
        /// <param name="p_query">URL query string</param>
        /// <param name="p_request">Request data (headers only)</param>
        /// <param name="p_callback">Request events handler</param>
        /// <returns>Running web request</returns>
        static public WebRequest Head(string p_url,WebRequestFlags p_flags,HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Head,p_url,p_flags,p_query,p_request,p_callback); }
        static public WebRequest Head(string p_url,WebRequestFlags p_flags,HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Head,p_url,p_flags,p_query,null     ,p_callback); }
        static public WebRequest Head(string p_url,WebRequestFlags p_flags,                  HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Head,p_url,p_flags,null   ,p_request,p_callback); }
        static public WebRequest Head(string p_url,WebRequestFlags p_flags,                                        Action<WebRequest> p_callback = null) { return Send(HttpMethod.Head,p_url,p_flags,null   ,null     ,p_callback); }
        static public WebRequest Head(string p_url,                           HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Head,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,p_query,p_request,p_callback); }
        static public WebRequest Head(string p_url,                           HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Head,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,p_query,null     ,p_callback); }
        static public WebRequest Head(string p_url,                                                                   Action<WebRequest> p_callback = null) { return Send(HttpMethod.Head,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,null   ,null     ,p_callback); }
        #endregion

        #region Create
        /// <summary>
        /// Creates and run a CREATE request with the informed arguments.
        /// </summary>        
        /// <param name="p_url">Request URL</param>
        /// <param name="p_flags">Request attribute flags</param>
        /// <param name="p_query">URL query string</param>
        /// <param name="p_request">Request data (headers only)</param>
        /// <param name="p_callback">Request events handler</param>
        /// <returns>Running web request</returns>
        static public WebRequest Create(string p_url,WebRequestFlags p_flags,HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Create,p_url,p_flags,p_query,p_request,p_callback); }
        static public WebRequest Create(string p_url,WebRequestFlags p_flags,HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Create,p_url,p_flags,p_query,null     ,p_callback); }
        static public WebRequest Create(string p_url,WebRequestFlags p_flags,                  HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Create,p_url,p_flags,null   ,p_request,p_callback); }
        static public WebRequest Create(string p_url,WebRequestFlags p_flags,                                        Action<WebRequest> p_callback = null) { return Send(HttpMethod.Create,p_url,p_flags,null   ,null     ,p_callback); }
        static public WebRequest Create(string p_url,                           HttpQuery p_query,HttpRequest p_request,Action<WebRequest> p_callback = null) { return Send(HttpMethod.Create,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,p_query,p_request,p_callback); }
        static public WebRequest Create(string p_url,                           HttpQuery p_query,                      Action<WebRequest> p_callback = null) { return Send(HttpMethod.Create,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,p_query,null     ,p_callback); }
        static public WebRequest Create(string p_url,                                                                   Action<WebRequest> p_callback = null) { return Send(HttpMethod.Create,p_url,WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache,null   ,null     ,p_callback); }
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
        protected bool active { 
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
        //new public WebRequestState state { get; private set; }

        /// <summary>
        /// Returns the combined progress of upload/download with a weight applied to prioritize either download or upload.
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
        public WebRequestFlags attribs { get;  private set; }
        private WebRequestFlags m_attrib_filtered;
        
        /// <summary>
        /// Helper to fetch the constrained request attribs, based on FS available and global flags
        /// </summary>        
        private WebRequestFlags GetAttribs() {
            WebRequestFlags a = attribs;            
            //If default modes use the global attribs
            if((a & WebRequestFlags.DefaultBuffer) != 0) a = (a & ~WebRequestFlags.BufferMask) | DefaultBufferMode;
            if((a & WebRequestFlags.DefaultCache ) != 0) a = (a & ~WebRequestFlags.CacheMask)  | DefaultCacheMode ;
            if(CanUseFileSystem) return a;            
            //If no FS force memory buffering
            a  = (a & ~WebRequestFlags.BufferMask) | WebRequestFlags.MemoryBuffer;
            //If no FS force memory based cache or keep no-cache if set
            bool is_no_cache = (a & WebRequestFlags.NoCache)!=0;
            a &= (a & ~WebRequestFlags.CacheMask) | (is_no_cache ? WebRequestFlags.NoCache : WebRequestFlags.MemoryCache );            
            return a;
        }

        /// <summary>
        /// Helper to check if an attrib is set
        /// </summary>        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsFlagEnabled(WebRequestFlags p_flags) { return (m_attrib_filtered & p_flags)!=0; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsFlagEnabled(WebRequestFlags p_flags,WebRequestFlags p_mask) { return (p_mask & p_flags) != 0; }
        #endregion

        #region Events

        /// <summary>
        /// Handler for execution loop
        /// </summary>
        new public Action<WebRequest> OnExecuteEvent;

        /// <summary>
        /// Handler for state changes
        /// </summary>
        new public Action<WebRequest,WebRequestState,WebRequestState> OnChangeEvent;

        /// <summary>
        /// Auxiliary Event Calling
        /// </summary>        
        protected override void InternalExecuteEvent(WebRequestState p_state                    ) { if (OnExecuteEvent != null) OnExecuteEvent(this            ); }
        protected override void InternalChangeEvent (WebRequestState p_from,WebRequestState p_to) { if (OnChangeEvent  != null) OnChangeEvent (this,p_from,p_to); }

        #endregion

        /// <summary>
        /// Internal
        /// </summary>                
        protected UnityWebRequest               m_uwr;
        protected UnityWebRequestAsyncOperation m_uwr_op;
        //new protected bool threadSafe { get { return base.threadSafe; } set { base.threadSafe = value; } }

        /// <summary>
        /// CTOR.
        /// </summary>
        public WebRequest(string p_id="") : base(p_id) {            
            method  = HttpMethod.None;            
            state   = WebRequestState.Idle;
            attribs = WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache;            
            query   = new HttpQuery();
            request = new HttpRequest();
            //Make sure loops are threadsafe as logic will be running both in threads and unity's loops
            fsm.threadSafe  = true;
            threadSafe      = true;            
            //If cache isn't initialized yet, do in the first run
            if (!m_fs_init) InitDataFileSystem();
        }

        #region Operations

        /// <summary>
        /// Starts the SEND process of this web request with the informed method and URL.
        /// </summary>
        /// <param name="p_method">Http Request Method</param>
        /// <param name="p_url">Base URL of the request</param>
        /// /// <param name="p_flags">Request Configuration attributes</param>
        /// <returns>Running Web Request</returns>
        public WebRequest Send(string p_method,string p_url,WebRequestFlags p_flags = WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache) {
            if(active) { Debug.LogWarning($"WebRequest> Already Running."); return this; }
            id        = $"web-{p_method.ToLower()}-{GetHashCode().ToString("x")}";
            method    = p_method;
            url       = p_url;            
            attribs   = p_flags;
            m_internal_url    = GetURL(false);
            m_attrib_filtered = GetAttribs();            
            code      = (HttpStatusCode)0; //invalid
            progress  = 0f;
            state = WebRequestState.Queue;            
            //Add to the execution pool
            base.Start(ProcessContext.Update);
            return this;
        }

        /// <summary>
        /// Starts a GET process of this web request with the informed and URL.
        /// </summary>        
        /// <param name="p_url">Base URL of the request</param>
        /// /// <param name="p_flags">Request Configuration attributes</param>
        /// <returns>Running Web Request</returns>
        public WebRequest Get(string p_url,WebRequestFlags p_flags = WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache) { return Send(HttpMethod.Get,p_url,p_flags); }

        /// <summary>
        /// Starts a POST process of this web request with the informed and URL.
        /// </summary>        
        /// <param name="p_url">Base URL of the request</param>
        /// /// <param name="p_flags">Request Configuration attributes</param>
        /// <returns>Running Web Request</returns>
        public WebRequest Post(string p_url,WebRequestFlags p_flags = WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache) { return Send(HttpMethod.Post,p_url,p_flags); }

        /// <summary>
        /// Starts a PUT process of this web request with the informed and URL.
        /// </summary>        
        /// <param name="p_url">Base URL of the request</param>
        /// /// <param name="p_flags">Request Configuration attributes</param>
        /// <returns>Running Web Request</returns>
        public WebRequest Put(string p_url,WebRequestFlags p_flags = WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache) { return Send(HttpMethod.Put,p_url,p_flags); }

        /// <summary>
        /// Starts a DELETE process of this web request with the informed and URL.
        /// </summary>        
        /// <param name="p_url">Base URL of the request</param>
        /// /// <param name="p_flags">Request Configuration attributes</param>
        /// <returns>Running Web Request</returns>
        public WebRequest Delete(string p_url,WebRequestFlags p_flags = WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache) { return Send(HttpMethod.Delete,p_url,p_flags); }

        /// <summary>
        /// Starts a HEAD process of this web request with the informed and URL.
        /// </summary>        
        /// <param name="p_url">Base URL of the request</param>
        /// /// <param name="p_flags">Request Configuration attributes</param>
        /// <returns>Running Web Request</returns>
        public WebRequest Head(string p_url,WebRequestFlags p_flags = WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache) { return Send(HttpMethod.Head,p_url,p_flags); }

        /// <summary>
        /// Starts a CREATE process of this web request with the informed and URL.
        /// </summary>        
        /// <param name="p_url">Base URL of the request</param>
        /// /// <param name="p_flags">Request Configuration attributes</param>
        /// <returns>Running Web Request</returns>
        public WebRequest Create(string p_url,WebRequestFlags p_flags = WebRequestFlags.DefaultBuffer | WebRequestFlags.DefaultCache) { return Send(HttpMethod.Create,p_url,p_flags); }

        #endregion

        /// <summary>
        /// Stops the request processing.
        /// </summary>
        public void Cancel() {
            if(m_uwr==null) return;
            InternalDispose();
            state = WebRequestState.Cancel;
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
        /// Handler called when the activity entered execution pool.
        /// </summary>
        protected override void OnStart() {
            state = WebRequestState.Start;
        }

        protected override void OnStop() {
            switch (state) {
                case WebRequestState.Queue: break;
                case WebRequestState.Cancel: {
                    state = WebRequestState.Stop;
                }
                break;
                default: {
                    state = WebRequestState.Stop;
                    state = isError ? WebRequestState.Error : WebRequestState.Success;
                }
                break;
            }
        }

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
            m_attrib_filtered = WebRequestFlags.None;            
        }

        /// <summary>
        /// Throws an exception for the web request
        /// </summary>
        /// <param name="p_error"></param>
        /// <param name="p_raise_event"></param>
        new public void Throw(Exception p_error,bool p_raise_event=false) {
            progress = 1f;
            base.Throw(p_error,p_raise_event);
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
        protected override void OnStateUpdate(WebRequestState p_state) {

            //Debug.Log($"WebRequest> OnStateUpdate / [{context}] {p_state}");

            switch (p_state) {

                case WebRequestState.Queue: {
                }
                break;
                case WebRequestState.Start: {
                    state = WebRequestState.CacheSearch;
                }
                break;

                #region CacheSearch
                case WebRequestState.CacheSearch: {
                    //Invalidate cached flag
                    cached = false;
                    //Start cache search data process loop
                    Process wr_cache_search_tsk = 
                    Process.Start(
                    delegate (ProcessContext p_ctx,Process p_proc) {
                        //Fetch TTL
                        float cache_ttl = ttl <= 0f ? DefaultCacheTTL : ttl;
                        //Check if cache will be used
                        bool use_cache = !IsFlagEnabled(WebRequestFlags.NoCache);
                        //Sample cache based on URL generated MD5 hash or <null> if no-cache
                        WebCacheEntry e = use_cache ? WebRequestCache.Get(m_internal_url) : null;
                        //Last check if cache exists or expired
                        use_cache = e == null ? false : e.IsAlive(cache_ttl);
                        //If not using cache skip to creating the response
                        if(!use_cache) {
                            //If entry exists dispose it
                            if(e!=null) WebRequestCache.Dispose(e);
                            //Switch to create state
                            state = WebRequestState.Create; 
                            //Exit process
                            return false;
                        }                        
                        //Retrieve cached stream and proceed to final steps
                        response = new HttpRequest();
                        response.body.stream = e.GetStream($"{TempPath}{e.hash}_response");
                        response.progress = 1f;
                        cached = true;
                        //Trigger Cache Success inside main thread
                        wr_cache_search_tsk =
                        Process.Start(
                        delegate (ProcessContext p_success_ctx,Process p_success_proc) {
                            state = WebRequestState.CacheSuccess;
                            return false;
                        },ProcessContext.Update);
                        wr_cache_search_tsk.name = $"WebRequest.{id}.Cache.Success";
                        return false;
                    },DataProcessContext);
                    state = WebRequestState.Processing;
                }
                break;
                #endregion

                #region Create
                case WebRequestState.Create: {
                    //Mini state machine 
                    WebRequestFlags buffer_mode = (m_attrib_filtered & WebRequestFlags.BufferMask);
                    int wr_create_state = 0;
                    Process wr_setup_task = null;
                    Process form_copy = null;
                    string temp_request_fp  = buffer_mode == WebRequestFlags.FileBuffer ? GetTempFilePath("request") : "";
                    string temp_response_fp = buffer_mode == WebRequestFlags.FileBuffer ? temp_request_fp.Replace("request","response") : "";
                    ProcessAction wr_create_task = null;
                    //Flag telling the stream in the request was made before
                    bool is_custom_stream = true;
                    //Initialization loop, will run inside a thread and also unity                    
                    wr_create_task =
                    delegate (ProcessContext p_ctx,Process p_proc) {
                        //In case of the main request ending
                        if (!active) return false;
                        //State machine step
                        switch (wr_create_state) {
                            //Initialize
                            case 0: {
                                //Assert Request instance
                                if (request == null) request = new HttpRequest();
                                //If the body doesnt have any stream
                                if (request.body.stream == null) {
                                    is_custom_stream = false;
                                    FileInfo fi = new FileInfo(temp_request_fp);                                    
                                    Stream ss = null;
                                    if(buffer_mode == WebRequestFlags.FileBuffer) {
                                        FileStream fs = fi.Open(FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite);
                                        fs.SetLength(0);
                                        //fs.Flush();
                                        ss = fs;
                                    }
                                    else {
                                        ss = new MemoryStream();
                                    }
                                    //Stream ss = buffer_mode == WebRequestFlags.FileBuffer ? (Stream)File.Open(temp_request_fp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.ReadWrite) : (Stream)new MemoryStream();
                                    request.body.Initialize(ss);
                                }
                                //Flush non unity
                                wr_create_state = 1;
                            }
                            break;
                            //Flush non-unity fields (per frame)
                            case 1: {
                                //If its a custom stream just skip the form generation
                                if (is_custom_stream) { wr_create_state = 3; form_copy = Process.Delay(1f / 60f); break; }
                                //Keep consuming fields to flush
                                if (request.body.FlushStep(false)) break;
                                //Set the step to 'unity' flush
                                wr_create_state = 2;
                                //Create another task but now in unity main thread
                                wr_setup_task = Process.Start(wr_create_task,ProcessContext.Update);
                                wr_setup_task.name = $"WebRequest.{id}.Streams";
                            }
                            //kill the thread mode
                            return false;
                            //Flush unity data fields (per frame)
                            case 2: {
                                //Keep consuming fields to flush
                                if (request.body.FlushStep(true)) break;
                                //Flush form information into the stream
                                form_copy = request.body.FlushFormAsync();
                                //Wait form copying into stream
                                wr_create_state = 3;
                            }
                            break;
                            //Wait form copy and completes the setup
                            case 3: {
                                //Null check
                                if (form_copy == null) { Throw(new Exception("WebRequest: Form Copy into Stream Failed to Start")); return false; }
                                //Wait copy completion
                                if (form_copy.state == ProcessState.Run) break;
                                if (form_copy.name == "form-error" ) { Throw(new Exception("WebRequest: Form Copy into Stream Failed")); return false; }
                                if (form_copy.name == "form-cancel") { Throw(new Exception("WebRequest: Form Copy into Stream Cancelled")); return false; }
                                //Request is ready to ship, so create response
                                response = new HttpRequest();
                                //Prepare the response stream
                                //Stream ss = buffer_mode == WebRequestFlags.FileBuffer ? (Stream)File.Open(temp_response_fp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.ReadWrite) : (Stream)new MemoryStream();
                                FileInfo fi = new FileInfo(temp_response_fp);
                                Stream ss = null;
                                if (buffer_mode == WebRequestFlags.FileBuffer) {
                                    FileStream fs = fi.Open(FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite);
                                    fs.SetLength(0);
                                    //fs.Flush();
                                    ss = fs;
                                } else {
                                    ss = new MemoryStream();
                                }
                                //Set the stream reference
                                response.body.stream = ss;
                                //Prepare Unity's Request Send inside main thread
                                Process wr_send_tsk = 
                                Process.Start(delegate (ProcessContext p_send_ctx,Process p_send_proc) {
                                    state = WebRequestState.Send;
                                    return false;
                                },ProcessContext.Update);
                                wr_send_tsk.name = $"WebRequest.{id}.Send";
                            }
                            //Kill task
                            return false;
                        }
                        return true;
                    };
                    //Start the initialization thread
                    wr_setup_task = Process.Start(wr_create_task,DataProcessContext);
                    wr_setup_task.name = $"WebRequest.{id}.Setup";
                    //Keep running until parallel task runs
                    state = WebRequestState.Processing;
                }
                break;
                #endregion

                #region Send
                case WebRequestState.Send: {
                    //Create handlers
                    UploadHandlerStream   uh = new UploadHandlerStream(request.body.stream);
                    DownloadHandlerStream dh = new DownloadHandlerStream(response.body.stream);
                    if (!dh.valid) { Throw(new Exception("WebRequest: Failed to Create Download Handler!")); break; }
                    //Create request
                    m_uwr = new UnityWebRequest(m_internal_url,method,dh,uh);
                    //Flag telling its a GET request so there is no upload
                    bool is_get = method == HttpMethod.Get;
                    //Upload size in mb
                    float upload_mb = ((float)request.body.length) / 1024f / 1024f;
                    //Balance out progress report the bigger the upload
                    //few kbytes = 20% 5mb+ 60%
                    float upload_progress_w = is_get ? 0f : Mathf.Lerp(0.05f,0.6f,upload_mb / 5f);
                    float download_progress_w = 1f - upload_progress_w;
                    //Fill headers
                    request.header.CopyTo(m_uwr);
                    //Start Request
                    m_uwr_op = m_uwr.SendWebRequest();
                    //Keep the main fsm looping
                    state = WebRequestState.Processing;
                    //Upload/Download state machine vars
                    float last_upload_progress = -0.1f;
                    float last_download_progress = -0.1f;
                    bool is_first_download = true;
                    bool is_first_upload = true;
                    bool is_download_complete = false;
                    bool is_upload_complete = false;
                    float timeout_elapsed = 0f;
                    //Starts the unity request polling task

                    Process uwr_task =
                    Process.Start(
                    delegate (ProcessContext p_ctx,Process p_proc) {
                        //In case of cancel
                        if (!active) return false;
                        float timeout_duration = timeout <= 0f ? DefaultTimeout : timeout;
                        //Update Timeout
                        if (timeout_duration > 0f) {
                            timeout_elapsed += Time.unscaledDeltaTime;
                            if (timeout_elapsed >= timeout_duration) {
                                state = WebRequestState.Timeout;
                                Throw(new Exception($"WebRequest: Timeout"));                                
                                return false;
                            }
                        }
                        //Progress values
                        bool is_done = m_uwr.isDone;
                        float upload_progress = is_done ? 1f : (is_get ? 1f : uh.progress * 0.99f);
                        float download_progress = is_done ? 1f : dh.progress * 0.99f;
                        progress = (upload_progress_w * upload_progress) + (download_progress_w * download_progress);
                        //Update the http data
                        request.progress  = upload_progress;
                        response.progress = download_progress;
                        //Process progression (GET will not trigger upload)
                        bool has_u = false;
                        bool has_d = false;
                        has_u = ProcessProgress(true,ref is_first_upload,ref is_upload_complete,ref last_upload_progress,upload_progress);
                        has_d = ProcessProgress(false,ref is_first_download,ref is_download_complete,ref last_download_progress,download_progress);
                        //Reset timeout counter if any progress
                        if (has_u || has_d) timeout_elapsed = 0f;
                        //Keep running
                        if (!is_done) return true;
                        //All steps completed                        
                        state = WebRequestState.RequestComplete;
                        return false;
                    },ProcessContext.Update);
                    uwr_task.name = $"WebRequest.Unity.Update";
                }
                break;
                #endregion

                #region RequestComplete
                case WebRequestState.RequestComplete: {
                    code = (HttpStatusCode)m_uwr.responseCode;
                    bool is_error = m_uwr.isNetworkError || m_uwr.isHttpError;
                    //Error event and finish loop                                                        
                    if (is_error) {
                        string error_pfx = m_uwr.isNetworkError ? "Network" : "HTTP";
                        //Write response headers
                        response.header.CopyFrom(m_uwr);
                        Throw(new Exception($"WebRequest: {error_pfx} Error @ {m_uwr.error}"));
                        return;
                    }
                    //Task to finalize the request                            
                    Process wr_finalize_task = null;
                    //Mini FSM
                    int wr_finalize_state = 0;
                    ProcessAction wr_finalize_cb = null;
                    wr_finalize_cb =
                    delegate (ProcessContext p_ctx,Process p_proc) {
                        switch (wr_finalize_state) {
                            //Execute heavy steps | Thread
                            case 0: {
                                //Reset Stream position
                                response.body.stream.Seek(0,SeekOrigin.Begin);
                                //Filter cache flags
                                WebRequestFlags cache_mode = (m_attrib_filtered & WebRequestFlags.CacheMask);
                                if (cache_mode == WebRequestFlags.DefaultCache) cache_mode = DefaultCacheMode;
                                //Validate cache is enabled
                                bool use_cache = IsFlagEnabled(cache_mode,WebRequestFlags.FileCache | WebRequestFlags.MemoryCache);
                                if (use_cache) {
                                    //If cache is file system based
                                    bool use_fs = IsFlagEnabled(cache_mode,WebRequestFlags.FileCache);
                                    //Save the cache
                                    WebRequestCache.Add(m_internal_url,response.body.stream,use_fs);
                                }
                                //Restore seek position
                                response.body.stream.Seek(0,SeekOrigin.Begin);
                                //next success state
                                wr_finalize_state = 1;
                                //Continue loop but in main thread
                                wr_finalize_task = Process.Start(wr_finalize_cb,ProcessContext.Update);
                                wr_finalize_task.name = $"WebRequest.{id}.Complete";
;
                            }
                            return false;
                            //Finalize in unity main thread
                            case 1: {
                                base.Stop();
                            }
                            return false;
                        }

                        return true;
                    };
                    //Run finalization as thread to avoid fps drops
                    wr_finalize_task = Process.Start(wr_finalize_cb,DataProcessContext);
                    wr_finalize_task.name = $"WebRequest.{id}.Finalize";
                    //Wait for finalize 
                    state = WebRequestState.Processing;
                }
                break;
                #endregion

                case WebRequestState.Processing: {
                    //Wait for secondary states to run
                }
                break;

                case WebRequestState.CacheSuccess: {
                    code = HttpStatusCode.NotModified;
                    //End request as it was cache hit
                    base.Stop();
                }
                break;

            }

        }      
        
        /// <summary>
        /// Helper for generating start,progress complete states
        /// </summary>        
        private bool ProcessProgress(bool p_is_upload,ref bool p_is_first,ref bool p_is_complete,ref float p_progress,float p_next_progress) {
            bool has_progress = false;
            if(p_next_progress>p_progress) {
                p_progress = p_next_progress;
                if(p_is_first) { 
                    p_is_first=false;
                    //Start
                    state = (p_is_upload ? WebRequestState.UploadStart : WebRequestState.DownloadStart);
                    state = WebRequestState.Processing;
                }
                //Progress                           
                state = (p_is_upload ? WebRequestState.UploadProgress : WebRequestState.DownloadProgress);
                state = WebRequestState.Processing;
                has_progress = true;
            }           
            if(p_is_complete) return true;            
            if(p_progress>=1f) {
                p_is_complete=true;
                //Progress                           
                state = (p_is_upload ? WebRequestState.UploadComplete : WebRequestState.DownloadComplete);
                has_progress = true;
            }
            return has_progress;
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
        public Process GetAudioClip(AudioType p_type,System.Action<AudioClip> p_callback,AudioClip p_default=null) {
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
            Process parser = null;
            parser =
            Process.Start(delegate(ProcessContext ctx, Process p) {
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
                        switch(c.loadState) {                            
                            case AudioDataLoadState.Failed: { if (p_callback != null) p_callback(p_default); return false; }
                            case AudioDataLoadState.Loaded: {
                                if (p_callback != null) p_callback(c);
                                temp_req.downloadHandler.Dispose();
                                temp_req.Dispose();
                            }
                            return false;
                        }
                    }
                    return true;
                }
                return true;
            });            
            return parser;
        }        
        #endregion

        #region IStatusProvider
        /// <summary>
        /// Returns this webrequest execution state converted to status flags.
        /// </summary>
        /// <returns>Current webrequest status</returns>
        public StatusType GetStatus() {
            switch(state) {
                case WebRequestState.Idle:             return StatusType.Idle;                
                case WebRequestState.Start:
                case WebRequestState.Queue:                
                case WebRequestState.UploadStart:                
                case WebRequestState.DownloadStart:
                case WebRequestState.UploadProgress:                
                case WebRequestState.UploadComplete:
                case WebRequestState.DownloadProgress:
                case WebRequestState.Processing:
                case WebRequestState.DownloadComplete: return StatusType.Running;
                case WebRequestState.CacheSuccess:
                case WebRequestState.Success:          return StatusType.Success;
                case WebRequestState.Timeout:
                case WebRequestState.Error:            return StatusType.Error;                
                case WebRequestState.Cancel:           return StatusType.Cancelled;
            }
            return StatusType.Invalid;
        }
        #endregion

        #region IProgressProvider
        /// <summary>
        /// Returns this webrequest progress status
        /// </summary>
        /// <returns>WebRequest progress related to the download/upload status</returns>
        override public float GetProgress() {
            return progress;
        }
        #endregion

    }
    #endregion

}

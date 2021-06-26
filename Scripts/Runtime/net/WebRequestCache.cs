using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityExt.Core.Net {

    #region class WebCacheEntry
    /// <summary>
    /// Class that implements a simple cache entry structure for querying.
    /// </summary>
    public class WebCacheEntry {

        /// <summary>
        /// Flag that tells the cache entry has information in it.
        /// </summary>
        public bool valid { get { return (stream!=null) || (file!=null); } }

        /// <summary>
        /// Reference to the stream containing the cache data.
        /// </summary>
        public Stream stream;

        /// <summary>
        /// Reference to the file if any.
        /// </summary>
        public FileInfo file;

        /// <summary>
        /// File path of the cache data if file mode.
        /// </summary>
        public string path { 
            get { 
                if(file!=null) return file.FullName;
                return "";                
            }
        }

        /// <summary>
        /// Hash of the entry, when in file mode its also the file name.
        /// </summary>
        public string hash;

        /// <summary>
        /// Cache Timestamp
        /// </summary>
        public DateTime timestamp;

        /// <summary>
        /// Check if the cache entry is alive based on the specified time to live.
        /// </summary>
        /// <param name="p_ttl"></param>
        /// <returns></returns>
        public bool IsAlive(float p_ttl) {
            if(file!=null) { file.Refresh(); timestamp = file.LastWriteTime; }
            TimeSpan off = DateTime.Now - timestamp;            
            return off.TotalMinutes<p_ttl;
        }

        /// <summary>
        /// Sets this entry information
        /// </summary>
        /// <param name="p_hash"></param>
        /// <param name="p_stream"></param>
        public void Set(string p_hash,Stream p_stream) {
            if(p_stream==null) return;
            hash      = p_hash;
            stream    = p_stream;
            timestamp = DateTime.Now;                
        }

        /// <summary>
        /// Set this entry data with an existing file.
        /// </summary>
        public void Set(FileInfo p_file) {
            if(p_file==null) return;
            file = p_file;
            file.Refresh();
            hash      = file.Name;
            timestamp = file.LastWriteTime;            
        }

        /// <summary>
        /// Transfers this cache content into the target stream.
        /// </summary>
        /// <param name="p_stream"></param>
        public bool CopyTo(Stream p_stream) {
            if(p_stream==null) return false;
            Stream ss = file==null ? stream : File.OpenRead(file.FullName);
            if(ss==null) { Debug.LogWarning("WebRequestCache> CopyTo / Failed to fetch cached stream!"); return false; }
            //Reset stream
            ss.Seek(0, SeekOrigin.Begin);
            ss.CopyTo(p_stream);
            p_stream.Flush();
            //If file close handle for next use
            if(file!=null) { ss.Close(); ss.Dispose(); }
            //success
            return true;
        }

        /// <summary>
        /// Returns the stream associated with this cache, if file create a copy into a chosen path to avoid sharing violation.
        /// </summary>
        /// <returns>Either a MemoryStream or a FileStream containing the cached data</returns>
        public Stream GetStream(string p_file_path="") {
            //If not file just return stream
            if(file==null)   return stream;
            if(!file.Exists) return null;
            FileStream new_file   = File.Open(p_file_path,   FileMode.Create,FileAccess.ReadWrite,FileShare.ReadWrite);
            FileStream cache_file = File.Open(file.FullName, FileMode.Open,  FileAccess.ReadWrite,FileShare.ReadWrite);
            cache_file.CopyTo(new_file);
            new_file.Flush(true);
            cache_file.Close();
            return new_file;
        }

        /// <summary>
        /// Dispose the cache entry.
        /// </summary>
        public void Dispose() {
            string fp = path;
            if(stream!=null) {
                stream.Close();
                stream.Dispose();
                stream=null;
            }
            if(string.IsNullOrEmpty(fp)) return;
            FileInfo fi = new FileInfo(fp);
            if(fi.Exists) { fi.Delete(); }
        }
    }
    #endregion

    /// <summary>
    /// Class that wraps the functionality of cache storing and entry retrieving.
    /// </summary>
    public class WebRequestCache {

        /// <summary>
        /// Table of available cache entries.
        /// </summary>
        static private Dictionary<string,WebCacheEntry> m_cache_table = new Dictionary<string, WebCacheEntry>();
        static private StreamWriter             m_hash_writer = new StreamWriter(new MemoryStream(1024),Encoding.ASCII);
        static private MD5CryptoServiceProvider m_md5         = new MD5CryptoServiceProvider();             
        
        /// <summary>
        /// Disposes all entries from the cache.
        /// </summary>
        static public void Clear() {
            ICollection<string> kl = m_cache_table.Keys;
            foreach(string it in kl) if(m_cache_table[it]!=null) m_cache_table[it].Dispose();
            m_cache_table.Clear();
        }

        /// <summary>
        /// Access the informed folder and load cached data in file format.
        /// </summary>
        /// <param name="p_cache_folder">Path to the cache folder.</param>
        static public void Load(string p_cache_folder) {
            DirectoryInfo di = new DirectoryInfo(p_cache_folder);
            if(!di.Exists) { Debug.LogWarning($"WebRequestCache> Directory [{p_cache_folder}] not found!"); return; }
            List<FileInfo> fl = new List<FileInfo>(di.GetFiles("*"));
            for(int i=0;i<fl.Count;i++) {
                FileInfo f = fl[i];
                //Skip empty files
                if(f.Length <= 0) { f.Delete(); continue; }
               Add(f);
            }
            if(m_cache_table.Count>0) Debug.Log($"WebRequestCache> Load / Found {m_cache_table.Count} cached entries!");
        }

        /// <summary>
        /// Adds a new cache entry from an existing file.
        /// </summary>
        /// <param name="p_file"></param>
        static public void Add(FileInfo p_file) {
            if(p_file==null)   return;
            if(!p_file.Exists) return;
            string h = p_file.Name;
            if(m_cache_table.ContainsKey(h)) { Debug.LogWarning($"WebRequestCache> Cache Collision at [{p_file.FullName}]"); return; }
            WebCacheEntry e = new WebCacheEntry();
            e.Set(p_file);
            m_cache_table[h] = e;
        }

        /// <summary>
        /// Adds a cache entry informing the hash and data stream.
        /// </summary>
        /// <param name="p_hash"></param>
        /// <param name="p_stream"></param>
        static public void Add(string p_key,Stream p_stream,bool p_use_filesystem) {
            if(p_stream==null) return;
            string h = Hash(p_key);            
            if(m_cache_table.ContainsKey(h)) { Debug.LogWarning($"WebRequestCache> Cache Collision at [{h}]"); return; }
            if(p_use_filesystem) {
                string fp = $"{WebRequest.CachePath}{h}";
                Debug.Log($"WebRequestCache> Writing Cache at {fp}");
                FileStream fs = File.Open(fp, FileMode.CreateNew, FileAccess.ReadWrite);
                p_stream.CopyTo(fs);
                fs.Flush();
                fs.Close();
                Add(new FileInfo(fp));
                return;
            }
            MemoryStream ms = new MemoryStream();
            p_stream.CopyTo(ms);
            ms.Flush();
            ms.Seek(0, SeekOrigin.Begin);
            WebCacheEntry e = new WebCacheEntry();
            e.Set(h,ms);  
            m_cache_table[h] = e;
        }

        /// <summary>
        /// Generates a MD5 hash to index a cache entry.
        /// </summary>
        /// <param name="p_key"></param>
        /// <returns></returns>
        static public string Hash(string p_key) {

            ulong c0 = 3074457345618258791ul;
            ulong c1 = 3074457345618258799ul;            
            ulong hv;
            int k = 0;
            hv = c0; for(int i = 0; i < p_key.Length; i++) { hv += p_key[i]; hv *= c1; }
            for(int i = 0; i < 8; i++) m_hash_bytes[k++] = (byte)((hv>>i)&0xff);
            hv = c0/2; for(int i = 0; i < p_key.Length; i++) { hv += p_key[i]; hv *= c1/2; }
            for(int i = 0; i < 8; i++) m_hash_bytes[k++] = (byte)((hv>>i)&0xff);
            hv = c0/4; for(int i = 0; i < p_key.Length; i++) { hv += p_key[i]; hv *= c1/4; }
            for(int i = 0; i < 8; i++) m_hash_bytes[k++] = (byte)((hv>>i)&0xff);            
            string h = System.Convert.ToBase64String(m_hash_bytes,Base64FormattingOptions.None);
            h = h.Replace('/','_');
            h = h.Replace('\\','_');
            return h;
            
        }
        static private byte[] m_hash_bytes = new byte[24];
        
        /// <summary>
        /// Access the cache table and return an Entry if one exists.
        /// </summary>
        /// <param name="p_key"></param>
        /// <returns></returns>
        static public WebCacheEntry Get(string p_key) {
            string h = Hash(p_key);
            if(!m_cache_table.ContainsKey(h)) return null;
            WebCacheEntry e = m_cache_table[h];
            if(e == null) { m_cache_table.Remove(h); return null; }
            if(!e.valid)  { m_cache_table.Remove(h); return null; }
            return e;
        }

        /// <summary>
        /// Check if a given key value is cached.
        /// </summary>
        /// <param name="p_key"></param>
        /// <returns></returns>
        static public bool Contains(string p_key) {
            WebCacheEntry e = Get(p_key);
            return e!=null;
        }

        /// <summary>
        /// Disposes and remove a cached entry.
        /// </summary>
        /// <param name="p_entry"></param>
        static public void Dispose(WebCacheEntry p_entry) {
            if(p_entry==null) return;
            string h = p_entry.hash;
            p_entry.Dispose();
            m_cache_table.Remove(h);
        }

        /// <summary>
        /// Iterates the full cache table and dispose all entries.
        /// </summary>
        static public void Dispose() {
            if(m_cache_table.Count<=0) return;
            Dictionary<string,WebCacheEntry>.KeyCollection kl = m_cache_table.Keys;
            foreach(string k in kl) if(m_cache_table[k]!=null) m_cache_table[k].Dispose();
            m_cache_table.Clear();
        }

    }
    
}

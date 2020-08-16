using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using UnityEngine.Scripting;
using System.Collections.Generic;
using System.Text;

namespace UnityExt.Core {

    /// <summary>
    /// Utility component that captures execution statistics and plot a simplified graph and update fields with stats.
    /// </summary>    
    public class UIFPSWidget : MonoBehaviour {

        #region static

        /// <summary>
        /// Creates and caches a basic color ramp texture to feedback execution quality
        /// </summary>
        static public Texture2D DefaultRamp {
            get {
                if(m_default_ramp) return m_default_ramp;
                m_default_ramp = new Texture2D(7,1);
                m_default_ramp.SetPixels(new Color[] { 
                    new Color(0.00f,0.80f,0.00f), //green
                    new Color(0.79f,0.89f,0.00f), //yellow
                    new Color(0.90f,0.66f,0.00f), //orange
                    new Color(0.90f,0.23f,0.00f), //red
                    new Color(0.85f,0.00f,0.26f), //dark-red
                    new Color(0.62f,0.00f,0.80f), //purple             
                    new Color(0.10f,0.00f,0.25f)  //dark-purple
                });
                m_default_ramp.filterMode = FilterMode.Point;
                m_default_ramp.name       = "fps-widget-default-ramp";
                m_default_ramp.hideFlags  = HideFlags.HideAndDontSave;
                m_default_ramp.Apply();
                return m_default_ramp;
            }
        }
        static private Texture2D m_default_ramp;

        /// <summary>
        /// Internals.
        /// </summary>
        static FrameTiming[] m_timing_samples = new FrameTiming[60];
         
        #endregion

        /// <summary>
        /// Reference to the graph plotting image.
        /// </summary>
        public RawImage graph { get { return m_graph ? m_graph : (m_graph = GetComponent<RawImage>()); } }
        [SerializeField]
        private RawImage m_graph;
        /// <summary>
        /// Color of the plot cursor.
        /// </summary>
        public Color graphCursorColor = Color.red;
        /// <summary>
        /// Reference to the ramp
        /// </summary>
        public Texture2D ramp { get { return m_ramp ? m_ramp : DefaultRamp; } set { m_ramp=value; } }
        [SerializeField]
        private Texture2D m_ramp;
        /// <summary>
        /// Reference to the text field where information will be written.
        /// </summary>
        public Text field;
        /// <summary>
        /// Target FPS for color grading.
        /// </summary>
        [Range(1f,1000f)]
        public int targetFPS = 60;        
        /// <summary>
        /// Number of samples captured to compose the FPS tracker
        /// </summary>
        [Range(1f,60f*10f)]
        public int targetFPSSamples = 60;
        protected float m_fps_samples_inv { get { return targetFPSSamples<=0 ? 0f : (1f/(float)targetFPSSamples); } }
        /// <summary>
        /// Current FPS
        /// </summary>
        public int fps { get { return m_fps; } protected set { m_fps=value; } }
        [SerializeField]
        private int m_fps;
        /// <summary>
        /// Internals
        /// </summary>        
        private int           m_graph_cursor;        
        private Texture2D     m_graph_buffer;
        private int           m_fps_samples_counter;
        private float         m_fps_sample_sum;
        private int           m_next_buffer_assert;
        private RectTransform m_field_rt;
        private StringBuilder m_stats_sb;

        static private List<string>  m_number_string;
        static private List<string>  m_vsync_string;

        private Canvas        GetCanvas() {            
            if(m_canvas) return m_canvas;
            Transform t = transform.parent;
            while(t) {
                m_canvas = t.GetComponent<Canvas>();
                if(m_canvas) break;
                t = t.parent;
            }
            return m_canvas;
       }
       private Canvas m_canvas;
        
        /// <summary>
        /// CTOR
        /// </summary>
        protected void Start() {                     
            //Clears the graph
            Clear();
            //Cache the field RT
            if(field) m_field_rt = field.rectTransform;            
            //Next buffer assertion force
            m_next_buffer_assert = 0;
            //Create and cache a number>string lut
            if(m_number_string==null) {
                m_number_string = new List<string>();
                for(int i=0;i<1201;i++) m_number_string.Add(string.Format("{0,3:###}",i));
            }
            //Create a cache lut for vsync string
            if(m_vsync_string==null) {
                m_vsync_string = new List<string>();
                m_vsync_string.Add("Off");
                m_vsync_string.Add("On");
                m_vsync_string.Add("Half");
            }
            //Create string builder for stats
            if(m_stats_sb==null) m_stats_sb = new StringBuilder();
        }

        /// <summary>
        /// Clears the buffer and reset the plotting cursor.
        /// </summary>
        public void Clear() {            
            m_graph_cursor       = 0;
            if(m_graph_buffer) {                
                for(int i = 0; i<m_graph_buffer.width; i++) m_graph_buffer.SetPixel(i,1,Color.clear);
                m_graph_buffer.Apply();
                m_graph_cursor=m_graph_buffer.width-1;
            }
            if(field) {
                field.text = "GPU:---ms|CPU:---ms|FPS:---";
            }
            m_fps_sample_sum      = 0f;
            m_fps_samples_counter = 0;
        }

        /// <summary>
        /// Asserts if the buffer needs creation
        /// </summary>
        protected void AssertBuffer() {
            m_next_buffer_assert--;
            if(m_next_buffer_assert>0) return;
            m_next_buffer_assert=10;
            //Skip if invalid graph
            if(!graph) return;
            //Transforms
            RectTransform rt  = graph.rectTransform;                
            Canvas        c   = GetCanvas();
            //Capture 'graph' image size
            Bounds        rtr = c ? RectTransformUtility.CalculateRelativeRectTransformBounds(c.transform,rt) : new Bounds();
            if(!c) rtr.size = new Vector3(128f,1f);
            int bw = (int) rtr.size.x;
            //Check if buffer size matches
            bool need_refresh = m_graph_buffer ? (m_graph_buffer.width != bw) : true;
            //Skip if not needed
            if(!need_refresh) return;            
            //If there is a previous buffer destroy it
            if(m_graph_buffer) Destroy(m_graph_buffer);
            //Create the buffer with the size of the raw image            
            m_graph_buffer      = new Texture2D(bw,1);
            m_graph_buffer.name = "fps-widget-graph-buffer";
            m_graph_buffer.filterMode = FilterMode.Point;
            graph.texture = m_graph_buffer;
            //Clear and reset
            Clear();
        }

        /// <summary>
        /// Executes the loop and collect information.
        /// </summary>
        protected void Update() {    
        
        
            AssertBuffer();


            //Sample data/time
            float dt     = Time.unscaledDeltaTime;            
            int   dt_ms  = (int)(dt*1000f);
            float dt_fps = dt<=0f ? 0f : (1f/dt);
            float fps_t  = (float)targetFPS;            
            //Capture FPS
            m_fps_samples_counter++;
            m_fps_sample_sum += dt_fps * m_fps_samples_inv;
            if(m_fps_samples_counter>targetFPSSamples) {
                fps = Mathf.RoundToInt(m_fps_sample_sum);
                m_fps_samples_counter = 0;
                m_fps_sample_sum      = 0f;
            }
            //   1.0 == Bad
            //   0.0 == Good
            float r  = Mathf.Clamp01(1f - (dt_fps/fps_t));
            //Draw
            Texture2D gb   = m_graph_buffer;            
            if(gb) {
                //Fetch ramp just once
                Texture2D ramp_tex   = ramp;
                // Sample Ramp color based on execution quality
                Color     ramp_color = ramp_tex.GetPixel(Mathf.RoundToInt(r*(ramp_tex.width-1)),0);
                //Graph cursor
                int       gb_c       = m_graph_cursor;
                //Paint the red plot tracker
                if(gb_c>1) gb.SetPixel(gb_c-1,0,graphCursorColor);
                //Paint the ramp sample
                gb.SetPixel(gb_c,0,ramp_color);
                //Apply changes
                gb.Apply();
                //Move the cursor and reset if out of bounds
                gb_c=gb_c<=0 ? (gb.width-1) : (gb_c-1);
                //Store cursor back
                m_graph_cursor=gb_c;
            }
            //Capture framing if available
            FrameTimingManager.CaptureFrameTimings();
            uint timing_data_len = FrameTimingManager.GetLatestTimings(1,m_timing_samples);
            //CPU Time in ms
            int cpu_time = timing_data_len<=0 ? dt_ms : (int)m_timing_samples[0].cpuFrameTime;
            //GPU Time in ms
            int gpu_time = timing_data_len<=0 ? -1    : (int)m_timing_samples[0].gpuFrameTime;
            //Log string (StringBuilder helps with GC.Alloc)
            m_stats_sb.Clear();
            //Fill timings and fps
            if(cpu_time>=0) {
                m_stats_sb.Append("CPU:");
                m_stats_sb.Append(m_number_string[Mathf.Clamp(cpu_time,0,1200)]);
                m_stats_sb.Append("ms|");
            }
            if(gpu_time>=0){
                m_stats_sb.Append("GPU:");
                m_stats_sb.Append(m_number_string[Mathf.Clamp(gpu_time,0,1200)]);
                m_stats_sb.Append("ms|");
            }
            m_stats_sb.Append("FPS:");
            m_stats_sb.Append(fps<=0 ? "---" : m_number_string[Mathf.Clamp(fps,0,1200)]);
            m_stats_sb.Append("|VSYNC: ");
            m_stats_sb.Append(m_vsync_string[QualitySettings.vSyncCount]);
            //If there is a field update it
            if(field) field.text = m_stats_sb.ToString();
        }

        /// <summary>
        /// DTOR
        /// </summary>
        protected void OnDestroy() {
            if(m_graph_buffer) Destroy(m_graph_buffer);            
        }

    }
}
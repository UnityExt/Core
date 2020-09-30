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
    public class UIMicroProfiler : ActivityBehaviour, IUpdateable {

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
                m_default_ramp.name       = "micro-profiler-default-ramp";
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
        /// Reference to the graph.
        /// </summary>
        public RawImage graph { get { return m_graph ? m_graph : (m_graph = GetComponent<RawImage>()); } }
        [SerializeField]
        private RawImage m_graph;

        /// <summary>
        /// Reference to the text field where information will be written.
        /// </summary>
        public Text field;

        /// <summary>
        /// Reference to the ramp
        /// </summary>
        public Texture2D ramp { get { return m_ramp ? m_ramp : DefaultRamp; } set { m_ramp=value; } }
        [SerializeField]
        private Texture2D m_ramp;

        /// <summary>
        /// Background color when the graph is clean.
        /// </summary>
        public Color graphBackgroundColor = new Color(0f,0f,0f,0.3f);
        
        /// <summary>
        /// Color of the plot cursor.
        /// </summary>
        public Color graphCursorColor = Color.red;
        
        /// <summary>
        /// Number of frames the profiler will request samples.
        /// </summary>
        public int sampleRate = 1;

        /// <summary>
        /// Sample min value mapped to the lowest ramp value.
        /// </summary>        
        public int minSample  = 0;

        /// <summary>
        /// Sample max value mapped to the highest ramp value.
        /// </summary>
        public int maxSample  = 100;

        /// <summary>
        /// Flag that tells this profiler will automatically plot the sample.
        /// </summary>
        public bool autoPlot  = true;

        /// <summary>
        /// Flag that tells the profiler will write the latest info in the field.
        /// </summary>
        public bool writeField = true;

        /// <summary>
        /// Current field text.
        /// </summary>
        public string fieldText { get { return m_field_text;  } }
        [SerializeField]
        private string m_field_text;

        /// <summary>
        /// Internals
        /// </summary>        
        private RectTransform m_rt;        
        private int           m_graph_cursor;        
        private Texture2D     m_graph_buffer;        
        private int           m_next_buffer_assert;
        private RectTransform m_field_rt;
        private StringBuilder m_field_sb;        
        private int           m_sample_frame;
        private bool          m_initialized;
        static private List<string>  m_number_string;


        /// <summary>
        /// Helper to find the canvas containing this widget.
        /// </summary>
        /// <returns></returns>
        private Canvas GetCanvas() {            
            if(m_canvas) return m_canvas;
            Transform t = transform.parent;
            while(t) { m_canvas = t.GetComponent<Canvas>(); if(m_canvas) break; t = t.parent; }
            return m_canvas;
        }
        private Canvas m_canvas;

        /// <summary>
        /// CTOR
        /// </summary>
        protected void Start() {            
            Initialize();
        }

        #if UNITY_EDITOR
        override protected void OnEnable() {
            base.OnEnable();
            Initialize();
        }
        #endif
        
        /// <summary>
        /// CTOR.
        /// </summary>
        virtual protected void Initialize() {
            //Lock flag
            if(m_initialized) return;
            m_initialized = true;
            //Clears the graph
            Clear();
            //Cache the field RT
            if(field) m_field_rt = field.rectTransform;      
            //Cache RT
            m_rt = graph ? graph.rectTransform : (RectTransform)transform;
            //Next buffer assertion force
            m_next_buffer_assert = 0;
            //Create and cache a number>string lut
            AssertNumberString();
            //Create string builder for stats
            if(m_field_sb==null) m_field_sb = new StringBuilder();
        }

        /// <summary>
        /// Asserts the creation of the string list for gc free number to string
        /// </summary>
        protected void AssertNumberString() {
            if(m_number_string!=null) return;
            m_number_string = new List<string>();
            for(int i=0;i<1201;i++) m_number_string.Add(string.Format("{0,3:###}",i));                        
        }

        /// <summary>
        /// Clears the buffer and reset the plotting cursor.
        /// </summary>
        virtual public void Clear() {            
            m_graph_cursor = 0;
            m_sample_frame = 0;
            if(m_graph_buffer) {                
                for(int i = 0; i<m_graph_buffer.width; i++) m_graph_buffer.SetPixel(i,1,graphBackgroundColor);
                m_graph_buffer.Apply();
                m_graph_cursor=m_graph_buffer.width-1;
            }
            if(field) field.text = "";
        }

        /// <summary>
        /// Asserts if the buffer needs creation
        /// </summary>
        private void AssertBuffer() {
            m_next_buffer_assert--;
            if(m_next_buffer_assert>0) return;
            m_next_buffer_assert=10;
            //Transforms
            RectTransform rt  = m_rt;                
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
            m_graph_buffer.name = "micro-profiler-graph-buffer";
            m_graph_buffer.filterMode = FilterMode.Point;
            if(graph) graph.texture = m_graph_buffer;
            //Clear and reset
            Clear();
        }
        
        /// <summary>
        /// Writes a number in the stringbuilder GC free.
        /// </summary>
        /// <param name="p_number"></param>
        /// <param name="p_round"></param>
        protected void WriteNumber(float p_number,float p_round=1f) {
            if(p_number<0f) m_field_sb.Append("-");
            p_number = p_number<0f ? -p_number : p_number;
            p_number = Mathf.Floor(p_number/p_round) * p_round;
            int vi = (int)p_number;
            #if UNITY_EDITOR
            AssertNumberString();
            #endif
            vi = Mathf.Clamp(vi,0,m_number_string.Count-1);
            m_field_sb.Append(m_number_string[vi]);
        }

        #region Plot

        /// <summary>
        /// Plots a color and move the cursor one step.
        /// </summary>
        /// <param name="p_color"></param>
        public void Plot(Color p_color) {
            //Ref to buffer
            Texture2D gb = m_graph_buffer;
            //Skip if invalid
            if(!gb) return;            
            //Graph cursor
            int       gb_c       = m_graph_cursor;
            //Paint the red plot tracker
            if(gb_c>1) gb.SetPixel(gb_c-1,0,graphCursorColor);
            //Paint the ramp sample
            gb.SetPixel(gb_c,0,p_color);
            //Apply changes
            gb.Apply();
            //Move the cursor and reset if out of bounds
            gb_c=gb_c<=0 ? (gb.width-1) : (gb_c-1);
            //Store cursor back
            m_graph_cursor=gb_c;            
        }

        /// <summary>
        /// Plots a ramp color based on its intensity.
        /// </summary>
        /// <param name="p_intensity">Value intensity in the 0..1 range.</param>
        public void Plot(float p_intensity) {
            //Clamped intensity
            float r = Mathf.Clamp01(p_intensity);
            //Fetch ramp just once
            Texture2D ramp_tex   = ramp;
            //Sample Ramp color and plot
            Plot(ramp_tex ? ramp_tex.GetPixel(Mathf.RoundToInt(r*(ramp_tex.width-1)),0) : new Color(p_intensity,p_intensity,p_intensity,1f));
        }

        #endregion

        /// <summary>
        /// Handler called on a sampling 
        /// </summary>
        virtual protected int OnSample() { return minSample; }

        /// <summary>
        /// Handler called to write into the text buffer.
        /// </summary>
        /// <param name="p_text"></param>
        virtual protected void OnWrite(StringBuilder p_text)  { }

        /// <summary>
        /// Executes the loop and collect information.
        /// </summary>
        virtual public void OnUpdate() {
            //Assert buffer texture
            AssertBuffer();
            //Frame counter
            m_sample_frame++;
            if(m_sample_frame>=sampleRate) {
                m_sample_frame=0;
                int   v  = OnSample();
                int   dv = maxSample-minSample;
                float r  = Mathf.Clamp01(dv<=0 ? 0f : ((float)(v-minSample)/(float)(dv)));
                if(autoPlot) Plot(r);
                //Assertion
                if(m_field_sb==null) m_field_sb = new StringBuilder();
                //Clear text buffer
                m_field_sb.Clear();
                //Write all information
                OnWrite(m_field_sb);
                //Store the latest field text
                m_field_text = m_field_sb.ToString();
                //If there is a field update it
                if(writeField) if(field) field.text = m_field_text;
            }
        }

        /// <summary>
        /// DTOR
        /// </summary>
        override protected void OnDestroy() {            
            base.OnDestroy();
            if(m_graph_buffer) {
                if(Application.isPlaying) Destroy(m_graph_buffer); else DestroyImmediate(m_graph_buffer);
            }
        }

    }
}
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using UnityEngine.Scripting;
using System.Collections.Generic;
using System.Text;

namespace UnityExt.Core.UI {

    #region enum SystemProfileMetricFlag

    /// <summary>
    /// Enumeration to select which information should be visible.
    /// </summary>    
    public enum SystemProfileMetricFlag {
        /// <summary>
        /// Vsync State
        /// </summary>
        VSync    = 1,
        /// <summary>
        /// FPS
        /// </summary>
        FPS      = 2,
        /// <summary>
        /// CPU Time
        /// </summary>
        CPU      = 4,
        /// <summary>
        /// GPU Time
        /// </summary>
        GPU      = 8,
        /// <summary>
        /// Total Memory
        /// </summary>
        TotalMem = 16,
        /// <summary>
        /// Heap Mem
        /// </summary>
        HeapMem  = 32
    }

    #endregion

    /// <summary>
    /// Utility component that captures execution statistics and plot a simplified graph and update fields with stats.
    /// </summary>    
    public class UISystemProfiler : UIMicroProfiler {
        
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
        /// List of Metrics to be displayed.
        /// </summary>        
        public List<SystemProfileMetricFlag> metrics = new List<SystemProfileMetricFlag>() {               
              SystemProfileMetricFlag.CPU,
              SystemProfileMetricFlag.FPS
        };
        
        /// <summary>
        /// Internals
        /// </summary>        
        private int           m_fps_samples_counter;
        private int           m_current_sample;
        private float         m_fps_sample_sum;             
        private FrameTiming[] m_timing_samples = new FrameTiming[1];
        static private List<string>  m_vsync_string;

        /// <summary>
        /// CTOR
        /// </summary>
        protected override void Initialize() { 
            //Init profiler
            base.Initialize();            
            //Create a cache lut for vsync string
            if(m_vsync_string==null) {
                m_vsync_string = new List<string>();
                m_vsync_string.Add("Off");
                m_vsync_string.Add("On");
                m_vsync_string.Add("Half");
            }            
        }

        /// <summary>
        /// Clears the buffer and reset the plotting cursor.
        /// </summary>
        override public void Clear() {            
            //Profiler Clear
            base.Clear();
            m_fps_sample_sum      = 0f;
            m_fps_samples_counter = 0;
        }

        /// <summary>
        /// Returns the sample to be plotted.
        /// </summary>
        /// <returns></returns>
        protected override int OnSample() {
            return m_current_sample;
        }

        /// <summary>
        /// Writes the information.
        /// </summary>
        /// <param name="p_text"></param>
        protected override void OnWrite(StringBuilder p_text) {
            StringBuilder sb = p_text;

            bool use_sample_timing = false;
            if(metrics.Contains(SystemProfileMetricFlag.CPU)) use_sample_timing = true; else
            if(metrics.Contains(SystemProfileMetricFlag.GPU)) use_sample_timing = true;

            //Capture framing if available
            uint timing_data_len = 0;
            if(use_sample_timing) {
                FrameTimingManager.CaptureFrameTimings();
                timing_data_len = FrameTimingManager.GetLatestTimings(1,m_timing_samples);
            }

            int   vi=0;
            long  vl=0;
            float vf=0f;

            for(int i=0;i<metrics.Count;i++) {
                if(i>0) sb.Append("|");
                switch(metrics[i]) {

                    case SystemProfileMetricFlag.GPU: { 
                        //GPU Time in ms
                        vi = timing_data_len<=0 ? -1 : (int)m_timing_samples[0].gpuFrameTime;
                        if(vi<0) { sb.Append("GPU: ---"); break; }
                        sb.Append("GPU: ");
                        WriteNumber((float)vi);
                        sb.Append("ms");
                    }
                    break;

                    case SystemProfileMetricFlag.CPU: { 
                        //CPU Time in ms
                        vi = timing_data_len<=0 ? (int)(Time.unscaledDeltaTime*1000f) : (int)m_timing_samples[0].cpuFrameTime;
                        sb.Append("CPU: ");
                        WriteNumber((float)vi);
                        sb.Append("ms");
                    }
                    break;              
                    
                    case SystemProfileMetricFlag.FPS: { 
                        sb.Append("FPS: ");
                        if(fps<=0) { sb.Append("---"); break; }
                        WriteNumber((float)fps);
                    }
                    break;

                    case SystemProfileMetricFlag.VSync: { 
                        sb.Append("VSYNC: ");
                        sb.Append(m_vsync_string[QualitySettings.vSyncCount]);
                    }
                    break;

                    case SystemProfileMetricFlag.HeapMem: { 
                        //HEAP used in bytes
                        vl = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong();
                        vf = (float)(vl/1024)/1024f/10f;
                        sb.Append("HEAP: ");
                        WriteNumber(vf);
                        sb.Append("0");
                        sb.Append("Mb");
                    }
                    break;

                    case SystemProfileMetricFlag.TotalMem: { 
                        sb.Append("MEM: ");                        
                        vl = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();                        
                        if(vl<=0) { sb.Append("---"); break; }                        
                        vf = (float)(vl/1024)/1024f/10f;
                        WriteNumber(vf);
                        sb.Append("0");
                        sb.Append("Mb");
                    }
                    break;

                }
                
            }
        }
        
        /// <summary>
        /// Executes the loop and collect information.
        /// </summary>
        override protected void Update() {    
            if(!Application.isPlaying) return;
            //Update Profiler
            base.Update();        
            //Sample data/time
            float dt     = Time.unscaledDeltaTime;            
            int   dt_ms  = (int)(dt*1000f);
            float dt_fps = dt<=0f ? 0f : (1f/dt);            
            //Sample to be plotted
            m_current_sample = dt_ms;
            //Capture FPS
            m_fps_samples_counter++;
            m_fps_sample_sum += dt_fps * m_fps_samples_inv;
            if(m_fps_samples_counter>targetFPSSamples) {
                fps = Mathf.RoundToInt(m_fps_sample_sum);
                m_fps_samples_counter = 0;
                m_fps_sample_sum      = 0f;
            }            
        }

    }
}
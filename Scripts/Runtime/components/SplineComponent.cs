using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityExt.Core {

    #region enum SplineTypeFlag
    /// <summary>
    /// Enumeration that describes which kind of spline to be used in the component.
    /// </summary>
    public enum SplineTypeFlag : int {
        /// <summary>
        /// Linear
        /// </summary>
        Linear=0,
        /// <summary>
        /// Quadratic Bezier
        /// </summary>
        BezierQuad,
        /// <summary>
        /// Cubic Bezier
        /// </summary>
        BezierCubic,
        /// <summary>
        /// Catmull ROM
        /// </summary>
        CatmullRom
    }
    #endregion

    /// <summary>
    /// Component that wraps the handling of spline curves using its Transform hierarchy.
    /// </summary>
    [ExecuteInEditMode]
    public class SplineComponent : MonoBehaviour {

        /// <summary>
        /// List of spline handles
        /// </summary>
        public List<SplineComponentHandle> handles { get { return m_handles==null ? (m_handles = new List<SplineComponentHandle>()) : m_handles; } }
        [HideInInspector][SerializeField]private List<SplineComponentHandle> m_handles;

        /// <summary>
        /// Spline Typé
        /// </summary>
        public SplineTypeFlag type {
            get { return m_type; }
            set { 
                curve.Clear();
                m_type = value;                                 
                RefreshHierarchy(true); 
                Refresh(true); 
            }
        }
        [SerializeField]
        SplineTypeFlag m_type = SplineTypeFlag.CatmullRom;

        /// <summary>
        /// Flag that tells if the spline must be closed (loop) or not.
        /// </summary>
        public bool closed {
            get { return m_closed; }
            set {
                linear.closed       = 
                bezier_cubic.closed =
                bezier_quad.closed  =
                catmull_rom.closed  = value;
                m_closed=value;
                RefreshHierarchy(true); 
                Refresh(true); 
            }
        }
        [HideInInspector][SerializeField] 
        private bool m_closed;

        /// <summary>
        /// Reference to the currently active curve.
        /// </summary>
        public Spline<Vector4> curve {
            get {
                if(linear       == null) linear       = new LinearTransform();
                if(bezier_cubic == null) bezier_cubic = new BezierCubicTransform();
                if(bezier_quad  == null) bezier_quad  = new BezierQuadTransform();
                if(catmull_rom  == null) catmull_rom  = new CatmullRomTransform();
                switch(type) {
                    case SplineTypeFlag.Linear:      return linear;
                    case SplineTypeFlag.CatmullRom:  return catmull_rom;
                    case SplineTypeFlag.BezierQuad:  return bezier_quad;
                    case SplineTypeFlag.BezierCubic: return bezier_cubic;
                }
                return linear;
            }
        }
        [HideInInspector][SerializeField] internal BezierCubicTransform bezier_cubic;
        [HideInInspector][SerializeField] internal BezierQuadTransform  bezier_quad;
        [HideInInspector][SerializeField] internal CatmullRomTransform  catmull_rom;
        [HideInInspector][SerializeField] internal LinearTransform      linear;

        /// <summary>
        /// Internals
        /// </summary>        
        internal List<SplineComponentHandle> handle_gc { get { return m_handle_gc==null ? (m_handle_gc = new List<SplineComponentHandle>()) : m_handle_gc; } }
        private List<SplineComponentHandle> m_handle_gc;
        private bool m_dirty;
        private bool m_dirty_hierarchy;

        #if UNITY_EDITOR
        public bool selected { 
            get {
                for(int i=0;i<handles.Count;i++) {
                    if(!handles[i]) continue;
                    if(handles[i].selected)         return true;
                    if(handles[i].childrenSelected) return true;
                }
                return m_selected;
            } 
            set { m_selected = value; }
        }
        private bool m_selected;

        [HideInInspector][SerializeField]internal int guide_mode;
        [HideInInspector][SerializeField]internal float simulation_index;
        [HideInInspector][SerializeField]internal float simulation_speed;
        [HideInInspector][SerializeField]internal float simulation_time;
        [HideInInspector][SerializeField]internal bool  simulation_enabled;
        [HideInInspector][SerializeField]internal bool  simulation_use_deriv;
        [HideInInspector][SerializeField]internal float      simulation_w;
        [HideInInspector][SerializeField]internal Vector3    simulation_pos;
        [HideInInspector][SerializeField]internal Quaternion simulation_rot;
        [HideInInspector][SerializeField]internal Vector3    simulation_scl;
        #endif

        /// <summary>
        /// CTOR.
        /// </summary>
        internal void Start() { RefreshHierarchy(); }

        /// <summary>
        /// CTOR
        /// </summary>
        internal void OnEnable() { RefreshHierarchy(); }

        #region Add|Remove

        public SplineComponentHandle Create(int p_index,Vector3 p_position) {
            GameObject cp = new GameObject();
            cp.transform.parent        = transform;
            cp.transform.localPosition = p_position;       
            if(p_index<0) {
                cp.transform.SetAsFirstSibling();
            } 
            else {
                cp.transform.SetSiblingIndex(p_index);
            }                        
            SplineComponentHandle cp_h = cp.AddComponent<SplineComponentHandle>();
            cp_h.AssertTangents();
            cp_h.ResetTangents();
            #if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(cp,"Spline Handle Create");
            #endif
            return cp_h;
        }

        public SplineComponentHandle Create(int p_index) {
            Vector3 pos = new Vector3(0f,0f,1f);
            SplineComponentHandle prev = handles.Count<=0 ? null : handles[handles.Count-1];
            if(prev) {
                pos = prev.transform.localPosition + prev.transform.forward * 0.3f;
            }                
            return Create(p_index,pos);
        }

        #endregion

        #region Evaluate
        /// <summary>
        /// Given a control point float index, evaluate the spline's position, rotation and scale of the transform handles.
        /// </summary>
        /// <param name="p_control_index">Control Point Index</param>
        /// <param name="p_position">Output Position</param>
        /// <param name="p_rotation">Output Rotation</param>
        /// <param name="p_scale">Output Scale</param>
        public void Evaluate(float p_control_index,out Vector3 p_position,out Quaternion p_rotation,out Vector3 p_scale) {
            if(handles.Count<=0) {
                p_position = transform.position;
                p_rotation = transform.localRotation;
                p_scale    = transform.localScale;
                return;
            }            
            p_position = handles[0] ? handles[0].transform.position       : Vector3.zero;
            p_rotation = handles[0] ? handles[0].transform.localRotation  : Quaternion.identity;
            p_scale    = handles[0] ? handles[0].transform.localScale     : Vector3.one;
            if(handles.Count<=1) {                
                return;
            }
            Vector4 sv  = curve.Evaluate(p_control_index);
            int     cc  = handles.Count;
            float   sw  = EvaluateBlend(p_control_index);            
            float   hr  = sw - Mathf.Floor(sw);
            int     i0  = Mathf.Clamp(((int)sw)%cc,0,cc-1);
            int     i1  = Mathf.Clamp((i0+1)%cc,0,cc-1);
            Transform ht0 = handles[i0] ? handles[i0].transform : null;
            Transform ht1 = handles[i1] ? handles[i1].transform : null;
            if(!ht0) return;
            if(!ht1) return;
            Vector3    pos = transform.TransformPoint(sv.ToVector3());            
            Quaternion rot = Quaternion.Lerp(ht0.localRotation,ht1.localRotation,hr);
            Vector3    vz  = Vector3.Lerp(ht0.forward,ht1.forward,hr);
            Vector3    vy  = rot * Vector3.up;
            if(vz.sqrMagnitude>=0.5f) if(vy.sqrMagnitude>=0.5f) rot = Quaternion.LookRotation(vz,vy);
            Vector3    scl = Vector3.Lerp(ht0.localScale,ht1.localScale,hr);
            p_position = pos;   
            p_rotation = rot;            
            p_scale    = scl;
        }
        /// <summary>
        /// Given a control point float index, evaluate the spline's position, rotation and scale of the transform handles.
        /// </summary>
        /// <param name="p_control_index">Control Point Index</param>
        /// <param name="p_position">Output Position</param>
        /// <param name="p_rotation">Output Rotation</param>        
        public void Evaluate(float p_control_index,out Vector3 p_position,out Quaternion p_rotation) { Vector3 scl; Evaluate(p_control_index,out p_position,out p_rotation,out scl); }
        /// <summary>
        /// Given a control point float index, evaluate the spline's position, rotation and scale of the transform handles.
        /// </summary>
        /// <param name="p_control_index">Control Point Index</param>
        /// <param name="p_position">Output Position</param>        
        public void Evaluate(float p_control_index,out Vector3 p_position) { Vector3 scl; Quaternion rot; Evaluate(p_control_index,out p_position,out rot,out scl); }

        /// <summary>
        /// Evaluates the blending factor at the provided control point index.
        /// </summary>
        /// <param name="p_control_index"></param>
        /// <returns></returns>
        public float EvaluateBlend(float p_control_index) {                        
            return curve.Evaluate(p_control_index).w;
        }

        #endregion

        #region void Refresh
        /// <summary>
        /// Refreshes the spline internal structure
        /// </summary>
        /// <param name="p_force"></param>
        public void Refresh(bool p_force=false) { m_dirty=true; if(p_force) OnRefresh(); }
        /// <summary>
        /// Apply Refresh
        /// </summary>
        protected void OnRefresh() {
            m_dirty=false;
            //Collect any pending handle destroy
            OnHandleCollect();            
            //Refresh Spline
            curve.Clear();
            int tpp = curve.tangentsPerPosition;
            int hc  = handles.Count;            
            for(int i=0;i<handles.Count;i++) {
                int i0 = i%handles.Count;
                SplineComponentHandle it0 = handles[i0];
                SplineComponentHandle it1 = handles[(i0+1)%handles.Count];                
                if(it0.children.Count<tpp) continue;                
                if(it1.children.Count<tpp) continue;
                Vector4 c_pos  = it0.transform.localPosition;
                Vector4 t0_pos = Vector3.zero;
                Vector4 t1_pos = Vector3.zero;
                //Assign Tangents
                switch(tpp) {
                    case 1: t0_pos = it0.children[1].transform.localPosition; break;
                    case 2: {
                        t0_pos = it0.children[1].transform.localPosition;
                        t1_pos = it1.children[0].transform.localPosition;
                    }
                    break;                    
                }
                //If tangent off, collapse all in the control point
                if(it0.tangentMode == SplineTagentMode.Off) { t0_pos = t1_pos = c_pos; }                
                //Store the the tangent blending for the spline to interpolate
                t0_pos.w = it0.blend;
                t1_pos.w = it0.blend;
                //Assign the spline's control and tangents
                switch(tpp) {
                    case 1:  curve.Add(c_pos,t0_pos);        break;
                    case 2:  curve.Add(c_pos,t0_pos,t1_pos); break;
                    default: curve.Add(c_pos);               break;
                }
            }
            #if UNITY_EDITOR
            //Only generates the cached points for editor purposes
            GenerateSplinePoints();
            #endif
        }
        #endregion

        #region void RefreshHierarchy
        /// <summary>
        /// Refreshes changes in hierarchy like handles add/remove
        /// </summary>
        /// <param name="p_force"></param>
        public void RefreshHierarchy(bool p_force=false) { m_dirty_hierarchy=true; if(p_force) OnRefreshHierarchy(); }
        /// <summary>
        /// Apply Refresh
        /// </summary>
        internal void OnRefreshHierarchy() {
            if(!m_dirty_hierarchy) return;
            m_dirty_hierarchy=false;
            //Collect any pending handle destroy
            OnHandleCollect();
            //Prepare hierarchy refresh
            int cc = transform.childCount;
            handles.Clear();
            int tpp = curve.tangentsPerPosition;
            //Collect all handles+Children
            for(int i=0;i<cc;i++) {
                Transform it = transform.GetChild(i);
                SplineComponentHandle it_h = it.GetComponent<SplineComponentHandle>();
                if(!it_h) continue;
                SplineComponentHandle hp = it_h.parent;
                //Tangent of a parent control point
                if(hp) {                    
                    if(!hp.children.Contains(it_h)) {                        
                        it_h.index = hp.children.Count;                        
                        hp.children.Add(it_h);
                    }        
                    bool is_active = false;
                    switch(tpp) {
                        case 0: break;
                        case 1: is_active = it_h.index==1; break;
                        case 2: is_active = true; break;
                    }
                    it_h.gameObject.SetActive(is_active);
                    it_h.name  = $"c{hp.index}t{it_h.index}";                       
                }
                //Control point
                else {
                    it_h.index = handles.Count;     
                    it_h.name  = $"c{it_h.index}";                    
                    handles.Add(it_h);
                }
            }
            //Re-order control vs tangents
            cc = handles.Count;
            int k=0;
            int si = handles.Count;
            for(int i = 0; i < cc; i++) {
                SplineComponentHandle it = handles[i];                
                it.transform.SetSiblingIndex(i);
                if(it.transform.parent != transform) it.transform.parent = transform;                
                for(int j=0;j<it.children.Count;j++) {
                    SplineComponentHandle it_c = it.children[j];
                    if(it_c.transform.parent != transform) it_c.transform.parent = transform;
                    it_c.transform.SetSiblingIndex(k+si);
                    k++;
                }                                
            }
            //Refreshes the spline
            Refresh();
        }
        #endregion
        
        #region Handles
        internal void OnHandleHierarchyChange(SplineComponentHandle p_handle) { RefreshHierarchy(); }
        internal void OnHandleMove  (SplineComponentHandle p_handle) { Refresh(); }
        internal void OnHandleRotate(SplineComponentHandle p_handle) { Refresh(); }
        internal void OnHandleScale (SplineComponentHandle p_handle) { Refresh(); }
        internal void OnHandleDestroy(SplineComponentHandle p_handle) {   
            //Add the handle and its children for collection/destroy
            if(p_handle.parent) handle_gc.Add(p_handle.parent); else { handle_gc.Add(p_handle); handle_gc.AddRange(p_handle.children); }
            RefreshHierarchy();
        }        
        internal void OnHandleCollect() {
            //Process GC            
            for(int i=0;i<handle_gc.Count;i++) if(handle_gc[i]) handle_gc[i].Destroy();
            handle_gc.Clear();
        }
        #endregion

        /// <summary>
        /// Updates the spline structure and detect changes
        /// </summary>
        internal void LateUpdate() {
            for(int i=0;i<handles.Count;i++) if(handles[i])handles[i].OnHandleUpdate();
            if(transform.hasChanged) { transform.hasChanged=false; Refresh(); }
            if(m_dirty_hierarchy) OnRefreshHierarchy();
            if(m_dirty) OnRefresh();
        }

        #if UNITY_EDITOR

        internal void SetEditorUpdateEnabled(bool p_flag) {
            if(p_flag==m_editor_update_enabled) return;
            m_editor_update_enabled = p_flag;
            if( m_editor_update_enabled) EditorApplication.update += EditorUpdateSimulation;
            if(!m_editor_update_enabled) EditorApplication.update -= EditorUpdateSimulation;
        }
        private bool m_editor_update_enabled;

        internal void EditorUpdateSimulation() {
            if(!simulation_enabled) return;
            if(!selected) return;
            int hc = closed ? handles.Count : handles.Count-1;
            if(hc<=0) { simulation_enabled = false; return; }

            float dt = Mathf.Min(0.1f,Time.realtimeSinceStartup - m_last_time);
            if(dt<=0.0f) return;

            m_last_time = Time.realtimeSinceStartup;

            if(float.IsNaN(simulation_index))simulation_index=0f;
            Vector3 pos;
            Quaternion rot;
            Vector3 scl;
            Evaluate(simulation_index,out pos, out rot, out scl);
            Vector3 drv = curve.Derivative(simulation_index);

            if(simulation_use_deriv) {
                Vector3 vy = rot * Vector3.up;
                rot = Quaternion.LookRotation(drv,vy);
            }
            
            simulation_w   = EvaluateBlend(simulation_index);
            simulation_pos = pos;
            simulation_rot = rot;
            simulation_scl = scl;
            
            float di = dt;

            /*
            float dpos = (pos-m_last_pos).magnitude/dt;
            avgspd.Add(dpos);
            if(avgspd.Count>8)avgspd.RemoveAt(0);
            float aspd = 0f;
            for(int i=0;i<avgspd.Count;i++) aspd += avgspd[i];
            float ic = avgspd.Count<=0 ? 0f : (1f/(float)avgspd.Count);
            aspd *= ic;
            Debug.Log($">>>> {(Mathf.Round(aspd*100f)/100f).ToString("0.00")} m/s {dt}");
            m_last_pos = pos;
            //*/

            simulation_time += dt;

            float drv_m = drv.magnitude;
            di = (drv_m<=0.0001f) ? 0f :  (di/drv_m);
            simulation_index += di * 1f * simulation_speed;

            if(simulation_index >= hc) {                    
                float clen = curve.length;
                //Debug.Log($"Simulation | {simulation_time.ToString("0.0")}s | {clen.ToString("0.00")}m | {(clen/simulation_time).ToString("0.00")}m/s");
                simulation_time   = 0f;
            }

            if(simulation_index >= hc) simulation_index = 0f;
            if(simulation_index <  0f) simulation_index = hc;

        }
        private float m_last_time;
        private Vector3 m_last_pos;
        private List<float> avgspd = new List<float>();
        #endif

        #region Gizmos

        #region void GenerateSplinePoints
        internal void GenerateSplinePoints() {            
            float cc = curve.closed ? curve.count : (curve.count-1);
            int total_len = Mathf.Max(1,(int)(cc/3))*200;            
            if(m_spline_samples.Length != total_len) m_spline_samples = new Vector4[total_len];
            if(m_samples_spos.Length   != total_len) m_samples_spos   = new Vector3[total_len];            
            curve.GetSamples(m_spline_samples);
            int hc = closed ? curve.count : curve.count-1;
            //Move to WorldSpace and correct blend index
            for(int i=0;i<m_spline_samples.Length;i++) {                
                Vector4 sp = m_spline_samples[i];
                float   sw = sp.w;
                sp = transform.TransformPoint(sp);
                sp.w = sw;
                m_spline_samples[i] = sp;
            }
            //Store it
            for(int i=0;i<m_samples_spos.Length;i++) m_samples_spos[i] = m_spline_samples[i];

            total_len = handles.Count<=1 ? 0 : Mathf.Max((int)cc,total_len/15);

            if(total_len != m_samples_tpos.Length) m_samples_tpos = new Vector4[total_len];
            if(total_len != m_samples_tx  .Length) m_samples_tx   = new Vector3[total_len];
            if(total_len != m_samples_ty  .Length) m_samples_ty   = new Vector3[total_len];
            if(total_len != m_samples_tz  .Length) m_samples_tz   = new Vector3[total_len];
            if(total_len != m_samples_scl .Length) m_samples_scl  = new Vector3[total_len];

            if(hc>=1)
            for(int i=0;i<total_len;i++) {
                float r   = (float)i/(float)(total_len-1);
                float cr  = r * (float)hc;                
                float sw  = EvaluateBlend(cr);
                Vector3    pos;
                Quaternion rot;
                Vector3    scl;

                Evaluate(cr,out pos,out rot,out scl);

                Vector3 tx = rot * Vector3.right;
                Vector3 ty = rot * Vector3.up;
                Vector3 tz = rot * Vector3.forward;

                m_samples_tpos[i]   = pos; 
                m_samples_tpos[i].w = sw;
                m_samples_tx  [i] = tx;
                m_samples_ty  [i] = ty;
                m_samples_tz  [i] = tz;
                m_samples_scl [i] = scl;
            }

        }
        [SerializeField] internal Vector4[] m_spline_samples   = new Vector4[0];
        [SerializeField] internal Vector4[] m_samples_blending = new Vector4[0];
        [SerializeField] internal Vector3[] m_samples_spos     = new Vector3[0];
        [SerializeField] internal Vector4[] m_samples_tpos     = new Vector4[0];
        [SerializeField] internal Vector3[] m_samples_tx       = new Vector3[0];
        [SerializeField] internal Vector3[] m_samples_ty       = new Vector3[0];
        [SerializeField] internal Vector3[] m_samples_tz       = new Vector3[0];
        [SerializeField] internal Vector3[] m_samples_scl      = new Vector3[0];
        #endregion

        #if UNITY_EDITOR         
        internal void OnDrawGizmos() {
            SplineComponentInspector.SplineSceneGUI(this);            
        }
        #endif

        #endregion

    }

}
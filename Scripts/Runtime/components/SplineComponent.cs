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

    #region SplineComponent Editor

    #if UNITY_EDITOR

    #region class SplineGUIConsts
    /// <summary>
    /// Helper class for styling the spline inspector.
    /// </summary>
    internal class SplineGUIConsts {

        static internal float     ControlPointBaseSize          = 0.22f;
        static internal float     ControlPointSelectSize        = 0.9f;
        static internal float     ControlPointDefaultSize       = 0.45f;
        static internal Color     ControlPointSelectColor       = Color.yellow;
        static internal Color     ControlPointDefaultColor      = Colorf.RGBAToColor(0xffffffaa);
        static internal Color     ChildLineColor                = Colorf.RGBAToColor(0x00aaffaa);
        static internal float     CurveSelectSize               = 2f;
        static internal float     CurveDefaultSize              = 1f;
        static internal Color     CurveSelectColor              = Colorf.RGBAToColor(0xff6600aa);
        static internal Color     CurveDefaultColor             = Colorf.RGBAToColor(0xffff00aa);
        static internal Color     CurvePreviewModeColor         = Colorf.RGBAToColor(0xffffff11);
        static internal GUIStyle  ControlPointLabelStyle        = new GUIStyle(EditorStyles.whiteLargeLabel);
        static internal GUIStyle  LayoutBoxTitleStyle           = new GUIStyle(EditorStyles.whiteBoldLabel);
        static internal GUIStyle  LayoutBoxLengthStyle          = new GUIStyle(EditorStyles.boldLabel);
        static internal Color     ControlPointLabelDefaultColor = new Color(1f,1f,1f,0.5f);
        static internal Color     ControlPointLabelSelectColor  = new Color(1f,1f,1f,1.0f);
        static internal Color     ControlPointPreviewColor      = Colorf.RGBAToColor(0xffcc00ff);
        static internal Texture2D CurveAATex                    = Texture2D.whiteTexture;

        static internal Color     GizmosAxisXColor              = Colorf.RGBAToColor(0xff4444ff);
        static internal Color     GizmosAxisYColor              = Colorf.RGBAToColor(0x44ff44ff);
        static internal Color     GizmosAxisZColor              = Colorf.RGBAToColor(0x4444ffff);
        static internal float     GizmosAxisSize                = 0.2f;
        static internal Color     GizmosBlendFrom               = Colorf.RGBAToColor(0xff0000ff);
        static internal Color     GizmosBlendTo                 = Colorf.RGBAToColor(0x00ff00ff);
        static internal Color     GizmosScaleGuidePColor        = new Color(1.0f,1.0f,1.0f,0.25f);
        static internal Color     GizmosScaleGuideNColor        = new Color(0.5f,0.5f,0.5f,0.25f);
        static internal float     GizmosScaleGuideSize          = 0.15f;

        static internal string[]  SplineTypeToolbarItems        = new string[] { "Linear", "Bezier2", "Bezier3", "Catmull" };
        static internal string[]  TangentModeToolbarItems       = new string[] { "Off", "Free", "Mirror", "Align" };
        static internal string[]  PreviewModeToolbarItems       = new string[] { "Off", "Axis", "Scale","Blending" };
        static internal string[]  OrientModeToolbarItems        = new string[] { "Nodes", "Path" };

        internal const string    CmdControlPointDropStart      = "control-point-drop@start";
        internal const string    CmdControlPointDropUpdate     = "control-point-drop@update";
        internal const string    CmdControlPointDropStop       = "control-point-drop@stop";
        internal const string    CmdControlPointDropApply      = "control-point-drop@apply";        
        internal const string    CmdControlPointDropCancel     = "control-point-drop@cancel";
        internal const KeyCode   CmdPointDropKey               = KeyCode.LeftControl;

        static SplineGUIConsts() {
            ControlPointLabelStyle.fontSize      = 10;
            ControlPointLabelStyle.contentOffset = new Vector2(10f,-7f);
            LayoutBoxTitleStyle.normal.textColor = Color.white;
            LayoutBoxLengthStyle.alignment = TextAnchor.MiddleRight;
            LayoutBoxLengthStyle.fontSize  = 12;
            LayoutBoxLengthStyle.normal.textColor = Colorf.RGBAToColor(0xffff44ff);
        }

    }
    #endregion

    #region class SplineComponentInspector
    [CustomEditor(typeof(SplineComponent))]
    public class SplineComponentInspector : Editor {

        #region static

        #region void SplineSceneGUI
        /// <summary>
        /// Renders the spline curve in the scene GUI loop
        /// </summary>        
        static internal void SplineSceneGUI(SplineComponent p_spline) {

            //if(Event.current.type != EventType.Repaint) return;

            SplineComponent sc = p_spline;
            bool spline_selected = sc.selected;
            
            int spline_guide_mode = spline_selected ? sc.guide_mode : 0;

            GUIStyle stl = new GUIStyle(EditorStyles.whiteMiniLabel);
            stl.fontSize = 8;

            #region SplineRender
            //Spline Rendering
            switch(spline_guide_mode) {
                //Blending Factor
                case 3: {                    
                    bool f=true;                    
                    for(int i=1;i<sc.m_spline_samples.Length;i++) {
                        Vector4 p0 = sc.m_spline_samples[i-1];
                        Vector4 p1 = sc.m_spline_samples[i  ];
                        float r = Mathf.Clamp01(p0.w - Mathf.Floor(p0.w));                                                
                        Handles.color = Color.Lerp(SplineGUIConsts.GizmosBlendFrom,SplineGUIConsts.GizmosBlendTo,r);
                        Handles.DrawLine(p0,p1);
                        f = !f;
                    }                    
                }
                break;
                //Regular Gizmo
                default: {                    
                    //Regular curve rendering
                    Handles.color    = spline_selected ? SplineGUIConsts.CurveSelectColor : SplineGUIConsts.CurveDefaultColor;
                    float curve_size = spline_selected ? SplineGUIConsts.CurveSelectSize  : SplineGUIConsts.CurveDefaultSize;
                    //In case of active guide modes, draw a lighter version
                    if(spline_selected)
                    if(sc.guide_mode!=0) {
                        Handles.color = SplineGUIConsts.CurvePreviewModeColor;
                        curve_size    = SplineGUIConsts.CurveDefaultSize;
                    }
                    //Draws the spline
                    Handles.DrawAAPolyLine(SplineGUIConsts.CurveAATex,curve_size,sc.m_samples_spos);                    
                }
                break;
            }
            #endregion

            #region Control Point Labels
            SplineGUIConsts.ControlPointLabelStyle.normal.textColor = spline_selected ? SplineGUIConsts.ControlPointLabelSelectColor : SplineGUIConsts.ControlPointLabelDefaultColor;
            for(int i=0;i<sc.handles.Count;i++) {
                SplineComponentHandle it = sc.handles[i];
                if(!it) continue;
                string   it_idx  = it.index.ToString("0");//+"|"+sc.curve.GetPosition(i).w.ToString("0.000");                
                Vector3  it_pos  = sc.transform.TransformPoint(it.transform.localPosition);                
                Handles.Label(it_pos,it_idx,SplineGUIConsts.ControlPointLabelStyle);
            }
            #endregion

            #region Guides Rendering
            switch(spline_guide_mode) {
                //Off
                case 0: break;
                //On
                default: {
                    for(int i=0;i<sc.m_samples_tpos.Length;i++) {
                        Vector3 tpos = sc.m_samples_tpos[i];
                        Vector3 tx   = sc.m_samples_tx[i];
                        Vector3 ty   = sc.m_samples_ty[i];
                        Vector3 tz   = sc.m_samples_tz[i];
                        Vector3 scl  = sc.m_samples_scl[i];
                        bool scl_inv = (scl.x<0f)||(scl.y<0f)||(scl.z<0f);
                        float tw = sc.m_samples_tpos[i].w;
                        float hs = HandleUtility.GetHandleSize(tpos);

                        switch(sc.guide_mode) {
                            //Transform
                            case 1: {                                
                                Handles.color = SplineGUIConsts.GizmosAxisXColor; Handles.DrawLine(tpos,tpos+tx*hs*SplineGUIConsts.GizmosAxisSize);
                                Handles.color = SplineGUIConsts.GizmosAxisYColor; Handles.DrawLine(tpos,tpos+ty*hs*SplineGUIConsts.GizmosAxisSize);
                                Handles.color = SplineGUIConsts.GizmosAxisZColor; Handles.DrawLine(tpos,tpos+tz*hs*SplineGUIConsts.GizmosAxisSize);
                            }
                            break;
                            //Scale
                            case 2: {                                
                                Handles.color = scl_inv ? SplineGUIConsts.GizmosScaleGuideNColor : SplineGUIConsts.GizmosScaleGuidePColor; 
                                Handles.SphereHandleCap(0,tpos,Quaternion.identity,scl.magnitude*hs*SplineGUIConsts.GizmosScaleGuideSize,EventType.Repaint);
                            }
                            break;
                            //Blending
                            case 3: {
                                tw = tw - Mathf.Floor(tw);
                                Handles.Label(tpos,tw.ToString("0.0"),stl);
                            }
                            break;
                        }                        
                    }
                }
                break;
            }
            #endregion

            #region Simulation Rendering
            if(spline_selected)
            if(sc.simulation_enabled) {
                Vector3 v;
                float      s_w   = sc.simulation_w;
                Vector3    s_pos = sc.simulation_pos;
                Quaternion s_rot = sc.simulation_rot;
                Vector3    s_scl = sc.simulation_scl;
                float hs = HandleUtility.GetHandleSize(s_pos);
                v = sc.simulation_rot * Vector3.right;  Handles.color = Colorf.RGBToColor(0xff4444); Handles.DrawAAPolyLine(4f,s_pos,s_pos+v*hs*0.33f);
                v = sc.simulation_rot * Vector3.up;     Handles.color = Colorf.RGBToColor(0x44ff44); Handles.DrawAAPolyLine(4f,s_pos,s_pos+v*hs*0.33f);
                v = sc.simulation_rot * Vector3.forward;Handles.color = Colorf.RGBToColor(0x4444ff); Handles.DrawAAPolyLine(4f,s_pos,s_pos+v*hs*0.33f);                
                Handles.color = new Color(1f,1f,1f,0.05f);
                Handles.SphereHandleCap(0,s_pos,Quaternion.identity,s_scl.magnitude*0.05f,EventType.Repaint);
                Handles.Label(s_pos,s_w.ToString("0.00"),stl);
                HandleUtility.Repaint();
            }
            #endregion

            #region Spline Mouse/Keyboard input
            //Create dictionary to track up/down transition of input (solves key repeat issue)
            if(m_kdown_tb == null) {
                m_kdown_tb = new Dictionary<KeyCode,bool>();
                m_kdown_tb[KeyCode.None]       = false;
                m_kdown_tb[SplineGUIConsts.CmdPointDropKey]       = false;                                
            }
            //Filter out non expected keycodes
            KeyCode kc  = Event.current.keyCode;            
            switch(kc) {
                case SplineGUIConsts.CmdPointDropKey: break;                
                default: kc = KeyCode.None; break;
            }
            //GUI Events Flags
            EventType evt = Event.current.type;
            bool k_down   = evt == EventType.KeyDown;
            bool k_up     = evt == EventType.KeyUp;
            bool m_down   = evt == EventType.MouseDown;
            bool m_up     = evt == EventType.MouseUp;                     
            string cmd    = spline_cmd;
            //Prevent repeat
            if(k_down) if(m_kdown_tb[kc]) k_down=false;
            //Store up kdown cache
            if(k_down || k_up) { m_kdown_tb[kc] = k_down ? true : (k_up ? false : m_kdown_tb[kc]); }
            //Shortcuts
            int  h_count = sc.handles.Count;
            bool h_empty = h_count<=0;
            //Input State Machine
            switch(cmd) {

                #region CmdControlPointDropStart
                case SplineGUIConsts.CmdControlPointDropStart: {  
                    
                    //Switch to point drop update loop
                    cmd = SplineGUIConsts.CmdControlPointDropUpdate;
                    //Detect if preview is close to first/last segment points (means its either a start/tail insert)
                    Vector3[] spline_pos = h_empty ? new Vector3[] { sc.transform.position, sc.transform.position } : sc.m_samples_spos;
                    spline_control_point_new     = HandleUtility.ClosestPointToPolyLine(spline_pos);
                    spline_control_point_new_free_idx = -1;
                    spline_control_point_new_idx      =  0;         
                    bool is_extreme = false;
                    if(!sc.closed) {
                        float d_first = Vector3.Distance(spline_control_point_new,spline_pos[0]);
                        float d_last  = Vector3.Distance(spline_control_point_new,spline_pos[spline_pos.Length-1]);                        
                        if(Mathf.Abs(d_first)<=0.05f) { spline_control_point_new_free_idx = 0;         is_extreme=true; }
                        if(Mathf.Abs(d_last )<=0.05f) { spline_control_point_new_free_idx = h_count-1; is_extreme=true; }                        
                    }
                    if(h_empty) spline_control_point_new_free_idx=0;

                    //Check if insertion trigger was too far and ignore to not conflic w/ other GUI interactions
                    Ray gui_ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                    
                    float control_point_trigger_distance = HandleUtility.DistancePointLine(spline_control_point_new,gui_ray.origin,gui_ray.GetPoint(4000f));
                    float distance_bias = is_extreme ? 15f : 2f;
                    if(control_point_trigger_distance>distance_bias) cmd="";
                }
                break;
                #endregion

                #region CmdControlPointDropUpdate
                case SplineGUIConsts.CmdControlPointDropUpdate: {
                    //First check if ongoing drag/click is too close to handle points and ignore adding points
                    bool handle_too_close=false;
                    for(int i=0;i<sc.handles.Count;i++) {
                        Transform it = sc.handles[i] ? sc.handles[i].transform : null;
                        if(!it) continue;
                        float d = Vector3.Distance(spline_control_point_new,it.position);
                        if(d<0.15f) { handle_too_close=true; break; }
                    }
                    //If PointDrop key is up stop loop
                    if(k_up) { if(kc == SplineGUIConsts.CmdPointDropKey) cmd = SplineGUIConsts.CmdControlPointDropStop; }
                    //If mouse down place the point unless too close of handle
                    if(!handle_too_close) if(m_down) {  cmd = SplineGUIConsts.CmdControlPointDropApply; Event.current.Use();  break;  }
                    //Ignore non rendering commands
                    if(evt != EventType.Repaint) break;                    
                    //Fetch current spline line segments
                    Vector3[] spline_pos = h_count<=0 ? new Vector3[] { sc.transform.position, sc.transform.position } : sc.m_samples_spos;
                    //Fetch closest 
                    spline_control_point_new     = HandleUtility.ClosestPointToPolyLine(spline_pos);
                    spline_control_point_new_idx = 0;
                    //Flag that tells if new point is free (start or tail)
                    bool is_free = spline_control_point_new_free_idx>=0;
                    int  h_idx   = Mathf.Clamp(spline_control_point_new_free_idx,0,h_count-1);
                    //If free position the preview using a raycast to the first/last point up vector plane
                    if(is_free) {
                        Transform h          = h_empty ? sc.transform : sc.handles[h_idx].transform;
                        Ray       gui_ray    = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                        Plane     h_plane    = new Plane(h.up, h.position);
                        float     h_gui_dist = 0f;
                        if(h_plane.Raycast(gui_ray,out h_gui_dist)) { spline_control_point_new = gui_ray.GetPoint(h_gui_dist); }
                        Handles.color = SplineGUIConsts.ControlPointPreviewColor;
                        Handles.DrawAAPolyLine(1f,h.position,spline_control_point_new);
                        if(h_idx==0)                  spline_control_point_new_idx = -1;
                        if(h_idx==h_count-1) spline_control_point_new_idx = h_count+1;
                    }
                    //Render the preview
                    float hs = HandleUtility.GetHandleSize(spline_control_point_new);
                    Color preview_color = SplineGUIConsts.ControlPointPreviewColor;
                    preview_color.a = handle_too_close ? 0.1f : 1f;                    
                    Handles.color = preview_color;
                    Handles.CubeHandleCap(0,spline_control_point_new,Quaternion.identity,hs*0.1f,EventType.Repaint);
                }
                break;
                #endregion

                #region CmdControlPointDropApply
                case SplineGUIConsts.CmdControlPointDropApply: {    
                    //After mouse down iterate all spline samples to find the closest one
                    Vector4[] spline_samples = sc.m_spline_samples;
                    int       c_idx  = 0;                    
                    float     c_dist = Vector3.Distance(spline_control_point_new,spline_samples[0]);
                    for(int i=1;i<spline_samples.Length;i++) {
                        Vector4 it_pos = spline_samples[i];
                        float d = Vector3.Distance(it_pos,spline_control_point_new);
                        if(d>=c_dist) continue;
                        c_dist = d;
                        c_idx  = i;
                    }
                    //Store closest sample and its index
                    Vector4 c_sample = spline_samples[c_idx];                    
                    Vector3 s_pos    = c_sample;
                    int     s_idx    = Mathf.CeilToInt(c_sample.w);
                    //If currently in 'free-point' mode use the free point information
                    if(spline_control_point_new_idx < 0)          { s_idx = -1;        s_pos = spline_control_point_new; spline_control_point_new_free_idx = 0;       }
                    if(spline_control_point_new_idx >= h_count+1) { s_idx = h_count+1; s_pos = spline_control_point_new; spline_control_point_new_free_idx = h_count; }
                    //Record operation and create point
                    Undo.RecordObject(sc,"Insert Control Point");
                    SplineComponentHandle new_handle = sc.Create(s_idx,sc.transform.InverseTransformPoint(s_pos));                    
                    //Loop back to update and wait for another insertion
                    cmd = SplineGUIConsts.CmdControlPointDropUpdate;
                }
                break;
                #endregion

                #region CmdControlPointDropStop
                case SplineGUIConsts.CmdControlPointDropStop: {                    
                    //If drop stop, just clear cmd state
                    cmd = "";
                }
                break;
                #endregion

                #region default
                default: {
                    //Check current input
                    switch(kc) {
                        case SplineGUIConsts.CmdPointDropKey: {
                            //Start Control Point Drop Op
                            if(k_down) cmd = SplineGUIConsts.CmdControlPointDropStart;
                            //Stop Control Point Drop Op
                            if(k_up)   cmd = SplineGUIConsts.CmdControlPointDropStop;
                        }
                        break;
                    }
                }
                break;
                #endregion

            }
            #endregion

            //Active input command for next loop
            spline_cmd = cmd;
            //If there is any ongoing input keep repainting
            if(!string.IsNullOrEmpty(cmd)) { SceneView.RepaintAll(); }

        }                
        static int      spline_control_point_new_idx;
        static int      spline_control_point_new_free_idx;        
        static Vector3  spline_control_point_new;
        static internal string spline_cmd;
        static private Dictionary<KeyCode,bool> m_kdown_tb;
        #endregion

        #region void SplineInspector
        /// <summary>
        /// Renders the Spline Inspector
        /// </summary>        
        static internal void SplineInspector(SplineComponent p_spline) {

            SplineComponent sc = p_spline;
            if(!sc) return;

            int   vi;
            bool  vb;
            float vf;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Spline",SplineGUIConsts.LayoutBoxTitleStyle);            
            float  spline_len  = sc.curve.length;
            string spline_unit = "m";
            if(spline_len<1f) {
                spline_len*=100f;
                spline_unit="cm";
            }
            GUILayout.Label($"{spline_len.ToString("0.00")}{spline_unit}",SplineGUIConsts.LayoutBoxLengthStyle);
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5f);

            vi = GUILayout.Toolbar((int)sc.type,SplineGUIConsts.SplineTypeToolbarItems,GUILayout.ExpandWidth(true));
            if(vi != (int)sc.type) {
                Undo.RecordObject(sc,"Change Spline Type");
                sc.type = (SplineTypeFlag)vi;                
            }

            vb = GUILayout.Toggle(sc.closed,"Closed");
            if(vb != sc.closed) {
                Undo.RecordObject(sc,"Change Spline Loop");
                sc.closed = vb;
            }

            switch(sc.type) {
                case SplineTypeFlag.CatmullRom: {
                    EditorGUIUtility.labelWidth = 55f;
                    vf = EditorGUILayout.Slider("Tension",sc.catmull_rom.tension,-5f,5f);
                    if(Mathf.Abs(vf - sc.catmull_rom.tension)>0f) {
                        Undo.RecordObject(sc,"Change Catmull Tension");
                        sc.catmull_rom.tension = vf;
                        sc.RefreshHierarchy(true);
                        sc.Refresh(true);
                    }
                    EditorGUIUtility.labelWidth = 0f;
                }
                break;
            }

            GUILayout.Space(4f);

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label($"Guides",SplineGUIConsts.LayoutBoxTitleStyle);
            vi = GUILayout.Toolbar((int)sc.guide_mode,SplineGUIConsts.PreviewModeToolbarItems,GUILayout.ExpandWidth(true));
            if(vi != (int)sc.guide_mode) {
                Undo.RecordObject(sc,"Change Guide Mode");
                sc.guide_mode = vi;    
                sc.RefreshHierarchy(true);
                sc.Refresh(true);
            }

            GUILayout.Space(4f);
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(3f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label($"Orient",SplineGUIConsts.LayoutBoxTitleStyle);
            vi = GUILayout.Toolbar(-1,SplineGUIConsts.OrientModeToolbarItems,GUILayout.ExpandWidth(true));
            if(vi > -1) {
                Undo.RecordObject(sc,"Change Orientation");                
                for(int i=0;i<sc.handles.Count;i++) {
                    int i0 = i;
                    int i1 = (i0+1)%sc.handles.Count;
                    SplineComponentHandle h0 = sc.handles[i0];
                    SplineComponentHandle h1 = sc.handles[i1];
                    Vector3 dv = h0.transform.forward;
                    switch(vi) {
                        case 0: { dv = h1.transform.localPosition-h0.transform.localPosition; } break;
                        case 1: { dv = sc.curve.Derivative(i);                                } break;
                    }
                    h0.transform.localRotation = Quaternion.LookRotation(dv,h0.transform.up);
                }
                sc.RefreshHierarchy(true);
                sc.Refresh(true);
            }
            GUILayout.Space(4f);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("Debug Animation",SplineGUIConsts.LayoutBoxTitleStyle);

            sc.simulation_enabled   = GUILayout.Toggle(sc.simulation_enabled,"Enabled");
            sc.simulation_use_deriv = GUILayout.Toggle(sc.simulation_use_deriv,"Orient");
            EditorGUIUtility.labelWidth = 80f;
            sc.simulation_speed   = EditorGUILayout.Slider("Speed (m/s)",sc.simulation_speed,-100f,100f);
            EditorGUIUtility.labelWidth = 0f;
            
            GUILayout.Space(4f);
            EditorGUILayout.EndVertical();

            

        }
        #endregion

        #endregion

        /// <summary>
        /// Reference to the inspected spline.
        /// </summary>
        new public SplineComponent target { get { return base.target as SplineComponent; } }

        static public bool selectionContainsHandles;

        #region Editor
        private void OnEnable() {         

            selectionContainsHandles = Selection.GetFiltered<SplineComponentHandle>( SelectionMode.Unfiltered).Length>0;

            spline_cmd = "";
            target.RefreshHierarchy();
            target.selected = true;
            target.SetEditorUpdateEnabled(true);
        }
        private void OnDisable() {
            spline_cmd = "";
            if(!target) return;
            target.RefreshHierarchy();
            target.selected = false;
            if(!target.selected) target.SetEditorUpdateEnabled(false);
        }
        public override void OnInspectorGUI() { /*SplineInspector(target);*/ }
        protected void OnSceneGUI() { 

            //Skip GUI is component handle are selected
            if(selectionContainsHandles) return;
            Handles.BeginGUI();
            SplineSceneGUI(target); 
            float sw = Screen.width;
            float sh = Screen.height;
            float margin = 15f;
            Rect layout_rect = new Rect(0f,0f,280f,sh-52f-margin);
            layout_rect.x = sw - (layout_rect.width+margin);
            layout_rect.y = margin;
            
            GUILayout.BeginArea(layout_rect);
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            SplineInspector(target);
            EditorGUILayout.EndVertical();
            GUILayout.EndArea();
            Handles.EndGUI();
        }
        #endregion

    }
    #endregion

    #endif

    #endregion

}
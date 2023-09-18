using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityExt.Core {

    #region enum SplineTagentMode
    /// <summary>
    /// Enumeration that describes how tangents behave when changed.
    /// </summary>
    public enum SplineTagentMode {
        /// <summary>
        /// No Tangents
        /// </summary>
        Off=0,
        /// <summary>
        /// Tangents moves independently
        /// </summary>
        Free,
        /// <summary>
        /// Tangents mirror orientation and distance from parent control point.
        /// </summary>
        Mirror,
        /// <summary>
        /// Tagents mirrot orientation and keep relative distance to parent control point
        /// </summary>
        Align
    }
    #endregion

    #region class SplineComponentHandle
    [ExecuteInEditMode]    
    public class SplineComponentHandle : MonoBehaviour {

        /// <summary>
        /// Tangent Mode
        /// </summary>
        public SplineTagentMode tangentMode {
            get { return m_tangent_mode; }
            set { 
                if(m_tangent_mode == value) return;
                m_tangent_mode = value;
                ApplyTangentMode();
                if(spline)spline.Refresh();
            }
        }
        [HideInInspector][SerializeField] SplineTagentMode m_tangent_mode = SplineTagentMode.Mirror;

        /// <summary>
        /// Reference to the parent spline component
        /// </summary>
        [HideInInspector] public SplineComponent spline;

        /// <summary>
        /// Reference to the control point owner.
        /// </summary>
        [HideInInspector]public SplineComponentHandle parent;  

        /// <summary>
        /// Cached Transform.
        /// </summary>
        new public Transform transform { get { return m_transform ? m_transform : (m_transform = base.transform); } }
        private Transform m_transform;
            
        /// <summary>
        /// List of child control points (tangents)
        /// </summary>
        public  List<SplineComponentHandle> children { get { return m_children==null ? (m_children = new List<SplineComponentHandle>()) : m_children; } }
        [HideInInspector] [SerializeField] private List<SplineComponentHandle> m_children;

        /// <summary>
        /// Control Point Index relative to its parent
        /// </summary>
        [HideInInspector] public int index;

        /// <summary>
        /// Blending Factor for Beziers
        /// </summary>
        public float blend = 0.5f;

        #if UNITY_EDITOR
        [HideInInspector] internal bool selected;
        internal bool childrenSelected { get { if(children==null) return false; for(int i=0;i<children.Count;i++) if(children[i])if(children[i].selected) return true; return false; } }
        #endif

        /// <summary>
        /// Internals
        /// </summary>
        internal  Vector3 m_position;
        internal  Vector4 m_rotation;
        internal  Vector3 m_scale;
        internal  int     m_sibling_index;
            
        /// <summary>
        /// CTOR.
        /// </summary>
        public void Awake() { 
            //If existing children (clone)                
            if(children.Count>=1)AssertTangents();
            //Lock starting transform information
            m_position = transform.localPosition;
            m_rotation = transform.localEulerAngles;
            m_scale    = transform.localScale;
        }

        public void Start() { }

        /// <summary>
        /// Asserts the creation of the children handles if needed
        /// </summary>
        internal void AssertTangents() {
            //Ensure there is a spline
            if(!spline) spline = GetComponentInParent<SplineComponent>();
            if(!spline) return;                
            //Check if the children of this handle are valid
            if(children.Count>=1) {
                bool is_valid = true;
                for(int i=0;i<children.Count;i++) if(children[i].parent != this) is_valid=false;
                if(is_valid) return;
            }                
            //Clear original list to build a new one
            children.Clear();
            //Container handle (this handle)
            SplineComponentHandle handle_ctn = this;
            //Create the children handles and let the spline configure them based on parenting
            GameObject cp;
            SplineComponentHandle hc;
            cp = new GameObject();
            cp.transform.parent = spline.transform;
            cp.transform.SetAsLastSibling();       
            cp.transform.localPosition = handle_ctn.transform.localPosition + Vector3.right * 0.4f;
            hc = cp.AddComponent<SplineComponentHandle>();
            hc.parent = handle_ctn;
            hc.spline = spline;
            cp = new GameObject();
            cp.transform.parent = spline.transform;
            cp.transform.SetAsLastSibling();                
            cp.transform.localPosition = handle_ctn.transform.localPosition - Vector3.right * 0.4f;
            hc = cp.AddComponent<SplineComponentHandle>();
            hc.parent = handle_ctn;
            hc.spline = spline;                
            //Trigger the spline refresh
            spline.RefreshHierarchy(Application.isEditor);            
        }

        /// <summary>
        /// Upon initialization trigger the spline refresh.
        /// </summary>
        internal void OnEnable() {
            if(!spline) spline = GetComponentInParent<SplineComponent>();
            if(!spline) return;
            spline.RefreshHierarchy();
        }

        /// <summary>
        /// Loop to detect value changes in this handle.
        /// </summary>
        internal void OnHandleUpdate() { 
            if(!this) return;
            Transform t   = transform;
            int     si    = t.GetSiblingIndex();
            Vector3 lpos  = t.localPosition;
            Vector4 lrot  = t.localRotation.ToVector4();
            Vector3 lscl  = t.localScale;

            List<SplineComponentHandle> cl = children;
            int clc = cl.Count;

            if(si!=m_sibling_index)                 { m_sibling_index = si;   spline.OnHandleHierarchyChange(this); }
            if((lrot - m_rotation).sqrMagnitude>0f) { m_rotation      = lrot; spline.OnHandleRotate(this);          }
            if((lscl - m_scale)   .sqrMagnitude>0f) { m_scale         = lscl; spline.OnHandleScale (this);          }

            if((lpos - m_position).sqrMagnitude>0f) { 
                Vector3 d = lpos - m_position;
                m_position = lpos; 
                spline.OnHandleMove(this);
                for(int i=0;i<clc;i++) if(cl[i])cl[i].transform.localPosition += d; 
                ApplyTangentMode();
            }
            for(int i=0;i<clc;i++) if(cl[i])cl[i].OnHandleUpdate();
            #if UNITY_EDITOR
            if(m_interaction_delay<m_interaction_wait)m_interaction_delay++;
            #endif
        }

        /// <summary>
        /// Applies the tangent mode on this handle.
        /// </summary>
        internal void ApplyTangentMode() {
            //If just a control point ignore
            if(!parent) return;
            switch(parent.tangentMode) {
                case SplineTagentMode.Off:  break;
                case SplineTagentMode.Free: break;
                default: {          
                    int tpp = spline.curve.tangentsPerPosition;
                    if(tpp<2) break;
                    SplineComponentHandle t0 = this;
                    SplineComponentHandle t1 = parent.children[(index+1) % tpp];
                    Vector3 ppos = parent.transform.localPosition;
                    Vector3 p0 = t0.transform.localPosition - ppos;
                    Vector3 p1 = t1.transform.localPosition - ppos;                        
                    float   d1 = Mathf.Min(p1.magnitude,1500f);
                    Vector3 v0 = parent.tangentMode == SplineTagentMode.Align ? (p0.normalized * d1) : p0;
                    t1.transform.localPosition = ppos-v0;
                    t1.m_position = t1.transform.localPosition;
                }
                break;
            }
        }

        internal void ResetTangents() {
            if(parent) return;
            if(spline.handles.Count<=1) return;            
            int tpp = spline.curve.tangentsPerPosition;
            if(children.Count<tpp) return;
            if(tpp<=0) return;
            SplineComponentHandle t0 = children[0];
            SplineComponentHandle t1 = children[1];

            bool closed = spline.closed;

            int i0 = index<=0 ? spline.handles.Count-1 : index-1;
            int i1 = (index+1)%spline.handles.Count;

            SplineComponentHandle prev = tpp<=1 ? this : spline.handles[i0];
            SplineComponentHandle next = spline.handles[i1];
            
            float d = Vector3.Distance(prev.transform.localPosition, next.transform.localPosition);
            Vector3 v = next.transform.localPosition - prev.transform.localPosition; v.Normalize();
            switch(tpp) {
                case 1: {                    
                    Vector3 prev_pos = spline.handles[i0].children[1].transform.localPosition;
                    if(index<=0) prev_pos = spline.handles[i0].transform.localPosition;
                    v = prev_pos - transform.localPosition;                    
                    t1.transform.localPosition = transform.localPosition - v;
                }
                break;
                case 2: {
                    t1.transform.localPosition = transform.localPosition + (v * d*0.2f);
                    t0.transform.localPosition = transform.localPosition - (v * d*0.2f);
                }
                break;
            }
        }

        /// <summary>
        /// Called on this handle is destroyed.
        /// </summary>
        internal void OnDestroy() {
            if(spline) spline.OnHandleDestroy(this);
        }

        /// <summary>
        /// Officially destroy this handle in the best timing for the spline.
        /// </summary>
        internal void Destroy() {
            #if UNITY_EDITOR                    
            DestroyImmediate(gameObject);
            #else
            Destroy(gameObject);
            #endif                
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Draws the needed gizmos for this handle.
        /// </summary>
        internal void OnDrawGizmos() {            
            if(m_interaction_delay<m_interaction_wait)m_interaction_delay++;
            SplineComponentHandleInspector.SplineHandleSceneGUI(this,m_interaction_delay>=m_interaction_wait);            
        }
        private int m_interaction_delay;
        const   int m_interaction_wait=20;
        #endif

    }
    #endregion

    #if UNITY_EDITOR

    [CustomEditor(typeof(SplineComponentHandle))]
    [CanEditMultipleObjects]
    public class SplineComponentHandleInspector : Editor {

        
        #region static 
        static internal void SplineHandleSceneGUI(SplineComponentHandle p_handle,bool p_interactable) {
            SplineComponentHandle h = p_handle;
            //If invalid spline skip
            if(!h.spline) return;            
            //Is handle able to be selected
            bool is_selectable = true;
            int tpp = h.spline.curve.tangentsPerPosition;
            //If tangent handle only render if inside the number of tangents
            if(h.parent) { 
                int cc = h.spline.curve.count;
                switch(h.spline.type) {
                    case SplineTypeFlag.Linear:      return;
                    case SplineTypeFlag.CatmullRom:  return;
                    case SplineTypeFlag.BezierCubic: {
                        if(!h.spline.closed) if(h.parent.index<=0)    if(h.index==0) return;
                        if(!h.spline.closed) if(h.parent.index>=cc-1) if(h.index==1) return;
                    }
                    break;
                    case SplineTypeFlag.BezierQuad: {
                        if(!h.spline.closed)if(h.parent.index>=(cc-1)) return;
                    }
                    break;                    
                }
                //Selection criteria for tangents
                is_selectable = h.spline.selected && (h.parent.tangentMode!= SplineTagentMode.Off);
            }
            
            float hs;

            if(p_interactable) {
                if(is_selectable) {
                    Gizmos.color = Color.clear;
                    hs = HandleUtility.GetHandleSize(h.transform.position)*SplineGUIConsts.ControlPointBaseSize*1f;
                    Gizmos.DrawCube(h.transform.position,Vector3.one * hs);
                }
            }
            
            float handle_size  = HandleUtility.GetHandleSize(h.transform.position);
            float state_size   = h.selected ? SplineGUIConsts.ControlPointSelectSize : SplineGUIConsts.ControlPointDefaultSize;

            if(h.parent) {
                if(is_selectable) {
                    Handles.color = SplineGUIConsts.ChildLineColor;
                    Handles.DrawAAPolyLine(3f,h.transform.position,h.parent.transform.position);
                    Handles.color = h.selected ? SplineGUIConsts.ControlPointSelectColor : SplineGUIConsts.ControlPointDefaultColor;   
                    hs = handle_size*SplineGUIConsts.ControlPointBaseSize*0.6f;
                    Handles.SphereHandleCap(0,h.transform.position,Quaternion.identity,hs,EventType.Repaint);
                }
            }
            else {
                Handles.color = h.selected ? SplineGUIConsts.ControlPointSelectColor : SplineGUIConsts.ControlPointDefaultColor;                        
                hs      = handle_size*state_size*SplineGUIConsts.ControlPointBaseSize;
                Handles.CubeHandleCap(0,h.transform.position,Quaternion.identity,hs,EventType.Repaint);            
            }
            
        }

        static void SplineHandleInspector(SplineComponentHandle[] p_targets) {

            SplineComponentHandle[] schl = p_targets;
            
            if(schl==null)     return;
            if(schl.Length<=0) return;

            SplineComponentHandle sch  = p_targets[0];
            //Skip if no spline
            if(!sch.spline) return;

            if(sch.parent) return;

            int tpp = sch.spline.curve.tangentsPerPosition;

            if(tpp<=0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("Tangents",SplineGUIConsts.LayoutBoxTitleStyle);
            GUILayout.Space(-5f);

            SerializedObject so = new SerializedObject(schl);
            SerializedProperty sp;
            int vi;
            //float vf;

            if(tpp>=2) {             
                GUILayout.Space(10f);
                sp = so.FindProperty("m_tangent_mode");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Mode",GUILayout.Width(50f));
                int tangent_mode = sp.hasMultipleDifferentValues ? -1 : sp.intValue;            
                vi = GUILayout.Toolbar(tangent_mode,SplineGUIConsts.TangentModeToolbarItems);
                if(vi != tangent_mode) {
                    sp.intValue = vi;
                    so.ApplyModifiedProperties();                
                    for(int i=0;i<schl.Length;i++) {
                        SplineComponentHandle it = schl[i] as SplineComponentHandle;
                        if(!it) continue;
                        if(it.children.Count>0)it.children[0].ApplyTangentMode();
                    }
                    sch.spline.RefreshHierarchy(true);
                    sch.spline.Refresh(true);
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(3f);
            }

            
            if(tpp>=1) {
                sp = so.FindProperty("blend");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Blending",GUILayout.Width(60f));                             
                EditorGUILayout.Slider(sp,0f,1f,"");
                if(so.hasModifiedProperties) {                    
                    so.ApplyModifiedProperties();                                    
                    sch.spline.RefreshHierarchy(true);
                    sch.spline.Refresh(true);
                }
                EditorGUILayout.EndHorizontal();

                if(GUILayout.Button("Reset")) {
                    List<SplineComponentHandle> hl = new List<SplineComponentHandle>(schl);                    
                    hl.Sort(delegate(SplineComponentHandle a,SplineComponentHandle b) { return a.index<b.index ? -1 : 1; });
                    for(int i=0;i<hl.Count;i++) { hl[i].ResetTangents(); }
                }   
            }

            if(!sch.parent) {
                GUILayout.Space(4f);
                EditorGUILayout.EndVertical();
            }
        }

        #endregion

        /// <summary>
        /// Target Handle
        /// </summary>
        new public SplineComponentHandle target { get { return base.target as SplineComponentHandle; } }

        /// <summary>
        /// Multi Select Handles
        /// </summary>
        new public SplineComponentHandle[] targets {  get { return m_targets; }  }
        private SplineComponentHandle[] m_targets;

        /// <summary>
        /// Boot up the target/serialized-object
        /// </summary>
        private void InitTargets() {
            List<SplineComponentHandle> schl = new List<SplineComponentHandle>();
            for(int i=0;i<base.targets.Length;i++) if(base.targets[i] is SplineComponentHandle) schl.Add(base.targets[i] as SplineComponentHandle);
            schl.Sort(delegate(SplineComponentHandle a,SplineComponentHandle b) { return a.index<b.index ? -1 : 1; });
            m_targets           = schl.ToArray();            
        }

        private void OnEnable() { 
            InitTargets();           
            SplineComponentInspector.selectionContainsHandles = true;
            SplineComponentInspector.spline_cmd = "";
            target.selected=true;
            if(target.spline) { target.spline.RefreshHierarchy(); target.spline.SetEditorUpdateEnabled(true); }
        }

        private void OnDisable() {           
            if(!target) return;
            AssertSelectionAll();
            if(target.spline) { target.spline.RefreshHierarchy(); if(!target.spline.selected)target.spline.SetEditorUpdateEnabled(false); }
        }

        internal void AssertSelectionAll() {
            for(int i = 0; i < targets.Length; i++) {                
                SplineComponentHandle sch = targets[i] as SplineComponentHandle;
                if(!sch)continue;
                AssertSelection(sch);
            }
        }

        internal void AssertSelection(SplineComponentHandle p_target) {            
            p_target.selected=false;
            for(int i=0;i<Selection.gameObjects.Length;i++) {
                if(p_target.gameObject != Selection.gameObjects[i])continue;
                p_target.selected=true;                
            }
        }
        
        public override void OnInspectorGUI() {            
        }

        protected void OnSceneGUI() {
            if(!target)        return;
            if(!target.spline) return;

            int idx = System.Array.IndexOf(targets,base.target);

            if(idx!=0) return;

            //Render the handle's spline just once (upon first hit)
            SplineComponent sc = target.spline;
            SplineComponentInspector.SplineSceneGUI(sc);
            
            float sw = Screen.width;
            float sh = Screen.height;
            float margin = 15f;
            Rect layout_rect = new Rect(0f,0f,280f,sh-52f-margin);
            layout_rect.x = sw - (layout_rect.width+margin);
            layout_rect.y = margin;
            Handles.BeginGUI();
            GUILayout.BeginArea(layout_rect);
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            SplineHandleInspector(targets);
            SplineComponentInspector.SplineInspector(target.spline);
            EditorGUILayout.EndVertical();
            GUILayout.EndArea();
            Handles.EndGUI();
            

        }


    }


#endif

}
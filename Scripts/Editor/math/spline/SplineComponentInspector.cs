using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityExt.Core {

    #if UNITY_EDITOR

    #region SplineComponent Editor

    #region class SplineGUIConsts
    /// <summary>
    /// Helper class for styling the spline inspector.
    /// </summary>
    internal class SplineGUIConsts {

        static internal float ControlPointBaseSize = 0.22f;
        static internal float ControlPointSelectSize = 0.9f;
        static internal float ControlPointDefaultSize = 0.45f;
        static internal Color ControlPointSelectColor = Color.yellow;
        static internal Color ControlPointDefaultColor = Colorf.RGBAToColor(0xffffffaa);
        static internal Color ChildLineColor = Colorf.RGBAToColor(0x00aaffaa);
        static internal float CurveSelectSize = 2f;
        static internal float CurveDefaultSize = 1f;
        static internal Color CurveSelectColor = Colorf.RGBAToColor(0xff6600aa);
        static internal Color CurveDefaultColor = Colorf.RGBAToColor(0xffff00aa);
        static internal Color CurvePreviewModeColor = Colorf.RGBAToColor(0xffffff11);
        static internal GUIStyle ControlPointLabelStyle = new GUIStyle(EditorStyles.whiteLargeLabel);
        static internal GUIStyle LayoutBoxTitleStyle = new GUIStyle(EditorStyles.whiteBoldLabel);
        static internal GUIStyle LayoutBoxLengthStyle = new GUIStyle(EditorStyles.boldLabel);
        static internal Color ControlPointLabelDefaultColor = new Color(1f,1f,1f,0.5f);
        static internal Color ControlPointLabelSelectColor = new Color(1f,1f,1f,1.0f);
        static internal Color ControlPointPreviewColor = Colorf.RGBAToColor(0xffcc00ff);
        static internal Texture2D CurveAATex = Texture2D.whiteTexture;

        static internal Color GizmosAxisXColor = Colorf.RGBAToColor(0xff4444ff);
        static internal Color GizmosAxisYColor = Colorf.RGBAToColor(0x44ff44ff);
        static internal Color GizmosAxisZColor = Colorf.RGBAToColor(0x4444ffff);
        static internal float GizmosAxisSize = 0.2f;
        static internal Color GizmosBlendFrom = Colorf.RGBAToColor(0xff0000ff);
        static internal Color GizmosBlendTo = Colorf.RGBAToColor(0x00ff00ff);
        static internal Color GizmosScaleGuidePColor = new Color(1.0f,1.0f,1.0f,0.25f);
        static internal Color GizmosScaleGuideNColor = new Color(0.5f,0.5f,0.5f,0.25f);
        static internal float GizmosScaleGuideSize = 0.15f;

        static internal string[] SplineTypeToolbarItems = new string[] { "Linear","Bezier2","Bezier3","Catmull" };
        static internal string[] TangentModeToolbarItems = new string[] { "Off","Free","Mirror","Align" };
        static internal string[] PreviewModeToolbarItems = new string[] { "Off","Axis","Scale","Blending" };
        static internal string[] OrientModeToolbarItems = new string[] { "Nodes","Path" };

        internal const string CmdControlPointDropStart = "control-point-drop@start";
        internal const string CmdControlPointDropUpdate = "control-point-drop@update";
        internal const string CmdControlPointDropStop = "control-point-drop@stop";
        internal const string CmdControlPointDropApply = "control-point-drop@apply";
        internal const string CmdControlPointDropCancel = "control-point-drop@cancel";
        internal const KeyCode CmdPointDropKey = KeyCode.LeftControl;

        static SplineGUIConsts() {
            ControlPointLabelStyle.fontSize = 10;
            ControlPointLabelStyle.contentOffset = new Vector2(10f,-7f);
            LayoutBoxTitleStyle.normal.textColor = Color.white;
            LayoutBoxLengthStyle.alignment = TextAnchor.MiddleRight;
            LayoutBoxLengthStyle.fontSize = 12;
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
            switch (spline_guide_mode) {
                //Blending Factor
                case 3: {
                    bool f = true;
                    for (int i = 1;i < sc.m_spline_samples.Length;i++) {
                        Vector4 p0 = sc.m_spline_samples[i - 1];
                        Vector4 p1 = sc.m_spline_samples[i];
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
                    Handles.color = spline_selected ? SplineGUIConsts.CurveSelectColor : SplineGUIConsts.CurveDefaultColor;
                    float curve_size = spline_selected ? SplineGUIConsts.CurveSelectSize : SplineGUIConsts.CurveDefaultSize;
                    //In case of active guide modes, draw a lighter version
                    if (spline_selected)
                        if (sc.guide_mode != 0) {
                            Handles.color = SplineGUIConsts.CurvePreviewModeColor;
                            curve_size = SplineGUIConsts.CurveDefaultSize;
                        }
                    //Draws the spline
                    Handles.DrawAAPolyLine(SplineGUIConsts.CurveAATex,curve_size,sc.m_samples_spos);
                }
                break;
            }
            #endregion

            #region Control Point Labels
            SplineGUIConsts.ControlPointLabelStyle.normal.textColor = spline_selected ? SplineGUIConsts.ControlPointLabelSelectColor : SplineGUIConsts.ControlPointLabelDefaultColor;
            for (int i = 0;i < sc.handles.Count;i++) {
                SplineComponentHandle it = sc.handles[i];
                if (!it) continue;
                string it_idx = it.index.ToString("0");//+"|"+sc.curve.GetPosition(i).w.ToString("0.000");                
                Vector3 it_pos = sc.transform.TransformPoint(it.transform.localPosition);
                Handles.Label(it_pos,it_idx,SplineGUIConsts.ControlPointLabelStyle);
            }
            #endregion

            #region Guides Rendering
            switch (spline_guide_mode) {
                //Off
                case 0: break;
                //On
                default: {
                    for (int i = 0;i < sc.m_samples_tpos.Length;i++) {
                        Vector3 tpos = sc.m_samples_tpos[i];
                        Vector3 tx = sc.m_samples_tx[i];
                        Vector3 ty = sc.m_samples_ty[i];
                        Vector3 tz = sc.m_samples_tz[i];
                        Vector3 scl = sc.m_samples_scl[i];
                        bool scl_inv = (scl.x < 0f) || (scl.y < 0f) || (scl.z < 0f);
                        float tw = sc.m_samples_tpos[i].w;
                        float hs = HandleUtility.GetHandleSize(tpos);

                        switch (sc.guide_mode) {
                            //Transform
                            case 1: {
                                Handles.color = SplineGUIConsts.GizmosAxisXColor; Handles.DrawLine(tpos,tpos + tx * hs * SplineGUIConsts.GizmosAxisSize);
                                Handles.color = SplineGUIConsts.GizmosAxisYColor; Handles.DrawLine(tpos,tpos + ty * hs * SplineGUIConsts.GizmosAxisSize);
                                Handles.color = SplineGUIConsts.GizmosAxisZColor; Handles.DrawLine(tpos,tpos + tz * hs * SplineGUIConsts.GizmosAxisSize);
                            }
                            break;
                            //Scale
                            case 2: {
                                Handles.color = scl_inv ? SplineGUIConsts.GizmosScaleGuideNColor : SplineGUIConsts.GizmosScaleGuidePColor;
                                Handles.SphereHandleCap(0,tpos,Quaternion.identity,scl.magnitude * hs * SplineGUIConsts.GizmosScaleGuideSize,EventType.Repaint);
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
            if (spline_selected)
                if (sc.simulation_enabled) {
                    Vector3 v;
                    float s_w = sc.simulation_w;
                    Vector3 s_pos = sc.simulation_pos;
                    Quaternion s_rot = sc.simulation_rot;
                    Vector3 s_scl = sc.simulation_scl;
                    float hs = HandleUtility.GetHandleSize(s_pos);
                    v = sc.simulation_rot * Vector3.right; Handles.color = Colorf.RGBToColor(0xff4444); Handles.DrawAAPolyLine(4f,s_pos,s_pos + v * hs * 0.33f);
                    v = sc.simulation_rot * Vector3.up; Handles.color = Colorf.RGBToColor(0x44ff44); Handles.DrawAAPolyLine(4f,s_pos,s_pos + v * hs * 0.33f);
                    v = sc.simulation_rot * Vector3.forward; Handles.color = Colorf.RGBToColor(0x4444ff); Handles.DrawAAPolyLine(4f,s_pos,s_pos + v * hs * 0.33f);
                    Handles.color = new Color(1f,1f,1f,0.05f);
                    Handles.SphereHandleCap(0,s_pos,Quaternion.identity,s_scl.magnitude * 0.05f,EventType.Repaint);
                    Handles.Label(s_pos,s_w.ToString("0.00"),stl);
                    HandleUtility.Repaint();
                }
            #endregion

            #region Spline Mouse/Keyboard input
            //Create dictionary to track up/down transition of input (solves key repeat issue)
            if (m_kdown_tb == null) {
                m_kdown_tb = new Dictionary<KeyCode,bool>();
                m_kdown_tb[KeyCode.None] = false;
                m_kdown_tb[SplineGUIConsts.CmdPointDropKey] = false;
            }
            //Filter out non expected keycodes
            KeyCode kc = Event.current.keyCode;
            switch (kc) {
                case SplineGUIConsts.CmdPointDropKey: break;
                default: kc = KeyCode.None; break;
            }
            //GUI Events Flags
            EventType evt = Event.current.type;
            bool k_down = evt == EventType.KeyDown;
            bool k_up = evt == EventType.KeyUp;
            bool m_down = evt == EventType.MouseDown;
            bool m_up = evt == EventType.MouseUp;
            string cmd = spline_cmd;
            //Prevent repeat
            if (k_down) if (m_kdown_tb[kc]) k_down = false;
            //Store up kdown cache
            if (k_down || k_up) { m_kdown_tb[kc] = k_down ? true : (k_up ? false : m_kdown_tb[kc]); }
            //Shortcuts
            int h_count = sc.handles.Count;
            bool h_empty = h_count <= 0;
            //Input State Machine
            switch (cmd) {

                #region CmdControlPointDropStart
                case SplineGUIConsts.CmdControlPointDropStart: {

                    //Switch to point drop update loop
                    cmd = SplineGUIConsts.CmdControlPointDropUpdate;
                    //Detect if preview is close to first/last segment points (means its either a start/tail insert)
                    Vector3[] spline_pos = h_empty ? new Vector3[] { sc.transform.position,sc.transform.position } : sc.m_samples_spos;
                    spline_control_point_new = HandleUtility.ClosestPointToPolyLine(spline_pos);
                    spline_control_point_new_free_idx = -1;
                    spline_control_point_new_idx = 0;
                    bool is_extreme = false;
                    if (!sc.closed) {
                        float d_first = Vector3.Distance(spline_control_point_new,spline_pos[0]);
                        float d_last = Vector3.Distance(spline_control_point_new,spline_pos[spline_pos.Length - 1]);
                        if (Mathf.Abs(d_first) <= 0.05f) { spline_control_point_new_free_idx = 0; is_extreme = true; }
                        if (Mathf.Abs(d_last) <= 0.05f) { spline_control_point_new_free_idx = h_count - 1; is_extreme = true; }
                    }
                    if (h_empty) spline_control_point_new_free_idx = 0;

                    //Check if insertion trigger was too far and ignore to not conflic w/ other GUI interactions
                    Ray gui_ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

                    float control_point_trigger_distance = HandleUtility.DistancePointLine(spline_control_point_new,gui_ray.origin,gui_ray.GetPoint(4000f));
                    float distance_bias = is_extreme ? 15f : 2f;
                    if (control_point_trigger_distance > distance_bias) cmd = "";
                }
                break;
                #endregion

                #region CmdControlPointDropUpdate
                case SplineGUIConsts.CmdControlPointDropUpdate: {
                    //First check if ongoing drag/click is too close to handle points and ignore adding points
                    bool handle_too_close = false;
                    for (int i = 0;i < sc.handles.Count;i++) {
                        Transform it = sc.handles[i] ? sc.handles[i].transform : null;
                        if (!it) continue;
                        float d = Vector3.Distance(spline_control_point_new,it.position);
                        if (d < 0.15f) { handle_too_close = true; break; }
                    }
                    //If PointDrop key is up stop loop
                    if (k_up) { if (kc == SplineGUIConsts.CmdPointDropKey) cmd = SplineGUIConsts.CmdControlPointDropStop; }
                    //If mouse down place the point unless too close of handle
                    if (!handle_too_close) if (m_down) { cmd = SplineGUIConsts.CmdControlPointDropApply; Event.current.Use(); break; }
                    //Ignore non rendering commands
                    if (evt != EventType.Repaint) break;
                    //Fetch current spline line segments
                    Vector3[] spline_pos = h_count <= 0 ? new Vector3[] { sc.transform.position,sc.transform.position } : sc.m_samples_spos;
                    //Fetch closest 
                    spline_control_point_new = HandleUtility.ClosestPointToPolyLine(spline_pos);
                    spline_control_point_new_idx = 0;
                    //Flag that tells if new point is free (start or tail)
                    bool is_free = spline_control_point_new_free_idx >= 0;
                    int h_idx = Mathf.Clamp(spline_control_point_new_free_idx,0,h_count - 1);
                    //If free position the preview using a raycast to the first/last point up vector plane
                    if (is_free) {
                        Transform h = h_empty ? sc.transform : sc.handles[h_idx].transform;
                        Ray gui_ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                        Plane h_plane = new Plane(h.up,h.position);
                        float h_gui_dist = 0f;
                        if (h_plane.Raycast(gui_ray,out h_gui_dist)) { spline_control_point_new = gui_ray.GetPoint(h_gui_dist); }
                        Handles.color = SplineGUIConsts.ControlPointPreviewColor;
                        Handles.DrawAAPolyLine(1f,h.position,spline_control_point_new);
                        if (h_idx == 0) spline_control_point_new_idx = -1;
                        if (h_idx == h_count - 1) spline_control_point_new_idx = h_count + 1;
                    }
                    //Render the preview
                    float hs = HandleUtility.GetHandleSize(spline_control_point_new);
                    Color preview_color = SplineGUIConsts.ControlPointPreviewColor;
                    preview_color.a = handle_too_close ? 0.1f : 1f;
                    Handles.color = preview_color;
                    Handles.CubeHandleCap(0,spline_control_point_new,Quaternion.identity,hs * 0.1f,EventType.Repaint);
                }
                break;
                #endregion

                #region CmdControlPointDropApply
                case SplineGUIConsts.CmdControlPointDropApply: {
                    //After mouse down iterate all spline samples to find the closest one
                    Vector4[] spline_samples = sc.m_spline_samples;
                    int c_idx = 0;
                    float c_dist = Vector3.Distance(spline_control_point_new,spline_samples[0]);
                    for (int i = 1;i < spline_samples.Length;i++) {
                        Vector4 it_pos = spline_samples[i];
                        float d = Vector3.Distance(it_pos,spline_control_point_new);
                        if (d >= c_dist) continue;
                        c_dist = d;
                        c_idx = i;
                    }
                    //Store closest sample and its index
                    Vector4 c_sample = spline_samples[c_idx];
                    Vector3 s_pos = c_sample;
                    int s_idx = Mathf.CeilToInt(c_sample.w);
                    //If currently in 'free-point' mode use the free point information
                    if (spline_control_point_new_idx < 0) { s_idx = -1; s_pos = spline_control_point_new; spline_control_point_new_free_idx = 0; }
                    if (spline_control_point_new_idx >= h_count + 1) { s_idx = h_count + 1; s_pos = spline_control_point_new; spline_control_point_new_free_idx = h_count; }
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
                    switch (kc) {
                        case SplineGUIConsts.CmdPointDropKey: {
                            //Start Control Point Drop Op
                            if (k_down) cmd = SplineGUIConsts.CmdControlPointDropStart;
                            //Stop Control Point Drop Op
                            if (k_up) cmd = SplineGUIConsts.CmdControlPointDropStop;
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
            if (!string.IsNullOrEmpty(cmd)) { SceneView.RepaintAll(); }

        }
        static int spline_control_point_new_idx;
        static int spline_control_point_new_free_idx;
        static Vector3 spline_control_point_new;
        static internal string spline_cmd;
        static private Dictionary<KeyCode,bool> m_kdown_tb;
        #endregion

        #region void SplineInspector
        /// <summary>
        /// Renders the Spline Inspector
        /// </summary>        
        static internal void SplineInspector(SplineComponent p_spline) {

            SplineComponent sc = p_spline;
            if (!sc) return;

            int vi;
            bool vb;
            float vf;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Spline",SplineGUIConsts.LayoutBoxTitleStyle);
            float spline_len = sc.curve.length;
            string spline_unit = "m";
            if (spline_len < 1f) {
                spline_len *= 100f;
                spline_unit = "cm";
            }
            GUILayout.Label($"{spline_len.ToString("0.00")}{spline_unit}",SplineGUIConsts.LayoutBoxLengthStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);

            vi = GUILayout.Toolbar((int)sc.type,SplineGUIConsts.SplineTypeToolbarItems,GUILayout.ExpandWidth(true));
            if (vi != (int)sc.type) {
                Undo.RecordObject(sc,"Change Spline Type");
                sc.type = (SplineTypeFlag)vi;
            }

            vb = GUILayout.Toggle(sc.closed,"Closed");
            if (vb != sc.closed) {
                Undo.RecordObject(sc,"Change Spline Loop");
                sc.closed = vb;
            }

            switch (sc.type) {
                case SplineTypeFlag.CatmullRom: {
                    EditorGUIUtility.labelWidth = 55f;
                    vf = EditorGUILayout.Slider("Tension",sc.catmull_rom.tension,-5f,5f);
                    if (Mathf.Abs(vf - sc.catmull_rom.tension) > 0f) {
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
            if (vi != (int)sc.guide_mode) {
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
            if (vi > -1) {
                Undo.RecordObject(sc,"Change Orientation");
                for (int i = 0;i < sc.handles.Count;i++) {
                    int i0 = i;
                    int i1 = (i0 + 1) % sc.handles.Count;
                    SplineComponentHandle h0 = sc.handles[i0];
                    SplineComponentHandle h1 = sc.handles[i1];
                    Vector3 dv = h0.transform.forward;
                    switch (vi) {
                        case 0: { dv = h1.transform.localPosition - h0.transform.localPosition; } break;
                        case 1: { dv = sc.curve.Derivative(i); } break;
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

            sc.simulation_enabled = GUILayout.Toggle(sc.simulation_enabled,"Enabled");
            sc.simulation_use_deriv = GUILayout.Toggle(sc.simulation_use_deriv,"Orient");
            EditorGUIUtility.labelWidth = 80f;
            sc.simulation_speed = EditorGUILayout.Slider("Speed (m/s)",sc.simulation_speed,-100f,100f);
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

            selectionContainsHandles = Selection.GetFiltered<SplineComponentHandle>(SelectionMode.Unfiltered).Length > 0;

            spline_cmd = "";
            target.RefreshHierarchy();
            target.selected = true;
            target.SetEditorUpdateEnabled(true);
        }
        private void OnDisable() {
            spline_cmd = "";
            if (!target) return;
            target.RefreshHierarchy();
            target.selected = false;
            if (!target.selected) target.SetEditorUpdateEnabled(false);
        }
        public override void OnInspectorGUI() { /*SplineInspector(target);*/ }
        protected void OnSceneGUI() {

            //Skip GUI is component handle are selected
            if (selectionContainsHandles) return;
            Handles.BeginGUI();
            SplineSceneGUI(target);
            float sw = Screen.width;
            float sh = Screen.height;
            float margin = 15f;
            Rect layout_rect = new Rect(0f,0f,280f,sh - 52f - margin);
            layout_rect.x = sw - (layout_rect.width + margin);
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

    #endregion

    #endif

}

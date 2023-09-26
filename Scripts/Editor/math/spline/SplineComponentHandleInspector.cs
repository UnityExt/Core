using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityExt.Core {

    #if UNITY_EDITOR

    [CustomEditor(typeof(SplineComponentHandle))]
    [CanEditMultipleObjects]
    public class SplineComponentHandleInspector : Editor {


        #region static 
        static internal void SplineHandleSceneGUI(SplineComponentHandle p_handle,bool p_interactable) {
            SplineComponentHandle h = p_handle;
            //If invalid spline skip
            if (!h.spline) return;
            //Is handle able to be selected
            bool is_selectable = true;
            int tpp = h.spline.curve.tangentsPerPosition;
            //If tangent handle only render if inside the number of tangents
            if (h.parent) {
                int cc = h.spline.curve.count;
                switch (h.spline.type) {
                    case SplineTypeFlag.Linear: return;
                    case SplineTypeFlag.CatmullRom: return;
                    case SplineTypeFlag.BezierCubic: {
                        if (!h.spline.closed) if (h.parent.index <= 0) if (h.index == 0) return;
                        if (!h.spline.closed) if (h.parent.index >= cc - 1) if (h.index == 1) return;
                    }
                    break;
                    case SplineTypeFlag.BezierQuad: {
                        if (!h.spline.closed) if (h.parent.index >= (cc - 1)) return;
                    }
                    break;
                }
                //Selection criteria for tangents
                is_selectable = h.spline.selected && (h.parent.tangentMode != SplineTagentMode.Off);
            }

            float hs;

            if (p_interactable) {
                if (is_selectable) {
                    Gizmos.color = Color.clear;
                    hs = HandleUtility.GetHandleSize(h.transform.position) * SplineGUIConsts.ControlPointBaseSize * 1f;
                    Gizmos.DrawCube(h.transform.position,Vector3.one * hs);
                }
            }

            float handle_size = HandleUtility.GetHandleSize(h.transform.position);
            float state_size = h.selected ? SplineGUIConsts.ControlPointSelectSize : SplineGUIConsts.ControlPointDefaultSize;

            if (h.parent) {
                if (is_selectable) {
                    Handles.color = SplineGUIConsts.ChildLineColor;
                    Handles.DrawAAPolyLine(3f,h.transform.position,h.parent.transform.position);
                    Handles.color = h.selected ? SplineGUIConsts.ControlPointSelectColor : SplineGUIConsts.ControlPointDefaultColor;
                    hs = handle_size * SplineGUIConsts.ControlPointBaseSize * 0.6f;
                    Handles.SphereHandleCap(0,h.transform.position,Quaternion.identity,hs,EventType.Repaint);
                }
            } else {
                Handles.color = h.selected ? SplineGUIConsts.ControlPointSelectColor : SplineGUIConsts.ControlPointDefaultColor;
                hs = handle_size * state_size * SplineGUIConsts.ControlPointBaseSize;
                Handles.CubeHandleCap(0,h.transform.position,Quaternion.identity,hs,EventType.Repaint);
            }

        }

        static void SplineHandleInspector(SplineComponentHandle[] p_targets) {

            SplineComponentHandle[] schl = p_targets;

            if (schl == null) return;
            if (schl.Length <= 0) return;

            SplineComponentHandle sch = p_targets[0];
            //Skip if no spline
            if (!sch.spline) return;

            if (sch.parent) return;

            int tpp = sch.spline.curve.tangentsPerPosition;

            if (tpp <= 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("Tangents",SplineGUIConsts.LayoutBoxTitleStyle);
            GUILayout.Space(-5f);

            SerializedObject so = new SerializedObject(schl);
            SerializedProperty sp;
            int vi;
            //float vf;

            if (tpp >= 2) {
                GUILayout.Space(10f);
                sp = so.FindProperty("m_tangent_mode");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Mode",GUILayout.Width(50f));
                int tangent_mode = sp.hasMultipleDifferentValues ? -1 : sp.intValue;
                vi = GUILayout.Toolbar(tangent_mode,SplineGUIConsts.TangentModeToolbarItems);
                if (vi != tangent_mode) {
                    sp.intValue = vi;
                    so.ApplyModifiedProperties();
                    for (int i = 0;i < schl.Length;i++) {
                        SplineComponentHandle it = schl[i] as SplineComponentHandle;
                        if (!it) continue;
                        if (it.children.Count > 0) it.children[0].ApplyTangentMode();
                    }
                    sch.spline.RefreshHierarchy(true);
                    sch.spline.Refresh(true);
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(3f);
            }


            if (tpp >= 1) {
                sp = so.FindProperty("blend");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Blending",GUILayout.Width(60f));
                EditorGUILayout.Slider(sp,0f,1f,"");
                if (so.hasModifiedProperties) {
                    so.ApplyModifiedProperties();
                    sch.spline.RefreshHierarchy(true);
                    sch.spline.Refresh(true);
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Reset")) {
                    List<SplineComponentHandle> hl = new List<SplineComponentHandle>(schl);
                    hl.Sort(delegate (SplineComponentHandle a,SplineComponentHandle b) { return a.index < b.index ? -1 : 1; });
                    for (int i = 0;i < hl.Count;i++) { hl[i].ResetTangents(); }
                }
            }

            if (!sch.parent) {
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
        new public SplineComponentHandle[] targets { get { return m_targets; } }
        private SplineComponentHandle[] m_targets;

        /// <summary>
        /// Boot up the target/serialized-object
        /// </summary>
        private void InitTargets() {
            List<SplineComponentHandle> schl = new List<SplineComponentHandle>();
            for (int i = 0;i < base.targets.Length;i++) if (base.targets[i] is SplineComponentHandle) schl.Add(base.targets[i] as SplineComponentHandle);
            schl.Sort(delegate (SplineComponentHandle a,SplineComponentHandle b) { return a.index < b.index ? -1 : 1; });
            m_targets = schl.ToArray();
        }

        private void OnEnable() {
            InitTargets();
            SplineComponentInspector.selectionContainsHandles = true;
            SplineComponentInspector.spline_cmd = "";
            target.selected = true;
            if (target.spline) { target.spline.RefreshHierarchy(); target.spline.SetEditorUpdateEnabled(true); }
        }

        private void OnDisable() {
            if (!target) return;
            AssertSelectionAll();
            if (target.spline) { target.spline.RefreshHierarchy(); if (!target.spline.selected) target.spline.SetEditorUpdateEnabled(false); }
        }

        internal void AssertSelectionAll() {
            for (int i = 0;i < targets.Length;i++) {
                SplineComponentHandle sch = targets[i] as SplineComponentHandle;
                if (!sch) continue;
                AssertSelection(sch);
            }
        }

        internal void AssertSelection(SplineComponentHandle p_target) {
            p_target.selected = false;
            for (int i = 0;i < Selection.gameObjects.Length;i++) {
                if (p_target.gameObject != Selection.gameObjects[i]) continue;
                p_target.selected = true;
            }
        }

        public override void OnInspectorGUI() {
        }

        protected void OnSceneGUI() {
            if (!target) return;
            if (!target.spline) return;

            int idx = System.Array.IndexOf(targets,base.target);

            if (idx != 0) return;

            //Render the handle's spline just once (upon first hit)
            SplineComponent sc = target.spline;
            SplineComponentInspector.SplineSceneGUI(sc);

            float sw = Screen.width;
            float sh = Screen.height;
            float margin = 15f;
            Rect layout_rect = new Rect(0f,0f,280f,sh - 52f - margin);
            layout_rect.x = sw - (layout_rect.width + margin);
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

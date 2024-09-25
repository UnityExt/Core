using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityExt.Core {

    /// <summary>
    /// Component to manipulate the Transform of this target and align it to the camera.
    /// </summary>
    [ExecuteInEditMode]
    public class BillboardTransform : MonoBehaviour {

        /// <summary>
        /// Transform to orient.
        /// </summary>
        public Transform target;

        /// <summary>
        /// Rotation offset.
        /// </summary>
        public Vector3 rotation;

        /// <summary>
        /// Scale offset.
        /// </summary>
        public Vector3 scale = Vector3.one;

        /// <summary>
        /// Flag that tells to orient the transform
        /// </summary>
        public bool orient = true;

        /// <summary>
        /// Flag that tells to resize the transform
        /// </summary>
        public bool resize;

        /// <summary>
        /// Internals.
        /// </summary>
        private MeshFilter m_mfilter;
        private Renderer m_renderer;
        private Transform m_transform_cache;
        private Transform m_target;

        /// <summary>
        /// CTOR
        /// </summary>
        virtual protected void Awake() {
            if (enabled) AssertComponents();
        }

        /// <summary>
        /// CTOR
        /// </summary>
        virtual protected void Start() { }

        /// <summary>
        /// Fetch components
        /// </summary>
        protected void AssertComponents() {
            if (!m_mfilter) { m_mfilter = GetComponent<MeshFilter>(); if (m_mfilter) if (!m_mfilter.sharedMesh) m_mfilter.hideFlags = HideFlags.HideInInspector; }
            if (!m_renderer) { m_renderer = GetComponent<Renderer>(); }
            if (!m_transform_cache) m_transform_cache = transform;
            if (!m_target) m_target = target ? target : m_transform_cache;
        }

        /// <summary>
        /// Handler for when this object will be rendered.
        /// </summary>
        virtual protected void OnWillRenderObject() {

            //For the billboard to behave nicely we need a Renderer
            //If there isn't a MeshFilter it is a dummy so we hide it
            //AssertComponents();

            bool valid_mfilter = true;
            if (!m_mfilter) valid_mfilter = false;
            if (m_mfilter) if (!m_mfilter.sharedMesh) valid_mfilter = false;

            if (valid_mfilter) {
                if (m_renderer) {
                    //m_renderer.hideFlags                  = HideFlags.HideInInspector; 
                    //m_renderer.sharedMaterials            = new Material[0];
                    //m_renderer.lightProbeUsage            = LightProbeUsage.Off;
                    //m_renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                }
            }

            if (!enabled) return;

            Camera c = Camera.current;
            Transform ct = c.transform;
            Transform t = m_target;//target ? target : m_transform_cache;

            if (orient) {
                Quaternion r = Quaternion.LookRotation(ct.forward,ct.up);
                t.rotation = r * Quaternion.Euler(rotation);
            }

            if (resize) {
                float d = Vector3.Dot(t.position - ct.position,ct.forward);
                float cw = c.pixelWidth;
                float ch = c.pixelHeight;
                float a = ch <= 0f ? 0f : cw / ch;
                float denom = Mathf.Sqrt(cw * cw + ch * ch) * Mathf.Tan(c.fieldOfView * Mathf.Deg2Rad);
                float f = Mathf.Max(0.0001f,d / denom * 100f);
                Vector3 sf = scale * f;


                t.localScale = Vector3.one;
                Vector3 iws = t.lossyScale; //inverse world scale (to prevent parent scaling)
                iws.x = Mathf.Abs(iws.x) <= 0.0001f ? 0f : 1f / iws.x;
                iws.y = Mathf.Abs(iws.y) <= 0.0001f ? 0f : 1f / iws.y;
                iws.z = Mathf.Abs(iws.z) <= 0.0001f ? 0f : 1f / iws.z;
                sf.Scale(iws);
                //*/

                t.localScale = sf;
            }

            //t.SetParent(p,true);

        }

        /// <summary>
        /// DTOR
        /// </summary>
        protected void OnDestroy() {
            /*
            if(!m_mfilter) {
                bool isp = Application.isPlaying;
                if(isp) { Destroy(m_mfilter);          if(m_renderer)  Destroy(m_renderer);          }
                else    { DestroyImmediate(m_mfilter); if(m_renderer)  DestroyImmediate(m_renderer); }
            } 
            //*/
        }

    }

}
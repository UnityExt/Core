using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityExt.Core {

    /// <summary>
    /// Component to scroll UV offsets of its targets.
    /// </summary>
    public class UIUVScroll : ActivityBehaviour, IUpdateable {

        /// <summary>
        /// List of targets. If empty, tries to fetch the owns Renderer.
        /// </summary>
        public Graphic target { get { Graphic t = m_target ? m_target : (m_target = GetComponent<Graphic>()); m_has_target = t!=null; return t; } set { m_target=value; m_has_target = value!=null; } }
        [SerializeField]
        private Graphic m_target;

        /// <summary>
        /// Scrolling speed.
        /// </summary>
        public Vector2 speed { get { return m_speed; } set { m_speed = value; } }
        [SerializeField]        
        private Vector2 m_speed;

        /// <summary>
        /// Scrolling Offset.
        /// </summary>
        public Vector2 offset { get { return m_offset; } set { m_offset = value; } }
        [SerializeField]        
        private Vector2 m_offset;

        /// <summary>
        /// Flag that tells the scrolling to use time scale.
        /// </summary>
        public bool useTimescale = true;

        /// <summary>
        /// Internals.
        /// </summary>
        private Rect m_uv;
        private Vector2 m_uv_pos;
        private bool m_has_target;

        /// <summary>
        /// CTOR.
        /// </summary>
        protected void Awake() {
            Graphic t = target;            
            m_uv = GetRect();
            m_uv_pos = new Vector2(m_uv.x,m_uv.y);
            m_has_target = t !=null;
        }

        protected Rect GetRect() {
            Graphic t = target;
            if(!t) return new Rect(0f,0f,1f,1f);
            if(t is RawImage) { RawImage g = (RawImage)t; return g.uvRect;           }
            //if(t is ImageHDR) { ImageHDR g = (ImageHDR)t; return g.bloomTextureRect; }
            return new Rect(0f,0f,1f,1f);
        }

        protected void SetRect(Rect r) {
            Graphic t = target;
            if(!t) return;
            if(t is RawImage) { RawImage g = (RawImage)t; g.uvRect = r;           }
            //if(t is ImageHDR) { ImageHDR g = (ImageHDR)t; g.bloomTextureRect = r; }            
        }

        /// <summary>
        /// Updates the scrolling.
        /// </summary>
        public void OnUpdate() {
            if (!gameObject.activeInHierarchy) return;
            if(!m_has_target) return;
            float dt = useTimescale ? Time.deltaTime : Time.unscaledDeltaTime;
            Rect uv = m_uv;
            uv.x += offset.x;
            uv.y += offset.y;
            uv.x -= m_uv_pos.x;
            uv.y -= m_uv_pos.y;            
            m_uv_pos += speed * dt;
            SetRect(uv);
        }

    }

}
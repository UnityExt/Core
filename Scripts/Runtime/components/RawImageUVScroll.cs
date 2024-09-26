using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace thelab.core {

    /// <summary>
    /// Component to scroll UV offsets of its targets.
    /// </summary>
    public class RawImageUVScroll : MonoBehaviour {

        /// <summary>
        /// List of targets. If empty, tries to fetch the owns Renderer.
        /// </summary>
        public RawImage target { get { RawImage t = m_target ? m_target : (m_target = GetComponent<RawImage>()); m_has_target = t!=null; return t; } set { m_target=value; m_has_target = value!=null; } }
        [SerializeField]
        private RawImage m_target;

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
        private bool m_has_target;

        /// <summary>
        /// CTOR.
        /// </summary>
        protected void Awake() {
            RawImage t = target;
            m_uv = new Rect(0f,0f,1f,1f);
            if(t) m_uv = t.uvRect;
            m_has_target = t !=null;
        }

        /// <summary>
        /// Updates the scrolling.
        /// </summary>
        protected void Update() {
            if (!enabled || !gameObject.activeInHierarchy) return;
            if(!m_has_target) return;
            float dt = useTimescale ? Time.deltaTime : Time.unscaledDeltaTime;
            Vector2 uv = new Vector2(m_uv.x,m_uv.y);            
            uv += offset;
            uv -= speed * dt;
            m_uv.x = uv.x;
            m_uv.y = uv.y;
            target.uvRect = m_uv;
        }

    }

}
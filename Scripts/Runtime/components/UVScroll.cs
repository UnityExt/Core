using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityExt.Core {

    /// <summary>
    /// Component to scroll UV offsets of its targets.
    /// </summary>
    public class UVScroll : ActivityBehaviour, IUpdateable {

        /// <summary>
        /// List of targets. If empty, tries to fetch the owns Renderer.
        /// </summary>
        public List<Renderer> targets;

        /// <summary>
        /// Scrolling speed.
        /// </summary>
        public Vector2 speed { get { return m_speed; } set { m_speed = p_target_speed = value; /*Tween.Kill(this);*/  } }
        [SerializeField]
        [HideInInspector]
        private Vector2 m_speed;

        /// <summary>
        /// Flag that tells the scrolling to use time scale.
        /// </summary>
        public bool useTimescale = true;

        /// <summary>
        /// Speed pulse.
        /// </summary>
        protected Vector2 p_target_speed;

        //public bool pulsate = true;
        //private float pulsePeriod = 1f;
        //private float pulseTimer = 1f;
        //private Vector2 m_pulseSpeed = new Vector2(0, 3f);
        //private float m_pulseStrength = 2f;
        //private float m_pulseDuration = 0.2f;

        /// <summary>
        /// CTOR.
        /// </summary>
        protected void Awake() {
            if (targets == null) targets = new List<Renderer>();
            if(targets.Count<=0) {
                Renderer r = GetComponent<Renderer>();
                if (r) targets.Add(r);
            }
            p_target_speed = speed;
                
        }

        /// <summary>
        /// Pumps the speed momentarely.
        /// </summary>
        /// <param name="p_speed"></param>
        /*
        public void Pulse(Vector2 p_speed,float p_strength, float p_duration) {
            Tween.Kill(this);
            float d = p_strength <= 0f ? 1f : (1f / p_strength);
            Tween.Add<Vector2>(this, "p_target_speed", p_speed, d, Cubic.Out);
            Tween.Add<Vector2>(this, "p_target_speed", m_speed,d,p_duration, Cubic.Out);
        }
        //*/
        /// <summary>
        /// Updates the scrolling.
        /// </summary>
        public void OnUpdate() {

            if (!enabled || !gameObject.activeInHierarchy) return;

            Vector2 spd = p_target_speed;
            spd.y = -spd.y;
            float dt = useTimescale ? Time.deltaTime : Time.unscaledDeltaTime;

            for(int i=0;i<targets.Count;i++) {
                Renderer it = targets[i];
                if (!it) continue;
                Material mat = it.sharedMaterial;
                Vector2 off = mat.mainTextureOffset;
                Vector2 off_n = off + (spd * dt);
                if(Mathf.Abs(off_n.x - off.x)<0.01f)
                if(Mathf.Abs(off_n.y - off.y)<0.01f) continue;
                mat.mainTextureOffset = off_n;
            }
            /*
            if (pulsate)
            {
                if (pulseTimer > 0)
                {
                    pulseTimer -= Time.deltaTime;
                }
                else
                {
                    Pulse(m_pulseSpeed, m_pulseStrength, m_pulseDuration);
                    pulseTimer = pulsePeriod;
                }
            }
            //*/
        }

    }

}
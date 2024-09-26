using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace UnityExt.Core {

    /// <summary>
    /// Component to oscilate position and rotation of targets. By adding speed and scale modifiers it is possible to compose cosine waves.
    /// </summary>
    public class OscilatorTransform : ActivityBehaviour, IUpdateable {

        /// <summary>
        /// List of targets.
        /// </summary>
        public List<Transform> targets;

        /// <summary>
        /// Offset angle per target.
        /// </summary>
        public Vector3 offset;

        /// <summary>
        /// Position amplitude.
        /// </summary>
        public Vector3 position;

        /// <summary>
        /// Rotation amplitude.
        /// </summary>
        public Vector3 rotation;

        /// <summary>
        /// Oscilation speed.
        /// </summary>
        public Vector3[] speed;

        /// <summary>
        /// Oscilation scale.
        /// </summary>
        public Vector3[] scale;

        /// <summary>
        /// Start position.
        /// </summary>
        public List<Vector3> startPosition;

        /// <summary>
        /// Start position.
        /// </summary>
        public List<Vector3> startRotation;

        /// <summary>
        /// Scale of random applied.
        /// </summary>
        public float randomScale;

        /// <summary>
        /// Generated Random.
        /// </summary>
        public Vector3[] randomSeeds;

        /// <summary>
        /// List of values.
        /// </summary>
        private List<Vector3> m_wave_values;
        private List<Transform> m_targets;

        /// <summary>
        /// CTOR.
        /// </summary>
        protected void Awake() {

            m_wave_values = new List<Vector3>();
            m_targets = new List<Transform>();

            //startPosition    = transform.localPosition;
            //startRotation    = transform.localEulerAngles;

            startPosition = new List<Vector3>();
            startRotation = new List<Vector3>();

            int len = Mathf.Min(speed.Length,scale.Length);
            randomSeeds = new Vector3[len];
            for (int i = 0;i < len;i++) {
                randomSeeds[i] = Random.insideUnitSphere;
            }
        }

        /// <summary>
        /// Updates the oscilator.
        /// </summary>
        public void OnUpdate() {

            Vector3 w = Vector3.zero;
            float t = Time.time * Mathf.Deg2Rad;
            int len = Mathf.Min(speed.Length,scale.Length);
            List<Transform> tl = m_targets;

            tl.Clear();
            if (targets.Count <= 0) tl.Add(transform); else tl.AddRange(targets);

            if (startPosition.Count != tl.Count) for (int i = 0;i < tl.Count;i++) startPosition.Add(tl[i].localPosition);
            if (startRotation.Count != tl.Count) for (int i = 0;i < tl.Count;i++) startRotation.Add(tl[i].localEulerAngles);

            m_wave_values.Clear();

            for (int j = 0;j < tl.Count;j++) {

                Vector3 off = (offset * ((float)j)) * Mathf.Deg2Rad;

                w = Vector3.zero;

                for (int i = 0;i < len;i++) {

                    float wx = Mathf.Sin((speed[i].x * t) + off.x);
                    float wy = Mathf.Sin((speed[i].y * t) + off.y);
                    float wz = Mathf.Sin((speed[i].z * t) + off.z);
                    wx *= scale[i].x * Mathf.Lerp(1f,randomSeeds[i].x * randomScale,Mathf.Clamp01(randomScale));
                    wy *= scale[i].y * Mathf.Lerp(1f,randomSeeds[i].y * randomScale,Mathf.Clamp01(randomScale));
                    wz *= scale[i].z * Mathf.Lerp(1f,randomSeeds[i].z * randomScale,Mathf.Clamp01(randomScale));
                    w.x += wx;
                    w.y += wy;
                    w.z += wz;

                    m_wave_values.Add(w);

                }

            }

            Transform tt;

            Vector3 pv = new Vector3();
            Vector3 rv = new Vector3();

            len = Mathf.Min(m_wave_values.Count,tl.Count);

            for (int i = 0;i < len;i++) {

                w = m_wave_values[i];

                pv.x = w.x * position.x;
                pv.y = w.y * position.y;
                pv.z = w.z * position.z;

                rv.x = w.x * rotation.x;
                rv.y = w.y * rotation.y;
                rv.z = w.z * rotation.z;

                tt = tl[i];
                tt.localPosition = startPosition[i] + pv;
                tt.localEulerAngles = startRotation[i] + rv;

            }


        }
    }

}
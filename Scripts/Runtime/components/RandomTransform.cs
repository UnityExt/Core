using UnityEngine;
using System.Collections;

namespace thelab.core {

    /// <summary>
    /// Class that do a simple randomization of parameter.
    /// </summary>
    public class RandomTransform : MonoBehaviour {

        /// <summary>
        /// Rotation min value
        /// </summary>
        public Vector3 minRotation;

        /// <summary>
        /// Rotation min value
        /// </summary>
        public Vector3 maxRotation;

        /// <summary>
        /// Position max value
        /// </summary>
        public Vector3 minPosition;

        /// <summary>
        /// Position max value
        /// </summary>
        public Vector3 maxPosition;

        /// <summary>
        /// Scale min value
        /// </summary>
        public Vector3 minScale;

        /// <summary>
        /// Scale max value
        /// </summary>
        public Vector3 maxScale;
        
        /// <summary>
        /// Apply the rotation on AWake
        /// </summary>
        public bool applyOnAwake;

        /// <summary>
        /// CTOR.
        /// </summary>
        protected void Awake() {
            if(applyOnAwake) Apply();
        }
        
        /// <summary>
        /// Apply random.
        /// </summary>
        public void Apply() {

            float v0;
            float v1;

            Vector3 v;

            v = transform.localEulerAngles;
            v0 = minRotation.x; v1 = maxRotation.x; v.x += Random.Range(v0,v1);
            v0 = minRotation.y; v1 = maxRotation.y; v.y += Random.Range(v0,v1);
            v0 = minRotation.z; v1 = maxRotation.z; v.z += Random.Range(v0,v1);
            transform.localEulerAngles = v;

            v = transform.localPosition;
            v0 = minPosition.x; v1 = maxPosition.x; v.x += Random.Range(v0,v1);
            v0 = minPosition.y; v1 = maxPosition.y; v.y += Random.Range(v0,v1);
            v0 = minPosition.z; v1 = maxPosition.z; v.z += Random.Range(v0,v1);
            transform.localPosition = v;

            v = transform.localScale;
            v0 = minScale.x; v1 = maxScale.x; v.x += Random.Range(v0,v1);
            v0 = minScale.y; v1 = maxScale.y; v.y += Random.Range(v0,v1);
            v0 = minScale.z; v1 = maxScale.z; v.z += Random.Range(v0,v1);
            transform.localScale = v;

        }

    }

}
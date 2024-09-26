using UnityEngine;
using System.Collections;

namespace UnityExt.Core {

    /// <summary>
    /// Class that do a simple rotation behaviour.
    /// </summary>
    public class RotatorTransform : ActivityBehaviour, IUpdateable {

        /// <summary>
        /// Rotation speed.
        /// </summary>
        public Vector3 speed;

        /// <summary>
        /// Starting angle.
        /// </summary>
        public Vector3 angle;

        /// <summary>
        /// CTOR.
        /// </summary>
        protected void Awake() {
            angle = transform.localEulerAngles;
        }

        // Update is called once per frame
        public void OnUpdate() {
            angle += Time.deltaTime * speed;
            transform.localEulerAngles = angle;
        }

        /// <summary>
        /// Clears the angle.
        /// </summary>
        public void Clear() {
            angle = Vector3.zero;
        }
    }

}
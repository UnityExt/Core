using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityExt.Core {

    public static class MathExt {

        #region Quaternion|Vector        
        /// <summary>
        /// Helper to return a Vector4 from a Quaternion.
        /// </summary>
        /// <param name="p_ref">Quaternion instance.</param>
        /// <returns>Vector4 with the quaternion data.</returns>
        static public Vector4 ToVector4(this Quaternion p_ref) { return new Vector4(p_ref.x,p_ref.y,p_ref.z,p_ref.w);  }

        /// <summary>
        /// Helper to return a Vector3 from a Vector4.
        /// </summary>
        /// <param name="p_ref">Vector3 instance.</param>
        /// <returns>Vector4 with the vector3 data and 'w'.</returns>
        static public Vector4 ToVector4(this Vector3 p_ref,float p_w=1f) { return new Vector4(p_ref.x,p_ref.y,p_ref.z,p_w);  }

        /// <summary>
        /// Helper to return a Quaternion from a Vector4.
        /// </summary>
        /// <param name="p_ref">Vector4 instance.</param>
        /// <returns>Quaternion with the Vector4 data.</returns>
        static public Quaternion ToQuaternion(this Vector4 p_ref,bool p_normalize=true) { Quaternion q = new Quaternion(p_ref.x,p_ref.y,p_ref.z,p_ref.w); if(p_normalize)q.Normalize(); return q; }

        /// <summary>
        /// Helper to return a Vector3 from a Vector4.
        /// </summary>
        /// <param name="p_ref">Vector4 instance.</param>
        /// <returns>Vector3 with the Vector4 data.</returns>
        static public Vector3 ToVector3(this Vector4 p_ref) { Vector3 res = new Vector3(p_ref.x,p_ref.y,p_ref.z); return res; }

        /// <summary>
        /// Helper to return a Vector2 from a Vector3.
        /// </summary>
        /// <param name="p_ref">Vector3 instance.</param>
        /// <returns>Vector3 with the Vector2 data.</returns>
        static public Vector2 ToVector2(this Vector3 p_ref) { Vector2 res = new Vector3(p_ref.x,p_ref.y); return res; }
        #endregion

    }
}

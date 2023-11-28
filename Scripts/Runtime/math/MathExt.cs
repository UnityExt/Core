using System;
using System.Collections;
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
        static public Vector4 ToVector4(this Quaternion p_ref) { return new Vector4(p_ref.x,p_ref.y,p_ref.z,p_ref.w); }

        /// <summary>
        /// Helper to return a Vector3 from a Vector4.
        /// </summary>
        /// <param name="p_ref">Vector3 instance.</param>
        /// <returns>Vector4 with the vector3 data and 'w'.</returns>
        static public Vector4 ToVector4(this Vector3 p_ref,float p_w = 1f) { return new Vector4(p_ref.x,p_ref.y,p_ref.z,p_w); }

        /// <summary>
        /// Helper to return a Quaternion from a Vector4.
        /// </summary>
        /// <param name="p_ref">Vector4 instance.</param>
        /// <returns>Quaternion with the Vector4 data.</returns>
        static public Quaternion ToQuaternion(this Vector4 p_ref,bool p_normalize = true) { Quaternion q = new Quaternion(p_ref.x,p_ref.y,p_ref.z,p_ref.w); if (p_normalize) q.Normalize(); return q; }

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

        #region Random
        //Credits: https://bitbucket.org/Superbest/superbest-random/src/master/Superbest%20random/RandomExtensions.cs

        /// <summary>
        ///   Generates normally distributed numbers. Each operation makes two Gaussians for the price of one, and apparently they can be cached or something for better performance, but who cares.
        /// </summary>
        /// <param name="r"></param>
        /// <param name = "mu">Mean of the distribution</param>
        /// <param name = "sigma">Standard deviation</param>
        /// <returns></returns>
        public static double NextGaussian(this System.Random r,double mu = 0,double sigma = 1) {
            var u1 = r.NextDouble();
            var u2 = r.NextDouble();
            
            var rand_std_normal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                                Math.Sin(2.0 * Math.PI * u2);

            var rand_normal = mu + sigma * rand_std_normal;

            return rand_normal;
        }

        /// <summary>
        ///   Generates values from a triangular distribution.
        /// </summary>
        /// <remarks>
        /// See http://en.wikipedia.org/wiki/Triangular_distribution for a description of the triangular probability distribution and the algorithm for generating one.
        /// </remarks>
        /// <param name="r"></param>
        /// <param name = "a">Minimum</param>
        /// <param name = "b">Maximum</param>
        /// <param name = "c">Mode (most frequent value)</param>
        /// <returns></returns>
        public static double NextTriangular(this System.Random r,double a,double b,double c) {
            var u = r.NextDouble();
            double d_ba_inv = (b - a);
            if (Math.Abs(d_ba_inv) <= 0.0000000001) d_ba_inv = 0.0;
            return u < ((c - a) * d_ba_inv)
                        ? a + Math.Sqrt(u * (b - a) * (c - a))
                        : b - Math.Sqrt((1 - u) * (b - a) * (b - c));
        }

        /// <summary>
        ///   Equally likely to return true or false. Uses <see cref="Random.Next()"/>.
        /// </summary>
        /// <returns></returns>
        public static bool NextBoolean(this System.Random r) {
            return r.Next(2) > 0;
        }

        /// <summary>
        ///   Shuffles a list in O(n) time by using the Fisher-Yates/Knuth algorithm.
        /// </summary>
        /// <param name="r"></param>
        /// <param name = "list"></param>
        public static void Shuffle(this System.Random r,IList list) {
            for (var i = 0;i < list.Count;i++) {
                var j = r.Next(0,i + 1);
                var temp = list[j];
                list[j] = list[i];
                list[i] = temp;
            }
        }

        /// <summary>
        /// Returns n unique random numbers in the range [1, n], inclusive. 
        /// This is equivalent to getting the first n numbers of some random permutation of the sequential numbers from 1 to max. 
        /// Runs in O(k^2) time.
        /// </summary>
        /// <param name="rand"></param>
        /// <param name="n">Maximum number possible.</param>
        /// <param name="k">How many numbers to return.</param>
        /// <returns></returns>
        public static int[] Permutation(this System.Random rand,int n,int k) {
            var result = new List<int>();
            var sorted = new SortedSet<int>();

            for (var i = 0;i < k;i++) {
                var r = rand.Next(1,n + 1 - i);

                foreach (var q in sorted)
                    if (r >= q) r++;

                result.Add(r);
                sorted.Add(r);
            }

            return result.ToArray();
        }
        #endregion
    }
        
}

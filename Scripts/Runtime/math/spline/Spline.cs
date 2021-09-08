using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityExt.Core {

    /// <summary>
    /// Base class for all spline implementations.
    /// </summary>
    [Serializable]
    public abstract class Spline {

        #region static

        #region Auxiliary

        /// <summary>
        /// Internals
        /// </summary>
        static private float [] rk4_step    = new float [1000];
        static private float [] rk4_value   = new float [1000];
        static private double[] rk4_step_d  = new double[1000];
        static private double[] rk4_value_d = new double[1000];

        #region Polynoms
        static double     Polynom2(double     a,double     b,double     c,double t) { double t2 = t*t; return (a*t2)+(b*t)+c; }
        static float      Polynom2(float      a,float      b,float      c,float  t) { float  t2 = t*t; return (a*t2)+(b*t)+c; }        
        static Vector2    Polynom2(Vector2    a,Vector2    b,Vector2    c,float  t) { float  t2 = t*t; return (a*t2)+(b*t)+c; }
        static Vector3    Polynom2(Vector3    a,Vector3    b,Vector3    c,float  t) { float  t2 = t*t; return (a*t2)+(b*t)+c; }
        static Vector4    Polynom2(Vector4    a,Vector4    b,Vector4    c,float  t) { float  t2 = t*t; return (a*t2)+(b*t)+c; }
        static Quaternion Polynom2(Quaternion a,Quaternion b,Quaternion c,float  t) { return Polynom2(a.ToVector4(),b.ToVector4(),c.ToVector4(),t).ToQuaternion(true); }

        static double     PolynomDeriv2(double     a,double     b,double t) { double t2 = t*t; return (2f*a*t)+(b); }
        static float      PolynomDeriv2(float      a,float      b,float  t) { float  t2 = t*t; return (2f*a*t)+(b); }        
        static Vector2    PolynomDeriv2(Vector2    a,Vector2    b,float  t) { float  t2 = t*t; return (2f*a*t)+(b); }
        static Vector3    PolynomDeriv2(Vector3    a,Vector3    b,float  t) { float  t2 = t*t; return (2f*a*t)+(b); }
        static Vector4    PolynomDeriv2(Vector4    a,Vector4    b,float  t) { float  t2 = t*t; return (2f*a*t)+(b); }
        static Quaternion PolynomDeriv2(Quaternion a,Quaternion b,float  t) { return PolynomDeriv2(a.ToVector4(),b.ToVector4(),t).ToQuaternion(true); }

        static double     Polynom3(double     a,double     b,double     c,double     d,double t) { double t2 = t*t,t3 = t2*t; return (a*t3)+(b*t2)+(c*t)+d; }
        static float      Polynom3(float      a,float      b,float      c,float      d,float  t) { float  t2 = t*t,t3 = t2*t; return (a*t3)+(b*t2)+(c*t)+d; }        
        static Vector2    Polynom3(Vector2    a,Vector2    b,Vector2    c,Vector2    d,float  t) { float  t2 = t*t,t3 = t2*t; return (a*t3)+(b*t2)+(c*t)+d; }
        static Vector3    Polynom3(Vector3    a,Vector3    b,Vector3    c,Vector3    d,float  t) { float  t2 = t*t,t3 = t2*t; return (a*t3)+(b*t2)+(c*t)+d; }
        static Vector4    Polynom3(Vector4    a,Vector4    b,Vector4    c,Vector4    d,float  t) { float  t2 = t*t,t3 = t2*t; return (a*t3)+(b*t2)+(c*t)+d; }
        static Quaternion Polynom3(Quaternion a,Quaternion b,Quaternion c,Quaternion d,float  t) { return Polynom3(a.ToVector4(),b.ToVector4(),c.ToVector4(),d.ToVector4(),t).ToQuaternion(true); }

        static double     PolynomDeriv3(double     a,double     b,double     c,double t) { double t2 = t*t; return (3f*a*t2)+(2f*b*t)+(c); }
        static float      PolynomDeriv3(float      a,float      b,float      c,float  t) { float  t2 = t*t; return (3f*a*t2)+(2f*b*t)+(c); }        
        static Vector2    PolynomDeriv3(Vector2    a,Vector2    b,Vector2    c,float  t) { float  t2 = t*t; return (3f*a*t2)+(2f*b*t)+(c); }
        static Vector3    PolynomDeriv3(Vector3    a,Vector3    b,Vector3    c,float  t) { float  t2 = t*t; return (3f*a*t2)+(2f*b*t)+(c); }
        static Vector4    PolynomDeriv3(Vector4    a,Vector4    b,Vector4    c,float  t) { float  t2 = t*t; return (3f*a*t2)+(2f*b*t)+(c); }
        static Quaternion PolynomDeriv3(Quaternion a,Quaternion b,Quaternion c,float  t) { return PolynomDeriv3(a.ToVector4(),b.ToVector4(),c.ToVector4(),t).ToQuaternion(true); }
        #endregion

        #endregion

        #region CatmullRom
        /// <summary>
        /// CatmulRom is a widely used spline due its useful properties for animation and interpolation across space.
        /// The spline passes through all of the control points.
        /// The spline is C1 continuous, meaning that there are no discontinuities in the tangent direction and magnitude.
        /// The spline is not C2 continuous.  The second derivative is linearly interpolated within each segment, causing the curvature to vary linearly over the length of the segment.
        /// Points on a segment may lie outside of the domain of P1 -> P2.
        /// Reference: https://www.mvps.org/directx/articles/catmull/
        /// Reference: http://graphics.cs.cmu.edu/nsp/course/15-462/Fall04/assts/catmullRom.pdf
        /// </summary>
        /// <param name="t">Parametric Factor</param>
        /// <param name="v0">Control Point 1</param>
        /// <param name="v1">Control Point 2</param>
        /// <param name="v2">Control Point 3</param>
        /// <param name="v3">Control Point 4</param>
        /// <param name="m">Curve Tension</param>
        /// <returns>Interpolated value for the catmull rom segment</returns>        
        /// Equation
        /// | 0  1   0     0| |v0|   |v1                                  |  |1 t t² t³| 
        /// |-m  0   m     0| |v1|   |v0*-m + v2*m                        | 
        /// |2m  m-3 3-2m -m| |v2| = |v0*2m + v1*(m-3) + v2*(3-2m) + v3*-m|              = 
        /// |-m  2-m m-2   m| |v3|   |v0*-m + v1*(2-m) + v2*(m- 2) + v3* m|        
        static public float      CatmullRom(float  t,float       v0,float       v1,float       v2,float       v3,float  m) { return Polynom3((v0 * -m + v1 * (-m + 2f) + v2 * (m - 2f) + v3 * m),(v0 * m * 2f + v1 * (m - 3f) + v2 * (-2f * m + 3f) + v3 * -m),(v0 * -m) + v2 * m,v1,t); }
        static public double     CatmullRom(double t,double      v0,double      v1,double      v2,double      v3,double m) { return Polynom3((v0 * -m + v1 * (-m + 2f) + v2 * (m - 2f) + v3 * m),(v0 * m * 2f + v1 * (m - 3f) + v2 * (-2f * m + 3f) + v3 * -m),(v0 * -m) + v2 * m,v1,t); }
        static public Vector2    CatmullRom(float  t,Vector2     v0,Vector2     v1,Vector2     v2,Vector2     v3,float  m) { return Polynom3((v0 * -m + v1 * (-m + 2f) + v2 * (m - 2f) + v3 * m),(v0 * m * 2f + v1 * (m - 3f) + v2 * (-2f * m + 3f) + v3 * -m),(v0 * -m) + v2 * m,v1,t); }
        static public Vector3    CatmullRom(float  t,Vector3     v0,Vector3     v1,Vector3     v2,Vector3     v3,float  m) { return Polynom3((v0 * -m + v1 * (-m + 2f) + v2 * (m - 2f) + v3 * m),(v0 * m * 2f + v1 * (m - 3f) + v2 * (-2f * m + 3f) + v3 * -m),(v0 * -m) + v2 * m,v1,t); }
        static public Vector4    CatmullRom(float  t,Vector4     v0,Vector4     v1,Vector4     v2,Vector4     v3,float  m) { return Polynom3((v0 * -m + v1 * (-m + 2f) + v2 * (m - 2f) + v3 * m),(v0 * m * 2f + v1 * (m - 3f) + v2 * (-2f * m + 3f) + v3 * -m),(v0 * -m) + v2 * m,v1,t); }
        static public Quaternion CatmullRom(float  t,Quaternion  v0,Quaternion  v1,Quaternion  v2,Quaternion  v3,float  m) { 
            Vector4 q0 = v0.ToVector4(),q1 = v1.ToVector4(),q2 = v2.ToVector4(),q3 = v3.ToVector4();
            return Polynom3((q0 * -m + q1 * (-m + 2f) + q2 * (m - 2f) + q3 * m),(q0 * m * 2f + q1 * (m - 3f) + q2 * (-2f * m + 3f) + q3 * -m),(q0 * -m) + q2 * m,q1,t).ToQuaternion(true); 
        }

        /// <summary>
        /// CatmullRom Derivative extracted from cubic polynomial derivative formula in the format [3at² + 2bt + c]
        /// </summary>
        /// <param name="t">Parametric Factor</param>
        /// <param name="v0">Control Point 1</param>
        /// <param name="v1">Control Point 2</param>
        /// <param name="v2">Control Point 3</param>
        /// <param name="v3">Control Point 4</param>
        /// <param name="m">Curve Tension</param>
        /// <returns>Derivative of the catmull rom at the desired parametric value.</returns>
        static public float      CatmullRomDeriv(float  t,float       v0,float       v1,float       v2,float       v3,float  m) { return PolynomDeriv3((v0 * -m + v1 * (-m + 2f) + v2 * (m - 2f) + v3 * m),(v0 * m * 2f + v1 * (m - 3f) + v2 * (-2f * m + 3f) + v3 * -m),(v0 * -m) + v2 * m,t); }
        static public double     CatmullRomDeriv(double t,double      v0,double      v1,double      v2,double      v3,double m) { return PolynomDeriv3((v0 * -m + v1 * (-m + 2f) + v2 * (m - 2f) + v3 * m),(v0 * m * 2f + v1 * (m - 3f) + v2 * (-2f * m + 3f) + v3 * -m),(v0 * -m) + v2 * m,t); }
        static public Vector2    CatmullRomDeriv(float  t,Vector2     v0,Vector2     v1,Vector2     v2,Vector2     v3,float  m) { return PolynomDeriv3((v0 * -m + v1 * (-m + 2f) + v2 * (m - 2f) + v3 * m),(v0 * m * 2f + v1 * (m - 3f) + v2 * (-2f * m + 3f) + v3 * -m),(v0 * -m) + v2 * m,t); }
        static public Vector3    CatmullRomDeriv(float  t,Vector3     v0,Vector3     v1,Vector3     v2,Vector3     v3,float  m) { return PolynomDeriv3((v0 * -m + v1 * (-m + 2f) + v2 * (m - 2f) + v3 * m),(v0 * m * 2f + v1 * (m - 3f) + v2 * (-2f * m + 3f) + v3 * -m),(v0 * -m) + v2 * m,t); }
        static public Vector4    CatmullRomDeriv(float  t,Vector4     v0,Vector4     v1,Vector4     v2,Vector4     v3,float  m) { return PolynomDeriv3((v0 * -m + v1 * (-m + 2f) + v2 * (m - 2f) + v3 * m),(v0 * m * 2f + v1 * (m - 3f) + v2 * (-2f * m + 3f) + v3 * -m),(v0 * -m) + v2 * m,t); }
        static public Quaternion CatmullRomDeriv(float  t,Quaternion  v0,Quaternion  v1,Quaternion  v2,Quaternion  v3,float  m) { 
            Vector4 q0 = v0.ToVector4(),q1 = v1.ToVector4(),q2 = v2.ToVector4(),q3 = v3.ToVector4();
            return PolynomDeriv3((q0 * -m + q1 * (-m + 2f) + q2 * (m - 2f) + q3 * m),(q0 * m * 2f + q1 * (m - 3f) + q2 * (-2f * m + 3f) + q3 * -m),(q0 * -m) + q2 * m,t).ToQuaternion(true); 
        }        

        /// <summary>
        /// CatmullRom ArcLength extracted from the equation A(t) = sqrt(1 + deriv²)
        /// Reference: https://tutorial.math.lamar.edu/classes/calcii/arclength.aspx
        /// </summary>
        /// <param name="t">Parametric Factor</param>
        /// <param name="v0">Control Point 1</param>
        /// <param name="v1">Control Point 2</param>
        /// <param name="v2">Control Point 3</param>
        /// <param name="v3">Control Point 4</param>
        /// <param name="m">Curve Tension</param>
        /// <returns>Arc Length of the catmull rom at the desired parametric value.</returns>
        static public float  CatmullRomArcLength(float  t,float      v0,float      v1,float      v2,float      v3,float  m)  { float  dv = Math.Abs(CatmullRomDeriv(t,v0,v1,v2,v3,m)); return Mathf.Sqrt(1f+(dv*dv)); }
        static public double CatmullRomArcLength(double t,double     v0,double     v1,double     v2,double     v3,double m)  { double dv = Math.Abs(CatmullRomDeriv(t,v0,v1,v2,v3,m)); return Math.Sqrt (1f+(dv*dv)); }        
        static public float  CatmullRomArcLength(float  t,Vector2    v0,Vector2    v1,Vector2    v2,Vector2    v3,float  m)  { float  dv = CatmullRomDeriv(t,v0,v1,v2,v3,m).magnitude; return Mathf.Sqrt(1f+(dv*dv)); }
        static public float  CatmullRomArcLength(float  t,Vector3    v0,Vector3    v1,Vector3    v2,Vector3    v3,float  m)  { float  dv = CatmullRomDeriv(t,v0,v1,v2,v3,m).magnitude; return Mathf.Sqrt(1f+(dv*dv)); }
        static public float  CatmullRomArcLength(float  t,Vector4    v0,Vector4    v1,Vector4    v2,Vector4    v3,float  m)  { float  dv = CatmullRomDeriv(t,v0,v1,v2,v3,m).magnitude; return Mathf.Sqrt(1f+(dv*dv)); }
        static public float  CatmullRomArcLength(float  t,Quaternion v0,Quaternion v1,Quaternion v2,Quaternion v3,float  m)  { float  dv = CatmullRomDeriv(t,v0.ToVector4(),v1.ToVector4(),v2.ToVector4(),v3.ToVector4(),m).magnitude; return Mathf.Sqrt(1f+(dv*dv)); }

        #region CatmullRomLength
        /// <summary>
        /// Calculates the CatmullRom segment total length using RungeKutta (RK4) approximation.
        /// </summary>
        /// <param name="p_steps">Number of steps of approximation, the bigger the more precise bu slower</param>
        /// <param name="v0">Control Point 1</param>
        /// <param name="v1">Control Point 2</param>
        /// <param name="v2">Control Point 3</param>
        /// <param name="v3">Control Point 4</param>
        /// <param name="m">Curve Tension</param>
        /// <returns>Curve length for the specified set of control points.</returns>
        static public float CatmullRomLength(float  v0,float v1,float v2,float v3,float m,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * CatmullRomArcLength(vs    ,v0,v1,v2,v3,m);
                dy2 = dt * CatmullRomArcLength(vshdt ,v0,v1,v2,v3,m);                
                dy4 = dt * CatmullRomArcLength(vsdt  ,v0,v1,v2,v3,m);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        static public double CatmullRomLength(double v0,double v1,double v2,double v3,float m,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step_d.Length-1);
            double dt  = 1.0/(double)p_steps;
            double hdt = dt*0.5;
            // RK4 Variables
            double dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step_d[0] = rk4_value_d[0] = 0.0;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                double vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * CatmullRomArcLength(vs    ,v0,v1,v2,v3,m);
                dy2 = dt * CatmullRomArcLength(vshdt ,v0,v1,v2,v3,m);                
                dy4 = dt * CatmullRomArcLength(vsdt  ,v0,v1,v2,v3,m);
                rk4_step_d[i+1] = vsdt;
                rk4_value_d[i+1] = rk4_value_d[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value_d[p_steps];
        }
        static public float CatmullRomLength(Vector2 v0,Vector2 v1,Vector2 v2,Vector2 v3,float m,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * CatmullRomArcLength(vs    ,v0,v1,v2,v3,m);
                dy2 = dt * CatmullRomArcLength(vshdt ,v0,v1,v2,v3,m);                
                dy4 = dt * CatmullRomArcLength(vsdt  ,v0,v1,v2,v3,m);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        static public float CatmullRomLength(Vector3 v0,Vector3 v1,Vector3 v2,Vector3 v3,float m,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * CatmullRomArcLength(vs    ,v0,v1,v2,v3,m);
                dy2 = dt * CatmullRomArcLength(vshdt ,v0,v1,v2,v3,m);                
                dy4 = dt * CatmullRomArcLength(vsdt  ,v0,v1,v2,v3,m);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        static public float CatmullRomLength(Vector4 v0,Vector4 v1,Vector4 v2,Vector4 v3,float m,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * CatmullRomArcLength(vs    ,v0,v1,v2,v3,m);
                dy2 = dt * CatmullRomArcLength(vshdt ,v0,v1,v2,v3,m);                
                dy4 = dt * CatmullRomArcLength(vsdt  ,v0,v1,v2,v3,m);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        static public float CatmullRomLength(Quaternion v0,Quaternion v1,Quaternion v2,Quaternion v3,float m,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * CatmullRomArcLength(vs    ,v0,v1,v2,v3,m);
                dy2 = dt * CatmullRomArcLength(vshdt ,v0,v1,v2,v3,m);                
                dy4 = dt * CatmullRomArcLength(vsdt  ,v0,v1,v2,v3,m);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        #endregion

        #endregion

        #region Bezier3   
        /// <summary>
        /// Four points v0, m0, m1 and v1 in the plane or in higher-dimensional space define a cubic Bézier curve.
        /// The curve starts at v0 going toward m0 and arrives at v1 coming from the direction of m1.
        /// Usually, it will not pass through m0 or m1; these points are only there to provide directional information. 
        /// The distance between m0 and m1 determines "how far" and "how fast" the curve moves towards m0 before turning towards m1.
        /// Reference: https://en.wikipedia.org/wiki/B%C3%A9zier_curve        
        /// </summary>
        /// <param name="t">Interpolation factor</param>
        /// <param name="v0">Control Point 1</param>
        /// <param name="v1">Control Point 2</param>
        /// <param name="m0">Tangent Point 1</param>
        /// <param name="m1">Tangent Point 2</param>        
        /// Equation
        /// | 1  0  0  0| |v0|   |v0                        |  |1 t t² t³| 
        /// |-3  3  0  0| |m0|   |v0*-3 + m0* 3             | 
        /// | 3 -6  3  0| |m1| = |v0* 3 + m0*-6 + m1* 3     |
        /// |-1  3 -3  1| |v1|   |v0*-1 + m0* 3 + m1*-3 + v1|        
        /// <returns>Interolated value of the cubic bezier</returns>
        static public float      Bezier3(float  t,float      v0,float      v1,float      m0,float      m1) { return Polynom3(-v0 + (m0*3f) + (m1*-3f) + v1,(v0*3f)+(m0*-6f)+(m1*3f),(v0*-3f)+(m0*3f),v0,t); }
        static public double     Bezier3(double t,double     v0,double     v1,double     m0,double     m1) { return Polynom3(-v0 + (m0*3f) + (m1*-3f) + v1,(v0*3f)+(m0*-6f)+(m1*3f),(v0*-3f)+(m0*3f),v0,t); }
        static public Vector2    Bezier3(float  t,Vector2    v0,Vector2    v1,Vector2    m0,Vector2    m1) { return Polynom3(-v0 + (m0*3f) + (m1*-3f) + v1,(v0*3f)+(m0*-6f)+(m1*3f),(v0*-3f)+(m0*3f),v0,t); }
        static public Vector3    Bezier3(float  t,Vector3    v0,Vector3    v1,Vector3    m0,Vector3    m1) { return Polynom3(-v0 + (m0*3f) + (m1*-3f) + v1,(v0*3f)+(m0*-6f)+(m1*3f),(v0*-3f)+(m0*3f),v0,t); }
        static public Vector4    Bezier3(float  t,Vector4    v0,Vector4    v1,Vector4    m0,Vector4    m1) { return Polynom3(-v0 + (m0*3f) + (m1*-3f) + v1,(v0*3f)+(m0*-6f)+(m1*3f),(v0*-3f)+(m0*3f),v0,t); }
        static public Quaternion Bezier3(float t,Quaternion v0,Quaternion v1,Quaternion m0,Quaternion m1) {
            Vector4 q0 = v0.ToVector4(), q1 = v1.ToVector4(), qm0 = m0.ToVector4(), qm1 = m1.ToVector4();
            return Polynom3(-q0 + (qm0*3f) + (qm1*-3f) + q1,(q0*3f)+(qm0*-6f)+(qm1*3f),(q0*-3f)+(qm0*3f),q0,t).ToQuaternion(true);
        }

        /// <summary>
        ///  Cubic Bezier Derivative extracted from cubic polynomial derivative formula in the format [3at² + 2bt + c]
        /// </summary>
        /// <param name="t">Parametric Factor</param>
        /// <param name="v0">Control Point 1</param>
        /// <param name="v1">Control Point 2</param>
        /// <param name="m0">Tangent Point 1</param>
        /// <param name="m1">Tangent Point 2</param>        
        /// <returns>First order derivative of the cubic bezier.</returns>
        static public float      Bezier3Deriv(float  t,float      v0,float      v1,float      m0,float      m1) { return PolynomDeriv3(-v0 + (m0*3f) + (m1*-3f) + v1,(v0*3f)+(m0*-6f)+(m1*3f),(v0*-3f)+(m0*3f),t); }
        static public double     Bezier3Deriv(double t,double     v0,double     v1,double     m0,double     m1) { return PolynomDeriv3(-v0 + (m0*3f) + (m1*-3f) + v1,(v0*3f)+(m0*-6f)+(m1*3f),(v0*-3f)+(m0*3f),t); }
        static public Vector2    Bezier3Deriv(float  t,Vector2    v0,Vector2    v1,Vector2    m0,Vector2    m1) { return PolynomDeriv3(-v0 + (m0*3f) + (m1*-3f) + v1,(v0*3f)+(m0*-6f)+(m1*3f),(v0*-3f)+(m0*3f),t); }
        static public Vector3    Bezier3Deriv(float  t,Vector3    v0,Vector3    v1,Vector3    m0,Vector3    m1) { return PolynomDeriv3(-v0 + (m0*3f) + (m1*-3f) + v1,(v0*3f)+(m0*-6f)+(m1*3f),(v0*-3f)+(m0*3f),t); }
        static public Vector4    Bezier3Deriv(float  t,Vector4    v0,Vector4    v1,Vector4    m0,Vector4    m1) { return PolynomDeriv3(-v0 + (m0*3f) + (m1*-3f) + v1,(v0*3f)+(m0*-6f)+(m1*3f),(v0*-3f)+(m0*3f),t); }
        static public Quaternion Bezier3Deriv(float  t,Quaternion v0,Quaternion v1,Quaternion m0,Quaternion m1) { 
            Vector4 q0 = v0.ToVector4(),q1 = v1.ToVector4(),qm0 = m0.ToVector4(),qm1 = m1.ToVector4();
            return PolynomDeriv3(-q0 + (qm0*3f) + (qm1*-3f) + q1,(q0*3f)+(qm0*-6f)+(qm1*3f),(q0*-3f)+(qm0*3f),t).ToQuaternion(true);             
        }

        /// <summary>
        /// CubicBezier ArcLength extracted from the equation A(t) = sqrt(1 + deriv²)
        /// Reference: https://tutorial.math.lamar.edu/classes/calcii/arclength.aspx
        /// </summary>
        /// <param name="t">Parametric Factor</param>
        /// <param name="v0">Control Point 1</param>
        /// <param name="v1">Control Point 2</param>
        /// <param name="m0">Tangent Point 1</param>
        /// <param name="m1">Tangent Point 2</param>        
        /// <returns>Arc Length of the cubic bezier at the desired parametric value.</returns>
        static public float  Bezier3ArcLength(float  t,float      v0,float      v1,float      m0,float      m1)  { float  dv = Math.Abs(Bezier3Deriv(t,v0,v1,m0,m1)); return Mathf.Sqrt(1f +(dv*dv));  }
        static public double Bezier3ArcLength(double t,double     v0,double     v1,double     m0,double     m1)  { double dv = Math.Abs(Bezier3Deriv(t,v0,v1,m0,m1)); return Math. Sqrt(1.0+(dv*dv));  }
        static public float  Bezier3ArcLength(float  t,Vector2    v0,Vector2    v1,Vector2    m0,Vector2    m1)  { float  dv = Bezier3Deriv(t,v0,v1,m0,m1).magnitude; return Mathf.Sqrt(1f +(dv*dv));  }
        static public float  Bezier3ArcLength(float  t,Vector3    v0,Vector3    v1,Vector3    m0,Vector3    m1)  { float  dv = Bezier3Deriv(t,v0,v1,m0,m1).magnitude; return Mathf.Sqrt(1f +(dv*dv));  }
        static public float  Bezier3ArcLength(float  t,Vector4    v0,Vector4    v1,Vector4    m0,Vector4    m1)  { float  dv = Bezier3Deriv(t,v0,v1,m0,m1).magnitude; return Mathf.Sqrt(1f +(dv*dv));  }
        static public float  Bezier3ArcLength(float  t,Quaternion v0,Quaternion v1,Quaternion m0,Quaternion m1)  { float  dv = Bezier3Deriv(t,v0.ToVector4(),v1.ToVector4(),m0.ToVector4(),m1.ToVector4()).magnitude; return Mathf.Sqrt(1f+(dv*dv)); }

        #region Bezier3Length
        /// <summary>
        /// Calculates the cubic bezier segment total length using RungeKutta (RK4) approximation.
        /// </summary>
        /// <param name="p_steps">Number of steps of approximation, the bigger the more precise bu slower</param>
        /// <param name="t">Parametric Factor</param>
        /// <param name="v0">Control Point 1</param>
        /// <param name="v1">Control Point 2</param>
        /// <param name="m0">Tangent Point 1</param>
        /// <param name="m1">Tangent Point 2</param>        
        /// <returns>Curve length for the specified set of control points.</returns>
        static public float Bezier3Length(float  v0,float v1,float m0,float m1,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * Bezier3ArcLength(vs    ,v0,v1,m0,m1);
                dy2 = dt * Bezier3ArcLength(vshdt ,v0,v1,m0,m1);
                dy4 = dt * Bezier3ArcLength(vsdt  ,v0,v1,m0,m1);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        static public double Bezier3Length(double v0,double v1,double m0,double m1,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step_d.Length-1);
            double dt  = 1.0/(double)p_steps;
            double hdt = dt*0.5;
            // RK4 Variables
            double dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step_d[0] = rk4_value_d[0] = 0.0;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                double vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * Bezier3ArcLength(vs    ,v0,v1,m0,m1);
                dy2 = dt * Bezier3ArcLength(vshdt ,v0,v1,m0,m1);
                dy4 = dt * Bezier3ArcLength(vsdt  ,v0,v1,m0,m1);
                rk4_step_d[i+1] = vsdt;
                rk4_value_d[i+1] = rk4_value_d[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value_d[p_steps];
        }
        static public float Bezier3Length(Vector2 v0,Vector2 v1,Vector2 m0,Vector2 m1,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * Bezier3ArcLength(vs    ,v0,v1,m0,m1);
                dy2 = dt * Bezier3ArcLength(vshdt ,v0,v1,m0,m1);
                dy4 = dt * Bezier3ArcLength(vsdt  ,v0,v1,m0,m1);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        static public float Bezier3Length(Vector3 v0,Vector3 v1,Vector3 m0,Vector3 m1,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * Bezier3ArcLength(vs    ,v0,v1,m0,m1);
                dy2 = dt * Bezier3ArcLength(vshdt ,v0,v1,m0,m1);
                dy4 = dt * Bezier3ArcLength(vsdt  ,v0,v1,m0,m1);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        static public float Bezier3Length(Vector4 v0,Vector4 v1,Vector4 m0,Vector4 m1,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * Bezier3ArcLength(vs    ,v0,v1,m0,m1);
                dy2 = dt * Bezier3ArcLength(vshdt ,v0,v1,m0,m1);
                dy4 = dt * Bezier3ArcLength(vsdt  ,v0,v1,m0,m1);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        static public float Bezier3Length(Quaternion v0,Quaternion v1,Quaternion m0,Quaternion m1,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * Bezier3ArcLength(vs    ,v0,v1,m0,m1);
                dy2 = dt * Bezier3ArcLength(vshdt ,v0,v1,m0,m1);
                dy4 = dt * Bezier3ArcLength(vsdt  ,v0,v1,m0,m1);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        #endregion

        #endregion

        #region Bezier2        
        /// <summary>
        /// A quadratic Bézier curve is the path traced by the function given points v0, m0, and v1,
        /// </summary>
        /// <param name="t">Interpolation Factor</param>
        /// <param name="v0">Control Point 1</param>
        /// <param name="v1">Control Point 2</param>
        /// <param name="m0">Tangent Point 1 & 2</param>
        /// Reference: https://en.wikipedia.org/wiki/B%C3%A9zier_curve
        /// <returns>Interpolated value of the quadratic bezier</returns>
        /// Equation
        /// | 1  0  0| |v0|   |v0                |  |1 t t²| 
        /// |-2  2  0| |m0|   |v0*-2 + m0* 2     | 
        /// | 1 -2  1| |v1| = |v0    + m0*-2 + v1|        
        /// <returns>Interolated value of the cubic bezier</returns>
        static public float      Bezier2(float  t,float       v0,float       v1,float       m0) { return Polynom2(v0 + (m0*-2f) + v1,(v0*-2f)+(m0*2f),v0,t); }
        static public double     Bezier2(double t,double      v0,double      v1,double      m0) { return Polynom2(v0 + (m0*-2f) + v1,(v0*-2f)+(m0*2f),v0,t); }
        static public Vector2    Bezier2(float  t,Vector2     v0,Vector2     v1,Vector2     m0) { return Polynom2(v0 + (m0*-2f) + v1,(v0*-2f)+(m0*2f),v0,t); }
        static public Vector3    Bezier2(float  t,Vector3     v0,Vector3     v1,Vector3     m0) { return Polynom2(v0 + (m0*-2f) + v1,(v0*-2f)+(m0*2f),v0,t); }
        static public Vector4    Bezier2(float  t,Vector4     v0,Vector4     v1,Vector4     m0) { return Polynom2(v0 + (m0*-2f) + v1,(v0*-2f)+(m0*2f),v0,t); }
        static public Quaternion Bezier2(float  t,Quaternion  v0,Quaternion  v1,Quaternion  m0) { 
            Vector4 q0 = v0.ToVector4(), q1 = v1.ToVector4(), qm0 = m0.ToVector4();
            return Polynom2(q0 + (qm0*-2f) + q1,(q0*-2f)+(qm0*2f),q0,t).ToQuaternion(true);
        }

        /// <summary>
        /// Quadratic Bezier Derivative extracted from quadratic polynomial derivative formula in the format [2bt + c]
        /// </summary>
        /// <param name="t">Interpolation Factor</param>
        /// <param name="v0">Control Point 1</param>
        /// <param name="v1">Control Point 2</param>
        /// <param name="m0">Tangent Point 1 & 2</param>
        /// <returns>First order derivative of the quadratic bezier</returns>
        static public float      Bezier2Deriv(float  t,float      v0,float      v1,float      m0) { return PolynomDeriv2(v0 + (m0*-2f) + v1,(v0*-2f)+(m0*2f),t); }
        static public double     Bezier2Deriv(double t,double     v0,double     v1,double     m0) { return PolynomDeriv2(v0 + (m0*-2f) + v1,(v0*-2f)+(m0*2f),t); }
        static public Vector2    Bezier2Deriv(float  t,Vector2    v0,Vector2    v1,Vector2    m0) { return PolynomDeriv2(v0 + (m0*-2f) + v1,(v0*-2f)+(m0*2f),t); }
        static public Vector3    Bezier2Deriv(float  t,Vector3    v0,Vector3    v1,Vector3    m0) { return PolynomDeriv2(v0 + (m0*-2f) + v1,(v0*-2f)+(m0*2f),t); }
        static public Vector4    Bezier2Deriv(float  t,Vector4    v0,Vector4    v1,Vector4    m0) { return PolynomDeriv2(v0 + (m0*-2f) + v1,(v0*-2f)+(m0*2f),t); }
        static public Quaternion Bezier2Deriv(float  t,Quaternion v0,Quaternion v1,Quaternion m0) { 
            Vector4 q0 = v0.ToVector4(), q1 = v1.ToVector4(), qm0 = m0.ToVector4();
            return PolynomDeriv2(q0 + (qm0*-2f) + q1,(q0*-2f)+(qm0*2f),t).ToQuaternion(true); 
        }

        /// <summary>
        /// Quadratic Bezier ArcLength extracted from the equation A(t) = sqrt(1 + deriv²)
        /// Reference: https://tutorial.math.lamar.edu/classes/calcii/arclength.aspx
        /// </summary>
        /// <param name="t">Parametric Factor</param>
        /// <param name="v0">Control Point 1</param>
        /// <param name="v1">Control Point 2</param>
        /// <param name="m0">Tangent Point 1</param>        
        /// <returns>Arc Length of the cubic bezier at the desired parametric value.</returns>
        static public float  Bezier2ArcLength(float  t,float      v0,float      v1,float      m0)  { float  dv = Math.Abs(Bezier2Deriv(t,v0,v1,m0)); return Mathf.Sqrt(1f +(dv*dv));  }
        static public double Bezier2ArcLength(double t,double     v0,double     v1,double     m0)  { double dv = Math.Abs(Bezier2Deriv(t,v0,v1,m0)); return Math. Sqrt(1.0+(dv*dv));  }
        static public float  Bezier2ArcLength(float  t,Vector2    v0,Vector2    v1,Vector2    m0)  { float  dv = Bezier2Deriv(t,v0,v1,m0).magnitude; return Mathf.Sqrt(1f +(dv*dv));  }
        static public float  Bezier2ArcLength(float  t,Vector3    v0,Vector3    v1,Vector3    m0)  { float  dv = Bezier2Deriv(t,v0,v1,m0).magnitude; return Mathf.Sqrt(1f +(dv*dv));  }
        static public float  Bezier2ArcLength(float  t,Vector4    v0,Vector4    v1,Vector4    m0)  { float  dv = Bezier2Deriv(t,v0,v1,m0).magnitude; return Mathf.Sqrt(1f +(dv*dv));  }
        static public float  Bezier2ArcLength(float  t,Quaternion v0,Quaternion v1,Quaternion m0)  { float  dv = Bezier2Deriv(t,v0.ToVector4(),v1.ToVector4(),m0.ToVector4()).magnitude; return Mathf.Sqrt(1f+(dv*dv)); }

        #region Bezier2Length
        /// <summary>
        /// Calculates the quadratic bezier segment total length using RungeKutta (RK4) approximation.
        /// </summary>
        /// <param name="p_steps">Number of steps of approximation, the bigger the more precise bu slower</param>
        /// <param name="t">Parametric Factor</param>
        /// <param name="v0">Control Point 1</param>
        /// <param name="v1">Control Point 2</param>
        /// <param name="m0">Tangent Point 1</param>
        /// <param name="m1">Tangent Point 2</param>        
        /// <returns>Curve length for the specified set of control points.</returns>
        static public float Bezier2Length(float  v0,float v1,float m0,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * Bezier2ArcLength(vs    ,v0,v1,m0);
                dy2 = dt * Bezier2ArcLength(vshdt ,v0,v1,m0);
                dy4 = dt * Bezier2ArcLength(vsdt  ,v0,v1,m0);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        static public double Bezier2Length(double v0,double v1,double m0,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step_d.Length-1);
            double dt  = 1.0/(double)p_steps;
            double hdt = dt*0.5;
            // RK4 Variables
            double dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step_d[0] = rk4_value_d[0] = 0.0;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                double vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * Bezier2ArcLength(vs    ,v0,v1,m0);
                dy2 = dt * Bezier2ArcLength(vshdt ,v0,v1,m0);
                dy4 = dt * Bezier2ArcLength(vsdt  ,v0,v1,m0);
                rk4_step_d[i+1] = vsdt;
                rk4_value_d[i+1] = rk4_value_d[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value_d[p_steps];
        }
        static public float Bezier2Length(Vector2 v0,Vector2 v1,Vector2 m0,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * Bezier2ArcLength(vs    ,v0,v1,m0);
                dy2 = dt * Bezier2ArcLength(vshdt ,v0,v1,m0);
                dy4 = dt * Bezier2ArcLength(vsdt  ,v0,v1,m0);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        static public float Bezier2Length(Vector3 v0,Vector3 v1,Vector3 m0,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * Bezier2ArcLength(vs    ,v0,v1,m0);
                dy2 = dt * Bezier2ArcLength(vshdt ,v0,v1,m0);
                dy4 = dt * Bezier2ArcLength(vsdt  ,v0,v1,m0);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        static public float Bezier2Length(Vector4 v0,Vector4 v1,Vector4 m0,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * Bezier2ArcLength(vs    ,v0,v1,m0);
                dy2 = dt * Bezier2ArcLength(vshdt ,v0,v1,m0);
                dy4 = dt * Bezier2ArcLength(vsdt  ,v0,v1,m0);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        static public float Bezier2Length(Quaternion v0,Quaternion v1,Quaternion m0,Quaternion m1,int p_steps=32) {
            //Incrementers to pass into the known solution
            p_steps   = Mathf.Clamp(p_steps,1,rk4_step.Length-1);
            float dt  = 1.0f/(float)p_steps;
            float hdt = dt*0.5f;
            // RK4 Variables
            float dy1,dy2,dy4; 
            // RK4 Initializations            
            rk4_step [0] = rk4_value[0] = 0.0f;
            //RK4 Loop
            for(int i=0;i<p_steps;i++) {    
                float vs=rk4_step[i],vshdt=vs+hdt,vsdt=vs+dt;
                dy1 = dt * Bezier2ArcLength(vs    ,v0,v1,m0);
                dy2 = dt * Bezier2ArcLength(vshdt ,v0,v1,m0);
                dy4 = dt * Bezier2ArcLength(vsdt  ,v0,v1,m0);
                rk4_step [i+1] = vsdt;
                rk4_value[i+1] = rk4_value[i] + (dy1 + 4f * dy2 + dy4) * 0.16666666666666666f;                                 
            }            
            return rk4_value[p_steps];
        }
        #endregion

        #endregion

        #region Linear        
        /// <summary>
        /// A linear curve isn't even a curve.
        /// </summary>
        /// <param name="t">Interpolation Factor</param>
        /// <param name="v0">Control Point 1</param>
        /// <param name="v1">Control Point 2</param>                
        /// <returns>Interpolated value of the linear curve</returns>
        static public float      Linear(float  t,float       v0,float       v1) { return v0 + (v1-v0)*t; }
        static public double     Linear(double t,double      v0,double      v1) { return v0 + (v1-v0)*t; }
        static public Vector2    Linear(float  t,Vector2     v0,Vector2     v1) { return v0 + (v1-v0)*t; }
        static public Vector3    Linear(float  t,Vector3     v0,Vector3     v1) { return v0 + (v1-v0)*t; }
        static public Vector4    Linear(float  t,Vector4     v0,Vector4     v1) { return v0 + (v1-v0)*t; }
        static public Quaternion Linear(float  t,Quaternion  v0,Quaternion  v1) { Vector4 q0=v0.ToVector4(),q1=v1.ToVector4(); return (q0 + (q1-q0)*t).ToQuaternion(true); }
        #endregion

        #endregion

        /// <summary>
        /// Flag that tells this spline closes a loop.
        /// </summary>
        public bool closed;

        /// <summary>
        /// Offseting index to access a control-point tangent information.
        /// </summary>
        public int tangentsPerPosition { get { return GetTangentOffset(); } }

        /// <summary>
        /// Spline Length.
        /// </summary>
        public float length { get { return GetLength();  } }

        #region Virtuals
        /// <summary>
        /// Override this method to support the creation of tangent lists per control point w/ the desired size.
        /// </summary>
        /// <returns></returns>
        virtual protected int GetTangentOffset() { return 0; }
        /// <summary>
        /// Returns the spline length
        /// </summary>
        /// <returns>Spline Length</returns>
        virtual protected float GetLength() { return 0f; }
        #endregion

    }

    /// <summary>
    /// Class that implements a base spline curve.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class Spline<T> : Spline {

        /// <summary>
        /// List of positions.
        /// </summary>
        protected List<T> positions { get { return m_positions==null ? (m_positions = new List<T>()) : m_positions; } set { m_positions = value; } }
        [SerializeField]
        private List<T> m_positions;
        
        /// <summary>
        /// List of positions.
        /// </summary>
        protected List<T> tangents { get { return m_tangents==null ? (m_tangents = new List<T>()) : m_tangents; } set { m_tangents = value; } }
        [SerializeField]
        private List<T> m_tangents;

        /// <summary>
        /// List of control points.
        /// </summary>
        public int count { get { return positions.Count; } }

        #region Add/Remove
        /// <summary>
        /// Adds a new control point and its set of tangents if appliable.
        /// </summary>
        /// <param name="p_position">Control Point Position</param>
        /// <param name="p_tangents">Set of Tagents</param>
        public void Add(T p_position,params T[] p_tangents) {            
            positions.Add(p_position);
            if(tangentsPerPosition<=0) return;
            for(int i = 0; i < tangentsPerPosition; i++) {
                T tv = default(T);
                if(i<p_tangents.Length) { tv = p_tangents[i]; }
                tangents.Add(tv);
            }
        }

        /// <summary>
        /// Inserts a new control point and its set of tangents.
        /// </summary>
        /// <param name="p_index">Index where to insert</param>
        /// <param name="p_position">Control Point Position</param>
        /// <param name="p_tangents">Tangent Set</param>
        public void Insert(int p_index,T p_position,params T[] p_tangents) {
            positions.Insert(p_index,p_position);
            if(tangentsPerPosition<=0) return;
            for(int i = tangentsPerPosition-1; i >= 0; i--) {
                T tv = default(T);
                if(i<p_tangents.Length) { tv = p_tangents[i]; }
                tangents.Insert(p_index*tangentsPerPosition,tv);
            }            
        }

        /// <summary>
        /// Removes a control point by index and its tangents.
        /// </summary>
        /// <param name="p_index">Control Point Index</param>
        public void Remove(int p_index) {
            int cc = count;
            if(cc<=0) return;
            int p = p_index;            
            if(p<0)   p = closed ?  (cc+p) : 0;
            if(p>=cc) p = closed ?  (p%cc) : cc-1;
            positions.RemoveAt(p);
            if(tangentsPerPosition<=0)return;            
            p*=tangentsPerPosition;
            for(int i=0;i<tangentsPerPosition;i++) tangents.RemoveAt(p);
        }

        /// <summary>
        /// Removes all control points.
        /// </summary>
        public void Clear() { positions.Clear(); tangents.Clear(); }
        #endregion

        #region Get/Set
        /// <summary>
        /// Returns the properly wrapped index of the desired control.
        /// </summary>        
        private int GetControlIndex(int p_index) {
            int cc = positions.Count;
            int p = p_index;            
            if(p<0)   p = closed ?  (cc+p) : 0;
            if(p>=cc) p = closed ?  (p%cc) : cc-1;
            return p;
        }

        /// <summary>
        /// Returns a control point value by index.
        /// </summary>
        /// <param name="p_index">Control Point Index</param>
        /// <returns>Control Point Value</returns>
        public T GetPosition(int p_index) {
            int cc = positions.Count;
            if(cc<=0) return default(T);
            int p = Mathf.Clamp(GetControlIndex(p_index),0,positions.Count-1);
            return positions[p];
        }

        /// <summary>
        /// Set a control point value at the index.
        /// </summary>
        /// <param name="p_index">Control Point Index.</param>
        /// <param name="p_value">Control Point Value</param>
        public void SetPosition(int p_index,T p_value) {
            int cc = positions.Count;
            if(cc<=0) return;
            int p = Mathf.Clamp(GetControlIndex(p_index),0,positions.Count-1);
            positions[p] = p_value;
        }

        /// <summary>
        /// Returns a control point tangent at its relative index.
        /// </summary>
        /// <param name="p_index">Control Point Index</param>
        /// <param name="p_tangent">Tangent Index</param>
        /// <returns></returns>
        public T GetTangent(int p_index,int p_tangent) {
            int cc = positions.Count;
            if(cc<=0) return default(T);
            if(tangentsPerPosition<=0) return default(T);
            int p = GetControlIndex(p_index);
            p*=tangentsPerPosition;
            p = Mathf.Clamp(p+p_tangent,0,tangents.Count-1);
            return tangents[p];
        }

        /// <summary>
        /// Sets a control point at index tangent value at its relative index.
        /// </summary>
        /// <param name="p_index">Control Point Value</param>
        /// <param name="p_tangent">Tangent Index</param>
        /// <param name="p_value">Tangent Value</param>
        public void SetTangent(int p_index,int p_tangent,T p_value) {
            if(tangentsPerPosition<=0) return;
            int cc = positions.Count;
            if(cc<=0) return;
            int p = GetControlIndex(p_index);
            p*=tangentsPerPosition;
            p = Mathf.Clamp(p+p_tangent,0,tangents.Count-1);
            tangents[p] = p_value;            
        }
        #endregion

        #region Evaluation
        /// <summary>
        /// Evaluates the spline at the segment index and its ratio.
        /// </summary>
        /// <param name="p_ratio">Spline ratio position</param>
        /// <param name="p_control">Control point segment index</param>
        /// <returns>Value sampled from the spline</returns>
        public T Evaluate(float p_ratio,int p_control) { return GetValue(p_ratio,p_control); }

        /// <summary>
        /// Evaluates the spline as a whole using a floating point index.
        /// </summary>
        /// <param name="p_control">Floating point index of the control point.</param>
        /// <returns>Value sampled from the spline</returns>
        public T Evaluate(float p_control) {
            float ci = Mathf.Floor(p_control);
            float cr = p_control - ci;
            return Evaluate(cr,(int)ci);
        }

        /// <summary>
        /// Returns the derivative of the curve at the segment and ratio.
        /// </summary>
        /// <param name="p_ratio">Spline ratio position</param>
        /// <param name="p_control">Control point segment index</param>
        /// <returns>Derivative at the segment and its ratio</returns>
        public T Derivative(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control); }

        /// <summary>
        /// Evaluates the spline derivative as a whole using a floating point index.
        /// </summary>
        /// <param name="p_control_index">Floating point index of the control point.</param>
        /// <returns>Value sampled from the spline</returns>
        public T Derivative(float p_control_index) {
            float ci = Mathf.Floor(p_control_index);
            float cr = p_control_index - ci;
            return Derivative(cr,(int)ci);
        }

        /// <summary>
        /// Given the informed ratio, returns the next ratio where the movement is evenly spaced.
        /// </summary>
        /// <param name="p_current">Ratio</param>
        /// <returns>Next Ratio which will configure an evenly spaced step.</returns>
        public float GetNextConstantRatio(float p_control_index) {
            float ci = Mathf.Floor(p_control_index);
            float cr = p_control_index - ci;
            float nr = GetNextConstantRatio(cr,(int)ci);
            return nr;
        }

        /// <summary>
        /// Given the informed ratio, returns the next ratio where the movement is evenly spaced.
        /// </summary>
        /// <param name="p_current">Ratio</param>
        /// <returns>Next Ratio which will configure an evenly spaced step.</returns>
        virtual public float GetNextConstantRatio(float p_ratio,int p_control) {
            return p_ratio;
        }

        /// <summary>
        /// Populates a static array of the curve samples.
        /// </summary>
        /// <param name="p_buffer">Array with the desired number of samples.</param>
        virtual public void GetSamples(T[] p_buffer) {
            if(positions.Count<=0) return;
            if(p_buffer==null)     return;
            if(p_buffer.Length<=2) return;
            T[] res = p_buffer;
            int c   = res.Length;
            float cc = closed ? (float)positions.Count : (float)positions.Count-1;
            for(int i=0;i<c;i++) {
                float r = ((float)i)/(float)(c-1);
                res[i] = Evaluate((r*cc));
            }            
        }

        /// <summary>
        /// Generates an array with this spline samples
        /// </summary>
        /// <param name="p_count">Number of samples.</param>
        /// <returns>Array of samples.</returns>
        public T[] GetSamples(int p_count) { T[] res = new T[p_count]; GetSamples(res); return res; }

        #endregion

        #region Virtuals        
        /// <summary>
        /// Evaluates the spline at the given segment and ratio.
        /// </summary>
        /// <param name="p_ratio">Spline ratio position of the chosen segment</param>
        /// <param name="p_control">Control point index</param>
        /// <returns>Spline value at the segment and ratio</returns>
        virtual protected T GetValue(float p_ratio,int p_control) { return default(T); }

        /// <summary>
        /// Evaluates the spline derivative at the given segment and ratio.
        /// </summary>
        /// <param name="p_ratio">Spline ratio position of the chosen segment</param>
        /// <param name="p_control">Control point index</param>
        /// <returns>Spline derivative at the segment and ratio</returns>
        virtual protected T GetDerivative(float p_ratio,int p_control) { return default(T); }

        /// <summary>
        /// Evaluates the spline derivative magnitude at the given segment and ratio. This information can be used to do even spaced ratio iteration.
        /// </summary>
        /// <param name="p_ratio">Spline ratio position of the chosen segment</param>
        /// <param name="p_control">Control point index</param>
        /// <returns>Spline derivative magnitude at the segment ratio.</returns>
        virtual protected float GetDerivativeLength(float p_ratio,int p_control) { return 0f; }

        /// <summary>
        /// Returns the spline length from a control point.
        /// </summary>
        /// <param name="p_control">Control point to evaluate the spline.</param>
        /// <returns>Distance of the segment around the control point.</returns>
        virtual public float GetLength(int p_control) { return 0f; }

        /// <summary>
        /// Computes the spine length as a sum of all segments distances.
        /// </summary>
        /// <returns></returns>
        override protected float GetLength() {
            int   cc = closed ? positions.Count : positions.Count-1;
            float d  = 0f;            
            for(int i=0;i<cc;i++) { d += GetLength(i); }
            return d;
        }
        #endregion

    }

    #region CatmullRom
    /// <summary>
    /// Class that extends the spline base class and implements the CatmullRom spline equation for different data types.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class CatmullRom<T> : Spline<T> {
        /// <summary>
        /// CatmullRom Tension factor.
        /// </summary>
        public float tension = 0.5f;
    }

    #region class CatmullRomVector2
    [Serializable]
    public class CatmullRomVector2 : CatmullRom<Vector2> {
        #region Virtuals
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return CatmullRomLength(GetPosition(p_control-1),GetPosition(p_control),GetPosition(p_control+1),GetPosition(p_control+2),tension); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector2 GetValue(float p_ratio,int p_control) { return CatmullRom(p_ratio,GetPosition(p_control-1),GetPosition(p_control),GetPosition(p_control+1),GetPosition(p_control+2),tension); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector2 GetDerivative(float p_ratio,int p_control) { return CatmullRomDeriv(p_ratio,GetPosition(p_control-1),GetPosition(p_control),GetPosition(p_control+1),GetPosition(p_control+2),tension); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #region class CatmullRomVector3
    [Serializable]
    public class CatmullRomVector3 : CatmullRom<Vector3> {
        #region Virtuals
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return CatmullRomLength(GetPosition(p_control-1),GetPosition(p_control),GetPosition(p_control+1),GetPosition(p_control+2),tension); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector3 GetValue(float p_ratio,int p_control) { return CatmullRom(p_ratio,GetPosition(p_control-1),GetPosition(p_control),GetPosition(p_control+1),GetPosition(p_control+2),tension); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector3 GetDerivative(float p_ratio,int p_control) { return CatmullRomDeriv(p_ratio,GetPosition(p_control-1),GetPosition(p_control),GetPosition(p_control+1),GetPosition(p_control+2),tension); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #region class CatmullRomVector4
    [Serializable]
    public class CatmullRomVector4 : CatmullRom<Vector4> {
        #region Virtuals
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return CatmullRomLength(GetPosition(p_control-1),GetPosition(p_control),GetPosition(p_control+1),GetPosition(p_control+2),tension); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector4 GetValue(float p_ratio,int p_control) { return CatmullRom(p_ratio,GetPosition(p_control-1),GetPosition(p_control),GetPosition(p_control+1),GetPosition(p_control+2),tension); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector4 GetDerivative(float p_ratio,int p_control) { return CatmullRomDeriv(p_ratio,GetPosition(p_control-1),GetPosition(p_control),GetPosition(p_control+1),GetPosition(p_control+2),tension); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #region class CatmullRomTransform
    [Serializable]
    public class CatmullRomTransform : CatmullRom<Vector4> {

        #region Virtuals
        private Vector4 _GetPosition(int p_index) { Vector4 v = GetPosition(p_index); v.w = p_index; return v; }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return CatmullRomLength(GetPosition(p_control-1).ToVector3(),GetPosition(p_control).ToVector3(),GetPosition(p_control+1).ToVector3(),GetPosition(p_control+2).ToVector3(),tension); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector4 GetValue(float p_ratio,int p_control) { return CatmullRom(p_ratio,_GetPosition(p_control-1),_GetPosition(p_control),_GetPosition(p_control+1),_GetPosition(p_control+2),tension); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector4 GetDerivative(float p_ratio,int p_control) { return CatmullRomDeriv(p_ratio,GetPosition(p_control-1).ToVector3(),GetPosition(p_control).ToVector3(),GetPosition(p_control+1).ToVector3(),GetPosition(p_control+2).ToVector3(),tension); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #endregion

    #region BezierCubic
    /// <summary>
    /// Class that extends the cubic bezier spline to give support to different data types.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class BezierCubic<T> : Spline<T> {
        override protected int GetTangentOffset() { return 2; }
    }

    #region BezierCubicVector2
    [Serializable]
    public class BezierCubicVector2 : BezierCubic<Vector2> {
        #region Virtuals
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return Bezier3Length(GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0),GetTangent(p_control,1)); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector2 GetValue(float p_ratio,int p_control) { return Bezier3(p_ratio,GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0),GetTangent(p_control,1)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector2 GetDerivative(float p_ratio,int p_control) { return Bezier3Deriv(p_ratio,GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0),GetTangent(p_control,1)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #region BezierCubicVector3
    [Serializable]
    public class BezierCubicVector3 : BezierCubic<Vector3> {
        #region Virtuals
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return Bezier3Length(GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0),GetTangent(p_control,1)); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector3 GetValue(float p_ratio,int p_control) { return Bezier3(p_ratio,GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0),GetTangent(p_control,1)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector3 GetDerivative(float p_ratio,int p_control) { return Bezier3Deriv(p_ratio,GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0),GetTangent(p_control,1)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #region BezierCubicVector4
    [Serializable]
    public class BezierCubicVector4 : BezierCubic<Vector4> {
        #region Virtuals
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return Bezier3Length(GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0),GetTangent(p_control,1)); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector4 GetValue(float p_ratio,int p_control) { return Bezier3(p_ratio,GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0),GetTangent(p_control,1)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector4 GetDerivative(float p_ratio,int p_control) { return Bezier3Deriv(p_ratio,GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0),GetTangent(p_control,1)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #region BezierCubicTransform
    [Serializable]
    public class BezierCubicTransform : BezierCubic<Vector4> {
        #region Virtuals
        private Vector4 _GetPosition(int p_index)               { Vector4 v = GetPosition(p_index);          v.w  = p_index;   return v; }
        private Vector4 _GetTangent (int p_control,int p_index) { Vector4 v = GetTangent(p_control,p_index); v.w += p_control; return v; }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return Bezier3Length(GetPosition(p_control).ToVector3(),GetPosition(p_control+1).ToVector3(),GetTangent(p_control,0).ToVector3(),GetTangent(p_control,1).ToVector3()); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector4 GetValue(float p_ratio,int p_control) { return Bezier3(p_ratio,_GetPosition(p_control),_GetPosition(p_control+1),_GetTangent(p_control,0),_GetTangent(p_control,1)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector4 GetDerivative(float p_ratio,int p_control) { return Bezier3Deriv(p_ratio,GetPosition(p_control).ToVector3(),GetPosition(p_control+1).ToVector3(),GetTangent(p_control,0).ToVector3(),GetTangent(p_control,1).ToVector3()); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #endregion

    #region BezierQuad
    /// <summary>
    /// Class that extends the quadratic bezier spline to give support to different data types.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class BezierQuad<T> : Spline<T> {
        override protected int GetTangentOffset() { return 1; }
    }

    #region BezierQuadVector2
    [Serializable]
    public class BezierQuadVector2 : BezierQuad<Vector2> {
        #region Virtuals
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return Bezier2Length(GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0)); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector2 GetValue(float p_ratio,int p_control) { return Bezier2(p_ratio,GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector2 GetDerivative(float p_ratio,int p_control) { return Bezier2Deriv(p_ratio,GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #region BezierQuadVector3
    [Serializable]
    public class BezierQuadVector3 : BezierQuad<Vector3> {
        #region Virtuals
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return Bezier2Length(GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0)); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector3 GetValue(float p_ratio,int p_control) { return Bezier2(p_ratio,GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector3 GetDerivative(float p_ratio,int p_control) { return Bezier2Deriv(p_ratio,GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #region BezierQuadVector4
    [Serializable]
    public class BezierQuadVector4 : BezierQuad<Vector4> {
        #region Virtuals
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return Bezier2Length(GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0)); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector4 GetValue(float p_ratio,int p_control) { return Bezier2(p_ratio,GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector4 GetDerivative(float p_ratio,int p_control) { return Bezier2Deriv(p_ratio,GetPosition(p_control),GetPosition(p_control+1),GetTangent(p_control,0)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #region BezierQuadTransform
    [Serializable]
    public class BezierQuadTransform : BezierQuad<Vector4> {
        #region Virtuals
        private Vector4 _GetPosition(int p_index)               { Vector4 v = GetPosition(p_index);           v.w  = p_index;   return v; }
        private Vector4 _GetTangent (int p_control,int p_index) { Vector4 v = GetTangent (p_control,p_index); v.w += p_control; return v; }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return Bezier2Length(GetPosition(p_control).ToVector3(),GetPosition(p_control+1).ToVector3(),GetTangent(p_control,0).ToVector3()); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector4 GetValue(float p_ratio,int p_control) { return Bezier2(p_ratio,_GetPosition(p_control),_GetPosition(p_control+1),_GetTangent(p_control,0)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector4 GetDerivative(float p_ratio,int p_control) { return Bezier2Deriv(p_ratio,GetPosition(p_control).ToVector3(),GetPosition(p_control+1).ToVector3(),GetTangent(p_control,0).ToVector3()); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #endregion

    #region Linear
    /// <summary>
    /// Class that extends the spline base class and implements the CatmullRom spline equation for different data types.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class Linear<T> : Spline<T> {

        /// <summary>
        /// Special case for linear splines.
        /// </summary>
        /// <param name="p_count"></param>
        /// <returns></returns>
        public override void GetSamples(T[] p_buffer) {            
            if(p_buffer==null)     return;
            if(p_buffer.Length<=2) return;
            int cc = closed ? count : count-1;
            if(cc<=0) cc=0;
            int samples_per_segment = cc<=0 ? 0 : p_buffer.Length/cc;
            T[] res = p_buffer;
            int k=0;            
            T cp;
            for(int i=0;i<cc;i++) {
                cp = Evaluate((float)i);
                res[k++] = cp;
                int spp = i>=cc-1 ? samples_per_segment : (samples_per_segment-1);
                for(int j=1;j<spp;j++) {
                    float r = (float)j/(float)(samples_per_segment-1);
                    float p = (float)i;                    
                    cp = Evaluate(p+r);
                    res[k++] = cp;
                }
            }
            if(k>0)for(int i=k;i<res.Length;i++) res[i] = res[k-1];
        }

    }

    #region class LinearVector2
    [Serializable]
    public class LinearVector2 : Linear<Vector2> {
        #region Virtuals
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return Vector2.Distance(GetPosition(p_control),GetPosition(p_control+1)); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector2 GetValue(float p_ratio,int p_control) { return Linear(p_ratio,GetPosition(p_control),GetPosition(p_control+1)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector2 GetDerivative(float p_ratio,int p_control) { return GetPosition(p_control+1)-GetPosition(p_control); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #region class LinearVector3
    [Serializable]
    public class LinearVector3 : Linear<Vector3> {
        #region Virtuals
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return Vector3.Distance(GetPosition(p_control),GetPosition(p_control+1)); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector3 GetValue(float p_ratio,int p_control) { return Linear(p_ratio,GetPosition(p_control),GetPosition(p_control+1)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector3 GetDerivative(float p_ratio,int p_control) { return GetPosition(p_control+1)-GetPosition(p_control); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #region class LinearVector4
    [Serializable]
    public class LinearVector4 : Linear<Vector4> {
        #region Virtuals
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return Vector4.Distance(GetPosition(p_control),GetPosition(p_control+1)); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector4 GetValue(float p_ratio,int p_control) { return Linear(p_ratio,GetPosition(p_control),GetPosition(p_control+1)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector4 GetDerivative(float p_ratio,int p_control) { return GetPosition(p_control+1)-GetPosition(p_control); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #region class LinearTransform
    [Serializable]
    public class LinearTransform : Linear<Vector4> {
        #region Virtuals
        private Vector4 _GetPosition(int p_index) { Vector4 v = GetPosition(p_index); v.w = p_index; return v; }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override public float GetLength(int p_control) { return Vector3.Distance(GetPosition(p_control).ToVector3(),GetPosition(p_control+1).ToVector3()); }
        /// <summary>
        /// <inheritdoc/>
        /// </summary>        
        override protected Vector4 GetValue(float p_ratio,int p_control) { return Linear(p_ratio,_GetPosition(p_control),_GetPosition(p_control+1)); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected Vector4 GetDerivative(float p_ratio,int p_control) { return GetPosition(p_control+1).ToVector3()-GetPosition(p_control).ToVector3(); }
        /// <summary>
        /// <inheritdoc>/>
        /// </summary>        
        override protected float GetDerivativeLength(float p_ratio,int p_control) { return GetDerivative(p_ratio,p_control).magnitude; }
        #endregion
    }
    #endregion

    #endregion

}
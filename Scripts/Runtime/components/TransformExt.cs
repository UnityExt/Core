using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Component = UnityEngine.Component;

namespace UnityExt.Core.Components {

    #region struct TransformVector
    /// <summary>
    /// Vector that describes the 3 components of a transform.
    /// </summary>
    public struct TransformVector {

        #region static
        /// <summary>
        /// Linear interpolates 2 Transform Vectors.
        /// </summary>
        /// <param name="a">From TransformVector</param>
        /// <param name="b">To TransformVector</param>
        /// <param name="t">Ratio</param>
        /// <returns>Interpolated TransformVector</returns>
        static public TransformVector Lerp(TransformVector a,TransformVector b,float t) {
            TransformVector res = new TransformVector();
            res.position = a.position + (b.position-a.position) * t;
            res.rotation = Quaternion.Slerp(a.rotation,b.rotation,t);
            res.scale    = a.scale    + (b.scale   -a.scale   )*t;
            return res;
        }
        #endregion

        /// <summary>
        /// Position
        /// </summary>
        public Vector3    position;
        /// <summary>
        /// Rotation
        /// </summary>
        public Quaternion rotation;
        /// <summary>
        /// Scale
        /// </summary>
        public Vector3    scale;

        #region CTOR
        /// <summary>
        /// Creates a Transform Vector from a Transform, either with local/global reference.
        /// </summary>
        /// <param name="p_target">Target Transform</param>
        /// <param name="p_local">Flag that tells if local/global vector data must be used.</param>
        public TransformVector(Transform p_target,bool p_local=true) : this(p_local ? p_target.localPosition : p_target.position,p_local ? p_target.localRotation : p_target.rotation,p_local ? p_target.localScale : p_target.lossyScale) { }
        /// <summary>
        /// Creates a Transform Vector from its 3 components.
        /// </summary>
        /// <param name="p_position"></param>
        /// <param name="p_rotation"></param>
        /// <param name="p_scale"></param>
        public TransformVector(Vector3 p_position,Quaternion p_rotation,Vector3 p_scale) { position = p_position; rotation = p_rotation; scale = p_scale; }
        public TransformVector(Vector3 p_position,Quaternion p_rotation) : this(p_position,p_rotation,Vector3.one) { }
        /// <summary>
        /// Creates a TransformVector from another one.
        /// </summary>
        /// <param name="p_target"></param>
        public TransformVector(TransformVector p_target) : this(p_target.position,p_target.rotation,p_target.scale) { }
        #endregion

        /// <summary>
        /// Applies this vector information into a transform.
        /// </summary>
        /// <param name="p_target">Transform to modify</param>
        /// <param name="p_local">Local or Global coordinates.</param>
        public void Set(Transform p_target,bool p_local=true) {
            if(p_local) {
                p_target.localPosition = position;
                p_target.localScale    = scale;
                p_target.localRotation = rotation;
            }
            else {
                p_target.position   = position;
                p_target.rotation   = rotation;
                p_target.localScale = scale;                    
            }
        }

        /// <summary>
        /// Fetch the information from the Transform and populate this vector.
        /// </summary>
        /// <param name="p_target">Target Transform</param>
        /// <param name="p_local">Local or Global coordinates.</param>
        public void Get(Transform p_target,bool p_local=true) {
            position = p_local ? p_target.localPosition : p_target.position;
            rotation = p_local ? p_target.localRotation : p_target.rotation;
            scale    = p_local ? p_target.localScale    : p_target.lossyScale;
        }

    }
    #endregion

    /// <summary>
    /// Extension class to improve the basic functionality of the Transform component.
    /// </summary>
    public static class TransformExt {

        #region Traverse
        /// <summary>
        /// Traverses the hierarchy of a transform calling the callback per child found.
        /// </summary>
        /// <param name="p_target">Transform to navigate</param>
        /// <param name="p_dfs">Flag that tells to do either DepthFirst or BreadthFirst searching of the hierarchy.</param>
        /// <param name="p_callback">Callback called on each iteration.</param>
        static public void Traverse(this Transform p_target,bool p_dfs,Delegate p_callback) {
            //Traversal data structures
            Stack<Transform> s = p_dfs ? new Stack<Transform>() : null;
            Queue<Transform> q = p_dfs ? null                   : new Queue<Transform>();
            //Callback casts
            Action   <Transform> cb_a = (p_callback is Action   <Transform>) ? (Action   <Transform>)p_callback : null;
            Predicate<Transform> cb_p = (p_callback is Predicate<Transform>) ? (Predicate<Transform>)p_callback : null;
            //Add first
            if(p_dfs) s.Push(p_target); else q.Enqueue(p_target);
            //Iterate
            while(true) {
                int hc = p_dfs ? s.Count : q.Count;
                if(hc<=0) break;
                Transform it = p_dfs ? s.Pop() : q.Dequeue();
                if(!it) continue;                
                //If 'Action' just invoke if 'Predicate' return result tells if iteration should continue
                bool will_continue = true;
                if(cb_a!=null) cb_a(it);
                if(cb_p != null) { will_continue = cb_p(it); }
                if(!will_continue)break;
                //Add children
                int c = it.childCount;
                for(int i = 0; i < c; i++) {
                    //If DFS reverse iterate children so stack handles items first to last
                    int idx = p_dfs ? (c-1-i) : i;
                    Transform c_it = it.GetChild(idx);
                    if(p_dfs) s.Push(c_it); else q.Enqueue(c_it);
                }
            }
        }

        /// <summary>
        /// Traverses the hierarchy of a transform calling the callback per child found.
        /// </summary>
        /// <param name="p_target">Transform to navigate</param>        
        /// <param name="p_callback">Callback called on each iteration.</param>
        static public void Traverse(this Transform p_target,Delegate p_callback) { Traverse(p_target,true,p_callback); }
        
        /// <summary>
        /// Traverses the hierarchy of a transform calling the callback per child found. It will run the desired number of steps per frame.
        /// </summary>
        /// <param name="p_target">Transform to navigate</param>
        /// <param name="p_dfs">Flag that tells to do either DepthFirst or BreadthFirst searching of the hierarchy.</param>
        /// <param name="p_steps">Number of traversal steps per frame to perform.</param>
        /// <param name="p_callback">Callback called on each iteration.</param>
        /// <returns>Running Activity doing the search</returns>
        static public Activity TraverseAsync(this Transform p_target,int p_steps,bool p_dfs,Delegate p_callback) {
            //Traversal data structures
            Stack<Transform> s = p_dfs ? new Stack<Transform>() : null;
            Queue<Transform> q = p_dfs ? null                   : new Queue<Transform>();
            //Callback casts
            Action   <Transform> cb_a = (p_callback is Action   <Transform>) ? (Action   <Transform>)p_callback : null;
            Predicate<Transform> cb_p = (p_callback is Predicate<Transform>) ? (Predicate<Transform>)p_callback : null;
            //Adds the first element
            if(p_dfs) s.Push(p_target); else q.Enqueue(p_target);
            //Run at least one step
            int stp = p_steps<=0 ? 1 : p_steps;
            //Iterate the hierarchy traversal 'step' items per frame to offload the navigation in more frames and ease the overhead
            Activity search_loop = 
            Activity.Run(
            delegate(Activity p_a) { 
                for(int i=0;i<stp;i++) {
                    int hc = p_dfs ? s.Count : q.Count;
                    if(hc<=0) return false;
                    Transform it = p_dfs ? s.Pop() : q.Dequeue();
                    if(!it) continue;
                    //If 'Action' just invoke if 'Predicate' return result tells if iteration should continue
                    bool will_continue = true;
                    if(cb_a != null) cb_a(it);
                    if(cb_p != null) { will_continue = cb_p(it); }
                    if(!will_continue) return false;
                    int c = it.childCount;
                    for(int j = 0; j < c; j++) {
                        //If DFS reverse iterate children so stack handles items first to last
                        int idx = p_dfs ? (c-1-j) : j;
                        Transform c_it = it.GetChild(idx);
                        if(p_dfs) s.Push(c_it); else q.Enqueue(c_it);
                    }
                }
                return true;
            });
            search_loop.id = "Transform.Traverse";
            //Returns the running activity instance
            return search_loop;
        }

        /// <summary>
        /// Traverses the hierarchy of a transform calling the callback per child found. It will run the desired number of steps per frame.
        /// </summary>
        /// <param name="p_target">Transform to navigate</param>        
        /// <param name="p_steps">Number of traversal steps per frame to perform.</param>
        /// <param name="p_callback">Callback called on each iteration.</param>
        /// <returns>Running Activity doing the search</returns>
        static public Activity TraverseAsync(this Transform p_target,int p_steps,Delegate p_callback) { return TraverseAsync(p_target,p_steps,true,p_callback); }

        /// <summary>
        /// Traverses the hierarchy of a transform calling the callback per child found. It will run the desired number of steps per frame.
        /// </summary>
        /// <param name="p_target">Transform to navigate</param>                
        /// <param name="p_callback">Callback called on each iteration.</param>
        /// <returns>Running Activity doing the search</returns>
        static public Activity TraverseAsync(this Transform p_target,Delegate p_callback) { return TraverseAsync(p_target,1,true,p_callback); }
        #endregion

        #region TraverseReverse
        /// <summary>
        /// Traverses the hierarchy reversibly iterating each parent until null
        /// </summary>
        /// <param name="p_target">Transform to navigate reverse</param>        
        /// <param name="p_callback">Callback called on each iteration.</param>
        static public void TraverseReverse(this Transform p_target,Delegate p_callback) {
            //Traversal data structures
            Transform it = p_target;            
            //Callback casts
            Action   <Transform> cb_a = (p_callback is Action   <Transform>) ? (Action   <Transform>)p_callback : null;
            Predicate<Transform> cb_p = (p_callback is Predicate<Transform>) ? (Predicate<Transform>)p_callback : null;            
            //Iterate
            while(it) {
                //If 'Action' just invoke if 'Predicate' return result tells if iteration should continue
                bool will_continue = true;
                if(cb_a!=null) cb_a(it);
                if(cb_p != null) { will_continue = cb_p(it); }
                if(!will_continue)break;
                //Move to next
                it = it.parent;
            }
        }

        /// <summary>
        /// Traverses the hierarchy of a transform calling the callback per child found. It will run the desired number of steps per frame.
        /// </summary>
        /// <param name="p_target">Transform to navigate reverse</param>        
        /// <param name="p_steps">Number of traversal steps per frame to perform.</param>
        /// <param name="p_callback">Callback called on each iteration.</param>
        /// <returns>Running Activity doing the search</returns>
        static public Activity TraverseReverseAsync(this Transform p_target,int p_steps,Delegate p_callback) {
            //Traversal data structures
            Transform it = p_target;            
            //Callback casts
            Action   <Transform> cb_a = (p_callback is Action   <Transform>) ? (Action   <Transform>)p_callback : null;
            Predicate<Transform> cb_p = (p_callback is Predicate<Transform>) ? (Predicate<Transform>)p_callback : null;
            //Run at least one step
            int stp = p_steps<=0 ? 1 : p_steps;
            //Iterate the hierarchy traversal 'step' items per frame to offload the navigation in more frames and ease the overhead
            Activity search_loop = 
            Activity.Run(
            delegate(Activity p_a) { 
                for(int i=0;i<stp;i++) {
                    //Exit if null
                    if(!it) return false;
                    //If 'Action' just invoke if 'Predicate' return result tells if iteration should continue
                    bool will_continue = true;
                    if(cb_a!=null) cb_a(it);
                    if(cb_p != null) { will_continue = cb_p(it); }                    
                    //Move to next or stop
                    it = will_continue ? it.parent : null;
                }
                return true;
            });
            search_loop.id = "Transform.TraverseReverse";
            //Returns the running activity instance
            return search_loop;
        }

        /// <summary>
        /// Traverses the hierarchy of a transform calling the callback per child found. It will run the desired number of steps per frame.
        /// </summary>
        /// <param name="p_target">Transform to navigate reverse</param>
        /// <param name="p_callback">Callback called on each iteration.</param>
        /// <returns>Running Activity doing the search</returns>
        static public Activity TraverseReverseAsync(this Transform p_target,Delegate p_callback) { return TraverseReverseAsync(p_target,1,p_callback); }

        #endregion

        #region GetComponentCached
        /// <summary>
        /// Transform<>Component LUT.
        /// </summary>
        static internal Dictionary<Transform,Component> m_component_lut = new Dictionary<Transform, Component>();

        /// <summary>
        /// Performs a GetComponent operation and caches the result. In case the old reference is lost/destroyed it tries again.
        /// </summary>
        /// <param name="p_target">Transform to sample the Component</param>
        /// <param name="p_type"></param>
        /// <returns>Component Instance</returns>
        static public Component GetComponentCached(this Transform p_target,Type p_type) {
            Dictionary<Transform,Component> d = m_component_lut;
            Component c = null;
            Transform t = p_target;
            if(d.ContainsKey(t)) {
                c = d[t];
                if(c) return c;
            }
            c = t.GetComponent(p_type);
            if(!c) return c;
            d[t] = c;            
            return c;
        }

        /// <summary>
        /// Performs a GetComponent operation and caches the result. In case the old reference is lost/destroyed it tries again.
        /// </summary>
        /// <typeparam name="T">Component Type</typeparam>
        /// <param name="p_target">Target Transform</param>
        /// <returns>Component Instance</returns>
        static public T GetComponentCached<T>(this Transform p_target) where T : Component { return (T)GetComponentCached(p_target,typeof(T)); }
        #endregion

        #region FindComponent
        /// <summary>
        /// Searches the hierarchy all occurrences of a component including the caller.
        /// </summary>        
        static private List<Component> FindComponentsCached(this Transform p_target,Type p_type,int p_count,bool p_reverse) {
            List<Component> res = new List<Component>();
            int k=0;
            Predicate<Transform> cb = delegate(Transform it) { Component c = GetComponentCached(it,p_type); if(c) { res.Add(c); k++; } if(p_count>0)if(k>=p_count) return false; return true; };            
            if(p_reverse) {
                TraverseReverse(p_target,cb);
            }
            else {
                Traverse(p_target,cb);
            }            
            return res;
        }
        static private List<T> FindComponentsCached<T>(this Transform p_target,int p_count,bool p_reverse) where T : Component {
            List<T> res = new List<T>();
            int k=0;
            Predicate<Transform> cb = delegate(Transform it) { T c = GetComponentCached<T>(it); if(c) { res.Add(c); k++; } if(p_count>0)if(k>=p_count) return false; return true; };            
            if(p_reverse) {
                TraverseReverse(p_target,cb);
            }
            else {
                Traverse(p_target,cb);
            }            
            return res;
        }

        /// <summary>
        /// Searches the hierarchy all occurrences of a component including the caller.
        /// </summary>
        /// <param name="p_target">Transform to traverse and search.</param>
        /// <param name="p_type">Type of the component</param>
        /// <returns>List of occurrences of the searched component</returns>
        static public List<Component> FindComponentsCached(this Transform p_target,Type p_type,bool p_reverse=false) { return FindComponentsCached(p_target,p_type,0,p_reverse); }

        /// <summary>
        /// Searches the hierarchy for all occurrence of a component including the caller.
        /// </summary>
        /// <param name="p_target">Transform to traverse and search.</param>
        /// <param name="p_type">Type of the component</param>
        /// <returns>List of occurrences of the searched component</returns>
        static public List<T> FindComponentsCached<T>(this Transform p_target,bool p_reverse=false) where T : Component { return FindComponentsCached<T>(p_target,0,p_reverse); }

        /// <summary>
        /// Searches the hierarchy for the first occurrence of a component including the caller.
        /// </summary>
        /// <param name="p_target">Transform to traverse and search.</param>
        /// <param name="p_type">Type of the component</param>
        /// <returns>First occurrence of the searched component</returns>
        static public Component FindComponentCached(this Transform p_target,Type p_type,bool p_reverse=false) { List<Component> cl = FindComponentsCached(p_target,p_type,1,p_reverse); return cl.Count<=0 ? null : cl[0]; }

        /// <summary>
        /// Searches the hierarchy for the first occurrence of a component including the caller.
        /// </summary>
        /// <param name="p_target">Transform to traverse and search.</param>        
        /// <returns>First occurrence of the searched component</returns>
        static public T FindComponentCached<T>(this Transform p_target,bool p_reverse=false) where T : Component { List<T> cl = FindComponentsCached<T>(p_target,1,p_reverse); return cl.Count<=0 ? null : cl[0]; }
        #endregion

        /// <summary>
        /// Returns the global depth of the target transform.
        /// </summary>
        /// <param name="p_target">Transform to check the hierarchy depth</param>
        /// <returns></returns>
        static public int GetDepth(this Transform p_target) {
            int d = 0;
            Transform t = p_target ? p_target.parent : null;
            while(t) { d++; t = t.parent; }
            return d;
        }

    }

    /// <summary>
    /// Extension class to improve the basic functionality of the GameObject component.
    /// </summary>
    public static class GameObjectExt {

        #region GetComponentCached
        /// <summary>
        /// Performs a GetComponent operation and caches the result. In case the old reference is lost/destroyed it tries again.
        /// </summary>
        /// <param name="p_target">GameObject to sample the Component</param>
        /// <param name="p_type"></param>
        /// <returns></returns>
        static public Component GetComponentCached(this GameObject p_target,Type p_type) {
            Dictionary<Transform,Component> d = TransformExt.m_component_lut;
            Component c = null;
            Transform t = p_target.transform;
            if(d.ContainsKey(t)) {
                c = d[t];
                if(c) return c;
            }
            c = t.GetComponent(p_type);
            if(!c) return c;
            d[t] = c;
            return c;
        }

        /// <summary>
        /// Performs a GetComponent operation and caches the result. In case the old reference is lost/destroyed it tries again.
        /// </summary>
        /// <typeparam name="T">Component Type</typeparam>
        /// <param name="p_target">Target GameObject</param>
        /// <returns>Component Instance</returns>
        static public T GetComponentCached<T>(this GameObject p_target) where T : Component { return (T)GetComponentCached(p_target,typeof(T)); }
        #endregion

        /// <summary>
        /// Iterate the GameObject hierarchy and sequentialy destroy elements a few steps per frame to ease up the overhead.        
        /// </summary>
        /// <param name="p_target">GameObject to sequentially destroy</param>
        /// <param name="p_steps">How many destructions per frame.</param>
        static public Activity DestroyAsync(this GameObject p_target,int p_steps) {            
            Transform t = p_target ? p_target.transform : null;
            if(!t) return null;            
            //Traverse the whole hierarchy and create the "garbage collection"
            List<Transform> gc = new List<Transform>();
            Action<Transform> cb = delegate(Transform it) { gc.Add(it); };
            t.Traverse(true,cb);
            int k   = 0;
            int c   = gc.Count;
            int stp = p_steps<=0 ? c : p_steps;
            Activity destroy_loop = 
            Activity.Run(delegate(Activity p_a) { 
                for(int i=0;i<stp;i++) {
                    if(k>=c) return false;
                    //Reverse iterate the gc list to destroy leaf->root
                    Transform it = gc[c-1-k];                    
                    GameObject.Destroy(it.gameObject);
                    k++;
                }
                //Keep iterating
                return true;
            });
            destroy_loop.id = "GameObject.DestroyAsync";
            //Returns the running destruction loop
            return destroy_loop;
        }
    }

}
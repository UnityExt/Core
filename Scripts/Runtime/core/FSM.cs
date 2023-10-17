using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using UnityEngine;

namespace UnityExt.Core {

    #region interface IFSMHandler
    /// <summary>
    /// Interface that implements a FSM handler object that will receive all state callbacks and have access to the calling FSM
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IFSMHandler<T> where T : Enum {

        /// <summary>
        /// State loop callback
        /// </summary>
        /// <param name="p_fsm"></param>
        /// <param name="p_state"></param>
        void OnStateUpdate(FSM<T> p_fsm,T p_state);

        /// <summary>
        /// State change callback, called once per change
        /// </summary>
        /// <param name="p_fsm"></param>
        /// <param name="p_from"></param>
        /// <param name="p_to"></param>
        void OnStateChange(FSM<T> p_fsm,T p_from,T p_to);

        /// <summary>
        /// FSM Reset callback
        /// </summary>
        /// <param name="p_fsm"></param>
        void OnStateReset(FSM<T> p_fsm);

    }
    #endregion

    #region class FSM<T>
    /// <summary>
    /// Finite State Machine class. 
    /// Represents a state handling system that loops the current state but also reports state changes, taking care to not repeat the state
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class FSM<T> : FSM where T : Enum {

        /// <summary>
        /// Current State Value
        /// </summary>
        new public T state { 
            get { return base.state.ToEnum<T>(); }
            set { base.state = value.ToLong();   }
        }
        
        /// <summary>
        /// First State
        /// </summary>
        new public T first { get; set; }

        /// <summary>
        /// Handler associated with this FSM
        /// </summary>
        public IFSMHandler<T> handler { get { return m_handler; } set { m_handler = value; hh = value != null; } }
        private IFSMHandler<T> m_handler;

        /// <summary>
        /// Internals
        /// </summary>
        private bool hh;
        
        /// <summary>
        /// CTOR.
        /// </summary>
        /// <param name="p_handler"></param>
        /// <param name="p_first_state"></param>
        public FSM(IFSMHandler<T> p_handler,T p_first_state) {
            handler = p_handler;            
            first   = p_first_state;
        }

        #region Virtuals
        /// <summary>
        /// Callback called upon reset
        /// </summary>
        virtual protected void OnStateReset() { }

        /// <summary>
        /// Callback called each update with the current state
        /// </summary>
        /// <param name="p_state"></param>
        virtual protected void OnState(T p_state) { }

        /// <summary>
        /// Callback called upon state changes different to the current one.
        /// </summary>
        /// <param name="p_from"></param>
        /// <param name="p_to"></param>
        virtual protected void OnStateChange(T p_from,T p_to) { }
        #endregion

        #region Internals
        /// <summary>
        /// Overrides to match 'T'
        /// </summary>        
        protected override void StateUpdate(long p_state)           { T s = p_state.ToEnum<T>(); OnState(s); if (hh) handler.OnStateUpdate(this,s); }
        protected override void StateChange(long p_from,long p_to)  { T f = p_from .ToEnum<T>(); T t = p_to.ToEnum<T>(); OnStateChange(f,t); if (hh) handler.OnStateChange(this,f,t); }
        protected override void StateReset ()                       { OnStateReset(); if (hh) handler.OnStateReset(this); }
        #endregion

    }
    #endregion

    #region class FSM
    /// <summary>
    /// Finite State Machine class. 
    /// Represents a state handling system that loops the current state but also reports state changes, taking care to not repeat the state
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class FSM {

        /// <summary>
        /// Current State Value
        /// </summary>
        public long state {
            get { return m_state; }
            set {
                //If inside the FSM and state change locked, queue for execution end
                if(m_lock_state) { m_queue_state = value; return;  }
                //From To
                long f = m_state;
                long t = value;
                m_queue_state = value;                
                //Skip if equal
                if (f==t) return;
                StateRefresh(t);
            }
        }
        private long m_state;
        private long m_queue_state;
        private long m_prev_state;
        private bool m_lock_state;

        /// <summary>
        /// First State
        /// </summary>
        public long first { get; set; }

        /// <summary>
        /// Flag that adds an extra layer of safety when state refreshing for multi-thread contexts.
        /// </summary>
        public bool threadSafe;

        /// <summary>
        /// Internal
        /// </summary>
        private object m_fsm_lock = new object();

        /// <summary>
        /// Resets the FSM to initial state
        /// </summary>
        public void Reset() {
            state = first;            
            InternalStateReset();            
        }

        /// <summary>
        /// Externally updates the FSM
        /// </summary>
        public void Update() { StateRefresh(state); }

        #region Virtuals

        /// <summary>
        /// FSM got Reset
        /// </summary>
        virtual protected void StateReset() { }

        /// <summary>
        /// FSM State Update
        /// </summary>
        /// <param name="p_state"></param>
        virtual protected void StateUpdate(long p_state) { }

        /// <summary>
        /// FSM State just changed
        /// </summary>
        /// <param name="p_from"></param>
        /// <param name="p_to"></param>
        virtual protected void StateChange(long p_from,long  p_to) { }

        #endregion

        #region Internals
        /// <summary>
        /// General state update handler, detect state difference and triggers all needed updates
        /// </summary>        
        private void StateRefresh(long p_state) {
            if(threadSafe) {
                lock(m_fsm_lock) {
                    InternalStateRefresh(p_state);
                }
            }
            else {
                InternalStateRefresh(p_state);
            }
        }

        private void InternalStateRefresh(long p_state) {
            m_lock_state = true;
            long f = m_prev_state;
            long s = p_state;
            if (f != s) {
                m_state = s;
                StateChange(f,s);
                m_prev_state = m_state;
            }
            StateUpdate(s);
            //If something changed above, apply
            m_state = m_queue_state;
            m_lock_state = false;
        }

        /// <summary>
        /// Safe State Reset
        /// </summary>
        internal void InternalStateReset() {
            if (threadSafe) {
                lock (m_fsm_lock) {
                    m_lock_state = true;
                    StateReset();
                    m_lock_state = false;
                }
            } else {
                m_lock_state = true;
                StateReset();
                m_lock_state = false;
            }            
        }            
        #endregion

    }
    #endregion

}
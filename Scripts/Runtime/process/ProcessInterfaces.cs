using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityExt.Sys;

namespace UnityExt.Sys {

    #region interface IActivity
    /// <summary>
    /// Interface that describes a simple activity that receives a process reference
    /// </summary>
    public interface IActivity {

        /// <summary>
        /// Parent Process
        /// </summary>
        Process process { get; set; }

    }
    #endregion

    #region interface IProcess
    /// <summary>
    /// Interface that describes an activity that receives execution steps from the process
    /// </summary>
    public interface IProcess : IActivity {

        /// <summary>
        /// Handler for when the process execute some state call
        /// </summary>
        void OnProcessUpdate(ProcessContext p_context,ProcessState p_state);

    }
    #endregion

    /// <summary>
    /// Interfaces for objects that wants to perform update loops.
    /// </summary>
    public interface IUpdateable : IActivity {
        /// <summary>
        /// Runs inside Monobehaviour.Update
        /// </summary>
        void OnUpdate();
    }

    /// <summary>
    /// Interfaces for objects that wants to perform late update loops.
    /// </summary>
    public interface ILateUpdateable : IActivity {
        /// <summary>
        /// Runs inside Monobehaviour.LateUpdate
        /// </summary>
        void OnLateUpdate();
    }

    /// <summary>
    /// Interfaces for objects that wants to perform fixed update loops.
    /// </summary>
    public interface IFixedUpdateable : IActivity {
        /// <summary>
        /// Runs inside Monobehaviour.FixedUpdate
        /// </summary>
        void OnFixedUpdate();
    }

    /// <summary>
    /// Interfaces for objects that wants to perform update loops inside a thread
    /// </summary>
    public interface IThreadUpdateable : IActivity {
        /// <summary>
        /// Runs inside a thread
        /// </summary>
        void OnThreadUpdate();
    }

}
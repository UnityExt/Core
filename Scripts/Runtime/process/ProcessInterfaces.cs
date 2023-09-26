using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityExt.Sys;

namespace UnityExt.Sys {


    #region interface IActivity
    /// <summary>
    /// Interface that describes a simple activity that runs until completion
    /// </summary>
    public interface IActivity {

        /// <summary>
        /// Parent Process
        /// </summary>
        Process process { get; set; }

        /// <summary>
        /// Flag that tells if this activity is completed
        /// </summary>
        bool completed { get; set; }

        /// <summary>
        /// Handler for when this activity is executing
        /// </summary>
        void OnStep(ProcessContext p_context);

    }
    #endregion

    #region interface IProcessActivity
    /// <summary>
    /// Interface that describes an Activity executing within a process scope receiving more detailed state information
    /// </summary>
    public interface IProcessActivity : IActivity {

        /// <summary>
        /// Handler for when the process changes execution state.
        /// </summary>
        /// <param name="p_state"></param>
        void OnProcessState(ProcessState p_state);

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
    /// Interfaces for objects that wants to perform update loops not bound by frames
    /// </summary>
    public interface IAsyncUpdateable : IActivity {
        /// <summary>
        /// Runs inside Monobehaviour.Update and only during the 'async-slice' duration per frame, so it can skip a few frames depending on the execution load.
        /// </summary>
        void OnAsyncUpdate();
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
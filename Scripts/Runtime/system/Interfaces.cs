using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityExt.Core {

    /// <summary>
    /// Interfaces for objects that wants to perform update loops.
    /// </summary>
    public interface IUpdateable {
        /// <summary>
        /// Runs inside Monobehaviour.Update
        /// </summary>
        void OnUpdate();
    }

    /// <summary>
    /// Interfaces for objects that wants to perform late update loops.
    /// </summary>
    public interface ILateUpdateable {
        /// <summary>
        /// Runs inside Monobehaviour.LateUpdate
        /// </summary>
        void OnLateUpdate();
    }

    /// <summary>
    /// Interfaces for objects that wants to perform fixed update loops.
    /// </summary>
    public interface IFixedUpdateable {
        /// <summary>
        /// Runs inside Monobehaviour.FixedUpdate
        /// </summary>
        void OnFixedUpdate();
    }

    /// <summary>
    /// Interfaces for objects that wants to perform update loops not bound by frames
    /// </summary>
    public interface IAsyncUpdateable {
        /// <summary>
        /// Runs inside Monobehaviour.Update and only during the 'async-slice' duration per frame, so it can skip a few frames depending on the execution load.
        /// </summary>
        void OnAsyncUpdate();
    }

    /// <summary>
    /// Interfaces for objects that wants to perform update loops inside a thread
    /// </summary>
    public interface IThreadUpdateable {
        /// <summary>
        /// Runs inside a thread
        /// </summary>
        void OnThreadUpdate();
    }

    /// <summary>
    /// Interface that helps job instances to be notified before and after execution to perform data management.
    /// </summary>
    public interface IJobComponent {
        /// <summary>
        /// Method called before either Run or Schedule in main thread.
        /// </summary>        
        void OnInit();
        /// <summary>
        /// Method called after either Run or Schedule in main thread.
        /// </summary>        
        void OnComplete();
        /// <summary>
        /// Method called after the activity is complete or stopped and left the execution pool.
        /// </summary>        
        void OnDestroy();
    }

    /// <summary>
    /// Interface that provides execution progress feedback
    /// </summary>
    public interface IProgressProvider {
        /// <summary>
        /// Returns the process execution progress in the [0,1] range.
        /// </summary>
        float GetProgress();
    }

    #region enum StatusFlag

    /// <summary>
    /// Enumeration that describes possible outcomes from executions.
    /// </summary>
    public enum StatusFlag {
        /// <summary>
        /// Invalid or inconsistent status
        /// </summary>
        Invalid=-1,
        /// <summary>
        /// Idle = Not doing anything
        /// </summary>
        Idle=0,
        /// <summary>
        /// Currently executing
        /// </summary>
        Running,
        /// <summary>
        /// Execution stopped by cancel.
        /// </summary>
        Cancelled,
        /// <summary>
        /// Execution completed in success.
        /// </summary>
        Success,
        /// <summary>
        /// Execution completed with errors.
        /// </summary>
        Error
    }

    #endregion

    /// <summary>
    /// Interface that provides execution status for managers to handle group of executing nodes.
    /// </summary>
    public interface IStatusProvider<T> where T : System.Enum {

        /// <summary>
        /// Returns the node status flag
        /// </summary>
        /// <returns></returns>
        T GetStatus();

    }

    /// <summary>
    /// Interface that provides execution status for managers to handle group of executing nodes.
    /// </summary>
    public interface IStatusProvider : IStatusProvider<StatusFlag> { }

}
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;


namespace UnityExt.Core {

    #region enum StatusType

    /// <summary>
    /// Enumeration that describes possible outcomes from executions.
    /// </summary>
    public enum StatusType {
        /// <summary>
        /// Invalid or inconsistent status
        /// </summary>
        Invalid = -1,
        /// <summary>
        /// Idle = Not doing anything
        /// </summary>
        Idle = 0,
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

    /// <summary>
    /// Base Interface for Process handling unity jobs
    /// </summary>
    public interface IJobProcess : IActivity {

        /// <summary>
        /// Callback called after job completion
        /// </summary>
        bool OnJobUpdate();

        /// <summary>
        /// Handler for cleanup
        /// </summary>
        void OnJobDispose();

        /// <summary>
        /// Fetch the iteration count to execute JobFor
        /// </summary>
        /// <param name=""></param>
        int GetForCount();

        /// <summary>
        /// Fetch the iteration parameters
        /// </summary>
        /// <param name="p_count"></param>
        /// <param name="p_batch"></param>
        int GetBatchCount();                                                                                                                                                                        

    }

    /// <summary>
    /// Base Interface for Process handling unity jobs
    /// </summary>
    public interface IJobProcess<T> : IJobProcess where T : struct {
        /// <summary>
        /// Handler for when the job is instantiated
        /// </summary>
        /// <param name="p_job"></param>
        T OnJobCreate();
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
    public interface IStatusProvider : IStatusProvider<StatusType> { }

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


}
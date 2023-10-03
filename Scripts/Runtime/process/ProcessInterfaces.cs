using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
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

}
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Threading.Tasks;
using UnityEngine;
using System.Threading;
using System;

namespace UnityExt.Core {

    #region enum TaskState
    /// <summary>
    /// State enumeration for the most basic activity for tasks
    /// </summary>
    public enum TaskState {
        /// <summary>
        /// Activity is just created
        /// </summary>
        Idle=0,
        /// <summary>
        /// Waiting to run
        /// </summary>
        Queue,
        /// <summary>
        /// Activity Started
        /// </summary>
        Start,
        /// <summary>
        /// Running
        /// </summary>
        Run,
        /// <summary>
        /// Complete with success
        /// </summary>
        Success,
        /// <summary>
        /// Stopped
        /// </summary>
        Stop,
        /// <summary>
        /// Complete with error
        /// </summary>
        Error
    }
    #endregion

    /// <summary>
    /// Standard Activity extension to handle simple tasks that are queued and completed with/without errors.
    /// </summary>
    public class TaskActivity : Activity<TaskState> {

        #region Events

        /// <summary>
        /// Handler for execution loop
        /// </summary>
        new public Action<TaskActivity> OnExecuteEvent;

        /// <summary>
        /// Handler for state changes
        /// </summary>
        new public Action<TaskActivity,TaskState,TaskState> OnChangeEvent;

        /// <summary>
        /// Auxiliary Event Calling
        /// </summary>        
        protected override void InternalExecuteEvent(TaskState p_state              ) { if (OnExecuteEvent != null) OnExecuteEvent(this            ); }
        protected override void InternalChangeEvent (TaskState p_from,TaskState p_to) { if (OnChangeEvent  != null) OnChangeEvent (this,p_from,p_to); }

        #endregion

        /// <summary>
        /// CTOR.
        /// </summary>
        /// <param name="p_id"></param>
        public TaskActivity(string p_id) : base(p_id) { }

        /// <summary>
        /// Task just started
        /// </summary>
        protected override void OnStart() {
            state = TaskState.Start;
            state = TaskState.Run;
        }

        /// <summary>
        /// Internal start handler
        /// </summary>
        /// <param name="p_editor"></param>
        /// <param name="p_context"></param>
        protected override void InternalStart(bool p_editor,ProcessContext p_context) {
            base.InternalStart(p_editor,p_context);
            state = TaskState.Queue;
        }

        /// <summary>
        /// Task removed from the pool
        /// </summary>
        protected override void OnStop() {
            switch (state) {
                case TaskState.Queue: break;
                default: {
                    state = TaskState.Stop;
                    state = isError ? TaskState.Error : TaskState.Success;
                }
                break;
            }            
        }

    }

}
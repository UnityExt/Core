using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityExt.Sys {

    public class Activity : IActivity {

        /// <summary>
        /// Reference to the calling process
        /// </summary>
        public Process process { get; set; }

        /// <summary>
        /// Flag that specifies this Activity is completed
        /// </summary>
        public bool completed { get; set; }
        
        /// <summary>
        /// Handler for process related steps
        /// </summary>
        /// <param name="p_state"></param>
        public void OnStep(ProcessContext p_context) {
            

        }

    }

}
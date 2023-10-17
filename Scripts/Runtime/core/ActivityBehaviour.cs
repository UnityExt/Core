
using UnityEngine;

namespace UnityExt.Core {

    /// <summary>
    /// Component that extends a Monobehaviour so it can execute inside the activity manager context instead of the slow unity loop.
    /// </summary>
    public class ActivityBehaviour : MonoBehaviour, IActivity {

        /// <summary>
        /// Activity Process
        /// </summary>
        Process IActivity.process { get; set; }
        /// <summary>
        /// When an activity behaviour is enabled it adds itself to the 'Activity' execution pool, based on its chosen interface.
        /// </summary>
        virtual protected void OnEnable() { Process p = Process.Start(this); p.name = name; }
        /// <summary>
        /// When an activity behaviour is disabled it removes itself from the 'Activity' execution pool.
        /// </summary>
        virtual protected void OnDisable() { Process.Dispose(this); }
        /// <summary>
        /// When an activity behaviour is destroyed it removes itself from the 'Activity' execution pool.
        /// </summary>
        virtual protected void OnDestroy() { Process.Dispose(this); }

    }

}

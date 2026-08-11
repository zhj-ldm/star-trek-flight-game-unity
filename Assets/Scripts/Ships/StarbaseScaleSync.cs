using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>
    /// Synchronizes the local scale of this GameObject to a "master" instance of the same
    /// space-station model. Attach to the SECOND instance (slave) and drag the master in.
    /// Runs in edit mode too, so manual scale tweaks on the master propagate live.
    /// </summary>
    [ExecuteInEditMode]
    public class StarbaseScaleSync : MonoBehaviour
    {
        [Tooltip("The master instance whose scale this object mirrors.")]
        public Transform masterTransform;

        void Update()
        {
            if (Application.isPlaying)
            {
                if (masterTransform != null && transform.localScale != masterTransform.localScale)
                    transform.localScale = masterTransform.localScale;
            }
        }

        void LateUpdate()
        {
            if (masterTransform != null && transform.localScale != masterTransform.localScale)
                transform.localScale = masterTransform.localScale;
        }
    }
}
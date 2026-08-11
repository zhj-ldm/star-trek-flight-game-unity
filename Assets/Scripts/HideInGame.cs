using UnityEngine;

namespace StarTrekCombat
{
    /// <summary>Editor-only visual aid. Disables renderer on Awake in Play mode.</summary>
    public class HideInGame : MonoBehaviour
    {
        void Awake()
        {
            if (Application.isPlaying)
            {
                var r = GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }
        }
    }
}

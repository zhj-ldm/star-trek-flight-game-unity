using UnityEngine;

namespace StarTrekCombat
{
    [RequireComponent(typeof(AudioSource))]
    public class ShipSFX : MonoBehaviour
    {
        [Header("Audio Clips")]
        public AudioClip phaserClip;
        public AudioClip torpedoLaunchClip;
        public AudioClip torpedoHitClip;
        public AudioClip destroyClip;
        public AudioClip warpClip;

        [Header("New Audio Clips")]
        public AudioClip engineClip;
        public AudioClip bridgeClip;
        public AudioClip redAlertClip;
        public AudioClip warpEngageClip;
        public AudioClip warpExitClip;
        public AudioClip rcsClip;

        [Header("Settings")]
        public float phaserVolume = 0.4f;
        public float torpedoVolume = 0.6f;
        public float hitVolume = 0.5f;
        public float destroyVolume = 1f;
        public float warpVolume = 0.8f;
        public float engineVolume = 2f;
        public float bridgeVolume = 12f;
        public float redAlertVolume = 0.7f;

        private AudioSource _audioSource;
        private AudioSource _warpSource;
        private AudioSource _engineSource;
        private AudioSource _bridgeSource;
        private AudioSource _rcsSource;
        private AudioSource _phaserSource;
        private bool _redAlertPlayed;

        /// <summary>Length of the phaser clip in seconds (0 if no clip).</summary>
        public float PhaserClipLength => phaserClip != null ? phaserClip.length : 0f;

        void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f;
            _audioSource.dopplerLevel = 0f;
            _audioSource.volume = 1f;
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;

            _warpSource = gameObject.AddComponent<AudioSource>();
            _warpSource.spatialBlend = 0f;
            _warpSource.dopplerLevel = 0f;
            _warpSource.loop = false;
            _warpSource.playOnAwake = false;

            _engineSource = gameObject.AddComponent<AudioSource>();
            _engineSource.spatialBlend = 0f;
            _engineSource.dopplerLevel = 0f;
            _engineSource.loop = true;
            _engineSource.playOnAwake = false;
            _engineSource.volume = engineVolume;

            _bridgeSource = gameObject.AddComponent<AudioSource>();
            _bridgeSource.spatialBlend = 0f;
            _bridgeSource.dopplerLevel = 0f;
            _bridgeSource.loop = true;
            _bridgeSource.playOnAwake = false;
            _bridgeSource.volume = bridgeVolume;

            // RCS thruster sustain loop. Loop=true so the clip repeats with no gap.
            _rcsSource = gameObject.AddComponent<AudioSource>();
            _rcsSource.spatialBlend = 0f;
            _rcsSource.dopplerLevel = 0f;
            _rcsSource.loop = true;
            _rcsSource.playOnAwake = false;
            _rcsSource.volume = 0.7f;
            if (rcsClip == null)
                rcsClip = Resources.Load<AudioClip>("RCS_Sustain");
            if (rcsClip != null)
                _rcsSource.clip = rcsClip;

            // Dedicated phaser source — NOT PlayOneShot, so we can Stop() it mid-play
            _phaserSource = gameObject.AddComponent<AudioSource>();
            _phaserSource.spatialBlend = 0f;
            _phaserSource.dopplerLevel = 0f;
            _phaserSource.loop = false;
            _phaserSource.playOnAwake = false;
        }

        void Start()
        {
            StartBridge();
        }

        public void StartBridge()
        {
            if (bridgeClip != null && _bridgeSource != null && !_bridgeSource.isPlaying)
            {
                _bridgeSource.clip = bridgeClip;
                _bridgeSource.Play();
            }
        }

        public void PlayEngine()
        {
            if (engineClip != null && _engineSource != null && !_engineSource.isPlaying)
            {
                _engineSource.clip = engineClip;
                _engineSource.volume = engineVolume;
                _engineSource.Play();
            }
        }

        public void StopEngine()
        {
            if (_engineSource != null && _engineSource.isPlaying)
                _engineSource.Stop();
        }

        /// <summary>Start looping the RCS thruster sustain. Repeated calls while playing are
        /// harmless (loop source — no stacking). Guarded with a tiny "restart" so a held key
        /// reads seamlessly.</summary>
        public void PlayRCS()
        {
            if (_rcsSource == null) return;
            if (rcsClip == null) rcsClip = Resources.Load<AudioClip>("RCS_Sustain");
            if (rcsClip == null) return;
            if (_rcsSource.clip != rcsClip) _rcsSource.clip = rcsClip;
            if (!_rcsSource.isPlaying)
                _rcsSource.Play();
        }

        /// <summary>Immediately stop the RCS thruster sustain sound.</summary>
        public void StopRCS()
        {
            if (_rcsSource != null && _rcsSource.isPlaying)
                _rcsSource.Stop();
        }

        public void PlayRedAlert()
        {
            if (_redAlertPlayed) return;
            _redAlertPlayed = true;
            if (redAlertClip != null && _audioSource != null)
                _audioSource.PlayOneShot(redAlertClip, redAlertVolume);
        }

        public void ResetRedAlert()
        {
            _redAlertPlayed = false;
        }

        public void PlayWarpEngage()
        {
            if (warpEngageClip != null && _warpSource != null)
            {
                _warpSource.clip = warpEngageClip;
                _warpSource.volume = warpVolume;
                _warpSource.Play();
            }
        }

        public void PlayWarpExit()
        {
            if (warpExitClip != null && _warpSource != null)
            {
                _warpSource.clip = warpExitClip;
                _warpSource.volume = warpVolume;
                _warpSource.Play();
            }
        }

        // Legacy methods (kept for compatibility)
        public void PlayPhaserFire()
        {
            if (phaserClip != null && _phaserSource != null)
            {
                _phaserSource.clip = phaserClip;
                _phaserSource.volume = phaserVolume;
                _phaserSource.Play();
            }
        }

        /// <summary>Stop the phaser sound immediately (if still playing).</summary>
        public void StopPhaserFire()
        {
            if (_phaserSource != null && _phaserSource.isPlaying)
                _phaserSource.Stop();
        }

        public void PlayTorpedoLaunch()
        {
            if (torpedoLaunchClip != null && _audioSource != null)
                _audioSource.PlayOneShot(torpedoLaunchClip, torpedoVolume);
        }

        public void PlayTorpedoHit()
        {
            if (torpedoHitClip != null && _audioSource != null)
                _audioSource.PlayOneShot(torpedoHitClip, hitVolume);
        }

        public void PlayDestroy()
        {
            if (destroyClip != null && _audioSource != null)
                _audioSource.PlayOneShot(destroyClip, destroyVolume);
        }

        public void PlayWarp()
        {
            PlayWarpEngage();
        }

        public void StopWarp()
        {
            if (_warpSource != null && _warpSource.isPlaying)
                _warpSource.Stop();
        }
    }
}

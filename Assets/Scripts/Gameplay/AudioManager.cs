using UnityEngine;
using UnityEngine.InputSystem;

namespace JumpNowBro.Gameplay
{
    /// The single audio routing point, on Bootstrap's persistent Manager (#42). Side-effect components call
    /// PlayJump / PlayLand / PlayDash / PlaySwap; nothing inside the simulation touches audio. Clips are assigned
    /// in the Inspector so they stay trivially swappable. Music (#116) and a volume control (#128) hang off this.
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("SFX clips (assign in the Inspector)")]
        [SerializeField] AudioClip jumpClip;
        [SerializeField] AudioClip landClip;
        [SerializeField] AudioClip dashClip;
        [SerializeField] AudioClip swapClip;

        [SerializeField, Range(0f, 1f)] float sfxVolume = 0.8f;   // master SFX level; #128's settings panel will drive this

        [Header("Music (assign in the Inspector)")]
        [SerializeField] AudioClip musicClip;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.5f;

        AudioSource sfx;
        AudioSource music;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;   // 2D: SFX are feedback, not positional

            // Looping background music (#116). Persists across level loads via DontDestroyOnLoad, so the loop
            // is continuous through transitions; AudioSource.loop avoids a re-trigger gap (the only seam risk
            // is the clip's own encoder padding — provide a loop-trimmed clip if a gap shows).
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            music.volume = musicVolume;
            if (musicClip != null) { music.clip = musicClip; music.Play(); }
        }

        // Interim mute affordance (#116 wants "at least a way to mute it"); #128's settings panel replaces it.
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) ToggleMusicMuted();
        }

        public bool MusicMuted => music != null && music.mute;
        public void SetMusicMuted(bool muted) { if (music != null) music.mute = muted; }
        public void ToggleMusicMuted() => SetMusicMuted(!MusicMuted);

        public void PlayJump() => Play(jumpClip);
        public void PlayLand() => Play(landClip);
        public void PlayDash() => Play(dashClip);
        public void PlaySwap() => Play(swapClip);

        // PlayOneShot so overlapping triggers (e.g. a dash landing into a swap) layer instead of cutting each other.
        void Play(AudioClip clip)
        {
            if (clip != null && sfx != null) sfx.PlayOneShot(clip, sfxVolume);
        }
    }
}

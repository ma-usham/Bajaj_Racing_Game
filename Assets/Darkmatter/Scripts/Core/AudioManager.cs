using UnityEngine;

namespace Darkmatter.Core
{
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Sources")]
        [Tooltip("Looping source for background music. Created automatically when left empty.")]
        [SerializeField]
        private AudioSource musicSource;

        [Tooltip("Source used for one-shot effects. Created automatically when left empty.")] [SerializeField]
        private AudioSource sfxSource;

        [Tooltip("Looping source for the vehicle note. Created automatically when left empty. " +
                 "It needs a source of its own because a one-shot cannot loop, and because " +
                 "bending its pitch on the shared sfx source would bend the countdown beeps too.")]
        [SerializeField]
        private AudioSource engineSource;

        [Header("Music")] [Tooltip("Played on startup. Leave empty to start silent.")] [SerializeField]
        private AudioClip backgroundMusic;

        [Header("Volume")] [Range(0f, 1f)] [SerializeField]
        private float musicVolume = 1f;

        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

        public AudioClip CurrentMusic => musicSource != null ? musicSource.clip : null;

        public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;

        public bool IsEnginePlaying => engineSource != null && engineSource.isPlaying;

        /// <summary>
        /// What the game last asked the engine to sound like, before the sfx volume is applied.
        /// Kept so moving the sfx slider can rescale a note that is already running.
        /// </summary>
        private float engineLevel = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            musicSource = PrepareSource(musicSource, loop: true, volume: musicVolume);
            sfxSource = PrepareSource(sfxSource, loop: false, volume: sfxVolume);
            engineSource = PrepareSource(engineSource, loop: true, volume: sfxVolume);

            if (backgroundMusic != null)
            {
                PlayMusic(backgroundMusic);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning($"{name}: PlayMusic was called with no clip.", this);
                return;
            }

            if (musicSource.clip == clip && musicSource.isPlaying)
            {
                return;
            }

            musicSource.clip = clip;
            musicSource.Play();
        }

        public void StopMusic()
        {
            musicSource.Stop();
            musicSource.clip = null;
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning($"{name}: PlaySfx was called with no clip.", this);
                return;
            }

            sfxSource.PlayOneShot(clip);
        }

        public void PlayEngine(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning($"{name}: PlayEngine was called with no clip.", this);
                return;
            }

            if (engineSource.clip == clip && engineSource.isPlaying)
            {
                return;
            }

            engineSource.clip = clip;
            engineSource.Play();
        }

        public void StopEngine()
        {
            engineSource.Stop();
        }

        /// <summary>
        /// The engine note, driven every frame by whatever is doing the driving.
        /// </summary>
        /// <param name="pitch">Unclamped, so a boost pad can rev past the usual top note.</param>
        /// <param name="volume">A 0 to 1 share of the sfx volume, not an absolute level, so the
        /// sfx slider still governs how loud the engine gets.</param>
        public void SetEngine(float pitch, float volume)
        {
            engineLevel = Mathf.Clamp01(volume);
            engineSource.pitch = pitch;
            engineSource.volume = engineLevel * sfxVolume;
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            musicSource.volume = musicVolume;
        }

        public void SetSfxVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            sfxSource.volume = sfxVolume;
            engineSource.volume = engineLevel * sfxVolume;
        }

        private AudioSource PrepareSource(AudioSource source, bool loop, float volume)
        {
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            return source;
        }
    }
}

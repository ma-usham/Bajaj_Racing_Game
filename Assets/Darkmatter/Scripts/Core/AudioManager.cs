using UnityEngine;

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

    [Header("Music")] [Tooltip("Played on startup. Leave empty to start silent.")] [SerializeField]
    private AudioClip backgroundMusic;

    [Header("Volume")] [Range(0f, 1f)] [SerializeField]
    private float musicVolume = 1f;

    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    public AudioClip CurrentMusic => musicSource != null ? musicSource.clip : null;

    public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;

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

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        musicSource.volume = musicVolume;
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        sfxSource.volume = sfxVolume;
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
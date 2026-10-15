using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotelTogether
{
    /// <summary>Day/night music crossfade, pause muffling and day/night stings.</summary>
    public sealed class HotelAudioDirector : MonoBehaviour
    {
        AudioSource dayMusic, nightMusic;
        AudioLowPassFilter dayFilter, nightFilter;
        HotelGuestSimulation hotel;
        HotelPlayer player;
        float muffle, level, started;
        bool wasNight, hadState;
        int lastDay;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin() { Ensure(SceneManager.GetActiveScene()); }
        static void Loaded(Scene scene, LoadSceneMode mode) { Ensure(scene); }
        static void Ensure(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name != "HotelLobby") return;
            foreach (var d in FindObjectsByType<HotelAudioDirector>(FindObjectsInactive.Include)) if (d.gameObject.scene == scene) return;
            var root = new GameObject("Hotel music and stings");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<HotelAudioDirector>();
        }

        AudioSource MakeSource(string clipName, out AudioLowPassFilter filter)
        {
            var go = new GameObject(clipName); go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = HotelAudio.Clip(clipName); source.loop = true; source.playOnAwake = false;
            source.spatialBlend = 0; source.ignoreListenerPause = true; source.volume = 0; source.priority = 20;
            filter = go.AddComponent<AudioLowPassFilter>(); filter.cutoffFrequency = 22000;
            if (source.clip) source.Play();
            return source;
        }

        void Start()
        {
            dayMusic = MakeSource("music_day", out dayFilter);
            nightMusic = MakeSource("music_night", out nightFilter);
            started = Time.unscaledTime;
        }

        void Update()
        {
            if (!hotel) hotel = FindFirstObjectByType<HotelGuestSimulation>();
            if (!player) player = FindFirstObjectByType<HotelPlayer>();
            var visual = HotelVisualDirector.Instance;
            float night = visual ? visual.NightBlend : hotel ? hotel.NightBlend : 0;
            bool title = player && !player.HasStartedShift;
            bool paused = player && player.GameplayPaused && !title;

            // Menu pause: duck and muffle (the music source ignores the listener pause).
            muffle = Mathf.MoveTowards(muffle, paused ? 1 : 0, Time.unscaledDeltaTime * 3);
            float fade = Mathf.Clamp01((Time.unscaledTime - started) / 2.5f);
            float master = HotelAudio.MusicVolume * fade * Mathf.Lerp(1, .55f, muffle) * (title ? .85f : .6f);
            float cutoff = Mathf.Lerp(22000, 700, muffle * muffle);
            float n = Mathf.SmoothStep(0, 1, night);
            SetTrack(dayMusic, dayFilter, master * Mathf.Cos(n * Mathf.PI * .5f), cutoff);
            SetTrack(nightMusic, nightFilter, master * Mathf.Sin(n * Mathf.PI * .5f), cutoff);

            if (!hotel || title) { if (hotel) { wasNight = hotel.IsNight; lastDay = hotel.DayNumber; hadState = true; } return; }
            if (!hadState) { wasNight = hotel.IsNight; lastDay = hotel.DayNumber; hadState = true; return; }
            if (hotel.IsNight && !wasNight) HotelAudio.Play("sting_night", .8f, 1f, .5f);
            else if (!hotel.IsNight && wasNight) HotelAudio.Play("sting_dawn", .8f, 1f, .5f);
            else if (hotel.DayNumber != lastDay && !hotel.IsNight) HotelAudio.Play("sting_day", .7f, 1f, .5f);
            wasNight = hotel.IsNight; lastDay = hotel.DayNumber;
        }

        static void SetTrack(AudioSource source, AudioLowPassFilter filter, float volume, float cutoff)
        {
            if (!source) return;
            source.volume = volume;
            if (filter) filter.cutoffFrequency = cutoff;
            if (source.clip && !source.isPlaying) source.Play();
        }
    }
}

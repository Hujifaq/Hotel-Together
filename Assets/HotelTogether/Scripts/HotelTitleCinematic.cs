using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HotelTogether
{
    // First-launch attract mode: slow cinematic shots of the hotel behind the
    // title and employee menu, then a swoop from the camera into first person.
    // It borrows the player's own camera, so no extra camera renders.
    [DefaultExecutionOrder(1000), DisallowMultipleComponent, RequireComponent(typeof(HotelPlayer))]
    public sealed class HotelTitleCinematic : MonoBehaviour
    {
        [Serializable]
        public struct Shot
        {
            public string caption;
            public Vector3 from, to, lookFrom, lookTo;
            [Range(20, 70)] public float fieldOfView;
            [Min(2)] public float seconds;
            [Range(0, 1)] public float night;
        }

        enum Phase { Attract, Swoop, Done }

        public List<Shot> shots = new List<Shot>();
        [Min(.4f)] public float swoopSeconds = 1.55f;
        [Min(.1f)] public float cutSeconds = .32f;
        public LayerMask cameraCollision = ~((1 << 8) | (1 << 9) | (1 << 2));

        public bool TitleVisible { get; private set; }
        /// <summary>True while the cinematic owns the screen behind the menus.</summary>
        public bool Attracting => phase == Phase.Attract;
        public bool Active => phase != Phase.Done;
        public float TitleAge => Time.unscaledTime - titleStarted;
        public float DismissAge => Time.unscaledTime - dismissedAt;
        /// <summary>0..1 full-screen fade used for the opening and shot cuts.</summary>
        public float Blackout { get; private set; } = 1;
        /// <summary>0..1 cinematic letterbox amount.</summary>
        public float Letterbox { get; private set; } = 1;
        public string Caption { get; private set; }
        public float CaptionAge => Time.unscaledTime - captionChanged;
        public float CaptionNight { get; private set; }

        HotelPlayer player;
        Camera view;
        Phase phase = Phase.Done;
        int shotIndex;
        float shotTime, titleStarted, dismissedAt = -100, captionChanged, swoopTime, smoothedDistance = -1;
        float originalFov;
        Vector3 originalLocalPosition, trueLocalPosition, swoopFrom;
        Quaternion originalLocalRotation, trueLocalRotation, swoopFromRotation;
        float swoopFromFov, lastFov = 60;
        Vector3 lastPosition;
        Quaternion lastRotation = Quaternion.identity;
        bool overriding, swoopHasCut, swoopCutDone;
        readonly Dictionary<Renderer, bool> hiddenRenderers = new Dictionary<Renderer, bool>();
        HotelEmployeeRig shownAvatar;
        ShadowCastingMode avatarHeadMode;
        readonly List<int> validShots = new List<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
            Attach(SceneManager.GetActiveScene());
        }
        static void Loaded(Scene scene, LoadSceneMode mode) { Attach(scene); }
        static void Attach(Scene scene)
        {
            if (!Application.isPlaying || !scene.IsValid() || scene.name != "HotelLobby") return;
            foreach (var p in FindObjectsByType<HotelPlayer>(FindObjectsSortMode.None))
                if (p.gameObject.scene == scene && !p.GetComponent<HotelTitleCinematic>()) p.gameObject.AddComponent<HotelTitleCinematic>();
        }

        void Awake()
        {
            player = GetComponent<HotelPlayer>();
            view = player.view;
            if (!view) { enabled = false; return; }
            originalFov = view.fieldOfView;
            originalLocalPosition = trueLocalPosition = view.transform.localPosition;
            originalLocalRotation = trueLocalRotation = view.transform.localRotation;
        }

        void Start()
        {
            if (!view) return;
            if (shots.Count == 0) BuildDefaultShots();
            ValidateShots();
            phase = Phase.Attract;
            TitleVisible = true;
            titleStarted = Time.unscaledTime;
            // Coming from the main menu: skip the "click to clock in" title and go straight to the employee picker.
            if (HotelMainMenu.ConsumeLaunch()) { TitleVisible = false; dismissedAt = Time.unscaledTime; }
            Blackout = 1;
            BeginShot(0);
        }

        // Default framing is derived from the hotel's own arrival and reception points,
        // so the shots follow the level if those markers move.
        void BuildDefaultShots()
        {
            var hotel = FindAnyObjectByType<HotelGuestSimulation>();
            Vector3 arrival = hotel ? hotel.arrivalPoint : new Vector3(0, 0, 13);
            Vector3 reception = hotel ? hotel.receptionPoint : new Vector3(-5.65f, 0, -3.6f);
            Vector3 facade = new Vector3(arrival.x + 1, 0, arrival.z - 4.5f);
            shots.Add(new Shot { caption = "WELCOME TO THE GRAND REOPENING", from = facade + new Vector3(33, 21, 24), to = facade + new Vector3(-27, 19, 26), lookFrom = facade + new Vector3(0, 4, -18), lookTo = facade + new Vector3(0, 5, -20), fieldOfView = 48, seconds = 9, night = 0 });
            shots.Add(new Shot { caption = "CHECK IN GUESTS. CARRY LUGGAGE. TRY NOT TO DROP IT.", from = facade + new Vector3(-13, 1.9f, 19), to = facade + new Vector3(11, 2.5f, 18), lookFrom = facade + new Vector3(-1.5f, 2.4f, 1.5f), lookTo = facade + new Vector3(1.5f, 2.8f, 1.5f), fieldOfView = 40, seconds = 8, night = 0 });
            shots.Add(new Shot { caption = "RUN THE HOTEL TOGETHER", from = reception + new Vector3(10.5f, 1.85f, 6.6f), to = reception + new Vector3(7.6f, 2.2f, 3.2f), lookFrom = reception + new Vector3(0, 1.25f, 0), lookTo = reception + new Vector3(.6f, 1.3f, .2f), fieldOfView = 46, seconds = 8, night = 0 });
            shots.Add(new Shot { caption = "...AND SURVIVE THE NIGHT", from = facade + new Vector3(14, 1.6f, 15), to = facade + new Vector3(-9, 2.8f, 13), lookFrom = facade + new Vector3(1, 3, 1.5f), lookTo = facade + new Vector3(-1, 3.4f, 1.5f), fieldOfView = 38, seconds = 9, night = 1 });
        }

        // Shots whose camera would be pushed far toward the subject by walls are skipped.
        void ValidateShots()
        {
            validShots.Clear();
            for (int i = 0; i < shots.Count; i++)
            {
                var s = shots[i];
                if (Clearance(s.lookFrom, s.from) > .55f && Clearance(s.lookTo, s.to) > .55f) validShots.Add(i);
            }
            if (validShots.Count == 0)
            {
                // Fallback: a slow orbit around the player's starting position.
                var eye = view.transform.position;
                shots.Add(new Shot { caption = "RUN THE HOTEL TOGETHER", from = eye + new Vector3(3.5f, .7f, 3.5f), to = eye + new Vector3(-3.5f, .9f, 3.5f), lookFrom = eye - Vector3.up * .3f, lookTo = eye - Vector3.up * .3f, fieldOfView = 50, seconds = 10 });
                validShots.Add(shots.Count - 1);
            }
        }
        float Clearance(Vector3 target, Vector3 camera)
        {
            var delta = camera - target; float length = delta.magnitude;
            if (length < .01f) return 1;
            return Physics.SphereCast(target, .3f, delta / length, out var hit, length, cameraCollision, QueryTriggerInteraction.Ignore) ? hit.distance / length : 1;
        }

        void BeginShot(int index)
        {
            shotIndex = validShots.Count == 0 ? 0 : ((index % validShots.Count) + validShots.Count) % validShots.Count;
            shotTime = 0; smoothedDistance = -1;
            var shot = shots[validShots[shotIndex]];
            if (Caption != shot.caption) { Caption = shot.caption; captionChanged = Time.unscaledTime; CaptionNight = shot.night; }
        }

        public void DismissTitle()
        {
            if (!TitleVisible) return;
            TitleVisible = false; dismissedAt = Time.unscaledTime;
        }

        void Update()
        {
            // Hand the camera back to the player before its LateUpdate runs.
            if (overriding && view) { view.transform.localPosition = trueLocalPosition; view.transform.localRotation = trueLocalRotation; }
        }

        void LateUpdate()
        {
            if (!view || phase == Phase.Done) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            if (phase == Phase.Attract)
            {
                if (player.HasStartedShift) { BeginSwoop(); }
                else { Attract(dt); return; }
            }
            if (phase == Phase.Swoop) Swoop(dt);
        }

        void Attract(float dt)
        {
            var director = HotelVisualDirector.Instance;
            var shot = shots[validShots[shotIndex]];
            shotTime += dt;
            float t = Mathf.Clamp01(shotTime / shot.seconds);
            float eased = t * t * (3 - 2 * t);
            var target = Vector3.Lerp(shot.lookFrom, shot.lookTo, eased);
            var desired = Vector3.Lerp(shot.from, shot.to, eased);
            var position = SafePosition(target, desired, dt);
            // Gentle handheld drift keeps the frame alive.
            float clock = Time.unscaledTime;
            position += new Vector3(Mathf.Sin(clock * .53f), Mathf.Sin(clock * .71f + 1.3f), Mathf.Sin(clock * .37f + 2.1f)) * .045f;
            Override(position, Quaternion.LookRotation(target - position, Vector3.up), shot.fieldOfView);

            // Dip to black around each cut; the opening fades up from black.
            float opening = Mathf.Clamp01(1 - TitleAge / 1.6f);
            float cutIn = Mathf.Clamp01(1 - shotTime / cutSeconds);
            float cutOut = Mathf.Clamp01((shotTime - (shot.seconds - cutSeconds)) / cutSeconds);
            Blackout = Mathf.Max(opening, Mathf.Max(TitleAge > 1.6f ? cutIn : 0, cutOut));
            Letterbox = Mathf.MoveTowards(Letterbox, TitleVisible ? 1 : 0, dt * 2.2f);
            if (director)
            {
                director.nightOverride = shot.night;
                director.cinematicWeight = Mathf.MoveTowards(director.cinematicWeight, 1, dt * 2);
                director.focusDistance = Vector3.Distance(position, target);
            }
            ShowPlayerAsCharacter(true);
            if (shotTime >= shot.seconds) BeginShot(shotIndex + 1);
        }

        Vector3 SafePosition(Vector3 target, Vector3 desired, float dt)
        {
            var delta = desired - target; float length = delta.magnitude;
            if (length < .01f) return desired;
            float allowed = length;
            if (Physics.SphereCast(target, .28f, delta / length, out var hit, length, cameraCollision, QueryTriggerInteraction.Ignore))
                allowed = Mathf.Max(.8f, hit.distance - .1f);
            smoothedDistance = smoothedDistance < 0 ? allowed : allowed < smoothedDistance ? allowed : Mathf.Lerp(smoothedDistance, allowed, 1 - Mathf.Exp(-dt * 2.5f));
            return target + delta / length * smoothedDistance;
        }

        void BeginSwoop()
        {
            phase = Phase.Swoop; swoopTime = 0; TitleVisible = false;
            HotelAudio.Play("sting_day", .9f, 1, .5f);
            // Start from the last cinematic frame; the camera now holds the player's eye pose.
            swoopFrom = lastPosition; swoopFromRotation = lastRotation; swoopFromFov = lastFov;
            var eye = view.transform.position;
            // If walls sit between the shot and the player, cut to just behind them first.
            swoopHasCut = Physics.Linecast(swoopFrom, eye, cameraCollision, QueryTriggerInteraction.Ignore);
            swoopCutDone = !swoopHasCut;
            var director = HotelVisualDirector.Instance;
            if (director) director.nightOverride = -1;
        }

        void Swoop(float dt)
        {
            swoopTime += dt;
            var director = HotelVisualDirector.Instance;
            // Player's own camera pose for this frame (already computed by HotelPlayer.LateUpdate).
            var eyePosition = view.transform.position; var eyeRotation = view.transform.rotation;
            trueLocalPosition = view.transform.localPosition; trueLocalRotation = view.transform.localRotation;
            float cut = swoopHasCut ? cutSeconds : 0;
            if (swoopHasCut && !swoopCutDone)
            {
                Blackout = Mathf.Clamp01(swoopTime / cut);
                Override(swoopFrom, swoopFromRotation, swoopFromFov);
                if (swoopTime >= cut)
                {
                    swoopCutDone = true;
                    var back = -player.transform.forward * 2.6f + Vector3.up * .9f;
                    float clear = Clearance(eyePosition, eyePosition + back);
                    swoopFrom = eyePosition + back * Mathf.Max(.35f, clear * .92f);
                    swoopFromRotation = Quaternion.LookRotation(eyePosition + player.transform.forward * 3 - swoopFrom, Vector3.up);
                    swoopFromFov = Mathf.Max(originalFov, 52);
                }
                return;
            }
            float t = Mathf.Clamp01((swoopTime - cut) / swoopSeconds);
            float e = t * t * t * (t * (6 * t - 15) + 10); // smootherstep
            if (swoopHasCut) Blackout = Mathf.Clamp01(1 - t * 4); else Blackout = 0;
            var arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * Mathf.Min(1.2f, Vector3.Distance(swoopFrom, eyePosition) * .12f);
            var position = Vector3.Lerp(swoopFrom, eyePosition, e) + arc;
            Override(position, Quaternion.Slerp(swoopFromRotation, eyeRotation, e), Mathf.Lerp(swoopFromFov, originalFov, e));
            Letterbox = Mathf.MoveTowards(Letterbox, 0, dt * 3);
            if (director) director.cinematicWeight = 1 - e;
            // Hide the avatar's head before the camera passes through it.
            ShowPlayerAsCharacter(t < .62f);
            if (t >= 1) Finish();
        }

        void Override(Vector3 position, Quaternion rotation, float fov)
        {
            if (!overriding) { trueLocalPosition = view.transform.localPosition; trueLocalRotation = view.transform.localRotation; }
            overriding = true;
            lastPosition = position; lastRotation = rotation; lastFov = fov;
            view.transform.SetPositionAndRotation(position, rotation);
            view.fieldOfView = fov;
        }

        // During the attract mode the player's employee stands in the lobby as a
        // full character; the first-person hands are hidden until the swoop lands.
        void ShowPlayerAsCharacter(bool show)
        {
            var avatar = player.employee ? player.employee.Avatar : null;
            if (show)
            {
                if (avatar && avatar != shownAvatar)
                {
                    RestoreAvatar();
                    shownAvatar = avatar;
                    if (avatar.headRenderer) { avatarHeadMode = avatar.headRenderer.shadowCastingMode; avatar.headRenderer.shadowCastingMode = ShadowCastingMode.On; }
                }
                var hands = player.transform.Find("Continuous cartoon hands and arms");
                if (hands)
                    foreach (var renderer in hands.GetComponentsInChildren<Renderer>(true))
                        if (!hiddenRenderers.ContainsKey(renderer)) { hiddenRenderers.Add(renderer, renderer.enabled); renderer.enabled = false; }
                        else renderer.enabled = false;
            }
            else RestoreAvatar();
        }
        void RestoreAvatar()
        {
            if (shownAvatar && shownAvatar.headRenderer) shownAvatar.headRenderer.shadowCastingMode = avatarHeadMode;
            shownAvatar = null;
        }
        void RestoreHands()
        {
            foreach (var entry in hiddenRenderers) if (entry.Key) entry.Key.enabled = entry.Value;
            hiddenRenderers.Clear();
        }

        void Finish()
        {
            phase = Phase.Done; Blackout = 0; Letterbox = 0;
            if (overriding && view)
            {
                view.transform.localPosition = trueLocalPosition; view.transform.localRotation = trueLocalRotation;
                view.fieldOfView = originalFov;
            }
            overriding = false;
            RestoreAvatar(); RestoreHands();
            var director = HotelVisualDirector.Instance;
            if (director) { director.cinematicWeight = 0; director.nightOverride = -1; }
        }

        void OnDisable() { if (phase != Phase.Done) Finish(); }
    }
}

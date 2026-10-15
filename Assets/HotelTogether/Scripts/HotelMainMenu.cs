using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static HotelTogether.HotelToyKit;

namespace HotelTogether
{
    /// <summary>
    /// Title screen for the HotelMainMenu scene: clay hotel background, four parallax/floating
    /// character cutouts on the left 60%, title and chunky buttons on the right 40%.
    /// Mouse, keyboard and gamepad navigation; Options and Credits panels; PLAY loads the hotel.
    /// Every layout and animation value below is exposed in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HotelMainMenu : MonoBehaviour
    {
        public const string SceneName = "HotelMainMenu";
        const string Prefix = "HotelTogether.Settings.";
        const string ArtFolder = "HotelTogetherMenu/";

        /// <summary>Set when PLAY GAME is pressed; the hotel then skips its own "click to clock in" title.</summary>
        public static bool LaunchedFromMenu { get; private set; }
        public static bool ConsumeLaunch() { bool value = LaunchedFromMenu; LaunchedFromMenu = false; return value; }
        public static bool MenuSceneAvailable => Application.CanStreamedLevelBeLoaded(SceneName);
        public static bool ReducedMotion => PlayerPrefs.GetInt(Prefix + "ReducedMotion", 0) != 0;

        /// <summary>Loads the main menu from gameplay (used by the in-game pause menu).</summary>
        public static void ReturnToMenu()
        {
            if (!MenuSceneAvailable) return;
            Time.timeScale = 1;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
        }

        [Serializable]
        public class MenuCharacter
        {
            public string label;
            [Tooltip("PNG inside Resources/HotelTogetherMenu (without extension).")]
            public string resource;
            [Tooltip("Feet position (bottom centre) in the design stage, pixels. The stage is 1152 x 1080 = the left 60% of a 1920 x 1080 screen.")]
            public Vector2 feet;
            [Tooltip("Drawn height in design-stage pixels. Width follows the image aspect ratio.")]
            public float height = 500;
            [Tooltip("Parallax depth. 1 = foreground (moves most), 0 = does not move.")]
            [Range(0, 1.5f)] public float depth = .5f;
            public float bobAmplitude = 8, bobSpeed = .3f, bobPhase;
            public float rotationAmplitude = 1.5f, rotationSpeed = .2f, rotationPhase;
            [Tooltip("Seconds after the menu opens before this character slides in.")]
            public float entranceDelay;
            public bool groundShadow = true;
            [NonSerialized] public Texture2D texture;
        }

        [Header("Scenes")]
        public string gameplayScene = "HotelLobby";

        [Header("Layout")]
        [Tooltip("Reference resolution for UI scale.")]
        public Vector2 referenceResolution = new Vector2(1920, 1080);
        [Range(.4f, .75f)] public float characterAreaFraction = .6f;
        public Vector2 designStage = new Vector2(1152, 1080);
        [Tooltip("Where the design stage sits inside a wider character area (0 = left, 1 = right).")]
        [Range(0, 1)] public float stageAnchor = .55f;
        [Range(0, 1)] public float backgroundAnchorX = .5f;
        public string backgroundResource = "background";

        [Header("Parallax")]
        [Range(0, 2)] public float parallaxStrength = 1;
        [Tooltip("Largest offset in reference pixels for a depth-1 layer.")]
        [Range(0, 40)] public float maxParallaxOffset = 20;
        [Range(0, 1)] public float backgroundDepth = .15f;
        [Range(.5f, 20)] public float parallaxSmoothing = 5;

        [Header("Floating")]
        [Range(0, 2)] public float floatStrength = 1;
        [Range(.1f, 3)] public float floatSpeed = 1;

        [Header("Entrance")]
        [Range(.1f, 3)] public float entranceDuration = .9f;

        [Header("Characters (drawn back to front)")]
        public List<MenuCharacter> characters = new List<MenuCharacter>
        {
            new MenuCharacter { label = "Zombie guest", resource = "zombie-guest", feet = new Vector2(935, 690), height = 390, depth = .35f, bobAmplitude = 6, bobSpeed = .27f, bobPhase = .7f, rotationAmplitude = 2.2f, rotationSpeed = .21f, rotationPhase = .2f, entranceDelay = .55f },
            new MenuCharacter { label = "Living guest", resource = "living-guest", feet = new Vector2(520, 835), height = 500, depth = .5f, bobAmplitude = 8, bobSpeed = .34f, bobPhase = .1f, rotationAmplitude = 1.6f, rotationSpeed = .17f, rotationPhase = .55f, entranceDelay = .35f },
            new MenuCharacter { label = "Bellhop", resource = "bellhop", feet = new Vector2(790, 1030), height = 590, depth = .75f, bobAmplitude = 10, bobSpeed = .41f, bobPhase = .45f, rotationAmplitude = 1.4f, rotationSpeed = .23f, rotationPhase = .9f, entranceDelay = .18f },
            new MenuCharacter { label = "Housekeeper", resource = "housekeeper", feet = new Vector2(330, 1040), height = 610, depth = 1f, bobAmplitude = 11, bobSpeed = .37f, bobPhase = .05f, rotationAmplitude = 1.8f, rotationSpeed = .19f, rotationPhase = .35f, entranceDelay = .05f },
        };

        [Header("Credits")]
        [Tooltip("Optional. Defaults to Resources/HotelTogetherMenu/Credits.txt.")]
        public TextAsset creditsText;

        // ------------------------------------------------------------------ palette
        static readonly Color TealTop = new Color(.11f, .49f, .52f), TealBottom = new Color(.05f, .31f, .35f), TealDeep = new Color(.03f, .16f, .19f);
        static readonly Color GoldLine = new Color(.98f, .79f, .36f), GoldTop = new Color(1, .82f, .34f), GoldBottom2 = new Color(.93f, .55f, .13f);
        static readonly Color CreamText = new Color(1, .96f, .84f), CreamPanel = new Color(1, .95f, .82f), CreamPanelBottom = new Color(.99f, .87f, .66f);
        static readonly Color Ink = new Color(.07f, .2f, .23f);

        enum Page { Main, Options, Credits, Loading }
        enum Row { Master, Music, Sensitivity, Graphics, Fullscreen, ReducedMotion, Back }

        Page page = Page.Main;
        int focus, optionFocus, lastHoverFocus = -1;
        float openedAt, pageChangedAt, loadStarted;
        Texture2D background, shadow, menuShade, logo;
        Vector2 pointer, smoothPointer;
        Vector2 lastMouse;
        bool mouseMovedThisFrame;
        float[] buttonScale = new float[4];
        float pressFlash; int pressedIndex = -1; Action pendingAction; float pendingAt;
        int dragRow = -1;
        float creditsScroll, creditsHeight;
        string credits;
        AsyncOperation loading;
        AudioSource music;
        string notice; float noticeUntil;
        float navRepeatAt; int navHeld;
        bool settingsDirty;

        // settings
        float masterVolume = 1, musicVolume = 1, sensitivity = .12f;
        int graphicsTier;
        bool fullscreen = true, reducedMotion;
        static readonly string[] tierLabels = { "LOW", "MEDIUM", "HIGH", "ULTRA" };

        string[] mainLabels;
        bool QuitSupported => Application.platform != RuntimePlatform.WebGLPlayer && !Application.isMobilePlatform;

        // ------------------------------------------------------------------ lifecycle
        void Awake()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            LoadSettings();
            EnsureCamera();
            background = Resources.Load<Texture2D>(ArtFolder + backgroundResource);
            logo = Resources.Load<Texture2D>(ArtFolder + "logo");
            foreach (var c in characters) c.texture = string.IsNullOrEmpty(c.resource) ? null : Resources.Load<Texture2D>(ArtFolder + c.resource);
            if (!creditsText) creditsText = Resources.Load<TextAsset>(ArtFolder + "Credits");
            credits = creditsText ? creditsText.text : DefaultCredits;
            shadow = BuildShadow();
            menuShade = BuildShade();
            mainLabels = QuitSupported ? new[] { "PLAY GAME", "OPTIONS", "CREDITS", "QUIT" } : new[] { "PLAY GAME", "OPTIONS", "CREDITS" };
            buttonScale = new float[mainLabels.Length];
            for (int i = 0; i < buttonScale.Length; i++) buttonScale[i] = 1;
            openedAt = Time.unscaledTime;
            var mouse = Mouse.current; if (mouse != null) lastMouse = mouse.position.ReadValue();
        }

        void OnEnable() { ShowCursor(); }

        void Start()
        {
            music = gameObject.AddComponent<AudioSource>();
            music.clip = HotelAudio.Clip("music_day");
            music.loop = true; music.playOnAwake = false; music.spatialBlend = 0; music.ignoreListenerPause = true; music.volume = 0;
            if (music.clip) music.Play();
            AudioListener.volume = masterVolume; // the hotel restores its own volume when it unloads
            ShowCursor();
        }

        void OnDestroy() { if (shadow) Destroy(shadow); if (menuShade) Destroy(menuShade); }

        // Horizontal ramp: transparent at the left, a light teal tint under the menu.
        static Texture2D BuildShade()
        {
            const int w = 128;
            var t = new Texture2D(w, 1, TextureFormat.RGBA32, false) { name = "Menu shade", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w];
            for (int x = 0; x < w; x++)
            {
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(x / (w * .28f)));
                px[x] = new Color(TealDeep.r, TealDeep.g, TealDeep.b, .22f * k);
            }
            t.SetPixels32(px); t.Apply(false, true); return t;
        }

        static void ShowCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        void EnsureCamera()
        {
            if (Camera.main || FindAnyObjectByType<Camera>()) { if (!FindAnyObjectByType<AudioListener>()) gameObject.AddComponent<AudioListener>(); return; }
            var go = new GameObject("Menu Camera");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.cullingMask = 0; cam.tag = "MainCamera";
            go.AddComponent<AudioListener>();
        }

        static Texture2D BuildShadow()
        {
            const int size = 64;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Menu soft shadow", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2 - 1, dy = (y + .5f) / size * 2 - 1;
                    float a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy)); a = a * a * (3 - 2 * a);
                    px[y * size + x] = new Color32(0, 0, 0, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply(false, true); return t;
        }

        // ------------------------------------------------------------------ settings
        void LoadSettings()
        {
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Volume", 1));
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Music", 1));
            sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "Sensitivity", .12f), .03f, .30f);
            graphicsTier = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "GraphicsTier", (int)HotelPerformanceDirector.DetectTier()), 0, 3);
            reducedMotion = PlayerPrefs.GetInt(Prefix + "ReducedMotion", 0) != 0;
            fullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
            if (!Application.isEditor && PlayerPrefs.HasKey(Prefix + "Fullscreen"))
            {
                bool wanted = PlayerPrefs.GetInt(Prefix + "Fullscreen") != 0;
                if (wanted != fullscreen) ApplyFullscreen(wanted);
            }
            AudioListener.volume = masterVolume;
            HotelAudio.SetMusicVolume(musicVolume, false);
        }

        void SaveSettings()
        {
            PlayerPrefs.SetFloat(Prefix + "Volume", masterVolume);
            PlayerPrefs.SetFloat(Prefix + "Music", musicVolume);
            PlayerPrefs.SetFloat(Prefix + "Sensitivity", sensitivity);
            PlayerPrefs.SetInt(Prefix + "GraphicsTier", graphicsTier);
            PlayerPrefs.SetInt(Prefix + "ReducedMotion", reducedMotion ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "Fullscreen", fullscreen ? 1 : 0);
            PlayerPrefs.Save();
            settingsDirty = false;
        }

        void ApplyFullscreen(bool on)
        {
            fullscreen = on;
            var res = Screen.currentResolution;
            int w = Display.main != null && Display.main.systemWidth > 0 ? Display.main.systemWidth : res.width;
            int h = Display.main != null && Display.main.systemHeight > 0 ? Display.main.systemHeight : res.height;
            if (on) Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
            else Screen.SetResolution(Mathf.RoundToInt(w * .8f), Mathf.RoundToInt(h * .8f), FullScreenMode.Windowed);
        }

        // ------------------------------------------------------------------ update / input
        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            if (page != Page.Loading) ShowCursor();

            // Music fades in, and out while loading the hotel.
            if (music)
            {
                float target = page == Page.Loading ? 0 : HotelAudio.MusicVolume * .75f * Mathf.Clamp01((Time.unscaledTime - openedAt) / 2f);
                music.volume = Mathf.MoveTowards(music.volume, target, dt * (page == Page.Loading ? 1.6f : .6f));
            }

            // Parallax pointer: mouse inside the window, or the right stick.
            var mouse = Mouse.current; var pad = Gamepad.current;
            mouseMovedThisFrame = false;
            if (mouse != null)
            {
                Vector2 p = mouse.position.ReadValue();
                if ((p - lastMouse).sqrMagnitude > 4) { mouseMovedThisFrame = true; lastMouse = p; }
                if (p.x >= 0 && p.y >= 0 && p.x <= Screen.width && p.y <= Screen.height)
                    pointer = new Vector2(p.x / Mathf.Max(1, Screen.width) * 2 - 1, p.y / Mathf.Max(1, Screen.height) * 2 - 1);
            }
            if (pad != null && pad.rightStick.ReadValue().sqrMagnitude > .04f) pointer = pad.rightStick.ReadValue();
            pointer = Vector2.ClampMagnitude(pointer, 1.2f);
            Vector2 goal = reducedMotion ? Vector2.zero : pointer;
            smoothPointer = Vector2.Lerp(smoothPointer, goal, 1 - Mathf.Exp(-dt * parallaxSmoothing));

            pressFlash = Mathf.MoveTowards(pressFlash, 0, dt * 6);
            for (int i = 0; i < buttonScale.Length; i++)
            {
                float t = page == Page.Main && i == focus && !reducedMotion ? 1.045f : 1;
                buttonScale[i] = Mathf.Lerp(buttonScale[i], t, 1 - Mathf.Exp(-dt * 14));
            }

            if (pendingAction != null && Time.unscaledTime >= pendingAt) { var a = pendingAction; pendingAction = null; pressedIndex = -1; a(); }

            if (page == Page.Loading) { UpdateLoading(); return; }
            HandleNavigation(dt);
        }

        void HandleNavigation(float dt)
        {
            var k = Keyboard.current; var pad = Gamepad.current;
            int vertical = 0, horizontal = 0; bool submit = false, back = false;
            if (k != null)
            {
                if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) vertical = -1;
                if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) vertical = 1;
                if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) horizontal = -1;
                if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) horizontal = 1;
                submit |= k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame;
                back |= k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame;
                // Held arrows repeat (useful for sliders).
                int heldDir = k.leftArrowKey.isPressed || k.aKey.isPressed ? -1 : k.rightArrowKey.isPressed || k.dKey.isPressed ? 1 : 0;
                int heldV = k.upArrowKey.isPressed || k.wKey.isPressed ? -1 : k.downArrowKey.isPressed || k.sKey.isPressed ? 1 : 0;
                Repeat(ref horizontal, ref vertical, heldDir, heldV, 1);
            }
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
                int sx = stick.x > .5f ? 1 : stick.x < -.5f ? -1 : 0, sy = stick.y > .5f ? -1 : stick.y < -.5f ? 1 : 0;
                Repeat(ref horizontal, ref vertical, sx, sy, 2);
                submit |= pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame;
                back |= pad.buttonEast.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame;
            }
            if (Time.unscaledTime - openedAt < .35f) return; // ignore the key that loaded the scene

            switch (page)
            {
                case Page.Main:
                    if (vertical != 0) MoveFocus(ref focus, vertical, mainLabels.Length);
                    if (submit) Activate(focus);
                    if (back && QuitSupported && focus != mainLabels.Length - 1) { focus = mainLabels.Length - 1; }
                    break;
                case Page.Options:
                    if (vertical != 0) MoveFocus(ref optionFocus, vertical, 7);
                    if (horizontal != 0) Adjust((Row)optionFocus, horizontal);
                    if (submit) SubmitOption((Row)optionFocus);
                    if (back) ClosePanel(1);
                    break;
                case Page.Credits:
                    if (vertical != 0) creditsScroll += vertical * 60;
                    if (submit || back) ClosePanel(2);
                    break;
            }
        }

        // Converts held directions into an initial step + auto-repeat.
        void Repeat(ref int horizontal, ref int vertical, int heldH, int heldV, int source)
        {
            int code = heldH != 0 ? heldH * 10 : heldV;
            if (code == 0) { if (navHeld != 0 && Mathf.Abs(navHeld) / 100 == source) navHeld = 0; return; }
            int tagged = code + Math.Sign(code) * source * 100;
            if (navHeld != tagged)
            {
                navHeld = tagged; navRepeatAt = Time.unscaledTime + .38f;
                if (source == 2) { if (heldH != 0) horizontal = heldH; else vertical = heldV; } // keyboard already reported its press
                return;
            }
            if (Time.unscaledTime >= navRepeatAt)
            {
                navRepeatAt = Time.unscaledTime + (heldH != 0 ? .06f : .12f);
                if (heldH != 0) horizontal = heldH; else vertical = heldV;
            }
        }

        void MoveFocus(ref int index, int step, int count)
        {
            index = (index + step + count) % count;
           
        }

        void Activate(int index)
        {
            if (pendingAction != null || page != Page.Main) return;
            focus = index; pressedIndex = index; pressFlash = 1;
            Action a = index switch
            {
                0 => StartGame,
                1 => () => OpenPanel(Page.Options),
                2 => () => OpenPanel(Page.Credits),
                _ => Quit
            };
            pendingAction = a; pendingAt = Time.unscaledTime + .14f;
        }

        void OpenPanel(Page p) { page = p; pageChangedAt = Time.unscaledTime; optionFocus = 0; creditsScroll = 0; dragRow = -1; }

        void ClosePanel(int focusButton)
        {
            if (settingsDirty) SaveSettings();
            page = Page.Main; pageChangedAt = Time.unscaledTime; focus = focusButton; dragRow = -1;
        }

        void StartGame()
        {
            if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
            {
                Notice("\"" + gameplayScene + "\" is not in Build Settings. Run Hotel Together > Main Menu > Rebuild Main Menu Scene.");
                return;
            }
            if (settingsDirty) SaveSettings();
            LaunchedFromMenu = true;
            page = Page.Loading; loadStarted = Time.unscaledTime; pageChangedAt = Time.unscaledTime;
        }

        void UpdateLoading()
        {
            // Fade to black first, then load; the hotel fades in from black on its side.
            if (loading == null && Time.unscaledTime - loadStarted > .55f)
            {
                loading = SceneManager.LoadSceneAsync(gameplayScene, LoadSceneMode.Single);
                if (loading == null) { page = Page.Main; Notice("Could not load " + gameplayScene + "."); }
            }
        }

        void Quit()
        {
            if (settingsDirty) SaveSettings();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Notice(string text) { notice = text; noticeUntil = Time.unscaledTime + 5; HotelAudio.Play("deny", .7f); }

        // ------------------------------------------------------------------ options logic
        void Adjust(Row row, int dir)
        {
            switch (row)
            {
                case Row.Master: masterVolume = Mathf.Clamp01(Mathf.Round((masterVolume + dir * .05f) * 20) / 20); AudioListener.volume = masterVolume; break;
                case Row.Music: musicVolume = Mathf.Clamp01(Mathf.Round((musicVolume + dir * .05f) * 20) / 20); HotelAudio.SetMusicVolume(musicVolume, false); break;
                case Row.Sensitivity: sensitivity = Mathf.Clamp(Mathf.Round((sensitivity + dir * .006f) * 1000) / 1000, .03f, .30f); break;
                case Row.Graphics: graphicsTier = (graphicsTier + dir + 4) % 4; break;
                case Row.Fullscreen: ApplyFullscreen(!fullscreen); break;
                case Row.ReducedMotion: reducedMotion = !reducedMotion; break;
                default: return;
            }
            settingsDirty = true;
            if (row >= Row.Graphics) SaveSettings();
        }

        void SubmitOption(Row row)
        {
            if (row == Row.Back) { ClosePanel(1); return; }
            if (row >= Row.Graphics) Adjust(row, 1);
        }

        float SliderValue(Row row) => row switch
        {
            Row.Master => masterVolume,
            Row.Music => musicVolume,
            Row.Sensitivity => Mathf.InverseLerp(.03f, .30f, sensitivity),
            _ => 0
        };

        void SetSlider(Row row, float t)
        {
            t = Mathf.Clamp01(t);
            switch (row)
            {
                case Row.Master: masterVolume = Mathf.Round(t * 100) / 100; AudioListener.volume = masterVolume; break;
                case Row.Music: musicVolume = Mathf.Round(t * 100) / 100; HotelAudio.SetMusicVolume(musicVolume, false); break;
                case Row.Sensitivity: sensitivity = Mathf.Lerp(.03f, .30f, t); break;
            }
            settingsDirty = true;
        }

        // ------------------------------------------------------------------ layout helpers
        float Scale => Mathf.Max(.01f, Mathf.Min(Screen.width / referenceResolution.x, Screen.height / referenceResolution.y));
        float Age => Time.unscaledTime - openedAt;
        static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); return 1 - Mathf.Pow(1 - t, 3); }
        static float EaseOutBack(float t) { t = Mathf.Clamp01(t); const float c = 1.6f; float u = t - 1; return 1 + (c + 1) * u * u * u + c * u * u; }
        float Entrance(float delay, float duration) => reducedMotion ? Mathf.Clamp01((Age - delay * .3f) / .45f) : Mathf.Clamp01((Age - delay) / (duration * entranceDuration));

        Rect RightArea(float vw, float vh) { float x = vw * characterAreaFraction; return new Rect(x, 0, vw - x, vh); }

        // ------------------------------------------------------------------ drawing
        void OnGUI()
        {
            var oldMatrix = GUI.matrix; var oldColor = GUI.color; var oldContent = GUI.contentColor;
            float s = Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float vw = Screen.width / s, vh = Screen.height / s;
            GUI.color = Color.white; GUI.contentColor = Color.white;

            DrawBackground(vw, vh);
            DrawCharacters(vw, vh);
            var right = RightArea(vw, vh);
            // Soft vignette behind the menu keeps text readable on any background crop.
            DrawTexture(new Rect(right.x - 260, -2, right.width + 264, vh + 4), menuShade);

            switch (page)
            {
                case Page.Main: DrawTitle(right, vh); DrawMainButtons(right, vh); break;
                case Page.Options: DrawTitle(right, vh, true); DrawOptions(right, vh); break;
                case Page.Credits: DrawTitle(right, vh, true); DrawCredits(right, vh); break;
                case Page.Loading: DrawTitle(right, vh); DrawMainButtons(right, vh); break;
            }

            if (!string.IsNullOrEmpty(notice) && Time.unscaledTime < noticeUntil)
            {
                var r = new Rect(right.x + 30, vh - 120, right.width - 60, 74);
                Bubble(r, HotelToyKit.Orange, HotelToyKit.OrangeBottom, 18, 5);
                Text(Inset(r, 14), notice, 20, Color.white, TextAnchor.MiddleCenter, 1.4f, null, true);
            }

            // Entrance fade from black and the loading fade to black.
            float black = Mathf.Clamp01(1 - Age / (reducedMotion ? .35f : .7f));
            if (page == Page.Loading) black = Mathf.Max(black, Mathf.Clamp01((Time.unscaledTime - loadStarted) / .5f));
            if (black > .002f) Rule(new Rect(-2, -2, vw + 4, vh + 4), new Color(0, 0, 0, black));
            if (page == Page.Loading) DrawLoading(vw, vh);

            GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.contentColor = oldContent;
        }

        static Rect Inset(Rect r, float n) => new Rect(r.x + n, r.y + n, r.width - n * 2, r.height - n * 2);

        void DrawTexture(Rect r, Texture texture, float alpha = 1)
        {
            if (Event.current.type != EventType.Repaint || !texture) return;
            var old = GUI.color; GUI.color = new Color(1, 1, 1, alpha * old.a);
            GUI.DrawTexture(r, texture, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        void DrawBackground(float vw, float vh)
        {
            if (!background) { Rule(new Rect(0, 0, vw, vh), TealDeep); return; }
            float margin = maxParallaxOffset * backgroundDepth * parallaxStrength + 4;
            float zoom = reducedMotion ? 1 : Mathf.Lerp(1.04f, 1, EaseOutCubic(Mathf.Clamp01(Age / 1.6f)));
            float cover = Mathf.Max((vw + margin * 2) / background.width, (vh + margin * 2) / background.height) * zoom;
            float w = background.width * cover, h = background.height * cover;
            Vector2 off = ParallaxOffset(backgroundDepth);
            var r = new Rect((vw - w) * backgroundAnchorX + off.x, (vh - h) * .5f + off.y, w, h);
            DrawTexture(r, background);
        }

        Vector2 ParallaxOffset(float depth)
        {
            var o = new Vector2(-smoothPointer.x, smoothPointer.y) * (maxParallaxOffset * depth * parallaxStrength);
            return Vector2.ClampMagnitude(o, maxParallaxOffset * Mathf.Max(1, depth));
        }

        void DrawCharacters(float vw, float vh)
        {
            float stageW = vw * characterAreaFraction;
            float fit = Mathf.Min(stageW / designStage.x, vh / designStage.y);
            float ox = (stageW - designStage.x * fit) * stageAnchor, oy = vh - designStage.y * fit;
            float t = Time.unscaledTime * floatSpeed;
            bool still = reducedMotion || floatStrength <= 0;
            foreach (var c in characters)
            {
                if (!c.texture) continue;
                float h = c.height * fit, w = h * c.texture.width / c.texture.height;
                float bob = still ? 0 : Mathf.Sin((t * c.bobSpeed + c.bobPhase) * Mathf.PI * 2) * c.bobAmplitude * floatStrength * fit;
                float angle = still ? 0 : Mathf.Sin((t * c.rotationSpeed + c.rotationPhase) * Mathf.PI * 2) * c.rotationAmplitude * floatStrength;
                Vector2 off = ParallaxOffset(c.depth) * fit;
                float e = Entrance(.15f + c.entranceDelay, .9f);
                float slide = reducedMotion ? 0 : (1 - EaseOutCubic(e)) * -(160 + 120 * c.depth) * fit;
                float alpha = Mathf.SmoothStep(0, 1, e);
                if (alpha <= .001f) continue;
                Vector2 feet = new Vector2(ox + c.feet.x * fit + off.x + slide, oy + c.feet.y * fit + off.y);

                if (c.groundShadow)
                {
                    float lift = Mathf.Clamp01(-bob / Mathf.Max(1, c.bobAmplitude * fit) * .5f + .5f);
                    float sw = w * Mathf.Lerp(.62f, .5f, lift), sh = Mathf.Max(8, h * .06f);
                    DrawTexture(new Rect(feet.x - sw / 2, feet.y - sh * .55f, sw, sh), shadow, alpha * Mathf.Lerp(.42f, .28f, lift) * (.6f + .4f * c.depth));
                }

                var rect = new Rect(feet.x - w / 2, feet.y - h + bob, w, h);
                var matrix = GUI.matrix;
                if (Mathf.Abs(angle) > .001f) RotateAround(angle, new Vector2(rect.center.x, rect.y + h * .62f));
                DrawTexture(rect, c.texture, alpha);
                GUI.matrix = matrix;
            }
        }

        // Thick cartoon outline: 8 offset copies + drop shadow, then the fill.
        void BigText(Rect r, string text, int size, Color fill, Color outline, float stroke, Color shadowColor, float shadowOffset)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (shadowOffset > 0) Text(new Rect(r.x, r.y + shadowOffset, r.width, r.height), text, size, shadowColor, TextAnchor.MiddleCenter, stroke, shadowColor);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                Text(new Rect(r.x + Mathf.Cos(a) * stroke, r.y + Mathf.Sin(a) * stroke, r.width, r.height), text, size, outline, TextAnchor.MiddleCenter);
            }
            Text(r, text, size, fill, TextAnchor.MiddleCenter);
        }

        // Logo artwork (Resources/HotelTogetherMenu/logo.png): a one-time pop-in, then it stays still.
        void DrawTitle(Rect right, float vh, bool compact = false)
        {
            float e = Entrance(.35f, .8f);
            if (e <= 0) return;
            float cx = right.center.x;
            float width = compact ? Mathf.Min(420, right.width - 80) : Mathf.Min(720, right.width - 40);
            float height = logo ? width * logo.height / logo.width : width * .43f;
            float top = compact ? Mathf.Max(16, vh * .03f) : Mathf.Max(24, vh * .5f - 450);
            var rect = new Rect(cx - width / 2, top, width, height);
            float pop = reducedMotion ? 1 : EaseOutBack(e);
            var matrix = GUI.matrix;
            ScaleAround(Vector2.one * Mathf.Lerp(.8f, 1, pop), rect.center);
            using (new AlphaScope(Mathf.Clamp01(e * 1.6f)))
            {
                if (logo) DrawTexture(rect, logo, Mathf.Clamp01(e * 1.6f));
                else BigText(rect, "HOTEL TOGETHER", 90, CreamText, Ink, 6, Color.clear, 0);
                if (!compact)
                {
                    var tag = new Rect(cx - 250, rect.yMax + 4, 500, 46);
                    Fill(new Rect(tag.x, tag.y + 4, tag.width, tag.height), new Color(0, 0, 0, .35f), 23);
                    Fill(tag, GoldLine, 23);
                    Fill(Inset(tag, 3), TealDeep, 20);
                    Text(new Rect(tag.x, tag.y - 1, tag.width, tag.height), "RUN THE HOTEL.  SURVIVE THE NIGHT.", 22, CreamText, TextAnchor.MiddleCenter);
                }
            }
            GUI.matrix = matrix;
        }

        void Sparkle(Vector2 c, float r)
        {
            var m = GUI.matrix;
            RotateAround(45, c);
            Fill(new Rect(c.x - r * .32f, c.y - r * .32f, r * .64f, r * .64f), GoldLine, 2);
            GUI.matrix = m;
            Fill(new Rect(c.x - r, c.y - 2, r * 2, 4), CreamText, 2);
            Fill(new Rect(c.x - 2, c.y - r, 4, r * 2), CreamText, 2);
        }

        // Same toy button as the in-game menus: brown outline, blue (primary) or orange bubble, white label.
        bool ToyButton(Rect r, string label, bool primary, bool focused, bool pressed, float alpha, int fontSize = 40)
        {
            Color top = primary ? HotelToyKit.Blue : HotelToyKit.Orange, bottom = primary ? HotelToyKit.BlueBottom : HotelToyKit.OrangeBottom;
            if (focused) top = Color.Lerp(top, Color.white, .22f);
            var draw = pressed ? new Rect(r.x, r.y + 3, r.width, r.height) : r;
            using (new AlphaScope(alpha))
            {
                if (focused)
                {
                    float pulse = reducedMotion ? .7f : .55f + .45f * Mathf.Sin(Time.unscaledTime * 5);
                    Fill(new Rect(draw.x - 9, draw.y - 9, draw.width + 18, draw.height + 24), new Color(1, .93f, .62f, .3f + .3f * pulse), 30);
                }
                Bubble(draw, top, bottom, 22, pressed ? 2 : 7);
                Text(new Rect(draw.x, draw.y - 2, draw.width, draw.height), label, fontSize, Color.white, TextAnchor.MiddleCenter, 2.2f, HotelToyKit.Brown);
                if (focused)
                {
                    float bounce = reducedMotion ? 0 : Mathf.Sin(Time.unscaledTime * 6) * 4;
                    Diamond(new Vector2(draw.x - 30 + bounce, draw.center.y), 11);
                    Diamond(new Vector2(draw.xMax + 30 - bounce, draw.center.y), 11);
                }
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        void Diamond(Vector2 c, float r)
        {
            var m = GUI.matrix;
            RotateAround(45, c);
            Fill(new Rect(c.x - r - 2, c.y - r - 2, r * 2 + 4, r * 2 + 4), TealDeep, 4);
            Fill(new Rect(c.x - r, c.y - r, r * 2, r * 2), GoldLine, 3);
            GUI.matrix = m;
        }

        bool HoverFocus(Rect r, ref int focusIndex, int index)
        {
            if (!r.Contains(Event.current.mousePosition)) return false;
            if (mouseMovedThisFrame && focusIndex != index && Event.current.type == EventType.Repaint && lastHoverFocus != index)
            {
                focusIndex = index;
            }
            if (Event.current.type == EventType.Repaint) lastHoverFocus = index;
            return true;
        }

        void DrawMainButtons(Rect right, float vh)
        {
            float bw = Mathf.Min(460, right.width - 140), bh = 86, gap = 106;
            float startY = Mathf.Max(380, vh * .5f - 40);
            if (startY + gap * mainLabels.Length + 40 > vh) startY = vh - gap * mainLabels.Length - 40;
            bool anyHover = false;
            for (int i = 0; i < mainLabels.Length; i++)
            {
                float e = Entrance(.6f + i * .09f, .7f);
                float slide = reducedMotion ? 0 : (1 - EaseOutCubic(e)) * 140;
                var r = new Rect(right.center.x - bw / 2 + slide, startY + i * gap, bw, bh);
                bool hovered = page == Page.Main && e >= 1 && HoverFocus(r, ref focus, i);
                anyHover |= hovered;
                bool pressed = pressedIndex == i || hovered && Event.current.type == EventType.Repaint && Mouse.current != null && Mouse.current.leftButton.isPressed;
                var matrix = GUI.matrix;
                if (Mathf.Abs(buttonScale[i] - 1) > .001f) ScaleAround(Vector2.one * buttonScale[i], r.center);
                bool clicked = ToyButton(r, mainLabels[i], i == 0, page == Page.Main && focus == i, pressed, Mathf.SmoothStep(0, 1, e));
                GUI.matrix = matrix;
                if (clicked && page == Page.Main && e >= 1) Activate(i);
            }
            if (!anyHover && Event.current.type == EventType.Repaint) lastHoverFocus = -1;
            if (page == Page.Main)
            {
                float hintE = Entrance(1.1f, .6f);
                using (new AlphaScope(hintE * .85f))
                    Text(new Rect(right.x + 20, Mathf.Min(vh - 50, startY + gap * mainLabels.Length + 6), right.width - 40, 30),
                        Gamepad.current != null ? "D-PAD / STICK  Move    A  Select    B  Back" : "ARROWS / W S  Move    ENTER  Select    ESC  Back", 18, CreamText, TextAnchor.MiddleCenter, 1.2f, TealDeep);
            }
        }

        Rect PanelRect(Rect right, float vh, float height)
        {
            float w = Mathf.Min(720, right.width - 60);
            float top = Mathf.Max(vh * .045f + 200, (vh - height) * .5f + 70);
            if (top + height > vh - 20) top = Mathf.Max(20, vh - 20 - height);
            return new Rect(right.center.x - w / 2, top, w, height);
        }

        void DrawPanelFrame(Rect r, string title)
        {
            Fill(new Rect(r.x, r.y + 10, r.width, r.height), new Color(TealDeep.r, TealDeep.g, TealDeep.b, .85f), 34);
            Fill(r, GoldLine, 34);
            Fill(Inset(r, 5), CreamPanelBottom, 30);
            Fill(new Rect(r.x + 5, r.y + 5, r.width - 10, (r.height - 10) * .55f), CreamPanel, 30);
            var head = new Rect(r.x + 24, r.y - 30, r.width - 48, 64);
            Fill(new Rect(head.x, head.y + 6, head.width, head.height), TealDeep, 24);
            Fill(head, GoldLine, 24);
            Fill(Inset(head, 4), TealBottom, 20);
            Fill(new Rect(head.x + 4, head.y + 4, head.width - 8, (head.height - 8) * .55f), TealTop, 20);
            Text(new Rect(head.x, head.y - 2, head.width, head.height), title, 36, CreamText, TextAnchor.MiddleCenter, 2.4f, TealDeep);
        }

        void DrawOptions(Rect right, float vh)
        {
            float e = Mathf.Clamp01((Time.unscaledTime - pageChangedAt) / (reducedMotion ? .15f : .3f));
            var r = PanelRect(right, vh, 700);
            if (!reducedMotion) r.y += (1 - EaseOutCubic(e)) * 40;
            using (new AlphaScope(e))
            {
                DrawPanelFrame(r, "OPTIONS");
                string[] labels = { "MASTER VOLUME", "MUSIC VOLUME", "MOUSE SENSITIVITY", "GRAPHICS QUALITY", "FULLSCREEN", "REDUCED MOTION" };
                float rowH = 74, y0 = r.y + 52;
                for (int i = 0; i < 6; i++)
                {
                    var row = new Rect(r.x + 26, y0 + i * rowH, r.width - 52, rowH - 10);
                    HoverFocus(row, ref optionFocus, i);
                    bool focused = optionFocus == i;
                    if (focused)
                    {
                        Fill(new Rect(row.x - 3, row.y - 3, row.width + 6, row.height + 6), GoldLine, 20);
                        Fill(row, new Color(1, .98f, .9f), 17);
                    }
                    else Fill(row, new Color(TealDeep.r, TealDeep.g, TealDeep.b, .06f), 17);
                    Text(new Rect(row.x + 22, row.y, 300, row.height), labels[i], 26, Ink, TextAnchor.MiddleLeft);
                    var control = new Rect(row.xMax - 330, row.y + 8, 310, row.height - 16);
                    DrawOptionControl((Row)i, control, focused);
                    // Clicking the label focuses the row.
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && row.Contains(Event.current.mousePosition) && !control.Contains(Event.current.mousePosition))
                    { optionFocus = i; Event.current.Use(); }
                }
                string note = (Row)optionFocus switch
                {
                    Row.Graphics => "Quality tier used inside the hotel. AUTO RES and FPS are in the in-game Settings.",
                    Row.Fullscreen => Application.isEditor ? "Fullscreen only changes in a built game, not in the Editor." : "Borderless full screen or an 80% window.",
                    Row.ReducedMotion => "Turns off parallax, floating characters and bouncy animation.",
                    Row.Sensitivity => "Mouse look speed inside the hotel.",
                    _ => "LEFT / RIGHT adjust.  Settings save automatically."
                };
                Text(new Rect(r.x + 30, y0 + 6 * rowH - 2, r.width - 60, 44), note, 18, new Color(Ink.r, Ink.g, Ink.b, .75f), TextAnchor.MiddleCenter, 0, null, true);
                var back = new Rect(r.center.x - 170, r.yMax - 104, 340, 74);
                bool hovered = HoverFocus(back, ref optionFocus, (int)Row.Back);
                bool pressed = hovered && Mouse.current != null && Mouse.current.leftButton.isPressed;
                if (ToyButton(back, "BACK", false, optionFocus == (int)Row.Back, pressed, 1, 34)) ClosePanel(1);
            }
            if (Event.current.type == EventType.MouseUp && dragRow >= 0) { dragRow = -1; SaveSettings(); }
        }

        void DrawOptionControl(Row row, Rect r, bool focused)
        {
            var ev = Event.current;
            switch (row)
            {
                case Row.Master:
                case Row.Music:
                case Row.Sensitivity:
                {
                    float v = SliderValue(row);
                    var track = new Rect(r.x + 10, r.center.y - 9, r.width - 100, 18);
                    Fill(new Rect(track.x - 3, track.y - 3, track.width + 6, track.height + 6), TealDeep, 12);
                    Fill(track, new Color(.85f, .78f, .62f), 9);
                    if (v > .001f) Fill(new Rect(track.x, track.y, Mathf.Max(18, track.width * v), track.height), focused ? GoldTop : TealTop, 9);
                    var knob = new Vector2(track.x + track.width * v, track.center.y);
                    Disc(knob + new Vector2(0, 3), 16, new Color(0, 0, 0, .3f));
                    Disc(knob, 16, TealDeep); Disc(knob, 13, GoldLine); Disc(knob + new Vector2(-3, -4), 4, new Color(1, 1, 1, .6f));
                    string value = row == Row.Sensitivity ? (sensitivity / .12f).ToString("0.00") + "x" : Mathf.RoundToInt(v * 100) + "%";
                    Text(new Rect(r.xMax - 84, r.y, 84, r.height), value, 24, Ink, TextAnchor.MiddleRight);
                    var hit = new Rect(track.x - 16, r.y, track.width + 32, r.height);
                    if (ev.type == EventType.MouseDown && ev.button == 0 && hit.Contains(ev.mousePosition)) { dragRow = (int)row; optionFocus = (int)row; SetSlider(row, (ev.mousePosition.x - track.x) / track.width); ev.Use(); }
                    else if (ev.type == EventType.MouseDrag && dragRow == (int)row) { SetSlider(row, (ev.mousePosition.x - track.x) / track.width); ev.Use(); }
                    break;
                }
                case Row.Graphics:
                {
                    var left = new Rect(r.x + 10, r.y + 2, 46, r.height - 4);
                    var rightBtn = new Rect(r.xMax - 56, r.y + 2, 46, r.height - 4);
                    if (SmallButton(left, "<")) { optionFocus = (int)row; Adjust(row, -1); }
                    if (SmallButton(rightBtn, ">")) { optionFocus = (int)row; Adjust(row, 1); }
                    Text(new Rect(left.xMax, r.y, rightBtn.x - left.xMax, r.height), tierLabels[graphicsTier], 28, Ink, TextAnchor.MiddleCenter);
                    break;
                }
                case Row.Fullscreen:
                case Row.ReducedMotion:
                {
                    bool on = row == Row.Fullscreen ? fullscreen : reducedMotion;
                    var pill = new Rect(r.xMax - 130, r.y + 4, 120, r.height - 8);
                    Fill(new Rect(pill.x - 3, pill.y - 3, pill.width + 6, pill.height + 6), TealDeep, 22);
                    Fill(pill, on ? new Color(.35f, .78f, .42f) : new Color(.8f, .73f, .6f), 19);
                    float kx = on ? pill.xMax - pill.height / 2 : pill.x + pill.height / 2;
                    Disc(new Vector2(kx, pill.center.y), pill.height / 2 - 4, CreamText);
                    Text(new Rect(on ? pill.x + 8 : pill.x + pill.height, pill.y, pill.width - pill.height - 8, pill.height), on ? "ON" : "OFF", 22, on ? Color.white : Ink, TextAnchor.MiddleCenter);
                    if (GUI.Button(new Rect(pill.x - 4, pill.y - 4, pill.width + 8, pill.height + 8), GUIContent.none, GUIStyle.none)) { optionFocus = (int)row; Adjust(row, 1); }
                    break;
                }
            }
        }

        bool SmallButton(Rect r, string label)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(new Rect(r.x, r.y + 4, r.width, r.height), TealDeep, 14);
            Fill(r, GoldLine, 14);
            Fill(Inset(r, 3), hover ? TealTop : TealBottom, 11);
            Text(new Rect(r.x, r.y - 2, r.width, r.height), label, 28, CreamText, TextAnchor.MiddleCenter, 1.6f, TealDeep);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        void DrawCredits(Rect right, float vh)
        {
            float e = Mathf.Clamp01((Time.unscaledTime - pageChangedAt) / (reducedMotion ? .15f : .3f));
            var r = PanelRect(right, vh, 700);
            if (!reducedMotion) r.y += (1 - EaseOutCubic(e)) * 40;
            using (new AlphaScope(e))
            {
                DrawPanelFrame(r, "CREDITS");
                var view = new Rect(r.x + 34, r.y + 50, r.width - 68, r.height - 170);
                var ev = Event.current;
                if (ev.type == EventType.ScrollWheel && view.Contains(ev.mousePosition)) { creditsScroll += ev.delta.y * 24; ev.Use(); }
                var pad = Gamepad.current;
                if (pad != null) creditsScroll -= pad.rightStick.ReadValue().y * 12;
                creditsScroll = Mathf.Clamp(creditsScroll, 0, Mathf.Max(0, creditsHeight - view.height));

                GUI.BeginGroup(view);
                float y = -creditsScroll;
                foreach (var raw in credits.Split('\n'))
                {
                    string line = raw.TrimEnd('\r');
                    if (line.StartsWith("#"))
                    {
                        y += 10;
                        Text(new Rect(0, y, view.width, 34), line.TrimStart('#', ' ').ToUpperInvariant(), 24, new Color(.62f, .38f, .06f), TextAnchor.MiddleCenter);
                        y += 36;
                    }
                    else if (line.Trim().Length == 0) y += 8;
                    else
                    {
                        var style = Label(19, TextAnchor.UpperCenter, true);
                        float h = style.CalcHeight(new GUIContent(line), view.width);
                        Text(new Rect(0, y, view.width, h + 2), line, 19, Ink, TextAnchor.UpperCenter, 0, null, true);
                        y += h + 4;
                    }
                }
                if (ev.type == EventType.Repaint) creditsHeight = y + creditsScroll + 6;
                GUI.EndGroup();
                if (creditsHeight > view.height)
                {
                    float frac = view.height / creditsHeight, pos = creditsScroll / Mathf.Max(1, creditsHeight - view.height);
                    var bar = new Rect(view.xMax + 12, view.y, 6, view.height);
                    Fill(bar, new Color(Ink.r, Ink.g, Ink.b, .15f), 3);
                    Fill(new Rect(bar.x, bar.y + (bar.height - bar.height * frac) * pos, 6, bar.height * frac), TealTop, 3);
                }
                var back = new Rect(r.center.x - 170, r.yMax - 104, 340, 74);
                bool hovered = back.Contains(ev.mousePosition);
                if (ToyButton(back, "BACK", false, true, hovered && Mouse.current != null && Mouse.current.leftButton.isPressed, 1, 34)) ClosePanel(2);
            }
        }

        void DrawLoading(float vw, float vh)
        {
            float k = Mathf.Clamp01((Time.unscaledTime - loadStarted - .3f) / .3f);
            if (k <= 0) return;
            using (new AlphaScope(k))
            {
                float progress = loading != null ? Mathf.Clamp01(loading.progress / .9f) : 0;
                var bar = new Rect(vw / 2 - 260, vh / 2 + 20, 520, 26);
                string dots = new string('.', 1 + (int)(Time.unscaledTime * 3) % 3);
                BigText(new Rect(vw / 2 - 400, vh / 2 - 90, 800, 90), "CHECKING IN" + dots, 54, CreamText, Ink, 4, Color.clear, 0);
                Fill(new Rect(bar.x - 5, bar.y - 5, bar.width + 10, bar.height + 10), GoldLine, 18);
                Fill(bar, TealDeep, 13);
                if (progress > .01f) Fill(new Rect(bar.x, bar.y, Mathf.Max(26, bar.width * progress), bar.height), GoldTop, 13);
            }
        }

        readonly struct AlphaScope : IDisposable
        {
            readonly Color previous;
            public AlphaScope(float alpha) { previous = GUI.contentColor; GUI.contentColor = new Color(previous.r, previous.g, previous.b, previous.a * Mathf.Clamp01(alpha)); }
            public void Dispose() { GUI.contentColor = previous; }
        }

        const string DefaultCredits =
            "# Hotel Together\nA four-player co-op hotel that turns into a zombie shelter every night.\n" +
            "# Font\nLilita One by Juan Montoreano. SIL Open Font License 1.1.\n" +
            "# Made with\nUnity 6 and the Universal Render Pipeline.";
    }
}

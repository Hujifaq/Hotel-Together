using UnityEngine;
using UnityEngine.InputSystem;

namespace HotelTogether
{
    // Title screen, cinematic overlays and the frame-rate readout. Drawing reuses the
    // existing toy-like IMGUI kit (Bubble/Text/Panel) so everything matches the menus.
    public partial class HotelPlayer
    {
        HotelTitleCinematic titleCinematic;

        /// <summary>True once the player has pressed Play at least once this session.</summary>
        public bool HasStartedShift => hasPlayed;
        HotelTitleCinematic Cinematic => titleCinematic ? titleCinematic : (titleCinematic = GetComponent<HotelTitleCinematic>());
        bool TitleScreenActive => !hasPlayed && Cinematic && titleCinematic.TitleVisible;
        bool CinematicBehindMenu => !hasPlayed && Cinematic && titleCinematic.Attracting;
        float MenuDim(float alpha) => CinematicBehindMenu ? alpha * .38f : alpha;
        float MenuFade => CinematicBehindMenu ? Mathf.Clamp01(titleCinematic.DismissAge / .35f) : 1;

        bool UpdateTitleInput()
        {
            if (!TitleScreenActive) return false;
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            // Mouse release (not press) so the click cannot also hit a menu button.
            bool any = keyboard != null && keyboard.anyKey.wasPressedThisFrame
                || mouse != null && (mouse.leftButton.wasReleasedThisFrame || mouse.rightButton.wasReleasedThisFrame);
            if (any && titleCinematic.TitleAge > .6f) titleCinematic.DismissTitle();
            return true;
        }

        static float EaseOutBack(float t) { t = Mathf.Clamp01(t); const float c = 1.9f; float u = t - 1; return 1 + (c + 1) * u * u * u + c * u * u; }
        static float EaseOutElastic(float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0 || t >= 1) return t;
            return Mathf.Pow(2, -10 * t) * Mathf.Sin((t * 10 - .75f) * (2 * Mathf.PI / 3)) + 1;
        }

        void DrawCinematicOverlay(Rect safe)
        {
            var c = Cinematic;
            if (!c || !c.Active) return;
            float scale = Mathf.Max(.01f, GUI.matrix.m00);
            var screen = new Rect(-4, -4, Screen.width / scale + 8, Screen.height / scale + 8);
            float bar = screen.height * .105f * Mathf.SmoothStep(0, 1, c.Letterbox);
            if (bar > .5f)
            {
                Rule(new Rect(screen.x, screen.y, screen.width, bar + 4), Color.black);
                Rule(new Rect(screen.x, screen.yMax - bar - 4, screen.width, bar + 4), Color.black);
            }
            if (c.Blackout > .002f) Rule(screen, new Color(0, 0, 0, c.Blackout));
        }

        void DrawTitle(Rect safe)
        {
            var c = titleCinematic;
            float age = c.TitleAge, time = Time.unscaledTime;
            float scale = Mathf.Max(.01f, GUI.matrix.m00);
            float bar = Screen.height / scale * .105f * Mathf.SmoothStep(0, 1, c.Letterbox);

            // Logo: drops in with an elastic bounce, then idles with a gentle wobble.
            float pop = Mathf.Clamp01((age - .9f) / .9f);
            if (pop > 0)
            {
                var matrix = GUI.matrix;
                var pivot = new Vector2(safe.center.x, safe.y + Mathf.Max(bar + 120, safe.height * .27f));
                float squash = 1 + Mathf.Sin(time * 2.1f) * .012f;
                HotelToyKit.ScaleAround(new Vector2(EaseOutElastic(pop) * squash, EaseOutElastic(pop) / squash), pivot);
                HotelToyKit.RotateAround(Mathf.Sin(time * 1.3f) * 1.4f - (1 - pop) * 9, pivot);
                float width = Mathf.Min(660, safe.width - 60);
                var r = new Rect(pivot.x - width / 2, pivot.y - 100, width, 196);
                Bubble(new Rect(r.x + 14, r.y + 22, r.width - 28, r.height), uiOrange, uiOrangeBottom, 34, 7);
                Bubble(r, uiBlue, uiBlueBottom, 34, 9);
                int heading = uiHeading.fontSize;
                uiHeading.fontSize = 84; Text(new Rect(r.x, r.y + 6, r.width, 96), "HOTEL", uiHeading, uiBrown, 3.6f);
                uiHeading.fontSize = 70; Text(new Rect(r.x, r.y + 90, r.width, 86), "TOGETHER", uiHeading, uiBrown, 3.3f);
                uiHeading.fontSize = heading;
                for (int i = 0; i < 4; i++)
                {
                    float twinkle = .55f + .45f * Mathf.Sin(time * 3.1f + i * 1.9f);
                    var spot = i switch { 0 => new Vector2(r.x + 30, r.y + 42), 1 => new Vector2(r.xMax - 34, r.y + 132), 2 => new Vector2(r.x + 64, r.yMax - 10), _ => new Vector2(r.xMax - 70, r.y - 2) };
                    Star(spot, (6 + i % 2 * 4) * twinkle + 2);
                }
                GUI.matrix = matrix;
            }

            // Shot caption, lower left, sliding in with each cut.
            if (!string.IsNullOrEmpty(c.Caption))
            {
                float k = Mathf.Clamp01((c.CaptionAge - .35f) / .45f);
                if (k > 0)
                {
                    using (new UiAlphaScope(k))
                    {
                        uiToast.wordWrap = false;
                        float width = Mathf.Min(uiToast.CalcSize(new GUIContent(c.Caption)).x + 52, safe.width - 64);
                        var r = new Rect(safe.x + 36 - (1 - EaseOutBack(k)) * 60, safe.yMax - bar - 98, width, 58);
                        bool night = c.CaptionNight > .5f;
                        Bubble(r, night ? uiOrange : uiCream, night ? uiOrangeBottom : uiCreamBottom, 18, 5);
                        GUI.Label(Inset(r, 10), c.Caption, uiToast);
                        uiToast.wordWrap = true;
                    }
                }
            }

            // Call to action.
            float prompt = Mathf.Clamp01((age - 2.1f) / .6f);
            if (prompt > 0)
            {
                float pulse = .72f + .28f * Mathf.Sin(time * 3.4f);
                using (new UiAlphaScope(prompt * pulse))
                {
                    float width = Mathf.Min(460, safe.width - 60);
                    var r = new Rect(safe.center.x - width / 2, safe.yMax - bar - 104 - Mathf.Abs(Mathf.Sin(time * 3.4f)) * 4, width, 64);
                    if (safe.width < 1100) r.y -= 70;
                    Bubble(r, uiBlue, uiBlueBottom, 20, 5);
                    Text(r, "CLICK TO CLOCK IN", uiButton, uiBrown, 1.7f);
                }
            }
        }

        void DrawFrameRate(Rect safe)
        {
            var performance = HotelPerformanceDirector.Instance;
            if (!performance || !performance.ShowFrameRate) return;
            var r = new Rect(safe.center.x - 120, safe.y + 10, 240, 34);
            Panel(r, new Color(.05f, .1f, .14f, .55f), 12);
            uiSmall.normal.textColor = Color.white;
            GUI.Label(r, Mathf.RoundToInt(performance.FramesPerSecond) + " FPS  /  " + performance.FrameMilliseconds.ToString("0.0") + " ms  /  " + Mathf.RoundToInt(performance.CurrentScale * 100) + "%", uiSmall);
            uiSmall.normal.textColor = uiBrown;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace HotelTogether
{
    /// <summary>
    /// Small IMGUI drawing kit in the game's cartoon style: chunky brown outlines, cream/blue/orange
    /// bubbles with a drop shadow. Rounded shapes use a handful of shared 9-sliced textures, so
    /// animated bars and counters never allocate textures.
    /// </summary>
    public static class HotelToyKit
    {
        public static readonly Color Brown = new Color(.36f, .20f, .13f);
        public static readonly Color Cream = new Color(1, .94f, .79f), CreamBottom = new Color(1, .84f, .59f);
        public static readonly Color Blue = new Color(.22f, .84f, .97f), BlueBottom = new Color(.02f, .52f, .82f);
        public static readonly Color Orange = new Color(1, .66f, .28f), OrangeBottom = new Color(.98f, .37f, .12f);
        public static readonly Color Green = new Color(.45f, .86f, .36f), GreenBottom = new Color(.16f, .62f, .24f);
        public static readonly Color Red = new Color(1, .38f, .33f), RedBottom = new Color(.82f, .14f, .17f);
        public static readonly Color Grey = new Color(.78f, .72f, .62f), GreyBottom = new Color(.62f, .56f, .47f);
        public static readonly Color Gold = new Color(1, .86f, .28f), GoldBottom = new Color(.98f, .62f, .08f);

        static readonly int[] radii = { 5, 8, 12, 17, 24, 32 };
        static readonly Dictionary<int, GUIStyle> fills = new();
        static readonly Dictionary<int, GUIStyle> labels = new();
        static Texture2D circle;
        static Font font;
        static bool fontOwned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { fills.Clear(); labels.Clear(); circle = null; font = null; }

        static float Alpha => GUI.contentColor.a;

        // GUIUtility.Rotate/ScaleAroundPivot take the pivot in unscaled screen space, which misplaces
        // anything drawn under a scaled GUI.matrix. These compose in the current (virtual) space instead.
        public static void RotateAround(float degrees, Vector2 pivot) { GUI.matrix = GUI.matrix * Matrix4x4.TRS(pivot, Quaternion.Euler(0, 0, degrees), Vector3.one) * Matrix4x4.Translate(-pivot); }
        public static void ScaleAround(Vector2 scale, Vector2 pivot) { GUI.matrix = GUI.matrix * Matrix4x4.TRS(pivot, Quaternion.identity, new Vector3(scale.x, scale.y, 1)) * Matrix4x4.Translate(-pivot); }


        static void EnsureAssets()
        {
            if (font && circle) return;
            font = Resources.Load<Font>("HotelTogetherUI/LilitaOne-Regular");
            fontOwned = !font;
            if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "Comic Sans MS", "Arial Rounded MT Bold", "Arial" }, 30);
            labels.Clear();
            if (!circle)
            {
                const int size = 64;
                circle = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Toy circle", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + .5f - size * .5f, dy = y + .5f - size * .5f;
                        byte a = (byte)(Mathf.Clamp01(size * .5f - Mathf.Sqrt(dx * dx + dy * dy)) * 255);
                        pixels[y * size + x] = new Color32(255, 255, 255, a);
                    }
                circle.SetPixels32(pixels); circle.Apply(false, true);
            }
        }

        static GUIStyle FillStyle(int r)
        {
            if (fills.TryGetValue(r, out var style) && style.normal.background) return style;
            int size = r * 2 + 3;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Toy rounded " + r, hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float half = size * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + .5f - half) - (half - r - .5f), 0), dy = Mathf.Max(Mathf.Abs(y + .5f - half) - (half - r - .5f), 0);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r + .5f - Mathf.Sqrt(dx * dx + dy * dy)) * 255));
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            int b = r + 1;
            style = new GUIStyle { border = new RectOffset(b, b, b, b), normal = { background = texture } };
            fills[r] = style; return style;
        }

        /// <summary>Flat rounded rectangle.</summary>
        public static void Fill(Rect rect, Color color, float radius = 12)
        {
            if (Event.current.type != EventType.Repaint || rect.width < 1 || rect.height < 1) return;
            EnsureAssets();
            float limit = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * .5f);
            int pick = -1;
            for (int i = 0; i < radii.Length; i++) if (radii[i] <= limit && (radii[i] + 1) * 2 <= Mathf.Min(rect.width, rect.height) + .01f) pick = radii[i];
            var old = GUI.color; color.a *= Alpha; GUI.color = color;
            if (pick < 0) GUI.DrawTexture(rect, Texture2D.whiteTexture);
            else FillStyle(pick).Draw(rect, GUIContent.none, false, false, false, false);
            GUI.color = old;
        }

        public static void Disc(Vector2 center, float radius, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            EnsureAssets();
            var old = GUI.color; color.a *= Alpha; GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), circle);
            GUI.color = old;
        }

        public static void Rule(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var old = GUI.color; color.a *= Alpha; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }

        /// <summary>Outlined two-tone bubble with a drop shadow and a glossy highlight.</summary>
        public static void Bubble(Rect r, Color top, Color bottom, float radius = 18, float depth = 5)
        {
            Fill(new Rect(r.x, r.y + depth, r.width, r.height), new Color(Brown.r * .7f, Brown.g * .7f, Brown.b * .7f, .85f), radius);
            Fill(r, Brown, radius);
            var inner = new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6);
            Fill(inner, bottom, Mathf.Max(3, radius - 3));
            Fill(new Rect(inner.x, inner.y, inner.width, inner.height * .62f), top, Mathf.Max(3, radius - 3));
            Fill(new Rect(r.x + 12, r.y + 5, Mathf.Max(1, r.width - 24), 3), new Color(1, 1, 1, .5f), 1.5f);
        }

        public static void Bubble(Rect r, Color top, Color bottom, float radius, float depth, bool pressed) => Bubble(pressed ? new Rect(r.x, r.y + 3, r.width, r.height) : r, top, bottom, radius, pressed ? 2 : depth);

        public static GUIStyle Label(int size, TextAnchor anchor = TextAnchor.MiddleCenter, bool wrap = false)
        {
            EnsureAssets();
            int key = size * 100 + (int)anchor * 2 + (wrap ? 1 : 0);
            if (labels.TryGetValue(key, out var style)) return style;
            style = new GUIStyle
            {
                font = font, fontSize = size, fontStyle = fontOwned ? FontStyle.Bold : FontStyle.Normal, alignment = anchor, wordWrap = wrap,
                clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = Color.white }, hover = { textColor = Color.white }, active = { textColor = Color.white }, focused = { textColor = Color.white }
            };
            labels[key] = style; return style;
        }

        static readonly GUIContent content = new GUIContent();

        /// <summary>Text with an optional outline (stroke &gt; 0).</summary>
        public static void Text(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, float stroke = 0, Color? outline = null, bool wrap = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            var style = Label(size, anchor, wrap);
            content.text = text;
            var old = GUI.contentColor; float a = old.a;
            if (stroke > 0)
            {
                var o = outline ?? Brown; GUI.contentColor = new Color(o.r, o.g, o.b, a * o.a);
                style.normal.textColor = Color.white;
                GUI.Label(new Rect(r.x - stroke, r.y, r.width, r.height), content, style); GUI.Label(new Rect(r.x + stroke, r.y, r.width, r.height), content, style);
                GUI.Label(new Rect(r.x, r.y - stroke, r.width, r.height), content, style); GUI.Label(new Rect(r.x, r.y + stroke, r.width, r.height), content, style);
            }
            GUI.contentColor = new Color(color.r, color.g, color.b, a * color.a);
            GUI.Label(r, content, style);
            GUI.contentColor = old;
        }

        /// <summary>Rounded progress bar with a track, fill and shine.</summary>
        public static void Bar(Rect r, float fraction, Color fill, Color track)
        {
            Fill(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Brown, r.height * .5f + 2);
            Fill(r, track, r.height * .5f);
            fraction = Mathf.Clamp01(fraction);
            if (fraction > 0.001f)
            {
                float w = Mathf.Max(r.height, r.width * fraction);
                var f = new Rect(r.x, r.y, w, r.height);
                Fill(f, fill, r.height * .5f);
                Fill(new Rect(f.x + 4, f.y + 2, Mathf.Max(1, f.width - 8), Mathf.Max(1, r.height * .28f)), new Color(1, 1, 1, .38f), r.height * .14f);
            }
        }

        /// <summary>Green tick, drawn from two rotated rounded bars (no font glyph needed).</summary>
        public static void Tick(Vector2 center, float size, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var matrix = GUI.matrix;
            RotateAround(45, center + new Vector2(-size * .1f, size * .1f));
            Fill(new Rect(center.x - size * .1f - size * .09f, center.y - size * .1f, size * .18f, size * .62f), color, size * .09f);
            GUI.matrix = matrix;
            RotateAround(-45, center + new Vector2(size * .05f, size * .2f));
            Fill(new Rect(center.x + size * .05f, center.y - size * .4f, size * .18f, size * .95f), color, size * .09f);
            GUI.matrix = matrix;
        }

        /// <summary>Clickable toy button. Returns true on click.</summary>
        public static bool Button(Rect r, string label, Color top, Color bottom, int size = 26, bool enabled = true, float radius = 16)
        {
            bool hover = enabled && r.Contains(Event.current.mousePosition);
            bool press = hover && Event.current.type != EventType.Layout && GUIUtility.hotControl != 0;
            if (!enabled) { top = Grey; bottom = GreyBottom; }
            else if (hover) top = Color.Lerp(top, Color.white, .22f);
            Bubble(r, top, bottom, radius, 5, press);
            var text = press ? new Rect(r.x, r.y + 3, r.width, r.height) : r;
            Text(text, label, size, enabled ? Color.white : new Color(1, 1, 1, .8f), TextAnchor.MiddleCenter, 1.6f);
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none) && enabled;
            return clicked;
        }
    }
}

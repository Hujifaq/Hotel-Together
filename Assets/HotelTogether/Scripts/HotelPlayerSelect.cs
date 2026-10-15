using UnityEngine;
using static HotelTogether.HotelToyKit;

namespace HotelTogether
{
    // "Choose your employee" screen: logo, two framed character cards on pedestals, a big LET'S PLAY button.
    // Art: Resources/HotelTogetherMenu/employee_bellhop.png and employee_housekeeper.png (falls back to the portrait).
    public partial class HotelPlayer
    {
        Texture2D selectLogo, selectBellhop, selectHousekeeper, selectTriangle;
        bool selectLoaded;
        float[] selectLift = new float[2];
        static readonly Color SelGold = new Color(.96f, .76f, .32f), SelCream = new Color(1, .95f, .84f), SelInset = new Color(.97f, .89f, .74f);
        static readonly Color SelInk = new Color(.27f, .14f, .08f), SelTeal = new Color(.13f, .5f, .55f), SelTealDark = new Color(.06f, .29f, .33f), SelNavy = new Color(.1f, .12f, .22f);

        void LoadSelectArt()
        {
            if (selectLoaded) return; selectLoaded = true;
            selectLogo = Resources.Load<Texture2D>("HotelTogetherMenu/logo");
            selectBellhop = Resources.Load<Texture2D>("HotelTogetherMenu/employee_bellhop");
            selectHousekeeper = Resources.Load<Texture2D>("HotelTogetherMenu/employee_housekeeper");
            const int s = 64;
            selectTriangle = new Texture2D(s, s, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, name = "Play triangle" };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float u = (x + .5f) / s, v = (y + .5f) / s, half = .5f * (1 - u);
                float d = Mathf.Min(Mathf.Min(u, half - Mathf.Abs(v - .5f)) * s, s);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d + .5f) * 255));
            }
            selectTriangle.SetPixels32(px); selectTriangle.Apply(false, true);
        }

        void SelTexture(Rect r, Texture t)
        {
            if (!t || Event.current.type != EventType.Repaint) return;
            var old = GUI.color; GUI.color = new Color(1, 1, 1, GUI.contentColor.a); GUI.DrawTexture(r, t, ScaleMode.ScaleToFit, true); GUI.color = old;
        }

        static string SelSubtitle(string title)
        {
            string t = (title ?? "").ToUpperInvariant();
            return t.Contains("BELL") ? "Guest Services" : t.Contains("HOUSE") || t.Contains("CLEAN") ? "Cleaning Crew" : "Hotel Staff";
        }

        Texture SelArt(HotelEmployeePlayer.Employee e)
        {
            string t = (e.title ?? "").ToUpperInvariant() + (e.id ?? "").ToUpperInvariant();
            if (t.Contains("BELL") && selectBellhop) return selectBellhop;
            if ((t.Contains("HOUSE") || t.Contains("CLEAN")) && selectHousekeeper) return selectHousekeeper;
            return e.portrait;
        }

        bool SelPill(Rect r, string label, Color fill, Color text, int size = 20)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            var d = hover ? new Rect(r.x, r.y - 2, r.width, r.height) : r;
            Fill(new Rect(d.x, d.y + 4, d.width, d.height), new Color(0, 0, 0, .35f), d.height / 2);
            Fill(d, SelGold, d.height / 2);
            Fill(new Rect(d.x + 3, d.y + 3, d.width - 6, d.height - 6), hover ? Color.Lerp(fill, Color.white, .12f) : fill, d.height / 2 - 3);
            HotelToyKit.Text(d, label, size, text, TextAnchor.MiddleCenter);
            bool click = GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (click) HotelAudio.Play("ui_click", .9f);
            return click;
        }

        void KeyChip(ref float x, float y, string key)
        {
            var style = Label(16); float w = Mathf.Max(30, style.CalcSize(new GUIContent(key)).x + 16);
            var r = new Rect(x, y, w, 28);
            Fill(r, new Color(.95f, .95f, .95f), 6); Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), SelNavy, 5);
            HotelToyKit.Text(r, key, 16, Color.white); x += w + 6;
        }
        void HintText(ref float x, float y, string text)
        {
            var style = Label(19); float w = style.CalcSize(new GUIContent(text)).x;
            HotelToyKit.Text(new Rect(x, y, w + 4, 28), text, 19, Color.white, TextAnchor.MiddleLeft, 1.4f, new Color(0, 0, 0, .7f)); x += w + 34;
        }

        void DrawEmployeeSelect(Rect safe)
        {
            LoadSelectArt();
            Rule(safe, new Color(.04f, .1f, .14f, MenuDim(.55f)));
            float total = 800, top = safe.center.y - total / 2, cx = safe.center.x;

            // Logo + subtitle with gold rules.
            float lw = 360, lh = selectLogo ? lw * selectLogo.height / selectLogo.width : 120;
            if (selectLogo) SelTexture(new Rect(cx - lw / 2, top, lw, lh), selectLogo);
            else HotelToyKit.Text(new Rect(cx - 300, top, 600, 120), "HOTEL TOGETHER", 60, SelCream, TextAnchor.MiddleCenter, 3, SelInk);
            float sy = top + lh + 2;
            HotelToyKit.Text(new Rect(cx - 260, sy, 520, 36), "CHOOSE YOUR EMPLOYEE", 30, SelCream, TextAnchor.MiddleCenter, 2.4f, SelInk);
            Fill(new Rect(cx - 330, sy + 17, 64, 4), SelGold, 2); Fill(new Rect(cx + 266, sy + 17, 64, 4), SelGold, 2);
            Disc(new Vector2(cx - 262, sy + 19), 5, SelGold); Disc(new Vector2(cx + 262, sy + 19), 5, SelGold);

            // Cards.
            float cw = 300, ch = 410, gap = 34, cy = sy + 52;
            int count = Mathf.Min(2, employee.employees.Length);
            float x0 = cx - (cw * count + gap * (count - 1)) / 2;
            for (int i = 0; i < count; i++)
            {
                var entry = employee.employees[i];
                bool selected = employee.SelectedIndex == i;
                var baseRect = new Rect(x0 + i * (cw + gap), cy, cw, ch);
                bool hover = baseRect.Contains(Event.current.mousePosition);
                if (Event.current.type == EventType.Repaint) selectLift[i] = Mathf.Lerp(selectLift[i], selected ? 1 : hover ? .5f : 0, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 12));
                var card = new Rect(baseRect.x, baseRect.y - selectLift[i] * 8, cw, ch);

                if (selected)
                {
                    float pulse = .6f + .4f * Mathf.Sin(Time.unscaledTime * 4);
                    Fill(new Rect(card.x - 12, card.y - 12, card.width + 24, card.height + 24), new Color(.35f, .9f, 1f, .22f + .18f * pulse), 34);
                    Fill(new Rect(card.x - 6, card.y - 6, card.width + 12, card.height + 12), new Color(.45f, .95f, 1f, .85f), 30);
                }
                Fill(new Rect(card.x, card.y + 8, card.width, card.height), new Color(0, 0, 0, .35f), 26);
                Fill(card, SelGold, 26);
                Fill(new Rect(card.x + 5, card.y + 5, card.width - 10, card.height - 10), new Color(.8f, .58f, .2f), 22);
                Fill(new Rect(card.x + 8, card.y + 8, card.width - 16, card.height - 16), SelCream, 20);
                // Arched inset behind the character.
                var inset = new Rect(card.x + 26, card.y + 26, card.width - 52, 252);
                Fill(inset, SelInset, 70);
                Fill(new Rect(inset.x, inset.y + 120, inset.width, inset.height - 120), SelInset, 8);

                // Pedestal.
                var ped = new Rect(card.x + 34, card.y + 262, card.width - 68, 40);
                Fill(new Rect(ped.x, ped.y + 8, ped.width, ped.height), SelTealDark, 20);
                Fill(new Rect(ped.x, ped.y + 4, ped.width, ped.height - 4), SelGold, 18);
                Fill(new Rect(ped.x + 3, ped.y, ped.width - 6, ped.height - 8), SelTeal, 16);

                // Character art standing on the pedestal.
                var art = SelArt(entry);
                float bob = selected ? Mathf.Sin(Time.unscaledTime * 2.2f) * 2 : 0;
                SelTexture(new Rect(card.x + 22, card.y + 20 + bob, card.width - 44, 268), art);

                // Status pill.
                var pill = new Rect(card.center.x - (selected ? 92 : 64), card.y + 284, selected ? 184 : 128, 40);
                if (selected)
                {
                    Fill(new Rect(pill.x, pill.y + 4, pill.width, pill.height), new Color(0, 0, 0, .3f), 20);
                    Fill(pill, SelGold, 20); Fill(new Rect(pill.x + 3, pill.y + 3, pill.width - 6, pill.height - 6), new Color(.16f, .66f, .74f), 17);
                    HotelToyKit.Text(new Rect(pill.x + 30, pill.y, pill.width - 30, pill.height - 2), "SELECTED", 21, Color.white, TextAnchor.MiddleCenter, 1.4f, SelTealDark);
                    var c = new Vector2(pill.x + 8, pill.center.y);
                    Disc(c, 24, SelInk); Disc(c, 21, SelGold); Disc(c, 16, new Color(1, .86f, .45f));
                    Tick(c + new Vector2(0, -1), 22, Color.white);
                }
                else
                {
                    Fill(new Rect(pill.x, pill.y + 4, pill.width, pill.height), new Color(0, 0, 0, .25f), 20);
                    Fill(pill, SelGold, 20); Fill(new Rect(pill.x + 3, pill.y + 3, pill.width - 6, pill.height - 6), SelCream, 17);
                    HotelToyKit.Text(new Rect(pill.x, pill.y, pill.width, pill.height - 2), "SELECT", 21, SelInk, TextAnchor.MiddleCenter);
                }

                HotelToyKit.Text(new Rect(card.x + 10, card.y + 330, card.width - 20, 42), (entry.title ?? "").ToUpperInvariant(), 36, SelInk, TextAnchor.MiddleCenter);
                HotelToyKit.Text(new Rect(card.x + 10, card.y + 368, card.width - 20, 26), SelSubtitle(entry.title), 20, new Color(SelInk.r, SelInk.g, SelInk.b, .85f), TextAnchor.MiddleCenter);

                if (GUI.Button(card, GUIContent.none, GUIStyle.none) && !selected) { employee.SelectEmployee(i); HotelAudio.Play("ui_hover", .8f); }
            }

            // LET'S PLAY.
            float by = cy + ch + 26;
            var play = new Rect(cx - 220, by, 440, 74);
            bool ph = play.Contains(Event.current.mousePosition);
            var pd = ph ? new Rect(play.x, play.y - 2, play.width, play.height) : play;
            Fill(new Rect(pd.x - 5, pd.y - 5, pd.width + 10, pd.height + 10), SelGold, 40);
            HotelToyKit.Bubble(pd, ph ? Color.Lerp(Blue, Color.white, .2f) : Blue, BlueBottom, 34, 6);
            var tri = new Rect(pd.x + 92, pd.y + 19, 34, 36);
            var old = GUI.color; GUI.color = new Color(0, 0, 0, .35f * GUI.contentColor.a); if (Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(tri.x + 2, tri.y + 3, tri.width, tri.height), selectTriangle); GUI.color = new Color(1, 1, 1, GUI.contentColor.a); if (Event.current.type == EventType.Repaint) GUI.DrawTexture(tri, selectTriangle); GUI.color = old;
            HotelToyKit.Text(new Rect(pd.x + 60, pd.y - 2, pd.width - 60, pd.height), hasPlayed ? "RESUME" : "LET'S PLAY", 40, Color.white, TextAnchor.MiddleCenter, 2.2f, new Color(.03f, .25f, .45f));
            if (GUI.Button(play, GUIContent.none, GUIStyle.none)) { HotelAudio.Play("ui_open", .9f); CaptureMouse(); }

            // Secondary pills.
            float py = by + 92;
            bool menu = HotelMainMenu.MenuSceneAvailable;
            int n = 1 + (hasPlayed ? 1 : 0) + (menu ? 1 : 0);
            float pw = 180, pg = 18, px = cx - (pw * n + pg * (n - 1)) / 2;
            if (SelPill(new Rect(px, py, pw, 44), "SETTINGS", SelNavy, Color.white)) settingsOpen = true; px += pw + pg;
            if (hasPlayed) { if (SelPill(new Rect(px, py, pw, 44), "START OVER", SelNavy, Color.white)) { ResetWorld(); CaptureMouse(); } px += pw + pg; }
            if (menu && SelPill(new Rect(px, py, pw, 44), "MAIN MENU", SelNavy, Color.white)) HotelMainMenu.ReturnToMenu();

            // Key hints.
            float hy = py + 62, hx = cx - 230;
            KeyChip(ref hx, hy, "LEFT"); HotelToyKit.Text(new Rect(hx - 4, hy, 14, 28), "/", 18, Color.white); hx += 12; KeyChip(ref hx, hy, "RIGHT"); HintText(ref hx, hy, "Choose");
            KeyChip(ref hx, hy, "ENTER"); HintText(ref hx, hy, "Play");
            KeyChip(ref hx, hy, "O"); HintText(ref hx, hy, "Settings");
        }
    }
}

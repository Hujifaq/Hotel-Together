using System.Collections.Generic;
using UnityEngine;
using static HotelTogether.HotelToyKit;

namespace HotelTogether
{
    /// <summary>Wallet card, upgrade shop, staff roster and shift report in the game's toy style.</summary>
    public sealed class HotelEconomyUI : MonoBehaviour
    {
        struct Popup { public string text; public float born; public bool gain; }

        bool staffView;
        HotelEconomy economy;
        float shownWallet, flash, bump, shownReputation, goalPulse;
        int lastWallet = int.MinValue;
        bool lastModal;
        readonly List<Popup> popups = new();
        readonly bool[] goalDone = new bool[3];
        int goalDay = -1;

        static readonly string[] shopNames = { "ROOM COMFORT", "WORKSHOP EQUIPMENT", "NIGHT SECURITY", "HIRE EMPLOYEE" };
        static readonly string[] shopDetails =
        {
            "+$25 room rate per level. Better tips and patience, plus an extra room cushion.",
            "Repairs, mopping and washing are 25% faster per level.",
            "Barricades and openings are 25% stronger per level, plus 5 seconds of night preparation.",
            "2 starter employees included. Hire up to 6 more at $45 per day each."
        };
        static readonly string[] shopIcons = { "R", "W", "N", "H" };
        static readonly Color[] shopTop = { HotelToyKit.Orange, HotelToyKit.Blue, HotelToyKit.Red, HotelToyKit.Green };
        static readonly Color[] shopBottom = { HotelToyKit.OrangeBottom, HotelToyKit.BlueBottom, HotelToyKit.RedBottom, HotelToyKit.GreenBottom };

        void Awake() { economy = GetComponent<HotelEconomy>(); }

        void Update()
        {
            if (!economy || !economy.Initialized) return;
            float dt = Time.unscaledDeltaTime;
            bool modal = economy.ModalOpen;
            if (modal != lastModal)
            {
                HotelAudio.Play(modal ? "ui_open" : "ui_close", .8f);
                if (!modal) staffView = false;
                lastModal = modal;
            }

            int wallet = economy.Wallet;
            if (lastWallet == int.MinValue) { lastWallet = wallet; shownWallet = wallet; shownReputation = economy.Reputation; }
            if (wallet != lastWallet)
            {
                int delta = wallet - lastWallet;
                if (!modal && economy.Hotel && economy.Hotel.Running)
                {
                    popups.Add(new Popup { text = (delta > 0 ? "+$" : "-$") + Mathf.Abs(delta), born = Time.unscaledTime, gain = delta > 0 });
                    if (popups.Count > 5) popups.RemoveAt(0);
                    flash = 1; bump = 1;
                    HotelAudio.Play(delta > 0 ? (delta >= 60 ? "coin_big" : "coin") : "coin_loss", delta > 0 ? .8f : .7f, 1, .22f);
                }
                lastWallet = wallet;
            }
            float follow = 1 - Mathf.Exp(-dt * 9);
            shownWallet = Mathf.Abs(shownWallet - wallet) < .5f ? wallet : Mathf.Lerp(shownWallet, wallet, follow);
            shownReputation = Mathf.Lerp(shownReputation, economy.Reputation, 1 - Mathf.Exp(-dt * 5));
            flash = Mathf.MoveTowards(flash, 0, dt * 1.6f);
            bump = Mathf.MoveTowards(bump, 0, dt * 3.2f);
            goalPulse = Mathf.MoveTowards(goalPulse, 0, dt * 1.4f);
            popups.RemoveAll(p => Time.unscaledTime - p.born > 1.6f);

            var shift = economy.Shift;
            if (shift != null)
            {
                bool[] done = { shift.checkIns >= economy.CheckInGoal, shift.services >= economy.ServiceGoal, shift.Revenue >= economy.IncomeGoal };
                // A new shift resets the counters; resync silently instead of replaying the fanfare.
                if (goalDay != economy.CompletedDays) { goalDay = economy.CompletedDays; for (int i = 0; i < 3; i++) goalDone[i] = done[i]; }
                for (int i = 0; i < 3; i++)
                {
                    if (done[i] && !goalDone[i] && economy.Hotel && economy.Hotel.Running) { HotelAudio.Play("goal", .9f, 1, .3f); goalPulse = 1; }
                    goalDone[i] = done[i];
                }
            }
        }

        void OnGUI()
        {
            if (!economy || !economy.Initialized) return;
            var oldMatrix = GUI.matrix; var oldColor = GUI.color; var oldContent = GUI.contentColor;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1440f, Screen.height / 900f), .1f, 2f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale); GUI.color = GUI.contentColor = Color.white;
            float width = Screen.width / scale, height = Screen.height / scale;

            if (economy.ModalOpen)
            {
                Rule(new Rect(0, 0, width, height), new Color(.05f, .12f, .17f, .62f));
                var r = new Rect((width - 780) * .5f, (height - 660) * .5f, 780, 660);
                Bubble(r, Cream, CreamBottom, 30, 8);
                if (economy.SummaryOpen) Report(r); else if (staffView) Roster(r); else Shop(r);
            }
            else if (economy.Hotel && economy.Hotel.Running && (!economy.Hotel.player || economy.Hotel.player.HasStartedShift)) WalletCard(24, 98);

            GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.contentColor = oldContent;
        }

        // ---------------------------------------------------------------- HUD

        void WalletCard(float x, float y)
        {
            const float W = 304;
            var shift = economy.Shift;
            var card = new Rect(x, y, W, 346);
            Bubble(card, Cream, CreamBottom, 26, 6);

            // Coin + animated wallet counter.
            var coinCenter = new Vector2(x + 44, y + 44);
            float spin = Mathf.Abs(Mathf.Cos(Time.unscaledTime * 1.8f));
            Disc(coinCenter + new Vector2(0, 3), 29, new Color(Brown.r * .7f, Brown.g * .7f, Brown.b * .7f, .8f));
            Disc(coinCenter, 29, Brown);
            Disc(coinCenter, 25.5f, GoldBottom);
            Disc(coinCenter + new Vector2(0, -2), 23, Gold);
            Disc(coinCenter + new Vector2(-8, -10), 5, new Color(1, 1, 1, .55f));
            Text(new Rect(coinCenter.x - 24, coinCenter.y - 24, 48, 46), "$", 36, Brown, TextAnchor.MiddleCenter);
            Text(new Rect(x + 86, y + 12, 130, 20), "WALLET", 16, new Color(Brown.r, Brown.g, Brown.b, .75f), TextAnchor.MiddleLeft);
            Color number = Color.Lerp(Brown, popups.Count > 0 && !popups[popups.Count - 1].gain ? RedBottom : GreenBottom, flash);
            int size = 44 + Mathf.RoundToInt(bump * 8);
            Text(new Rect(x + 84, y + 28, 206, 56), "$" + Mathf.RoundToInt(shownWallet), size, number, TextAnchor.MiddleLeft);

            foreach (var p in popups)
            {
                float age = (Time.unscaledTime - p.born) / 1.6f;
                float a = age < .15f ? age / .15f : 1 - Mathf.InverseLerp(.55f, 1, age);
                var c = p.gain ? Green : Red;
                Text(new Rect(x + W - 112, y - 6 + 50 - EaseOut(age) * 46, 100, 34), p.text, 28, new Color(c.r, c.g, c.b, Mathf.Clamp01(a)), TextAnchor.MiddleRight, 2, new Color(Brown.r, Brown.g, Brown.b, Mathf.Clamp01(a)));
            }

            // Reputation.
            float rep = Mathf.Clamp(shownReputation, 0, 100);
            Color repTop = rep < 35 ? Red : rep < 65 ? Orange : Green, repBottom = rep < 35 ? RedBottom : rep < 65 ? OrangeBottom : GreenBottom;
            Text(new Rect(x + 18, y + 90, 170, 22), "REPUTATION", 17, Brown, TextAnchor.MiddleLeft);
            Text(new Rect(x + W - 108, y + 90, 90, 22), Mathf.RoundToInt(economy.Reputation) + " / 100", 17, Brown, TextAnchor.MiddleRight);
            var bar = new Rect(x + 20, y + 118, W - 40, 16);
            Bar(bar, rep / 100f, Color.Lerp(repTop, Color.white, .12f), new Color(Brown.r, Brown.g, Brown.b, .22f));
            Rule(new Rect(bar.x + bar.width * .65f - 1, bar.y - 4, 2, bar.height + 8), new Color(Brown.r, Brown.g, Brown.b, .55f));

            // Goals.
            Rule(new Rect(x + 20, y + 148, W - 40, 2), new Color(Brown.r, Brown.g, Brown.b, .18f));
            Text(new Rect(x + 18, y + 154, W - 36, 22), "TODAY'S GOALS", 16, new Color(Brown.r, Brown.g, Brown.b, .75f), TextAnchor.MiddleLeft);
            GoalRow(x, y + 178, "Check-ins", shift.checkIns + " / " + economy.CheckInGoal, shift.checkIns, economy.CheckInGoal, 0);
            GoalRow(x, y + 214, "Service", shift.services + " / " + economy.ServiceGoal, shift.services, economy.ServiceGoal, 1);
            GoalRow(x, y + 250, "Collected", "$" + shift.Revenue + " / $" + economy.IncomeGoal, shift.Revenue, economy.IncomeGoal, 2);
            KeyHint(x + 4, y + 294, "U", "HOTEL UPGRADES");
        }

        void GoalRow(float x, float y, string label, string value, int have, int need, int index)
        {
            const float W = 304;
            bool done = have >= need;
            float pulse = done && goalPulse > 0 ? 1 + Mathf.Sin(goalPulse * Mathf.PI) * .35f : 1;
            var icon = new Vector2(x + 30, y + 13);
            Disc(icon, 12.5f * pulse, Brown);
            Disc(icon, 10 * pulse, done ? Green : new Color(Brown.r, Brown.g, Brown.b, .16f));
            if (done) Tick(icon + new Vector2(0, -1), 14 * pulse, Color.white);
            Text(new Rect(x + 52, y, 130, 22), label, 18, Brown, TextAnchor.MiddleLeft);
            Text(new Rect(x + W - 140, y, 120, 22), value, 18, done ? GreenBottom : Brown, TextAnchor.MiddleRight);
            Bar(new Rect(x + 52, y + 24, W - 72, 7), need > 0 ? (float)have / need : 1, done ? Green : Blue, new Color(Brown.r, Brown.g, Brown.b, .2f));
        }

        void KeyHint(float x, float y, string key, string label)
        {
            const float W = 304;
            var r = new Rect(x + 14, y - 2, W - 28, 40);
            Bubble(r, Blue, BlueBottom, 14, 4);
            var chip = new Rect(r.x + 8, r.y + 6, 28, 27);
            Fill(chip, Brown, 8); Fill(new Rect(chip.x + 2, chip.y + 2, chip.width - 4, chip.height - 4), Cream, 6);
            Text(chip, key, 20, Brown, TextAnchor.MiddleCenter);
            Text(new Rect(r.x + 44, r.y, r.width - 52, r.height - 3), label, 21, Color.white, TextAnchor.MiddleLeft, 1.4f);
        }

        static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1 - (1 - t) * (1 - t); }

        // ---------------------------------------------------------------- Modals

        void Banner(Rect r, string title, string right = null)
        {
            var banner = new Rect(r.x + 24, r.y + 20, r.width - 48, 62);
            Bubble(banner, Blue, BlueBottom, 22, 5);
            Text(new Rect(banner.x + 22, banner.y, banner.width - 44, banner.height - 3), title, 34, Color.white, TextAnchor.MiddleLeft, 2.2f);
            if (right != null)
            {
                var pill = new Rect(banner.xMax - 190, banner.y + 10, 172, 38);
                Fill(new Rect(pill.x, pill.y + 3, pill.width, pill.height), new Color(Brown.r, Brown.g, Brown.b, .5f), 19);
                Fill(pill, Brown, 19); Fill(new Rect(pill.x + 3, pill.y + 3, pill.width - 6, pill.height - 6), Cream, 16);
                Disc(new Vector2(pill.x + 20, pill.center.y), 11, Gold);
                Text(new Rect(pill.x + 34, pill.y, pill.width - 42, pill.height - 2), right, 25, Brown, TextAnchor.MiddleLeft);
            }
        }

        bool Click(Rect r, string label, Color top, Color bottom, int size = 26, bool enabled = true)
        {
            bool clicked = Button(r, label, top, bottom, size, enabled);
            if (clicked) HotelAudio.Play("ui_click", .9f);
            return clicked;
        }

        void Shop(Rect r)
        {
            Banner(r, "HOTEL MANAGEMENT", "$" + economy.Wallet);
            for (int i = 0; i < 4; i++)
            {
                var card = new Rect(r.x + 24, r.y + 98 + i * 98, r.width - 48, 88);
                Fill(new Rect(card.x, card.y + 4, card.width, card.height), new Color(Brown.r, Brown.g, Brown.b, .22f), 20);
                Fill(card, Brown, 20); Fill(new Rect(card.x + 3, card.y + 3, card.width - 6, card.height - 6), new Color(1, .98f, .9f), 17);

                var ic = new Vector2(card.x + 48, card.center.y);
                Disc(ic + new Vector2(0, 3), 31, new Color(Brown.r, Brown.g, Brown.b, .7f)); Disc(ic, 31, Brown); Disc(ic, 27.5f, shopBottom[i]); Disc(ic + new Vector2(0, -2), 25, shopTop[i]);
                Text(new Rect(ic.x - 26, ic.y - 26, 52, 50), shopIcons[i], 34, Color.white, TextAnchor.MiddleCenter, 1.8f);

                int level = economy.Levels[i], maxLevel = i == 3 ? 6 : 3;
                bool max = level >= maxLevel;
                int cost = economy.Price((HotelUpgrade)i);
                Text(new Rect(card.x + 94, card.y + 8, 330, 30), shopNames[i], 25, Brown, TextAnchor.MiddleLeft);
                for (int p = 0; p < maxLevel; p++)
                {
                    var c = new Vector2(card.x + 100 + p * 17, card.y + 49);
                    Disc(c, 7, Brown); Disc(c, 5, p < level ? Gold : new Color(Brown.r, Brown.g, Brown.b, .15f));
                }
                Text(new Rect(card.x + 100 + maxLevel * 17 + 6, card.y + 38, 120, 22), "LEVEL " + level + "/" + maxLevel, 16, new Color(Brown.r, Brown.g, Brown.b, .75f), TextAnchor.MiddleLeft);
                Text(new Rect(card.x + 94, card.y + 60, 420, 26), shopDetails[i], 15, new Color(Brown.r, Brown.g, Brown.b, .85f), TextAnchor.UpperLeft, 0, null, true);

                bool afford = economy.Wallet >= cost;
                string label = max ? "MAX" : "BUY  $" + cost;
                var button = new Rect(card.xMax - 158, card.y + 18, 140, 52);
                if (Click(button, label, afford ? Green : Orange, afford ? GreenBottom : OrangeBottom, 25, !max))
                {
                    if (afford) { if (economy.Purchase((HotelUpgrade)i)) HotelAudio.Play("buy", 1); else HotelAudio.Play("deny", .8f); }
                    else HotelAudio.Play("deny", .8f);
                }
            }
            Text(new Rect(r.x + 30, r.y + 494, r.width - 60, 40), "Collect cash from customers. Daily bills: $95 + staff wages. Upgrades and your career balance save at the end of a shift.", 16, new Color(Brown.r, Brown.g, Brown.b, .8f), TextAnchor.MiddleCenter, 0, null, true);
            if (Click(new Rect(r.x + 24, r.y + 540, r.width - 48, 50), "MANAGE EMPLOYEES  (" + economy.Staff.Count + ")", Orange, OrangeBottom, 26)) staffView = true;
            if (Click(new Rect(r.x + 24, r.y + 598, r.width - 48, 50), "BACK TO WORK", Blue, BlueBottom, 28)) { staffView = false; economy.CloseShop(); }
        }

        void Roster(Rect r)
        {
            Banner(r, "YOUR EMPLOYEES", economy.Staff.Count + " STAFF");
            Text(new Rect(r.x + 30, r.y + 88, r.width - 60, 26), "Tap a duty to cycle it. Pick a specialist, or ALL for automatic dispatch.", 17, new Color(Brown.r, Brown.g, Brown.b, .8f), TextAnchor.MiddleCenter);
            int count = economy.Staff.Count;
            float pitch = Mathf.Min(54, 420f / Mathf.Max(1, count));
            for (int i = 0; i < count; i++)
            {
                var worker = economy.Staff[i];
                var row = new Rect(r.x + 24, r.y + 120 + i * pitch, r.width - 48, pitch - 6);
                Fill(row, Brown, 16); Fill(new Rect(row.x + 3, row.y + 3, row.width - 6, row.height - 6), i % 2 == 0 ? new Color(1, .98f, .9f) : new Color(1, .96f, .84f), 13);
                Text(new Rect(row.x + 16, row.y, 160, row.height - 2), worker.StaffName, 22, Brown, TextAnchor.MiddleLeft);
                var duty = new Rect(row.x + 176, row.y + 5, 132, row.height - 14);
                if (Click(duty, worker.OnDuty ? "ON DUTY" : "RESTING", worker.OnDuty ? Green : Orange, worker.OnDuty ? GreenBottom : OrangeBottom, 19)) { worker.SetOnDuty(!worker.OnDuty); economy.SaveStaffSettings(); }
                var job = new Rect(row.x + 316, row.y + 5, 132, row.height - 14);
                if (Click(job, worker.Duty.ToString().ToUpperInvariant(), Blue, BlueBottom, 19)) { worker.SetDuty((StaffDuty)(((int)worker.Duty + 1) % 6)); economy.SaveStaffSettings(); }
                Text(new Rect(row.x + 458, row.y, row.width - 470, row.height - 2), worker.State + "  /  " + worker.CompletedJobs + " jobs", 15, new Color(Brown.r, Brown.g, Brown.b, .8f), TextAnchor.MiddleLeft);
            }
            if (Click(new Rect(r.x + 24, r.y + 552, r.width - 48, 46), "BACK TO UPGRADES", Orange, OrangeBottom, 25)) staffView = false;
            if (Click(new Rect(r.x + 24, r.y + 606, r.width - 48, 46), "BACK TO WORK", Blue, BlueBottom, 26)) { staffView = false; economy.CloseShop(); }
        }

        void Chip(Rect r, string label, string value)
        {
            Fill(r, Brown, 14); Fill(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), new Color(1, .98f, .9f), 11);
            Text(new Rect(r.x + 12, r.y, r.width - 70, r.height - 2), label, 15, new Color(Brown.r, Brown.g, Brown.b, .8f), TextAnchor.MiddleLeft);
            Text(new Rect(r.xMax - 70, r.y, 60, r.height - 2), value, 22, Brown, TextAnchor.MiddleRight);
        }

        void Report(Rect r)
        {
            var s = economy.LastReport;
            if (s == null) return;
            Banner(r, "DAY " + s.day + "  SHIFT REPORT");
            string[,] stats =
            {
                { "Check-ins", s.checkIns.ToString() }, { "Services", s.services.ToString() }, { "Cleaning", s.cleaning.ToString() }, { "Repairs", s.repairs.ToString() },
                { "Zombies stopped", s.kills.ToString() }, { "Lives lost", s.deaths.ToString() }, { "Rating", Mathf.RoundToInt(s.rating) + "/100" }
            };
            float cw = (r.width - 48 - 18) / 4f;
            for (int i = 0; i < 7; i++) Chip(new Rect(r.x + 24 + (i % 4) * (cw + 6), r.y + 96 + (i / 4) * 46, cw, 40), stats[i, 0], stats[i, 1]);

            string[] names = { "Collected room payments", "Tips and work rewards", "Daily goal bonuses", "Utilities and upkeep", "Staff wages", "Guest compensation", "Upgrade purchases" };
            int[] amounts = { s.rent, s.tips, s.bonus, -s.upkeep, -s.wages, -s.fines, -s.investments };
            for (int i = 0; i < names.Length; i++)
            {
                var row = new Rect(r.x + 24, r.y + 196 + i * 33, r.width - 48, 31);
                if (i % 2 == 0) Fill(row, new Color(Brown.r, Brown.g, Brown.b, .08f), 12);
                Text(new Rect(row.x + 14, row.y, 400, row.height), names[i], 19, Brown, TextAnchor.MiddleLeft);
                int a = amounts[i];
                Text(new Rect(row.xMax - 164, row.y, 150, row.height), (a > 0 ? "+$" : a < 0 ? "-$" : "$") + Mathf.Abs(a), 21, a > 0 ? GreenBottom : a < 0 ? RedBottom : new Color(Brown.r, Brown.g, Brown.b, .6f), TextAnchor.MiddleRight);
            }
            var net = new Rect(r.x + 24, r.y + 196 + 7 * 33 + 4, r.width - 48, 46);
            bool gain = s.Net >= 0;
            Bubble(net, gain ? Green : Red, gain ? GreenBottom : RedBottom, 18, 4);
            Text(new Rect(net.x + 20, net.y, 300, net.height - 3), "SHIFT NET", 26, Color.white, TextAnchor.MiddleLeft, 1.8f);
            Text(new Rect(net.xMax - 220, net.y, 200, net.height - 3), (gain ? "+$" : "-$") + Mathf.Abs(s.Net), 30, Color.white, TextAnchor.MiddleRight, 1.8f);

            Text(new Rect(r.x + 30, r.y + 488, r.width - 60, 48), "Wallet $" + economy.Wallet + ".  Goals: " + s.checkInTarget + " check-ins, " + s.serviceTarget + " services, collect $" + s.incomeTarget + ", rating 65+, protect every guest.", 16, new Color(Brown.r, Brown.g, Brown.b, .85f), TextAnchor.MiddleCenter, 0, null, true);
            Text(new Rect(r.x + 30, r.y + 534, r.width - 60, 22), "Career money, reputation and upgrades are saved at dawn.", 15, new Color(Brown.r, Brown.g, Brown.b, .65f), TextAnchor.MiddleCenter);
            if (Click(new Rect(r.x + 24, r.y + 574, r.width - 48, 64), "START DAY " + economy.Hotel.DayNumber, Blue, BlueBottom, 34)) economy.ContinueDay();
        }
    }
}

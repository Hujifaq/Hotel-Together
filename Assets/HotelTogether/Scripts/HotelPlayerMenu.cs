using UnityEngine;
using UnityEngine.InputSystem;

namespace HotelTogether
{
    // Menu state belongs to the player; this partial keeps UI options out of carrying/physics code.
    public partial class HotelPlayer
    {
        const string SettingsPrefix = "HotelTogether.Settings.";
        bool menuPaused, settingsOpen, hasPlayed, invertLook, settingsDirty, applicationFocused = true;
        float resumeTimeScale = 1, masterVolume = 1, previousVolume;
        bool previousAudioPause;
        double noticePauseStarted;

        public bool SettingsOpen => settingsOpen;
        public bool GameplayPaused=>menuPaused||!applicationFocused||Time.timeScale<=0;
        public bool GameplayInputEnabled => isActiveAndEnabled && applicationFocused && !menuPaused
            && Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0;

        void Awake()
        {
            previousVolume = AudioListener.volume;
            sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SettingsPrefix + "Sensitivity", sensitivity), .03f, .30f);
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsPrefix + "Volume", previousVolume));
            walkingCameraBob = PlayerPrefs.GetInt(SettingsPrefix + "CameraBob", walkingCameraBob ? 1 : 0) != 0;
            invertLook = PlayerPrefs.GetInt(SettingsPrefix + "InvertLook", 0) != 0;
            AudioListener.volume = masterVolume;
        }

        void OnEnable() { PauseGameplay(); }
        void OnDisable()
        {
            CancelMouseGrip();
            ReleaseGuest(false);
            if (settingsDirty) SaveSettings();
            RestoreSimulation();
            AudioListener.volume = previousVolume;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        void OnApplicationFocus(bool focusedNow)
        {
            applicationFocused = focusedNow;
            if (!focusedNow && isActiveAndEnabled) PauseGameplay();
        }
        void OnApplicationPause(bool paused) { if (paused && isActiveAndEnabled) PauseGameplay(); }

        public void PauseGameplay()
        {
            CancelMouseGrip();
            if (!menuPaused)
            {
                // A reload/re-enabled player can inherit a previous menu's global pause.
                // Only a positive speed is a valid destination for the Play button.
                bool runningSpeed=Time.timeScale>0&&float.IsFinite(Time.timeScale);
                resumeTimeScale = runningSpeed?Time.timeScale:1;
                previousAudioPause = runningSpeed&&AudioListener.pause;
                noticePauseStarted = Time.unscaledTimeAsDouble;
                menuPaused = true;
            }
            Time.timeScale = 0;
            AudioListener.pause = true;
            jobsOpen = false;
            focused = null;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void RestoreSimulation()
        {
            if (!menuPaused) return;
            // Restore the speed and audio state that this menu temporarily took ownership of.
            bool validSpeed=resumeTimeScale>0&&float.IsFinite(resumeTimeScale);
            Time.timeScale = validSpeed?resumeTimeScale:1;
            AudioListener.pause = validSpeed&&previousAudioPause;
            if (messageUntil > 0) messageUntil += (float)(Time.unscaledTimeAsDouble - noticePauseStarted);
            menuPaused = false;
        }

        public void ResumeGameplay()
        {
            if (!applicationFocused || !isActiveAndEnabled) return;
            RestoreSimulation();
            AudioListener.volume = masterVolume;
            if (settingsDirty) SaveSettings();
            settingsOpen = false;
            hasPlayed = true;
            awaitPrimaryRelease=UnityEngine.InputSystem.Mouse.current!=null&&UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        bool UpdateMenuInput()
        {
            if(Economy&&Economy.ModalOpen){var modalKeys=Keyboard.current;if(modalKeys!=null){if(Economy.SummaryOpen&&modalKeys.enterKey.wasPressedThisFrame)Economy.ContinueDay();else if(Economy.ShopOpen&&(modalKeys.escapeKey.wasPressedThisFrame||modalKeys.uKey.wasPressedThisFrame))Economy.CloseShop();}return true;}
            // Unity can release the cursor without an Esc event (focus change / Editor escape).
            if (!menuPaused && Cursor.lockState != CursorLockMode.Locked) PauseGameplay();
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (settingsOpen) settingsOpen = false;
                else if (!menuPaused) PauseGameplay();
                else if (hasPlayed) ResumeGameplay();
                return true;
            }
            if (!menuPaused) return false;
            if (keyboard != null)
            {
                if (keyboard.oKey.wasPressedThisFrame) settingsOpen = !settingsOpen;
                if (keyboard.enterKey.wasPressedThisFrame && !settingsOpen) ResumeGameplay();
            }
            return true;
        }

        void SaveSettings()
        {
            PlayerPrefs.SetFloat(SettingsPrefix + "Sensitivity", sensitivity);
            PlayerPrefs.SetFloat(SettingsPrefix + "Volume", masterVolume);
            PlayerPrefs.SetInt(SettingsPrefix + "CameraBob", walkingCameraBob ? 1 : 0);
            PlayerPrefs.SetInt(SettingsPrefix + "InvertLook", invertLook ? 1 : 0);
            PlayerPrefs.Save();
            settingsDirty = false;
        }

        float SettingsSlider(Rect row, string label, float value, float min, float max, string display)
        {
            GUI.Label(new Rect(row.x, row.y, row.width - 80, 26), label, hudTitle);
            GUI.Label(new Rect(row.xMax - 80, row.y, 80, 26), display, uiSmall);
            return GUI.HorizontalSlider(new Rect(row.x, row.y + 37, row.width, 24), value, min, max);
        }

        void DrawSettingsMenu(Rect safe)
        {
            Rule(safe, new Color(.05f, .15f, .20f, .63f));
            float width = Mathf.Min(660, safe.width - 36);
            var r = new Rect(safe.center.x - width / 2, safe.center.y - 353, width, 706);
            Bubble(r, uiCream, uiCreamBottom, 28, 7);
            Bubble(new Rect(r.x + 20, r.y + 18, width - 40, 68), uiBlue, uiBlueBottom, 20, 4);
            uiHeading.fontSize = 32;
            Text(new Rect(r.x + 20, r.y + 18, width - 40, 68), "SETTINGS & CONTROLS", uiHeading, uiBrown, 2);
            GUI.Label(new Rect(r.x + 24, r.y + 98, width - 48, 26), "Game paused. Your settings are saved automatically.", uiSmall);
            var oldSensitivity = sensitivity;
            var oldVolume = masterVolume;
            sensitivity = SettingsSlider(new Rect(r.x + 32, r.y + 143, width - 64, 62),
                "MOUSE SENSITIVITY", sensitivity, .03f, .30f, (sensitivity / .12f).ToString("0.00") + "x");
            masterVolume = SettingsSlider(new Rect(r.x + 32, r.y + 222, width - 64, 62),
                "MASTER VOLUME", masterVolume, 0, 1, Mathf.RoundToInt(masterVolume * 100) + "%");
            bool changed = !Mathf.Approximately(oldSensitivity, sensitivity) || !Mathf.Approximately(oldVolume, masterVolume);
            float half = (width - 78) / 2;
            if (MenuButton(new Rect(r.x + 32, r.y + 305, half, 48), "CAMERA BOB: " + (walkingCameraBob ? "ON" : "OFF"), false))
            { walkingCameraBob = !walkingCameraBob; changed = true; }
            if (MenuButton(new Rect(r.x + 46 + half, r.y + 305, half, 48), "INVERT Y: " + (invertLook ? "ON" : "OFF"), false))
            { invertLook = !invertLook; changed = true; }
            if (changed)
            {
                AudioListener.volume = masterVolume;
                settingsDirty = true;
            }
            // Sliders update live; write to disk once the drag/click ends.
            if (settingsDirty && Event.current.rawType == EventType.MouseUp) SaveSettings();
            Rule(new Rect(r.x + 32, r.y + 378, width - 64, 1), uiCreamBottom);
            GUI.Label(new Rect(r.x + 32, r.y + 397, width - 64, 26), "YOUR SHIFT", hudTitle);
            GUI.Label(new Rect(r.x + 32, r.y + 435, width - 64, 26), "WASD Move  /  SHIFT Run  /  SPACE Jump", uiSmall);
            GUI.Label(new Rect(r.x + 32, r.y + 467, width - 64, 26), "LMB Interact / hold  /  Release LMB drop  /  RMB Throw", uiSmall);
            GUI.Label(new Rect(r.x + 32, r.y + 499, width - 64, 26), "LMB Fire / Hit  /  R Reload gun  /  Q Put down  /  H Heal", uiSmall);
            GUI.Label(new Rect(r.x + 32, r.y + 531, width - 64, 26), "B / N / T Barricades  /  R Reset without gun  /  ESC Menu", uiSmall);
            if (MenuButton(new Rect(r.x + 32, r.y + 590, width - 64, 58), "BACK TO MENU", true)) settingsOpen = false;
            GUI.Label(new Rect(r.x + 24, r.y + 668, width - 48, 24), "ESC  Back  |  O  Settings", uiSmall);
        }
    }
}

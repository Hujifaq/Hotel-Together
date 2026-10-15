# Main menu

Scene: `Assets/HotelTogether/Scenes/HotelMainMenu.unity` (index 0 in Build Settings, HotelLobby next).
Created automatically by `Editor/HotelMainMenuSetup.cs`; rebuild with **Hotel Together > Main Menu > Rebuild Main Menu Scene**.
**Hotel Together > Main Menu > Start Play Mode From Main Menu** (on by default) makes the Editor's Play button start at the menu; untick it to play HotelLobby directly.

## Tuning (select "Main Menu" in the scene)
- Characters: feet position and height in a 1152 x 1080 design stage (= the left 60% of 1920 x 1080), parallax depth, bob/rotation amplitude, speed and phase, entrance delay. Drawn back to front.
- Parallax strength, max offset (reference px), background depth, smoothing. Float strength/speed. Entrance duration.
- Layout: character area fraction (0.6), stage anchor, background anchor.

## Behaviour
- PLAY GAME fades out and loads HotelLobby, which then skips its "click to clock in" title and opens the employee picker.
- OPTIONS: master and music volume, mouse sensitivity, graphics tier, fullscreen (built game only), reduced motion. Saved to PlayerPrefs with the same keys as the in-game Settings.
- CREDITS: `Resources/HotelTogetherMenu/Credits.txt` (lines starting with # are headings).
- QUIT: exits the built game; stops Play mode in the Editor. Hidden on WebGL/mobile.
- Controls: mouse, keyboard (arrows/WASD, Enter/Space, Esc/Backspace) and gamepad (stick/D-pad, A/Start, B). Right stick also drives parallax.
- In-game pause menus now have a MAIN MENU button.

## Art
`Resources/HotelTogetherMenu/*.png`, prepared from hotel-clay-parallax-v1.zip with `Tools/Menu/prepare_menu_art.py`
(body alpha made fully opaque, edge specks removed, trimmed, colour-bled edges). Import settings are enforced by the editor script.

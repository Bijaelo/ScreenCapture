# ScreenCaptureApp – Follow-up Implementation Plan

Goal: address post-implementation feedback on window naming/highlight, layout, picker behavior, and multi-hotkey support.

## 1) Window naming & hover highlight
- Change dropdown display text to a prefix format to reduce width: `[Process] Title` (or `Process — Title`), with safe truncation for long titles and full text on tooltip.
- Owner-draw the window ComboBox (`DrawMode.OwnerDrawFixed`) and track hovered items; on hover, show a reversible frame highlight over the associated window (`ControlPaint.DrawReversibleFrame` with the window bounds) and clear the highlight when the mouse leaves or selection changes.
- Keep folder naming as `Window_{Title}` but ensure UI labels stay compact.

## 2) Target layout & visibility
- Rework the target group into two inline rows: row 1 radio buttons (`All screens`, `Specific screen`, `Window`); row 2 shows only the relevant selector.
  - For `Specific screen`: show the screen dropdown only; hide window controls so they don’t occupy space.
  - For `Window`: show the window dropdown plus the `Pick` button inline beneath/next to it; hide the screen dropdown.
- Adjust group height/positions accordingly so there’s no overlap; keep “Pick” directly adjacent to the window selector.

## 3) Picker selects only app window
- Enter pick mode by minimizing/hiding the form (or moving it out of the way) and setting a crosshair cursor; install a low-level mouse hook to capture the first click anywhere.
- On click, use `WindowFromPoint` and parent-walk to the top-level window; ignore this app’s window handle; restore the form afterward and populate/select the chosen window in the dropdown.
- Ensure highlighting and pick-mode flags are cleared even if the click hits an invalid target.

## 4) Multiple hotkeys with add/remove list
- Replace the single hotkey box with an input + “Add” button and a listbox showing configured hotkeys; include a “Remove”/“Clear All” control.
- Store hotkeys in a `List<HotkeyBinding>`; prevent duplicates; Start requires at least one binding in keystroke mode.
- Match keystrokes against the list; display selected hotkeys in the list UI, and update labels/tooltips accordingly.

## Sequencing
1) Tidy target layout (radio row + conditional selectors; place “Pick” next to window dropdown; hide unused controls).
2) Fix picker behavior (hide/minimize during pick; target top-level windows; exclude self).
3) Implement hover highlight for window dropdown items.
4) Add multi-hotkey list UI + logic with validation.
5) Manual verification: layout with all modes/targets, picker on external windows, hover highlight, minimized start/stop, multi-hotkey trigger matching.

## Additional UI polish (hotkey controls)
- Hide hotkey input/add/remove/list while “Any key” is selected; show them only when “Any key” is unchecked.
- After clicking “Add,” return focus to the hotkey input to streamline adding multiple combos.
- Keep “Remove” enabled whenever “Any key” is unchecked and the list has entries; disable it otherwise. Tie the enablement to both the checkbox state and list change events.

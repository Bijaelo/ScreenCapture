# ScreenCaptureApp – UX Feedback Implementation Plan

Goal: address the five observations (hotkey UI, window target list/quality, minimized windows, and screen hotplug) while keeping the UI uncluttered and behavior predictable.

## 1) Trigger UI – hide irrelevant controls and fix layout
- Move the hotkey controls into their own row under “On Keystroke”; stack label + textbox + clear button without overlapping the interval slider.
- Only show/enable the seconds label + slider when the trigger is “Interval”; collapse or remove the hotkey row when not in keyboard mode so hidden elements do not reserve space.
- Ensure the trigger label text matches the selected mode (e.g., “Hotkey” instead of “Seconds” in keyboard mode).

## 2) Window targeting – dynamic list and clearer titles
- Refresh the window dropdown on `DropDown` (and on explicit Refresh) so tab/process changes and closed windows are reflected.
- Render entries as “Title — ProcessName.exe” for clarity; keep the dropdown width wide enough to avoid overlap.
- Add a “Pick window” button/eyedropper mode that lets the user click any window to select its handle as an alternative to the dropdown.

## 3) Window list filtering
- Exclude non-user windows: skip invisible, empty titles, tool windows; filter out titles matching patterns like `#\d+`, “Settings”, “Windows Input Experience”, “MainWindow”, etc., and optionally ignore known host processes unless the title appears user-facing.
- Keep the filter lightweight and documented so future adjustments are straightforward.

## 4) Minimized window capture handling
- Before capturing a window, detect `IsIconic`; if minimized, skip the capture silently.
- Track consecutive skipped attempts and elapsed time since last successful capture; if >5 minutes pass with ≥3 skip attempts, stop capture and show one tray warning, then require manual restart.

## 5) Screen selection hotplug support
- Refresh the screen list on dropdown open and before starting capture; revalidate the selected screen against current monitors.
- If the selected screen vanished, auto-fallback to the default/primary screen; if disappearance is detected during capture, stop capturing and show a tray warning.
- Include new screens that appear after the app starts so users can select them immediately.

## Sequence
1) Refine trigger UI layout/visibility toggles (interval vs. hotkey) to remove overlaps.
2) Implement dynamic list refresh for screens and windows; add display/window filters and improved labels.
3) Add window picker (eyedropper) mode and integrate with target selection.
4) Apply minimized-window skip logic with timed stop/warning; add screen hotplug fallback/stop handling.
5) Smoke test all modes (interval/mouse/keyboard) across targets (all/screen/window), then update README if behaviors change.

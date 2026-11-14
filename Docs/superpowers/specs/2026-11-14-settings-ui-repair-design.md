# Settings UI Repair Design

## Goal

Repair the white placeholder blocks shown in the supermarket home, difficulty selection, gameplay HUD, and settings panel while preserving the existing cartoon UI art direction. The repair must not reintroduce removed game modes or activity systems.

## Confirmed Scope

- Restore the existing settings button sprite in the supermarket home and gameplay HUD.
- Keep one settings entry only. Do not restore the lucky-wheel entry.
- Restore all visible settings-panel presentation pieces: title art, close icon, sound and music icons, slider backgrounds, fills and handles, panel frame, and action-button decoration.
- Retain the existing sound-volume, music-volume, continue-game, and `director.ShowHome()` behavior.
- Normalize the settings entry to a 48 by 48 pixel visual target at the reference resolution, with an 18 pixel top-left safe margin.
- Move the supermarket progress HUD below the settings entry when necessary so the controls do not overlap.
- Preserve the existing blue, cream, purple, and orange cartoon palette. Reuse original project assets rather than generating a second visual language.

## Explicitly Excluded

- Lucky-wheel icon or entry button.
- Lucky-wheel panel or any wheel-opening navigation.
- Wheel animation, spin logic, rewards, databases, slots, or services.
- Daily sign-in UI and logic.
- Free coin or gem offer UI and logic.
- Any content from the deleted non-supermarket modes.

## Root Cause

The supermarket-only cleanup explicitly removed the wheel panel, its HUD prefab, scripts, reward configuration, and art. That explains the missing wheel UI, and the removal remains correct under the revised scope.

The settings case is different: the settings prefab, its referenced gear/title/close/slider sprites, and their original GUIDs are still present in the repository. The screenshots therefore cannot be explained by those files simply being absent. Before changing production assets, implementation must reproduce the issue in the current worktree and record whether the white blocks come from a null runtime Sprite, a failed asset import, a stale scene/prefab override, or another rendering-state problem. The fix will target the confirmed failure point and add a validator that catches the same condition.

## Visual Design

### Home and Gameplay HUD

The settings button remains in the top-left corner. Its visible icon uses the original blue rounded-square gear artwork. The visual target is 48 by 48 at the reference resolution, positioned 18 pixels from the top and left safe edges. The supermarket progress counters begin below the button with a clear 10 to 12 pixel gap.

No wheel button or empty placeholder is displayed beside the settings button.

### Settings Panel

The panel keeps the original cream content surface, cyan outer border, purple lower shadow, and outlined Chinese labels. The title and close icon are restored and aligned within one header row. The sound and music rows use equal label widths and aligned slider tracks. Slider background, fill, and handle sprites must all resolve correctly at runtime.

The bottom actions remain `返回主页` and `继续游戏`. Their visual dimensions and spacing are normalized, but their existing behavior is unchanged.

## Implementation Boundaries

- Prefer repairing serialized Sprite references and restoring original `.png` plus `.meta` pairs from the pre-cleanup commit.
- Restore only the minimum dependency closure required by the settings scene objects and `SettingPanel.prefab`.
- Do not restore the old `GameHudUGUI.prefab` wholesale because it contains wheel and other removed activity entries.
- Do not restore lucky-wheel scripts, prefabs, databases, or reward models.
- Extend the supermarket-only validator so retained UI `Image` components that are expected to be visible cannot have a null Sprite, and so wheel-related scene objects or assets remain forbidden.
- Update the cleanup utility's protected asset seeds or rules so the repaired settings assets survive future cleanup runs.

## Verification

1. Reproduce the current settings rendering issue and add the smallest validator or editor test that fails for the confirmed cause.
2. After repair, the validator must pass for the supermarket scene and settings prefab.
3. Unity must compile without C# errors or missing-script warnings.
4. The supermarket home, difficulty popup, gameplay HUD, and settings panel must be captured at the target aspect ratio and checked for white placeholder blocks, overlap, and inconsistent spacing.
5. Opening settings, changing both volume sliders, continuing the game, and returning home must work.
6. The scene and retained assets must contain no lucky-wheel button, panel, service, database, or reward reference.
7. A Windows development Player must build successfully with only the supermarket scene.

## Acceptance Criteria

- No solid-white placeholder blocks appear in any of the four reported UI states.
- The settings gear, title, close icon, sliders, and bottom buttons render with the original project artwork.
- The settings entry and progress HUD do not overlap at the supported reference resolution.
- Settings behavior remains functional, including return to the supermarket home.
- No lucky-wheel UI or functionality is present.
- The supermarket-only validator, Addressables build, and Windows Player build succeed.

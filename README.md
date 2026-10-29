# Manual Rotation Gains

What: Rotates the camera relative to the current orientation of the user's head. 

How: Uses the joystick to turn left/right 

The main menu follows the headset, centred 2.2 metres in front of the player
as they move and turn. This applies to every menu screen.

The main menu opens with **Start Experiment** and **Custom Order**. Choose
**Custom Order** to open the movement-selection menu and select a movement mode.
**Play Game** launches the compact terrain environment; **Test Demo** launches
the original orange-grid floor with the current movement controls. Both use your
selected movement mode and support holding Menu to return. **Back** returns to
the main menu without losing your choice.
Start Experiment is disabled until the experiment flow is implemented.

To adjust the manual rotation type, go to `XR Origin > Movement Mode Selector (Script)` and edit the `Movement Mode`.

- **Use Tilt**: Tilt either controller to turn.
- **Use Joystick**: Turn with the joystick, including acceleration and release deceleration.
- **Continuous Rotation**: Original steady joystick turning, stopping immediately on release.

Continuous Rotation uses the stock joystick turn calculation from Jaz's initial
project (`c61da11`). Full stick deflection turns at five times the configured turn speed
(currently 10 degrees/second); partial deflection scales the speed proportionally.
A held stick produces a steady speed, with no adjustable multiplier or release
coasting. Head-turn gain and A/B/X/Y multiplier adjustments are inactive in this
mode. Continuous Rotation does not produce turning haptic ticks.
Use Joystick and Use Tilt retain their enhanced turning behavior and haptics.

In gameplay, hold the **left controller's Menu button for two seconds** to return
to the main menu. A progress bar appears while holding; release early to cancel.
For Editor testing, hold **Escape**. Returning ends the current run; starting
again reloads the terrain and resets the score, while keeping the previous
movement choice selected in the menu. Both scenes must be enabled in the build
scene list. The hold duration is configurable on XR Origin's
`Hold To Return To Menu` component.

In **Use Joystick** or **Use Tilt**, press **B or Y** to increase the maximum
manual turn multiplier by **0.5**, or **A or X** to decrease it. The range is
**1–8**, starting from the scene's configured **2** (4 degrees/second at full
input). Adjust `Manual Turn Adjustment` on XR Origin to change the step or limits.
Increasing gives **two short buzzes** on the controller pressed; decreasing gives
**one**. No buzz plays at a limit. Turning ticks pause during this feedback.
Buttons adjust once per press; opposing buttons cancel and simultaneous increases
or decreases count as one step. The adjustment is retained across menu trips and
resets when the application restarts. Continuous Rotation uses its fixed 10-degree-per-second joystick speed.

## Compact terrain: second reduction

The gameplay scene retains its current four-tile layout. Its footprint is now
**500 x 500 metres**, reduced from 1,000 x 1,000 metres. Each tile is
250 x 250 metres with a 200-metre elevation range. This is another 50% reduction
of the previous layout, or 25% of the original tile dimensions.

Scenery is repositioned from the current authored layout. Interactive objects,
assembled prefab parts, and the XR rig retain their sizes. Water follows the
terrain footprint and elevation. Coins keep a minimum 2.5-metre horizontal
clearance from the rig spawn to avoid immediate pickup after compression.

**Tools > Terrain Environment > Reduce Compact Environment To 25 Percent**
performs this coordinated change and is safe to rerun: it does not halve the
scene again once the target scale is reached. Pre-change scene, scenery-plan,
and terrain-data backups are in `Backups/CompactSecondReduction/`. Validation
reports are in `Logs/compact-second-reduction.txt` and
`Logs/compact-gameplay-validation.txt`.

Rebuild the APK to test the new layout on the headset. Check destination travel
times, crowded scenery, pier approaches, and coin reach before using it in a study.

## Gameplay countdown

A headset-following timer appears at the bottom left, opposite the coin counter
at the bottom right. Each gameplay run starts at **03:00** and counts down to
**00:00**, which advances to the next experiment step using the same action as
collecting all coins (or returns to the menu outside an experiment). The countdown
pauses when game time is paused and resets when the gameplay scene reloads.

# Manual Rotation Gains

What: Rotates the camera relative to the current orientation of the user's head. 

How: Uses the joystick to turn left/right 

To adjust the manual rotation type, go to `XR Origin > Movement Mode Selector (Script)` and edit the `Movement Mode`.

- **Use Rotation**: Tilt either controller to turn.
- **Use Joystick**: Turn with the joystick, including acceleration and release deceleration.
- **Continuous Rotation**: Amplifies turning based on the gain.

In gameplay, hold the **left controller's Menu button for two seconds** to return
to the main menu. A progress bar appears while holding; release early to cancel.
For Editor testing, hold **Escape**. Returning ends the current run; starting
again reloads the terrain and resets the score, while keeping the previous
movement choice selected in the menu. Both scenes must be enabled in the build
scene list. The hold duration is configurable on XR Origin's
`Hold To Return To Menu` component.

In **Use Joystick** or **Use Rotation**, press **B or Y** to increase the maximum
manual turn multiplier by **0.5**, or **A or X** to decrease it. The range is
**1–8**, starting from the scene's configured **2** (4 degrees/second at full
input). Adjust `Manual Turn Adjustment` on XR Origin to change the step or limits.
Increasing gives **two short buzzes** on the controller pressed; decreasing gives
**one**. No buzz plays at a limit. Turning ticks pause during this feedback.
Buttons adjust once per press; opposing buttons cancel and simultaneous increases
or decreases count as one step. The adjustment is retained across menu trips and
resets when the application restarts. Continuous Rotation's head gain is unchanged.

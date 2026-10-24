# Manual Rotation Gains

What: Rotates the camera relative to the current orientation of the user's head. 

How: Uses the joystick to turn left/right 

To adjust the rotation speed, go to `XR Origin > Continuous Turn Provider` and edit the `Turn Speed`.

For desktop Play mode with SteamVR, keep the **Meta XR** feature group disabled in
`Project Settings > XR Plug-in Management > OpenXR > Windows, Mac, Linux`.
OpenXR and the Oculus Touch controller profile remain enabled. The Android Meta XR
feature group stays enabled for Quest builds. The project's Meta XR enable prompt
is suppressed because accepting it re-enables the desktop group as well.

Enabling the desktop Meta XR group with SteamVR can crash the editor in
`OVRPlugin / UnityPluginUnload` when entering Play mode
([Unity issue](https://issuetracker.unity3d.com/issues/editor-crashes-on-ovrplugin-unitypluginunload-when-entering-into-play-mode-while-steamvr-is-set-as-openxr-runtime-in-steamvr-settings)).
If desktop testing needs Meta-specific features, use Meta Quest Link's OpenXR
runtime and explicitly enable the desktop Meta XR group before entering Play mode.

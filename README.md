# Manual Rotation Gains

## Project Overview

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
project (`c61da11`). Full stick deflection turns at a fixed
30 degrees/second; partial deflection scales the speed proportionally.
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

In **Use Joystick** or **Use Tilt**, press **Y** to increase the maximum
manual turn multiplier by **0.5**, or **X** to decrease it. The range is
**1–8**, starting from the scene's configured **2** (30 degrees/second at full
input). Both modes use a 15-degree/second base speed, so each 0.5 multiplier
step changes full-input speed by 7.5 degrees/second (range: 15?120 degrees/second).
Adjust `Manual Turn Adjustment` on XR Origin to change the step or limits.
Increasing gives **two short buzzes** on the controller pressed; decreasing gives
**one**. No buzz plays at a limit. Turning ticks pause during this feedback.
Buttons adjust once per press; opposing buttons cancel and simultaneous increases
or decreases count as one step. The adjustment is retained across menu trips and
resets when the application restarts. Continuous Rotation uses its fixed 30-degree-per-second joystick speed.

## CSV column labels

Participant CSVs keep the participant ID on row 1 and descriptive column
labels on row 2. Data begins on row 3.

`P##Coins.csv` labels its three columns **Passenger collection time since area
start (s)**, **Conductor collection time since area start (s)**, and **Turn type**.
Each turn type has 15 coin rows in numeric coin-name order. Blank times mean the
coin was not collected in that recorded run.

`P##Frames.csv` labels each of its six columns with the **turn type**, **role**,
**elapsed time (s)**, and **head world XYZ (Unity units)**. Each cell contains
`seconds (x, y, z)`, with elapsed time measured from the start of that area.
Columns remain Continuous Turn, Joystick, and Tilt for Passenger, followed by
the same three for Conductor.

Resume accepts the previous column labels. Existing files receive the new
labels when next written; their data positions and column order stay the same.

`P##TurnSpeed.csv` records one row per rendered frame throughout an experiment,
including demos, game areas, and menu waits. Its labelled columns are:

- Participant PID, recording session ID, and UTC timestamp.
- Session elapsed seconds, step number, scene, role, and turn type.
- Scene elapsed seconds and frame duration in game-time seconds.
- **Applied manual turn (deg/s)**: rotation applied by the turn provider during
  that frame divided by frame duration. Positive is right/clockwise; negative is
  left/counterclockwise. Partial input and release deceleration are included.
- **Full-input speed limit (deg/s)**: the current maximum, including X/Y changes;
  Continuous Rotation uses the Settings speed.
- **Saved starting speed (deg/s)** and whether a turn provider is available.

This measures manual rig turning, not physical head rotation or additional
head-turn redirection. Stationary frames record zero applied speed. Menu frames
have no turn provider and leave the full-input limit blank. Scene time aligns
with the coin pickup timestamps in game areas. Session elapsed time uses real
time and resets on resume; the new session ID distinguishes resumed attempts.

Speed samples are appended in background batches about once a second, and
flushed on app pause or experiment completion. A sudden crash can lose the last
buffered/in-flight samples. Resume backs up this CSV, preserves its completed
rows, removes an incomplete trailing row if necessary, and appends the new
attempt. Starting a fresh experiment resets the speed CSV for that PID. No
controller-connection diagnostics are included in this CSV.


## Pull CSV files from the headset

Connect the headset by USB, enable developer/USB debugging, and accept the USB
debugging prompt inside the headset. Finish the experiment, or return to the
menu and allow pending saves to finish, before exporting. Frame recordings are
saved when an area closes; force-closing during an area loses those unsaved frames.

Run these commands in **Windows PowerShell**. The ADB path below matches this
project's Unity installation; change it if Unity is installed elsewhere.

```powershell
$adb = 'C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
$package = 'com.UnityTechnologies.com.unity.template.urpblank'
$remote = "/sdcard/Android/data/$package/files"
& $adb devices
& $adb shell ls -1 $remote
```

The headset should be listed as `device`. If it says `unauthorized`, accept the
debugging prompt and run `devices` again. With multiple devices connected, add
`-s SERIAL` after `$adb` in each command, using the headset serial from `devices`.

Copy **all participant CSVs** into a new timestamped folder on the PC:

```powershell
$destination = Join-Path (Get-Location) ('CsvExports\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$remoteFiles = & $adb shell ls -1 $remote
if ($LASTEXITCODE -ne 0) { throw 'Cannot list headset recordings. Check the connection and package name.' }
foreach ($name in $remoteFiles) {
    $name = $name.Trim()
    if ($name -match '^P[0-9]+(Coins|Frames|TurnSpeed)\.csv$') {
        & $adb pull "$remote/$name" $destination
        if ($LASTEXITCODE -ne 0) { throw "Failed to pull $name" }
    }
}
Get-ChildItem -LiteralPath $destination -Filter '*.csv'
```

Alternatively, after the setup above, pull just **PID 7** (change `P07` to the
desired PID; for example `P01` or `P12`):

```powershell
$destination = Join-Path (Get-Location) ('CsvExports\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$participantPrefix = 'P07'
& $adb pull "$remote/${participantPrefix}Coins.csv" $destination
& $adb pull "$remote/${participantPrefix}Frames.csv" $destination
& $adb pull "$remote/${participantPrefix}TurnSpeed.csv" $destination
```

The copy leaves the headset files in place. Older experiments may not have a
`TurnSpeed.csv`. The CSV-only commands exclude tilt diagnostic logs and `.bak`
files. To also export backups and other app files, use
`& $adb pull $remote (Join-Path $destination 'HeadsetFiles')`.

The package name comes from **Project Settings > Player > Android > Other
Settings > Identification > Package Name**. If the installed APK uses a different
package name, update `$package` and `$remote` before running the commands.
The file location follows Unity's
[Android persistent-data path](https://docs.unity.cn/6000.3/Documentation/ScriptReference/Application-persistentDataPath.html);
file copying uses [ADB pull](https://developer.android.com/tools/adb#copyfiles).


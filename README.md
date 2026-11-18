# Lenovo Fan Boost

A small Windows app for controlling the fans on the **Lenovo Legion 5 15IAH7H (type 82RB)**. It shows both fan speeds and lets you choose **Full Speed** or **Automatic**.

## Run it

1. Download [LenovoFanBoost.exe](https://github.com/xParadox0/legion-fan-boost/releases/latest/download/LenovoFanBoost.exe) from GitHub Releases.
2. Press **Fn + Q** to select **Legion Balance mode**. The power-button light is white in this mode.
3. Open the EXE and approve the administrator prompt. The app needs this access to talk to Lenovo's built-in fan interface.
4. Select **Full Speed** to run both fans at full speed. Select **Automatic** to restore Lenovo's automatic fan control.

Full Speed can remain active after you close the app. Select **Automatic** when you're done. If the laptop still overheats or airflow is weak at full speed, shut it down and have the cooling system inspected.

## Compatibility

Tested on Windows 11 x64, Lenovo Legion 5 15IAH7H (82RB), BIOS J2CN57WW. The app checks the model before controlling the fans and requires Balance mode to enable Full Speed. It uses the Lenovo firmware interface already installed on the laptop.

The release EXE is unsigned. Windows SmartScreen may warn about it. You can review the source and build it yourself.

## Build from source

Run in PowerShell from the repository folder:

~~~powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\build.ps1
~~~

The build uses the x64 .NET Framework C# compiler included with Windows. The output is **dist\LenovoFanBoost.exe**.

[Lenovo operating modes](https://download.lenovo.com/pccbbs/pubs/legion_5_15_7/html_en/EN/performance_mode.html) · [Lenovo power-light indicators](https://download.lenovo.com/pccbbs/pubs/legion_5_15_7/html_en/EN/view_base_Legion5.html)

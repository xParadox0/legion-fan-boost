using System;
using System.Drawing;
using System.Management;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Lenovo Fan Boost")]
[assembly: AssemblyProduct("Lenovo Fan Boost")]
[assembly: AssemblyDescription("Full-speed fan controls for the Legion 5 15IAH7H")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        bool firstInstance;
        using (var instance = new Mutex(true, @"Local\LenovoFanBoostWindow", out firstInstance))
        {
            if (!firstInstance)
            {
                MessageBox.Show("Lenovo Fan Boost is already open.", "Lenovo Fan Boost");
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FanWindow());
        }
    }
}

internal sealed class FanWindow : Form
{
    private readonly Label status;
    private readonly Label speeds;
    private readonly Button fullSpeed;
    private readonly Button automatic;
    private readonly System.Windows.Forms.Timer refreshTimer;

    internal FanWindow()
    {
        Text = "Lenovo Fan Boost";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(438, 238);
        Font = new Font("Segoe UI", 10F);

        var title = new Label
        {
            Text = "Lenovo Fan Boost",
            Font = new Font(Font.FontFamily, 19F, FontStyle.Bold),
            Location = new Point(24, 20),
            AutoSize = true
        };

        status = new Label { Text = "Reading fan control...", Location = new Point(25, 64), Size = new Size(388, 44) };
        speeds = new Label { Location = new Point(25, 113), Size = new Size(388, 23) };
        fullSpeed = new Button { Text = "&Full Speed", Location = new Point(25, 151), Size = new Size(182, 38), Enabled = false };
        automatic = new Button { Text = "&Automatic", Location = new Point(220, 151), Size = new Size(193, 38), Enabled = false };

        var note = new Label
        {
            Text = "Full Speed stays on when you close this window.",
            Location = new Point(25, 204),
            AutoSize = true
        };

        fullSpeed.Click += (sender, args) => ChangeMode(true);
        automatic.Click += (sender, args) => ChangeMode(false);
        Controls.AddRange(new Control[] { title, status, speeds, fullSpeed, automatic, note });

        refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        refreshTimer.Tick += (sender, args) => RefreshState();
        Shown += (sender, args) =>
        {
            RefreshState();
            if (FanController.IsSupportedModel()) refreshTimer.Start();
        };
        FormClosed += (sender, args) => refreshTimer.Dispose();
    }

    private void RefreshState()
    {
        fullSpeed.Enabled = false;
        automatic.Enabled = false;
        speeds.Text = string.Empty;

        try
        {
            using (var fans = FanController.OpenFans())
            {
                bool boosted = FanController.ReadFullSpeed(fans);
                bool balanced = boosted || FanController.IsBalanceMode();
                status.Text = boosted ? "Full-speed fans are active." : "Lenovo automatic fan control is active.";
                if (!balanced) status.Text += " Press Fn+Q to select Balance.";

                speeds.Text = string.Format("Fan 1: {0}   |   Fan 2: {1}",
                    FanController.ReadSpeed(fans, 0), FanController.ReadSpeed(fans, 1));

                fullSpeed.Enabled = !boosted && balanced;
                automatic.Enabled = boosted;
            }
        }
        catch (Exception error)
        {
            status.Text = error.Message;
        }
    }

    private void ChangeMode(bool enabled)
    {
        refreshTimer.Stop();
        try
        {
            FanController.SetFullSpeed(enabled);
        }
        catch (Exception error)
        {
            MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            RefreshState();
            refreshTimer.Start();
        }
    }
}

internal static class FanController
{
    public static bool IsSupportedModel()
    {
        using (var bios = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
            return bios != null && string.Equals(bios.GetValue("SystemProductName") as string,
                "82RB", StringComparison.OrdinalIgnoreCase);
    }

    public static ManagementObject OpenFans()
    {
        if (!IsSupportedModel())
            throw new InvalidOperationException("Supported laptop: Lenovo Legion 5 15IAH7H (type 82RB).");

        return OpenController("LENOVO_FAN_METHOD");
    }

    public static bool IsBalanceMode()
    {
        using (var thermal = OpenController("LENOVO_GAMEZONE_DATA"))
        using (var result = thermal.InvokeMethod("GetSmartFanMode", null, null))
            return result != null && Convert.ToUInt32(result["Data"]) == 2U;
    }

    public static bool ReadFullSpeed(ManagementObject fans)
    {
        using (var result = fans.InvokeMethod("Fan_Get_FullSpeed", null, null))
        {
            if (result == null || !(result["Status"] is bool))
                throw new InvalidOperationException("Lenovo firmware did not report the fan mode.");

            return (bool)result["Status"];
        }
    }

    public static string ReadSpeed(ManagementObject fans, byte fanId)
    {
        try
        {
            using (var input = fans.GetMethodParameters("Fan_GetCurrentFanSpeed"))
            {
                input["FanID"] = fanId;
                using (var result = fans.InvokeMethod("Fan_GetCurrentFanSpeed", input, null))
                {
                    if (result == null || result["CurrentFanSpeed"] == null) return "unavailable";
                    return string.Format("{0:N0} RPM", result["CurrentFanSpeed"]);
                }
            }
        }
        catch (ManagementException) { return "unavailable"; }
        catch (System.Runtime.InteropServices.COMException) { return "unavailable"; }
    }

    public static void SetFullSpeed(bool enabled)
    {
        using (var mutex = new Mutex(false, @"Local\LenovoFanBoostToggle"))
        {
            bool locked;
            try { locked = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { locked = true; }
            if (!locked) throw new InvalidOperationException("Another fan change is in progress.");

            try
            {
                using (var fans = OpenFans())
                {
                    bool previous = ReadFullSpeed(fans);
                    if (previous == enabled) return;
                    if (enabled && !IsBalanceMode())
                        throw new InvalidOperationException("Switch to Legion Balance mode with Fn+Q first.");

                    try
                    {
                        WriteFullSpeed(fans, enabled);
                        Thread.Sleep(500);
                        if (ReadFullSpeed(fans) != enabled)
                            throw new InvalidOperationException("Lenovo firmware did not confirm the requested fan mode.");
                    }
                    catch
                    {
                        try { WriteFullSpeed(fans, previous); }
                        catch (Exception error)
                        {
                            throw new InvalidOperationException("Could not confirm or restore the fan mode. Check the current state.", error);
                        }
                        throw;
                    }
                }
            }
            finally { mutex.ReleaseMutex(); }
        }
    }

    private static void WriteFullSpeed(ManagementObject fans, bool enabled)
    {
        using (var input = fans.GetMethodParameters("Fan_Set_FullSpeed"))
        {
            input["Status"] = enabled;
            using (fans.InvokeMethod("Fan_Set_FullSpeed", input, null)) { }
        }
    }

    private static ManagementObject OpenController(string name)
    {
        using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM " + name))
        using (var objects = searcher.Get())
            foreach (ManagementObject controller in objects) return controller;

        throw new InvalidOperationException("The Lenovo fan interface is unavailable.");
    }
}

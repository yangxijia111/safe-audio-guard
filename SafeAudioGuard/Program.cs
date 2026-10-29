using System.Threading;
using SafeAudioGuard.UI;

namespace SafeAudioGuard;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true,
            name: @"Local\SafeAudioGuard_9A96A7A3_B9B8_42C2_A6AF_B4B233F1C18C",
            createdNew: out bool firstInstance);

        if (!firstInstance)
        {
            MessageBox.Show("音量保护工具已经在运行，请查看任务栏托盘。", "SafeAudioGuard",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        try
        {
            Application.Run(new TrayApplicationContext());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"程序启动失败：{ex.Message}", "SafeAudioGuard",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

namespace SafeAudioGuard.Audio;

public static class ProtectionPolicy
{
    public const int MaximumPercent = 45;
    public static float Threshold => MaximumPercent / 100f;

    // 浮点数容差，避免设备把 45% 回读为 45.00000...% 时重复写音量。
    public static bool ShouldClamp(float current) =>
        float.IsFinite(current) && current > Threshold + 0.0001f;
}

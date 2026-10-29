using SafeAudioGuard.Audio;

var cases = new (float Input, bool Clamp)[]
{
    (0f, false),
    (0.30f, false),
    (0.45f, false),
    (0.46f, true),
    (0.90f, true),
    (1.00f, true),
    (float.NaN, false)
};

foreach (var (input, expected) in cases)
{
    var actual = ProtectionPolicy.ShouldClamp(input);
    if (actual != expected)
        throw new Exception($"测试失败：输入={input}, 预期={expected}, 实际={actual}");
}

if (ProtectionPolicy.MaximumPercent != 45 || Math.Abs(ProtectionPolicy.Threshold - 0.45f) > 0.00001f)
    throw new Exception("保护上限不等于 45%");

Console.WriteLine($"PASS：{cases.Length + 1} 项音量策略自检通过（不涉及真实硬件）。");

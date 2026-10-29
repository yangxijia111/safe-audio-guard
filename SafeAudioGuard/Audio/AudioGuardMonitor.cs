using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using SafeAudioGuard.Infrastructure;

namespace SafeAudioGuard.Audio;

internal sealed class AudioGuardMonitor : IDisposable
{
    // 加快端点激活后的处理；同时覆盖某些驱动“先激活、稍后恢复旧音量”的行为。
    // 只在事件/手动检查后的 1.8s 窗口内工作，不持续监听音量改动。
    private static readonly int[] RetryDelaysMs = [0, 60, 180, 450, 900, 1800];

    private readonly AppLog _log;
    private readonly Action<string> _reportAdjustment;
    private readonly Channel<(string Id, string Reason)> _events = Channel.CreateUnbounded<(string, string)>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly Channel<CheckJob> _queue = Channel.CreateUnbounded<CheckJob>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly ConcurrentDictionary<string, long> _generations = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stop = new();
    private readonly IMMDeviceEnumerator _uiEnumerator;
    private readonly EndpointNotificationSink _notificationSink;
    private readonly Task _worker;
    private readonly Task _eventWorker;
    private bool _registered;
    private volatile bool _paused;
    private int _disposed;

    private readonly record struct CheckJob(string DeviceId, long Generation, int Attempt, string Reason);

    internal bool Paused => _paused;

    internal AudioGuardMonitor(AppLog log, Action<string> reportAdjustment)
    {
        _log = log;
        _reportAdjustment = reportAdjustment;
        _notificationSink = new EndpointNotificationSink((id, reason) => QueueConnection(id, reason));
        _uiEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        _worker = Task.Run(ProcessQueueAsync);
        _eventWorker = Task.Run(ProcessEventsAsync);

        try
        {
            // 先订阅，再枚举；降低“枚举期间新接入设备却漏掉事件”的概率。
            _uiEnumerator.RegisterEndpointNotificationCallback(_notificationSink);
            _registered = true;
            _log.Write("已注册音频设备事件；输出设备保护上限 45%。");
            CheckAllActiveOutputs("启动时安全检查");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal void SetPaused(bool value)
    {
        _paused = value;
        _log.Write(value ? "用户暂停自动保护。" : "用户恢复自动保护。");
        if (!value) CheckAllActiveOutputs("恢复时安全检查");
    }

    internal void CheckAllActiveOutputs(string reason)
    {
        if (_paused || Volatile.Read(ref _disposed) != 0) return;
        IMMDeviceCollection? collection = null;
        try
        {
            _uiEnumerator.EnumAudioEndpoints(DataFlow.Render,
                CoreAudioConstants.DeviceStateActive, out collection);
            collection.GetCount(out uint count);
            for (uint index = 0; index < count; index++)
            {
                IMMDevice? device = null;
                try
                {
                    collection.Item(index, out device);
                    device.GetId(out string id);
                    QueueConnection(id, reason);
                }
                catch (COMException ex)
                {
                    _log.Write($"枚举输出设备 #{index} 失败：0x{ex.HResult:X8}");
                }
                finally { ReleaseCom(device); }
            }
        }
        catch (COMException ex)
        {
            _log.Write($"枚举输出设备失败：0x{ex.HResult:X8}");
        }
        finally { ReleaseCom(collection); }
    }

    // 回调路径只做非阻塞的队列写入，不做锁等待/COM/计时器操作。
    private void QueueConnection(string id, string reason)
    {
        if (_paused || Volatile.Read(ref _disposed) != 0 || string.IsNullOrWhiteSpace(id)) return;
        _events.Writer.TryWrite((id, reason));
    }

    private async Task ProcessEventsAsync()
    {
        try
        {
            await foreach (var item in _events.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false))
            {
                if (_paused || Volatile.Read(ref _disposed) != 0) continue;
                long generation = _generations.AddOrUpdate(item.Id, 1, (_, previous) => previous + 1);
                for (int attempt = 0; attempt < RetryDelaysMs.Length; attempt++)
                {
                    int delay = RetryDelaysMs[attempt];
                    var job = new CheckJob(item.Id, generation, attempt, item.Reason);
                    if (delay == 0) _queue.Writer.TryWrite(job);
                    else _ = EnqueueAfterDelayAsync(job, delay);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log.Write("设备事件队列异常：" + ex); }
    }

    private async Task EnqueueAfterDelayAsync(CheckJob job, int delay)
    {
        try
        {
            await Task.Delay(delay, _stop.Token).ConfigureAwait(false);
            if (!_paused && Volatile.Read(ref _disposed) == 0)
                _queue.Writer.TryWrite(job);
        }
        catch (OperationCanceledException) { }
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var job in _queue.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false))
            {
                if (_paused || Volatile.Read(ref _disposed) != 0) continue;
                if (!_generations.TryGetValue(job.DeviceId, out long latest) || latest != job.Generation)
                    continue;

                // 每次调用创建并释放自身 COM 对象，不跨线程共享 COM RCW。
                TryClampOneOutput(job);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log.Write("设备检查工作线程异常：" + ex); }
    }

    private void TryClampOneOutput(CheckJob job)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioEndpointVolume? volume = null;
        string shortId = AppLog.Fingerprint(job.DeviceId);
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            enumerator.GetDevice(job.DeviceId, out device);
            device.GetState(out uint state);
            if ((state & CoreAudioConstants.DeviceStateActive) == 0) return;

            // 监听是全端点通知，必须明确检查它是“输出”而非麦克风输入。
            if (device is not IMMEndpoint endpoint) return;
            endpoint.GetDataFlow(out DataFlow flow);
            if (flow != DataFlow.Render) return;

            var iid = CoreAudioConstants.EndpointVolumeIid;
            device.Activate(ref iid, CoreAudioConstants.ClsCtxAll, IntPtr.Zero, out volume);
            volume.GetMasterVolumeLevelScalar(out float before);
            if (!ProtectionPolicy.ShouldClamp(before)) return;

            if (_paused || Volatile.Read(ref _disposed) != 0) return;
            volume.SetMasterVolumeLevelScalar(ProtectionPolicy.Threshold, IntPtr.Zero);
            volume.GetMasterVolumeLevelScalar(out float after);
            if (after > ProtectionPolicy.Threshold + 0.001f)
            {
                if (job.Attempt == RetryDelaysMs.Length - 1)
                    _log.Write($"[{shortId}] 驱动回读音量 {after:P1}，未能确认 <=45%，请检查设备硬件音量。");
                return;
            }

            var report = $"[{shortId}] {job.Reason}: {before:P0} -> {after:P0}";
            _log.Write("已限制 " + report);
            _reportAdjustment(report);
        }
        catch (COMException ex)
        {
            // 未激活/刚移除时暂时无法获取设备属于正常情况，直到最后一次才记错。
            if (job.Attempt == RetryDelaysMs.Length - 1)
                _log.Write($"[{shortId}] {job.Reason} 检查未完成：0x{ex.HResult:X8}");
        }
        catch (Exception ex)
        {
            if (job.Attempt == RetryDelaysMs.Length - 1)
                _log.Write($"[{shortId}] 检查异常：{ex.Message}");
        }
        finally
        {
            ReleaseCom(volume);
            ReleaseCom(device);
            ReleaseCom(enumerator);
        }
    }

    private static void ReleaseCom(object? obj)
    {
        if (obj is null) return;
        try { if (Marshal.IsComObject(obj)) Marshal.ReleaseComObject(obj); }
        catch (InvalidComObjectException) { }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try
        {
            if (_registered)
            {
                _uiEnumerator.UnregisterEndpointNotificationCallback(_notificationSink);
                _registered = false;
            }
        }
        catch (COMException ex) { _log.Write($"停止监听失败：0x{ex.HResult:X8}"); }
        finally
        {
            _stop.Cancel();
            _events.Writer.TryComplete();
            _queue.Writer.TryComplete();
            ReleaseCom(_uiEnumerator);
            _log.Write("音频设备事件监听已结束。");
        }
    }
}

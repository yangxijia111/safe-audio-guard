using System.Runtime.InteropServices;

namespace SafeAudioGuard.Audio;

// Windows Core Audio 原生 COM 互操作，独立于 NAudio/NuGet。
// 顺序须严格匹配 mmdeviceapi.h / endpointvolume.h 的 IUnknown vtable。
internal enum DataFlow { Render = 0, Capture = 1, All = 2 }
internal enum DeviceRole { Console = 0, Multimedia = 1, Communications = 2 }
internal static class CoreAudioConstants
{
    internal const uint DeviceStateActive = 0x00000001;
    internal const uint ClsCtxAll = 0x17;
    internal static readonly Guid EndpointVolumeIid =
        new("5CDF2C82-841E-4546-9722-0CF74078229A");
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject { }

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    void EnumAudioEndpoints(DataFlow flow, uint stateMask, out IMMDeviceCollection devices);
    void GetDefaultAudioEndpoint(DataFlow flow, DeviceRole role, out IMMDevice device);
    void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    void RegisterEndpointNotificationCallback(IMMNotificationClient client);
    void UnregisterEndpointNotificationCallback(IMMNotificationClient client);
}

[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    void GetCount(out uint count);
    void Item(uint index, out IMMDevice device);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    void Activate([In] ref Guid iid, uint clsCtx, IntPtr activationParams,
        [MarshalAs(UnmanagedType.Interface)] out IAudioEndpointVolume volume);
    void OpenPropertyStore(uint access, out IntPtr propertyStore);
    void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetState(out uint state);
}

[ComImport]
[Guid("1BE09788-6894-4089-8586-9A2A6C265AC5")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMEndpoint
{
    void GetDataFlow(out DataFlow flow);
}

// 仅声明用到的前七个方法，顺序与原生 COM 规范完全一致。
[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    void RegisterControlChangeNotify(IntPtr client);
    void UnregisterControlChangeNotify(IntPtr client);
    void GetChannelCount(out uint count);
    void SetMasterVolumeLevel(float decibels, IntPtr eventContext);
    void SetMasterVolumeLevelScalar(float level, IntPtr eventContext);
    void GetMasterVolumeLevel(out float decibels);
    void GetMasterVolumeLevelScalar(out float level);
}

[ComVisible(true)]
[Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMNotificationClient
{
    void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint newState);
    void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
    void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
    void OnDefaultDeviceChanged(DataFlow flow, DeviceRole role,
        [MarshalAs(UnmanagedType.LPWStr)] string? defaultDeviceId);
    void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id,
        ref PropertyKey key);
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    internal Guid FormatId;
    internal uint PropertyId;
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class EndpointNotificationSink(Action<string, string> enqueue) : IMMNotificationClient
{
    // 系统回调必须立即返回，不在这里做 COM 查询、写文件、锁等待或设备音量修改。
    public void OnDeviceStateChanged(string id, uint state)
    {
        if (state == CoreAudioConstants.DeviceStateActive)
            enqueue(id, "设备激活");
    }

    public void OnDeviceAdded(string id) => enqueue(id, "设备新增");
    public void OnDefaultDeviceChanged(DataFlow flow, DeviceRole role, string? id)
    {
        if (flow == DataFlow.Render && !string.IsNullOrEmpty(id))
            enqueue(id, $"默认输出切换({role})");
    }

    public void OnDeviceRemoved(string id) { }
    public void OnPropertyValueChanged(string id, ref PropertyKey key) { }
}

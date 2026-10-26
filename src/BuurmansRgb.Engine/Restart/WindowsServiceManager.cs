using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BuurmansRgb.Engine.Restart;

// The service control manager of Windows through advapi32, as System.ServiceProcess.ServiceController
// is a NuGet package of its own and not part of .NET. Stopping, starting and waiting for it go
// through ServiceControl.
internal sealed partial class WindowsServiceManager : IServiceManager
{
    public bool Exists(string name)
    {
        using var manager = OpenManager();
        using var service = OpenService(manager, name, ServiceQueryStatus);
        return !service.IsInvalid;
    }

    public unsafe string? FindDriver(string text)
    {
        using var manager = OpenManager();
        uint resume = 0;
        var size = 0u;
        var buffer = Array.Empty<byte>();
        while (true)
        {
            fixed (byte* bytes = buffer)
            {
                if (EnumServicesStatusExW(manager, ScEnumProcessInfo, ServiceDriver, ServiceStateAll, bytes, size, out var needed, out var count, ref resume, null))
                {
                    return Match(bytes, count, text);
                }

                var error = Marshal.GetLastPInvokeError();
                if (error != ErrorMoreData)
                {
                    throw new Win32Exception(error);
                }

                // A part of the list fits; look in it before reading on from where it stopped.
                var found = Match(bytes, count, text);
                if (found is not null)
                {
                    return found;
                }

                size = Math.Max(size, needed);
                buffer = new byte[size];
            }
        }
    }

    public void Stop(string name, TimeSpan timeout)
    {
        using var manager = OpenManager();
        using var service = OpenExisting(manager, name, ServiceQueryStatus | ServiceStop);
        ServiceControl.System.Stop(new OpenedService(service, name), name, timeout);
    }

    public void Start(string name, TimeSpan timeout)
    {
        using var manager = OpenManager();
        using var service = OpenExisting(manager, name, ServiceQueryStatus | ServiceStart);
        ServiceControl.System.Start(new OpenedService(service, name), name, timeout);
    }

    private static unsafe string? Match(byte* bytes, uint count, string text)
    {
        var entries = (EnumServiceStatusProcess*)bytes;
        for (var i = 0; i < count; i++)
        {
            var name = new string(entries[i].ServiceName);
            var displayName = entries[i].DisplayName is null ? string.Empty : new string(entries[i].DisplayName);
            if (name.Contains(text, StringComparison.OrdinalIgnoreCase) || displayName.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }

    private static ServiceHandle OpenManager()
    {
        var manager = OpenSCManagerW(null, null, ScManagerConnect | ScManagerEnumerateService);
        if (manager.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            manager.Dispose();
            throw new Win32Exception(error);
        }

        return manager;
    }

    // An invalid handle when there is no such service; throws for another failure, such as no access.
    private static ServiceHandle OpenService(ServiceHandle manager, string name, uint access)
    {
        var service = OpenServiceW(manager, name, access);
        if (service.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            if (error != ErrorServiceDoesNotExist)
            {
                service.Dispose();
                throw new Win32Exception(error);
            }
        }

        return service;
    }

    private static ServiceHandle OpenExisting(ServiceHandle manager, string name, uint access)
    {
        var service = OpenService(manager, name, access);
        if (service.IsInvalid)
        {
            service.Dispose();
            throw new ServiceControlException(name, ServiceControlFailure.Other, "Not installed.");
        }

        return service;
    }

    private const uint ScManagerConnect = 0x0001;
    private const uint ScManagerEnumerateService = 0x0004;
    private const uint ServiceQueryStatus = 0x0004;
    private const uint ServiceStart = 0x0010;
    private const uint ServiceStop = 0x0020;
    private const uint ServiceControlStop = 0x00000001;
    private const uint ServiceDriver = 0x0000000B;
    private const uint ServiceStateAll = 0x00000003;
    private const int ScEnumProcessInfo = 0;

    private const int ErrorMoreData = 234;
    private const int ErrorServiceDoesNotExist = 1060;

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct EnumServiceStatusProcess
    {
        public char* ServiceName;
        public char* DisplayName;
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
        public uint ProcessId;
        public uint ServiceFlags;
    }

    private sealed class OpenedService(ServiceHandle service, string name) : IServiceHandle
    {
        public ServiceState QueryState() =>
            QueryServiceStatus(service, out var status) ? (ServiceState)status.CurrentState : throw ServiceControl.Failure(name, Marshal.GetLastPInvokeError());

        public int SendStop() => ControlService(service, ServiceControlStop, out _) ? 0 : Marshal.GetLastPInvokeError();

        public int SendStart() => StartServiceW(service, 0, 0) ? 0 : Marshal.GetLastPInvokeError();
    }

    private sealed class ServiceHandle() : SafeHandle(0, ownsHandle: true)
    {
        public override bool IsInvalid => handle == 0;

        protected override bool ReleaseHandle() => CloseServiceHandle(handle);
    }

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial ServiceHandle OpenSCManagerW(string? machineName, string? databaseName, uint desiredAccess);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial ServiceHandle OpenServiceW(ServiceHandle manager, string serviceName, uint desiredAccess);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseServiceHandle(nint handle);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryServiceStatus(ServiceHandle service, out ServiceStatus status);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ControlService(ServiceHandle service, uint control, out ServiceStatus status);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool StartServiceW(ServiceHandle service, uint argumentCount, nint arguments);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool EnumServicesStatusExW(
        ServiceHandle manager,
        int infoLevel,
        uint serviceType,
        uint serviceState,
        byte* services,
        uint bufferSize,
        out uint bytesNeeded,
        out uint servicesReturned,
        ref uint resumeHandle,
        string? groupName);
}

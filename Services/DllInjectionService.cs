using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DreamLauncher.Services;

public sealed class DllInjectionService
{
    private const uint WaitTimeout = 0x00000102;
    private const uint WaitFailed = 0xFFFFFFFF;
    private const uint InjectionTimeoutMs = 15000;

    public void Inject(Process process, string dllPath)
    {
        if (process.HasExited)
        {
            throw new InvalidOperationException("Game process exited before DLL injection.");
        }

        var fullDllPath = Path.GetFullPath(dllPath);

        if (!File.Exists(fullDllPath))
        {
            throw new FileNotFoundException("DLL file was not found.", fullDllPath);
        }

        var dllPathBytes = Encoding.Unicode.GetBytes(fullDllPath + '\0');
        var processHandle = OpenProcess(
            ProcessAccess.CreateThread |
            ProcessAccess.QueryInformation |
            ProcessAccess.VirtualMemoryOperation |
            ProcessAccess.VirtualMemoryWrite |
            ProcessAccess.VirtualMemoryRead,
            false,
            process.Id);

        if (processHandle == IntPtr.Zero)
        {
            ThrowWin32("OpenProcess failed.");
        }

        try
        {
            var remotePathAddress = VirtualAllocEx(
                processHandle,
                IntPtr.Zero,
                new UIntPtr((uint)dllPathBytes.Length),
                AllocationType.Commit | AllocationType.Reserve,
                MemoryProtection.ReadWrite);

            if (remotePathAddress == IntPtr.Zero)
            {
                ThrowWin32("VirtualAllocEx failed.");
            }

            try
            {
                if (!WriteProcessMemory(
                    processHandle,
                    remotePathAddress,
                    dllPathBytes,
                    new UIntPtr((uint)dllPathBytes.Length),
                    out var bytesWritten) ||
                    bytesWritten.ToUInt64() != (ulong)dllPathBytes.Length)
                {
                    ThrowWin32("WriteProcessMemory failed.");
                }

                var kernel32 = GetModuleHandle("kernel32.dll");
                if (kernel32 == IntPtr.Zero)
                {
                    ThrowWin32("GetModuleHandle(kernel32.dll) failed.");
                }

                var loadLibrary = GetProcAddress(kernel32, "LoadLibraryW");
                if (loadLibrary == IntPtr.Zero)
                {
                    ThrowWin32("GetProcAddress(LoadLibraryW) failed.");
                }

                var threadHandle = CreateRemoteThread(
                    processHandle,
                    IntPtr.Zero,
                    0,
                    loadLibrary,
                    remotePathAddress,
                    0,
                    out _);

                if (threadHandle == IntPtr.Zero)
                {
                    ThrowWin32("CreateRemoteThread failed.");
                }

                try
                {
                    var waitResult = WaitForSingleObject(threadHandle, InjectionTimeoutMs);

                    if (waitResult == WaitTimeout)
                    {
                        throw new TimeoutException("DLL injection timed out.");
                    }

                    if (waitResult == WaitFailed)
                    {
                        ThrowWin32("WaitForSingleObject failed.");
                    }

                    if (!GetExitCodeThread(threadHandle, out var exitCode))
                    {
                        ThrowWin32("GetExitCodeThread failed.");
                    }

                    if (exitCode == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("LoadLibraryW returned a null module handle.");
                    }
                }
                finally
                {
                    CloseHandle(threadHandle);
                }
            }
            finally
            {
                VirtualFreeEx(processHandle, remotePathAddress, UIntPtr.Zero, FreeType.Release);
            }
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    private static void ThrowWin32(string message)
    {
        throw new Win32Exception(Marshal.GetLastWin32Error(), message);
    }

    [Flags]
    private enum ProcessAccess : uint
    {
        CreateThread = 0x0002,
        QueryInformation = 0x0400,
        VirtualMemoryOperation = 0x0008,
        VirtualMemoryRead = 0x0010,
        VirtualMemoryWrite = 0x0020
    }

    [Flags]
    private enum AllocationType : uint
    {
        Commit = 0x1000,
        Reserve = 0x2000
    }

    private enum MemoryProtection : uint
    {
        ReadWrite = 0x04
    }

    private enum FreeType : uint
    {
        Release = 0x8000
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(ProcessAccess desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(
        IntPtr processHandle,
        IntPtr address,
        UIntPtr size,
        AllocationType allocationType,
        MemoryProtection protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(
        IntPtr processHandle,
        IntPtr address,
        UIntPtr size,
        FreeType freeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(
        IntPtr processHandle,
        IntPtr baseAddress,
        byte[] buffer,
        UIntPtr size,
        out UIntPtr bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string moduleName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string procName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateRemoteThread(
        IntPtr processHandle,
        IntPtr threadAttributes,
        uint stackSize,
        IntPtr startAddress,
        IntPtr parameter,
        uint creationFlags,
        out uint threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeThread(IntPtr threadHandle, out IntPtr exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}

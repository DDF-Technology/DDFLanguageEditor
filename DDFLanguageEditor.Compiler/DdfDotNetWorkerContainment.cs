using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetWorkerResourceLimits
    {
        public DdfDotNetWorkerResourceLimits(TimeSpan timeout,
            long maxProcessMemoryBytes = 1024L * 1024 * 1024,
            TimeSpan? maxProcessCpuTime = null,
            bool requireProtectedWorkingDirectory = false)
        {
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (maxProcessMemoryBytes < 64L * 1024 * 1024)
                throw new ArgumentOutOfRangeException(
                    nameof(maxProcessMemoryBytes));
            TimeSpan cpu = maxProcessCpuTime ?? timeout;
            if (cpu <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxProcessCpuTime));
            Timeout = timeout;
            MaxProcessMemoryBytes = maxProcessMemoryBytes;
            MaxProcessCpuTime = cpu;
            RequireProtectedWorkingDirectory =
                requireProtectedWorkingDirectory;
        }

        public TimeSpan Timeout { get; }
        public long MaxProcessMemoryBytes { get; }
        public TimeSpan MaxProcessCpuTime { get; }
        public bool RequireProtectedWorkingDirectory { get; }
    }

    internal sealed class DdfWorkerTemporaryDirectory : IDisposable
    {
        private DdfWorkerTemporaryDirectory(string path,
            bool accessControlApplied)
        {
            Path = path;
            AccessControlApplied = accessControlApplied;
        }
        internal string Path { get; }
        internal bool AccessControlApplied { get; }

        internal static DdfWorkerTemporaryDirectory Create()
        {
            string root = System.IO.Path.GetFullPath(
                System.IO.Path.GetTempPath());
            string path = System.IO.Path.Combine(root,
                "ddf-worker-" + Guid.NewGuid().ToString("N"));
            DirectoryInfo directory = Directory.CreateDirectory(path);
            bool accessControlApplied = false;
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        SecurityIdentifier user = WindowsIdentity.GetCurrent()
                            .User ?? throw new InvalidOperationException(
                                "Identità Windows corrente non disponibile.");
                        var security = new DirectorySecurity();
                        security.SetAccessRuleProtection(isProtected: true,
                            preserveInheritance: false);
                        security.AddAccessRule(new FileSystemAccessRule(user,
                            FileSystemRights.FullControl,
                            InheritanceFlags.ContainerInherit |
                            InheritanceFlags.ObjectInherit,
                            PropagationFlags.None, AccessControlType.Allow));
                        directory.SetAccessControl(security);
                        accessControlApplied = true;
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (PlatformNotSupportedException) { }
                }
                return new DdfWorkerTemporaryDirectory(path,
                    accessControlApplied);
            }
            catch
            {
                try { directory.Delete(recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                string full = System.IO.Path.GetFullPath(Path);
                string root = System.IO.Path.GetFullPath(
                    System.IO.Path.GetTempPath());
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                    System.IO.Path.GetFileName(full).StartsWith("ddf-worker-",
                        StringComparison.Ordinal))
                    Directory.Delete(full, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal sealed class DdfWindowsJobObject : IDisposable
    {
        private const uint JobObjectLimitProcessTime = 0x00000002;
        private const uint JobObjectLimitActiveProcess = 0x00000008;
        private const uint JobObjectLimitProcessMemory = 0x00000100;
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;
        private const uint JobObjectLimitDieOnUnhandledException = 0x00000400;
        private IntPtr handle;

        private DdfWindowsJobObject(IntPtr handle) { this.handle = handle; }

        internal static DdfWindowsJobObject Create(
            DdfDotNetWorkerResourceLimits limits)
        {
            if (!OperatingSystem.IsWindows()) return null;
            IntPtr handle = CreateJobObject(IntPtr.Zero, null);
            if (handle == IntPtr.Zero) throw new Win32Exception(
                Marshal.GetLastWin32Error(), "Creazione Job Object fallita.");
            var job = new DdfWindowsJobObject(handle);
            try
            {
                uint activeProcessLimit = CurrentTokenIsRestricted() ? 1u : 2u;
                var information = new JobObjectExtendedLimitInformation
                {
                    BasicLimitInformation = new JobObjectBasicLimitInformation
                    {
                        PerProcessUserTimeLimit = limits.MaxProcessCpuTime.Ticks,
                        ActiveProcessLimit = activeProcessLimit,
                        LimitFlags = JobObjectLimitProcessTime |
                            JobObjectLimitActiveProcess |
                            JobObjectLimitProcessMemory |
                            JobObjectLimitKillOnJobClose |
                            JobObjectLimitDieOnUnhandledException
                    },
                    ProcessMemoryLimit = (UIntPtr)checked((ulong)
                        limits.MaxProcessMemoryBytes)
                };
                int length = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
                IntPtr buffer = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(information, buffer,
                        fDeleteOld: false);
                    if (!SetInformationJobObject(handle, 9, buffer,
                            (uint)length))
                        throw new Win32Exception(Marshal.GetLastWin32Error(),
                            "Configurazione Job Object fallita.");
                }
                finally { Marshal.FreeHGlobal(buffer); }
                return job;
            }
            catch
            {
                job.Dispose();
                throw;
            }
        }

        private static bool CurrentTokenIsRestricted()
        {
            if (!OpenProcessToken(GetCurrentProcess(), 0x0008,
                    out IntPtr token))
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Lettura del token per il limite processi fallita.");
            try { return IsTokenRestricted(token); }
            finally { CloseHandle(token); }
        }

        internal void Assign(Process process)
        {
            if (handle == IntPtr.Zero) throw new ObjectDisposedException(
                nameof(DdfWindowsJobObject));
            if (!AssignProcessToJobObject(handle, process.Handle))
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Assegnazione del worker al Job Object fallita.");
        }

        public void Dispose()
        {
            IntPtr value = handle;
            handle = IntPtr.Zero;
            if (value != IntPtr.Zero) CloseHandle(value);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
        {
            internal long PerProcessUserTimeLimit;
            internal long PerJobUserTimeLimit;
            internal uint LimitFlags;
            internal UIntPtr MinimumWorkingSetSize;
            internal UIntPtr MaximumWorkingSetSize;
            internal uint ActiveProcessLimit;
            internal UIntPtr Affinity;
            internal uint PriorityClass;
            internal uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            internal ulong ReadOperationCount;
            internal ulong WriteOperationCount;
            internal ulong OtherOperationCount;
            internal ulong ReadTransferCount;
            internal ulong WriteTransferCount;
            internal ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformation
        {
            internal JobObjectBasicLimitInformation BasicLimitInformation;
            internal IoCounters IoInfo;
            internal UIntPtr ProcessMemoryLimit;
            internal UIntPtr JobMemoryLimit;
            internal UIntPtr PeakProcessMemoryUsed;
            internal UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr attributes,
            string name);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr process,
            uint desiredAccess, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsTokenRestricted(IntPtr token);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(IntPtr job,
            int informationClass, IntPtr information, uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr job,
            IntPtr process);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}

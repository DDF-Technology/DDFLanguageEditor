using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;

namespace DDFLanguageEditor.Runner
{
    [SupportedOSPlatform("windows")]
    internal static class DdfWindowsRestrictedLauncher
    {
        internal const string ChildEnvironmentName =
            "DDF_DOTNET_RESTRICTED_CHILD";
        private const uint TokenAssignPrimary = 0x0001;
        private const uint TokenDuplicate = 0x0002;
        private const uint TokenQuery = 0x0008;
        private const uint DisableMaxPrivilege = 0x00000001;
        private const uint LuaToken = 0x00000004;
        private const uint CreateNoWindow = 0x08000000;
        private const uint StartfUseStdHandles = 0x00000100;
        private const uint Infinite = 0xffffffff;
        private const uint SeGroupUseForDenyOnly = 0x00000010;

        internal static bool IsChild => string.Equals(
            Environment.GetEnvironmentVariable(ChildEnvironmentName), "1",
            StringComparison.Ordinal);

        internal static bool IsCurrentTokenRestricted()
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery,
                    out IntPtr token))
                ThrowLastError("Apertura del token worker fallita.");
            try
            {
                return IsTokenRestricted(token) ||
                    HasDenyOnlyInteractiveSid(token);
            }
            finally { CloseHandle(token); }
        }

        private static bool HasDenyOnlyInteractiveSid(IntPtr token)
        {
            GetTokenInformation(token, 2, IntPtr.Zero, 0,
                out uint required);
            if (required == 0) return false;
            IntPtr buffer = Marshal.AllocHGlobal(checked((int)required));
            try
            {
                if (!GetTokenInformation(token, 2, buffer, required,
                        out _))
                    ThrowLastError("Lettura dei gruppi del token fallita.");
                uint count = unchecked((uint)Marshal.ReadInt32(buffer));
                int offset = IntPtr.Size;
                int itemSize = Marshal.SizeOf<SidAndAttributes>();
                const string interactiveSid = "S-1-5-4";
                for (uint index = 0; index < count; index++)
                {
                    SidAndAttributes item = Marshal.PtrToStructure<
                        SidAndAttributes>(IntPtr.Add(buffer,
                            offset + checked((int)index) * itemSize));
                    if ((item.Attributes & SeGroupUseForDenyOnly) != 0 &&
                        new SecurityIdentifier(item.Sid).Value ==
                            interactiveSid)
                        return true;
                }
                return false;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        internal static int RunRestrictedChild()
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenAssignPrimary |
                    TokenDuplicate | TokenQuery, out IntPtr sourceToken))
                ThrowLastError("Apertura del token worker fallita.");
            IntPtr restrictedToken = IntPtr.Zero;
            using var disabledSids = RestrictingSidBuffer.Create();
            try
            {
                if (!CreateRestrictedToken(sourceToken,
                        DisableMaxPrivilege | LuaToken, disabledSids.Count,
                        disabledSids.Pointer, 0, IntPtr.Zero, 0,
                        IntPtr.Zero, out restrictedToken))
                    ThrowLastError("Creazione del token ridotto fallita.");
                return StartAndWait(restrictedToken);
            }
            finally
            {
                if (restrictedToken != IntPtr.Zero) CloseHandle(restrictedToken);
                CloseHandle(sourceToken);
            }
        }

        private static int StartAndWait(IntPtr token)
        {
            string executable = Environment.ProcessPath ?? throw new
                InvalidOperationException("Percorso del worker non disponibile.");
            var startup = new StartupInfo
            {
                Size = (uint)Marshal.SizeOf<StartupInfo>(),
                Flags = StartfUseStdHandles,
                StandardInput = GetStdHandle(-10),
                StandardOutput = GetStdHandle(-11),
                StandardError = GetStdHandle(-12)
            };
            var commandLine = new StringBuilder(BuildCommandLine(executable));
            string previous = Environment.GetEnvironmentVariable(
                ChildEnvironmentName);
            Environment.SetEnvironmentVariable(ChildEnvironmentName, "1");
            ProcessInformation process;
            try
            {
                if (!CreateProcessAsUser(token, executable, commandLine,
                        IntPtr.Zero, IntPtr.Zero, true, CreateNoWindow,
                        IntPtr.Zero, Environment.CurrentDirectory, ref startup,
                        out process))
                    ThrowLastError("Avvio del worker ridotto fallito.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ChildEnvironmentName,
                    previous);
            }
            try
            {
                if (WaitForSingleObject(process.Process, Infinite) == Infinite)
                    ThrowLastError("Attesa del worker ridotto fallita.");
                if (!GetExitCodeProcess(process.Process, out uint exitCode))
                    ThrowLastError("Lettura esito del worker ridotto fallita.");
                return unchecked((int)exitCode);
            }
            finally
            {
                CloseHandle(process.Thread);
                CloseHandle(process.Process);
            }
        }

        private static string BuildCommandLine(string executable)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Length != 0 && string.Equals(
                    System.IO.Path.GetFullPath(arguments[0]),
                    System.IO.Path.GetFullPath(executable),
                    StringComparison.OrdinalIgnoreCase))
                arguments = arguments.Skip(1).ToArray();
            return Quote(executable) + (arguments.Length == 0 ? string.Empty :
                " " + string.Join(" ", arguments.Select(Quote)));
        }

        private static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in value ?? string.Empty)
            {
                if (character == '\\') { slashes++; continue; }
                if (character == '"')
                {
                    result.Append('\\', slashes * 2 + 1).Append('"');
                    slashes = 0;
                }
                else
                {
                    result.Append('\\', slashes).Append(character);
                    slashes = 0;
                }
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        private static void ThrowLastError(string message)
        {
            int error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, message + " Win32=" + error + ".");
        }

        private sealed class RestrictingSidBuffer : IDisposable
        {
            private readonly IntPtr[] sids;
            private RestrictingSidBuffer(IntPtr pointer, IntPtr[] sids)
            {
                Pointer = pointer;
                this.sids = sids;
            }
            internal IntPtr Pointer { get; }
            internal uint Count => (uint)sids.Length;

            internal static RestrictingSidBuffer Create()
            {
                var identities = new[]
                {
                    new SecurityIdentifier("S-1-5-4")
                };
                var sidPointers = new IntPtr[identities.Length];
                int itemSize = Marshal.SizeOf<SidAndAttributes>();
                IntPtr items = Marshal.AllocHGlobal(itemSize * identities.Length);
                try
                {
                    for (int index = 0; index < identities.Length; index++)
                    {
                        byte[] bytes = new byte[identities[index].BinaryLength];
                        identities[index].GetBinaryForm(bytes, 0);
                        sidPointers[index] = Marshal.AllocHGlobal(bytes.Length);
                        Marshal.Copy(bytes, 0, sidPointers[index], bytes.Length);
                        Marshal.StructureToPtr(new SidAndAttributes
                        {
                            Sid = sidPointers[index], Attributes = 0
                        }, IntPtr.Add(items, itemSize * index), false);
                    }
                    return new RestrictingSidBuffer(items, sidPointers);
                }
                catch
                {
                    foreach (IntPtr sid in sidPointers)
                        if (sid != IntPtr.Zero) Marshal.FreeHGlobal(sid);
                    Marshal.FreeHGlobal(items);
                    throw;
                }
            }

            public void Dispose()
            {
                foreach (IntPtr sid in sids) Marshal.FreeHGlobal(sid);
                Marshal.FreeHGlobal(Pointer);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SidAndAttributes { internal IntPtr Sid; internal uint Attributes; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            internal uint Size; internal string Reserved; internal string Desktop;
            internal string Title; internal uint X; internal uint Y;
            internal uint XSize; internal uint YSize; internal uint XCountChars;
            internal uint YCountChars; internal uint FillAttribute;
            internal uint Flags; internal ushort ShowWindow;
            internal ushort Reserved2Size; internal IntPtr Reserved2;
            internal IntPtr StandardInput; internal IntPtr StandardOutput;
            internal IntPtr StandardError;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            internal IntPtr Process; internal IntPtr Thread;
            internal uint ProcessId; internal uint ThreadId;
        }

        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GetStdHandle(int value);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsTokenRestricted(IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetTokenInformation(IntPtr token, int informationClass, IntPtr information, uint informationLength, out uint returnLength);
        [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateRestrictedToken(IntPtr token, uint flags, uint disableSidCount, IntPtr sidsToDisable, uint deletePrivilegeCount, IntPtr privilegesToDelete, uint restrictedSidCount, IntPtr restrictedSids, out IntPtr newToken);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateProcessAsUser(IntPtr token, string applicationName, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint creationFlags, IntPtr environment, string currentDirectory, ref StartupInfo startupInfo, out ProcessInformation processInformation);
    }
}

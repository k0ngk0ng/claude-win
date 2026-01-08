using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ClaudeCodeWin.Native
{
    /// <summary>
    /// Windows ConPTY (Pseudo Console) 原生封装
    /// 需要 Windows 10 1809 (Build 17763) 或更高版本
    /// </summary>
    public static class ConPty
    {
        #region Native Constants

        private const uint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
        private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        private const int STARTF_USESTDHANDLES = 0x00000100;

        #endregion

        #region Native Structures

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD
        {
            public short X;
            public short Y;

            public COORD(short x, short y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        #endregion

        #region Native Methods

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int CreatePseudoConsole(
            COORD size,
            IntPtr hInput,
            IntPtr hOutput,
            uint dwFlags,
            out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreatePipe(
            out IntPtr hReadPipe,
            out IntPtr hWritePipe,
            ref SECURITY_ATTRIBUTES lpPipeAttributes,
            uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList,
            int dwAttributeCount,
            int dwFlags,
            ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            uint dwFlags,
            IntPtr Attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcess(
            string? lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateEnvironmentBlock(
            out IntPtr lpEnvironment,
            IntPtr hToken,
            bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        #endregion

        #region Public API

        /// <summary>
        /// 创建伪终端会话
        /// </summary>
        public static PseudoConsoleSession Create(
            string command,
            string? workingDirectory,
            IDictionary<string, string>? environment,
            short cols = 120,
            short rows = 30)
        {
            // 创建管道用于 PTY 输入输出
            var pipeAttributes = new SECURITY_ATTRIBUTES
            {
                nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
                bInheritHandle = 1
            };

            // 输入管道：我们写入 -> PTY 读取
            if (!CreatePipe(out var inputReadSide, out var inputWriteSide, ref pipeAttributes, 0))
                throw new InvalidOperationException($"CreatePipe failed for input: {Marshal.GetLastWin32Error()}");

            // 输出管道：PTY 写入 -> 我们读取
            if (!CreatePipe(out var outputReadSide, out var outputWriteSide, ref pipeAttributes, 0))
            {
                CloseHandle(inputReadSide);
                CloseHandle(inputWriteSide);
                throw new InvalidOperationException($"CreatePipe failed for output: {Marshal.GetLastWin32Error()}");
            }

            // 创建伪控制台
            var size = new COORD(cols, rows);
            var result = CreatePseudoConsole(size, inputReadSide, outputWriteSide, 0, out var hPC);
            if (result != 0)
            {
                CloseHandle(inputReadSide);
                CloseHandle(inputWriteSide);
                CloseHandle(outputReadSide);
                CloseHandle(outputWriteSide);
                throw new InvalidOperationException($"CreatePseudoConsole failed: {result}");
            }

            // 关闭 PTY 侧的管道句柄（我们不需要它们）
            CloseHandle(inputReadSide);
            CloseHandle(outputWriteSide);

            // 创建进程属性列表
            IntPtr attrListSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrListSize);
            var attrList = Marshal.AllocHGlobal(attrListSize);

            try
            {
                if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref attrListSize))
                    throw new InvalidOperationException($"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");

                // 将伪控制台与进程关联
                if (!UpdateProcThreadAttribute(
                    attrList,
                    0,
                    (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    hPC,
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
                {
                    throw new InvalidOperationException($"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");
                }

                // 准备启动信息
                var startupInfo = new STARTUPINFOEX
                {
                    StartupInfo = new STARTUPINFO
                    {
                        cb = Marshal.SizeOf<STARTUPINFOEX>()
                    },
                    lpAttributeList = attrList
                };

                // 创建环境变量块
                IntPtr envBlock = IntPtr.Zero;
                if (environment != null && environment.Count > 0)
                {
                    envBlock = CreateEnvironmentBlockFromDictionary(environment);
                }

                // 创建进程
                if (!CreateProcess(
                    null,
                    command,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,
                    envBlock,
                    workingDirectory,
                    ref startupInfo,
                    out var processInfo))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (envBlock != IntPtr.Zero)
                        Marshal.FreeHGlobal(envBlock);
                    throw new InvalidOperationException($"CreateProcess failed: {error}");
                }

                if (envBlock != IntPtr.Zero)
                    Marshal.FreeHGlobal(envBlock);

                // 关闭线程句柄（我们不需要它）
                CloseHandle(processInfo.hThread);

                // 创建流
                var inputStream = new FileStream(
                    new SafeFileHandle(inputWriteSide, true),
                    FileAccess.Write, 4096, false);

                var outputStream = new FileStream(
                    new SafeFileHandle(outputReadSide, true),
                    FileAccess.Read, 4096, false);

                return new PseudoConsoleSession(
                    hPC,
                    processInfo.hProcess,
                    processInfo.dwProcessId,
                    inputStream,
                    outputStream);
            }
            finally
            {
                DeleteProcThreadAttributeList(attrList);
                Marshal.FreeHGlobal(attrList);
            }
        }

        private static IntPtr CreateEnvironmentBlockFromDictionary(IDictionary<string, string> environment)
        {
            var sb = new StringBuilder();
            foreach (var kv in environment)
            {
                sb.Append(kv.Key);
                sb.Append('=');
                sb.Append(kv.Value);
                sb.Append('\0');
            }
            sb.Append('\0'); // 双空字符结尾

            var bytes = Encoding.Unicode.GetBytes(sb.ToString());
            var ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return ptr;
        }

        #endregion
    }

    /// <summary>
    /// 伪控制台会话
    /// </summary>
    public class PseudoConsoleSession : IDisposable
    {
        private IntPtr _hPC;
        private IntPtr _hProcess;
        private bool _disposed;

        public int ProcessId { get; }
        public Stream InputStream { get; }
        public Stream OutputStream { get; }

        public bool HasExited
        {
            get
            {
                if (_hProcess == IntPtr.Zero) return true;
                var result = WaitForSingleObject(_hProcess, 0);
                return result == 0; // WAIT_OBJECT_0
            }
        }

        public int ExitCode
        {
            get
            {
                if (_hProcess == IntPtr.Zero) return -1;
                GetExitCodeProcess(_hProcess, out var exitCode);
                return (int)exitCode;
            }
        }

        internal PseudoConsoleSession(
            IntPtr hPC,
            IntPtr hProcess,
            int processId,
            Stream inputStream,
            Stream outputStream)
        {
            _hPC = hPC;
            _hProcess = hProcess;
            ProcessId = processId;
            InputStream = inputStream;
            OutputStream = outputStream;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int ResizePseudoConsole(IntPtr hPC, short cols, short rows);

        public void Resize(short cols, short rows)
        {
            if (_hPC != IntPtr.Zero)
            {
                ResizePseudoConsole(_hPC, cols, rows);
            }
        }

        public void Kill()
        {
            if (_hProcess != IntPtr.Zero)
            {
                TerminateProcess(_hProcess, 1);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { InputStream?.Dispose(); } catch { }
            try { OutputStream?.Dispose(); } catch { }

            if (_hPC != IntPtr.Zero)
            {
                ClosePseudoConsole(_hPC);
                _hPC = IntPtr.Zero;
            }

            if (_hProcess != IntPtr.Zero)
            {
                CloseHandle(_hProcess);
                _hProcess = IntPtr.Zero;
            }
        }
    }
}

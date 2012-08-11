using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Cairo.WindowsHooksWrapper
{
    /// <summary>
    /// Process Specific Access Mode.
    /// http://msdn.microsoft.com/en-us/library/ms684880(VS.85).aspx
    /// </summary>
    [Flags]
    public enum ProcessSpecificAccess : uint
    {
        PROCESS_VM_READ = 0x0010,
        PROCESS_VM_WRITE = 0x0020
    }

    public class NativeMethods
    {

        /*
            Includes some P/Invoked Methods();
        */

        /// <summary>
        /// Retrieves the fully-qualified path for the file containing the specified module.
        /// http://msdn.microsoft.com/en-us/library/ms683198(VS.85).aspx
        /// </summary>
        /// <param name="hProcess"></param>
        /// <param name="hModule"></param>
        /// <param name="lpBaseName"></param>
        /// <param name="nSize"></param>
        /// <returns></returns>
        [DllImport ("psapi.dll")] //Supported under Windows Vista and Windows Server 2008.
        static extern uint GetModuleFileNameEx (IntPtr hProcess, IntPtr hModule, out StringBuilder lpBaseName,
         [In] [MarshalAs (UnmanagedType.U4)] int nSize);


        /// <summary>
        /// Retrieves the identifier of the thread that created the specified window and, optionally, the identifier of the process that created the window.
        /// http://msdn.microsoft.com/en-us/library/ms633522(VS.85).aspx
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="processId"></param>
        /// <returns></returns>
        [DllImport ("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId (IntPtr handle, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint dwFlags,
            [Out, MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpExeName,
            ref uint lpdwSize);

        /// <summary>
        /// Retrieves the Path of a running process.
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        public static string GetProcessPath (IntPtr hwnd)
        {
            try
            {
                uint pid = 0;
                GetWindowThreadProcessId (hwnd, out pid);
                Process proc = Process.GetProcessById ((int)pid); //Gets the process by ID.
                return proc.MainModule.FileName.ToString ();    //Returns the path.
            }
            catch (Exception ex)
            {
                return ex.Message.ToString ();
            }
        }

        [DllImport("kernel32.dll")]
        static extern uint GetFileSize(IntPtr hFile, IntPtr lpFileSizeHigh);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr CreateFileMapping(
            IntPtr hFile,
            IntPtr lpFileMappingAttributes,
            FileMapProtection flProtect,
            uint dwMaximumSizeHigh,
            uint dwMaximumSizeLow,
            [MarshalAs(UnmanagedType.LPTStr)]string lpName);

        [Flags]
        public enum FileMapProtection : uint
        {
            PageReadonly = 0x02,
            PageReadWrite = 0x04,
            PageWriteCopy = 0x08,
            PageExecuteRead = 0x20,
            PageExecuteReadWrite = 0x40,
            SectionCommit = 0x8000000,
            SectionImage = 0x1000000,
            SectionNoCache = 0x10000000,
            SectionReserve = 0x4000000,
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr MapViewOfFile(
            IntPtr hFileMappingObject,
            FileMapAccess dwDesiredAccess,
            uint dwFileOffsetHigh,
            uint dwFileOffsetLow,
            uint dwNumberOfBytesToMap);

        [Flags]
        public enum FileMapAccess : uint
        {
            FileMapCopy = 0x0001,
            FileMapWrite = 0x0002,
            FileMapRead = 0x0004,
            FileMapAllAccess = 0x001f,
            fileMapExecute = 0x0020,
        }

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern uint GetMappedFileName(IntPtr m_hProcess, IntPtr lpv, StringBuilder
                lpFilename, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        public static string GetFileNameFromHandle(IntPtr FileHandle)
        {
            string fileName = String.Empty;
            IntPtr fileMap = IntPtr.Zero, fileSizeHi = IntPtr.Zero;
            UInt32 fileSizeLo = 0;

            fileSizeLo = GetFileSize(FileHandle, fileSizeHi);

            if (fileSizeLo == 0)
            {
                // cannot map an 0 byte file
                return "Empty file.";
            }

            fileMap = CreateFileMapping(FileHandle, IntPtr.Zero, FileMapProtection.PageReadonly, 0, 1, null);

            if (fileMap != IntPtr.Zero)
            {
                IntPtr pMem = MapViewOfFile(fileMap, FileMapAccess.FileMapRead, 0, 0, 1);
                if (pMem != IntPtr.Zero)
                {
                    StringBuilder fn = new StringBuilder(250);
                    GetMappedFileName(System.Diagnostics.Process.GetCurrentProcess().Handle, pMem, fn, 250);
                    if (fn.Length > 0)
                    {
                        UnmapViewOfFile(pMem);
                        CloseHandle(FileHandle);
                        return fn.ToString();
                    }
                    else
                    {
                        UnmapViewOfFile(pMem);
                        CloseHandle(FileHandle);
                        return "Empty filename.";
                    }
                }
            }

            return "Empty filemap handle.";
        }

        /// <summary>
        /// Retrieves a running process.
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        public static Process GetProcess (IntPtr hwnd)
        {
            try
            {
                uint pid = 0;
                GetWindowThreadProcessId (hwnd, out pid);
                return Process.GetProcessById ((int)pid); //Gets the process by ID.
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

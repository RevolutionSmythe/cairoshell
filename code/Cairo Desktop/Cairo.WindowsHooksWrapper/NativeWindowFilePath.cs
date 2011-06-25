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
        static extern uint GetModuleFileNameEx (IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName,
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

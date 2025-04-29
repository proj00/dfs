using Org.BouncyCastle.Tls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace common_test
{
    public static class ProcessHandling
    {
        public static void KillSolutionProcesses(string[] exes)
        {
            foreach (var exeName in exes)
            {
                try
                {
                    var processes = Process.GetProcessesByName(
                        Path.GetFileNameWithoutExtension(exeName));

                    foreach (var process in processes)
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                // Try graceful shutdown first
                                if (!process.CloseMainWindow())
                                {
                                    // Force kill if needed
                                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                                    {
                                        // More reliable on Windows
                                        TerminateProcess(process.Handle, 0);
                                    }
                                    else
                                    {
                                        process.Kill();
                                    }
                                }
                                process.WaitForExit(500); // Wait up to 500ms
                            }
                        }
                        finally
                        {
                            process.Dispose();
                        }
                    }
                }
                catch
                {
                    // Ignore any exceptions because why not
                }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);
    }
}

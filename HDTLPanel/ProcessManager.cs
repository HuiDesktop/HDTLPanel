using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HDTLPanel
{
    internal class ProcessManager
    {
        public readonly Process process;
        public event EventHandler? Exited;

        public ProcessManager(string exeName, string workingDirectory, string arguments)
        {
            ProcessStartInfo processStartInfo = new(exeName, arguments) {
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            process = Process.Start(processStartInfo) ?? throw new Exception("Failed to start process");
            process.Exited += Process_Exited;
            process.EnableRaisingEvents = true;
        }

        public void TryCloseWindow()
        {
            process.CloseMainWindow();
        }

        public void ForceCloseWindow()
        {
            process.Kill(true);
        }

        private void Process_Exited(object? sender, EventArgs e)
        {
            Exited?.Invoke(this, e);
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HDTLPanel
{
    internal class ProcessManager : IDisposable
    {
        public readonly Process process;
        public event EventHandler? Exited;
        public ManagedIpc txIpc, rxIpc;
        private bool disposedValue;

        public ProcessManager(string exeName, string workingDirectory, string arguments)
        {
            txIpc = ManagedIpc.CreateInstance(128);
            rxIpc = ManagedIpc.CreateInstance(128);
            ProcessStartInfo processStartInfo = new(exeName, arguments + " " + txIpc.GetName() + " " + rxIpc.GetName())
            {
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            process = Process.Start(processStartInfo) ?? throw new Exception("Failed to start process");
            process.ErrorDataReceived += Process_ErrorDataReceived;
            process.Exited += Process_Exited;
            process.EnableRaisingEvents = true;
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Debug.WriteLine(e.Data);
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)
                    txIpc.Dispose();
                    rxIpc.Dispose();
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue=true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~ProcessManager()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
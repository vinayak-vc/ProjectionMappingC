using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Runs one external process (ffmpeg or the installer batch) without blocking the main
    /// thread: stdout/stderr lines are queued by background callbacks and drained with
    /// <see cref="TryDequeue"/> from Update. Arguments are passed as a list (no shell quoting).
    /// </summary>
    public sealed class PMSDKFfmpegJob : IDisposable
    {
        private readonly ConcurrentQueue<string> lines = new ConcurrentQueue<string>();
        private Process process;
        private bool drained;
        private bool cancelled;

        /// <summary>Set when the process could not even be started.</summary>
        public string StartError { get; private set; }

        public bool Cancelled {
            get { return cancelled; }
        }

        /// <summary>True once the process ended (or never started) and all its output was read.</summary>
        public bool IsFinished {
            get {
                if (process == null) {
                    return true;
                }
                if (!process.HasExited) {
                    return false;
                }
                if (!drained) {
                    // Flushes the async stdout/stderr callbacks before we report completion.
                    process.WaitForExit();
                    drained = true;
                }
                return true;
            }
        }

        public int ExitCode {
            get {
                if (process == null || !IsFinished) {
                    return StartError != null ? -1 : 0;
                }
                return process.ExitCode;
            }
        }

        public bool Succeeded {
            get { return StartError == null && !cancelled && IsFinished && process != null && process.ExitCode == 0; }
        }

        public static PMSDKFfmpegJob Start(string fileName, IList<string> arguments)
        {
            PMSDKFfmpegJob job = new PMSDKFfmpegJob();
            try {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = fileName;
                for (int i = 0; i < arguments.Count; i++) {
                    info.ArgumentList.Add(arguments[i]);
                }
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                info.RedirectStandardInput = false;
                info.StandardOutputEncoding = Encoding.UTF8;
                info.StandardErrorEncoding = Encoding.UTF8;

                Process p = new Process();
                p.StartInfo = info;
                p.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e) { job.Enqueue(e.Data); };
                p.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e) { job.Enqueue(e.Data); };
                if (!p.Start()) {
                    job.StartError = "The process did not start.";
                    p.Dispose();
                    return job;
                }
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                job.process = p;
            } catch (Exception ex) {
                job.StartError = ex.Message;
            }
            return job;
        }

        /// <summary>Next output line (any thread produced it), or false when none is waiting.</summary>
        public bool TryDequeue(out string line)
        {
            return lines.TryDequeue(out line);
        }

        /// <summary>Stop the process (and children, e.g. a running ffmpeg started by the batch).</summary>
        public void Cancel()
        {
            cancelled = true;
            try {
                if (process != null && !process.HasExited) {
                    int id = process.Id;
                    if (PMSDKFfmpeg.IsWindows) {
                        // taskkill /T also stops children (ffmpeg started by the installer batch).
                        ProcessStartInfo kill = new ProcessStartInfo("taskkill");
                        kill.ArgumentList.Add("/PID");
                        kill.ArgumentList.Add(id.ToString());
                        kill.ArgumentList.Add("/T");
                        kill.ArgumentList.Add("/F");
                        kill.UseShellExecute = false;
                        kill.CreateNoWindow = true;
                        using (Process killer = Process.Start(kill)) {
                            if (killer != null) {
                                killer.WaitForExit(3000);
                            }
                        }
                    }
                    if (!process.HasExited) {
                        process.Kill();
                    }
                }
            } catch (Exception) {
                // Already gone - nothing to do.
            }
        }

        public void Dispose()
        {
            if (process != null) {
                if (!process.HasExited) {
                    Cancel();
                }
                process.Dispose();
                process = null;
            }
        }

        private void Enqueue(string data)
        {
            if (data != null) {
                lines.Enqueue(data);
            }
        }
    }
}

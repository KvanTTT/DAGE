using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace AntlrGrammarEditor
{
    public class Processor : IDisposable
    {
        private int _eventInvokeCounter;
        private Process _process;

        public string ToolName { get; }

        public string Arguments { get; set; } = "";

        public string WorkingDirectory { get; set; }

        public int Timeout { get; set; } = 0;

        public int CheckTimeout { get; set; } = 200;

        public event EventHandler<DataReceivedEventArgs> ErrorDataReceived;

        public event EventHandler<DataReceivedEventArgs> OutputDataReceived;

        public CancellationToken CancellationToken { get; set; }

        public Processor(string toolName, string arguments = "", string workingDirectory = "", int timeout = 0)
        {
            ToolName = toolName ?? throw new ArgumentNullException(nameof(toolName));
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
            WorkingDirectory = workingDirectory;
            Timeout = timeout;
        }

        public ProcessExecutionResult Start()
        {
            var result = new ProcessExecutionResult();
            _process = new Process();

            var startInfo = _process.StartInfo;
            startInfo.FileName = ToolName;
            startInfo.Arguments = Arguments;
            if (WorkingDirectory != null)
            {
                startInfo.WorkingDirectory = WorkingDirectory;
            }
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.StandardErrorEncoding = Encoding.UTF8;
            startInfo.StandardOutputEncoding = Encoding.UTF8;

            _process.ErrorDataReceived += (sender, e) =>
            {
                Interlocked.Increment(ref _eventInvokeCounter);
                try
                {
                    ErrorDataReceived?.Invoke(sender, e);
                }
                finally
                {
                    Interlocked.Decrement(ref _eventInvokeCounter);
                }
            };

            _process.OutputDataReceived += (sender, e) =>
            {
                Interlocked.Increment(ref _eventInvokeCounter);
                try
                {
                    OutputDataReceived?.Invoke(sender, e);
                }
                finally
                {
                    Interlocked.Decrement(ref _eventInvokeCounter);
                }
            };

            _process.Exited += (sender, e) =>
            {
                Interlocked.Decrement(ref _eventInvokeCounter);
            };

            var stopwatch = Stopwatch.StartNew();
            _eventInvokeCounter = 1;
            _process.EnableRaisingEvents = true;
            _process.Start();
            _process.StandardInput.WriteLine();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            result.ProcessId = _process.Id;

            long timeout = Timeout == 0 ? long.MaxValue : Timeout;

            while (stopwatch.ElapsedMilliseconds < timeout &&
                (!_process.HasExited || Thread.VolatileRead(ref _eventInvokeCounter) > 0))
            {
                Thread.Sleep(CheckTimeout);
                CancellationToken.ThrowIfCancellationRequested();
            }

            result.ExitCode = _process.ExitCode;

            return result;
        }

        public void Dispose()
        {
            if (_process != null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill();
                    }
                }
                catch (InvalidOperationException)
                {
                }
                _process.Dispose();
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Text;

namespace NScript.AndroidBot
{
    public class ProcessSession : IDisposable
    {
        /// <summary>
        /// Linux 或 macosx 下，有的命令行会以 \r 来换行，用 OutputDataReceived 会接收不到数据
        /// </summary>
        public class StreamHelper
        {
            public bool Stopped;
            public Action<String> OnMsg;
            public Action<String> OnErr;

            /// <summary>
            /// 除了 '\n' 外，另一个断句分隔符
            /// </summary>
            public Char ExtSplitter = '\r';

            public Process Owner;

            private StringBuilder sbOut = new StringBuilder();
            private StringBuilder sbErr = new StringBuilder();

            public void Start()
            {
                new Thread(new ThreadStart(this.RunParseStdOut)).Start();
                new Thread(new ThreadStart(this.RunParseStdErr)).Start();
            }

            private void RunParseStdOut()
            {
                while (Stopped == false)
                {
                    FlushStdOut();
                }

                FlushStdOut();
            }

            private void RunParseStdErr()
            {
                while (Stopped == false)
                {
                    FlushStdErr();
                }

                FlushStdErr();
            }

            private void FlushStdOut(bool flushAll = false)
            {
                StreamReader sOut = Owner.StandardOutput;
                if (sOut.EndOfStream == true)
                    return;

                int val = sOut.Read();
                if (val >= 0)
                {
                    Char c = (Char)val;
                    ParseStr(sbOut, c, flushAll);
                }
            }

            private void FlushStdErr(bool flushAll = false)
            {
                StreamReader sErr = Owner.StandardError;
                if (sErr.EndOfStream == true)
                    return;

                int val = sErr.Read();
                if (val >= 0)
                {
                    Char c = (Char)val;
                    ParseStr(sbErr, c, flushAll);
                }
            }

            private void ParseStr(StringBuilder sb, Char c, bool flushAll = false)
            {
                if (flushAll == true)
                {
                    sb.Append(c);
                    String msg = sb.ToString();
                    if (sb.Length > 0) sb.Remove(0, sb.Length);
                    if (OnMsg != null)
                        OnMsg(msg);
                    return;
                }

                sb.Append(c);
                if (c == '\n' || c == ExtSplitter)
                {
                    String msg = sb.ToString();
                    if (sb.Length > 0) sb.Remove(0, sb.Length);

                    if (OnMsg != null)
                        OnMsg(msg);
                }
            }
        }

        public Action<String> OnMsg;
        public Action<String> OnErr;
        private Process _process;

        public void KillProcess()
        {
            if (_process != null)
            {
                try
                {
                    _process.Kill();
                }
                catch
                {
                }
                finally
                {
                    _process = null;
                }
            }
        }

        public ProcessSession(String fileName, String arguments, Action<String> onMsg = null, Action<String> onErr = null)
        {
            OnMsg = onMsg;
            OnErr = onErr;

            Process process = new Process();     //创建进程对象
            _process = process;
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = fileName;      //设定需要执行的命令
            startInfo.Arguments = arguments;   //设定参数，其中的“/C”表示执行完命令后马上退出
            startInfo.UseShellExecute = false;     //不使用系统外壳程序启动
            startInfo.RedirectStandardInput = false;   //不重定向输入
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;   //重定向输出
            startInfo.CreateNoWindow = true;     //不创建窗口

            process.OutputDataReceived += new DataReceivedEventHandler(process_OutputDataReceived);
            process.ErrorDataReceived += new DataReceivedEventHandler(process_ErrorDataReceived);
            process.StartInfo = startInfo;
        }

        public ProcessSession(String[] arguments, Action<String> onMsg = null, Action<String> onErr = null)
        {
            OnMsg = onMsg;
            OnErr = onErr;

            Process process = new Process();     //创建进程对象
            _process = process;
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = arguments[0];      //设定需要执行的命令
            startInfo.Arguments = BuildArguments(arguments, true);   //设定参数，其中的“/C”表示执行完命令后马上退出
            startInfo.UseShellExecute = false;     //不使用系统外壳程序启动
            startInfo.RedirectStandardInput = false;   //不重定向输入
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;   //重定向输出
            startInfo.CreateNoWindow = true;     //不创建窗口

            process.OutputDataReceived += new DataReceivedEventHandler(process_OutputDataReceived);
            process.ErrorDataReceived += new DataReceivedEventHandler(process_ErrorDataReceived);
            process.StartInfo = startInfo;
        }

        private String BuildArguments(String[] args, bool ignoreFirstArg)
        {
            StringBuilder sb = new StringBuilder();
            int i0 = ignoreFirstArg ? 1 : 0;
            for(int i = i0; i < args.Length; i++)
            {
                String item = args[i];
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(item);
            }
            return sb.ToString();
        }

        public bool Run()
        {
            bool rtn = false;
            Process process = _process;
            try
            {
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                process.WaitForExit();
                process.Close();
                process.Dispose();
                rtn = true;
                _process = null;
            }
            catch (Exception e)
            {
                if (OnErr != null) OnErr(e.Message);
            }
            finally
            {
                if (process != null)
                    process.Close();
            }
            return rtn;
        }

        public void Kill()
        {
            if(_process?.HasExited == false)
            {
                _process?.Kill();
            }
        }

        private List<String> CacheOutputs = new List<String>();

        private String FetchCacheOutputs()
        {
            StringBuilder sb = new StringBuilder();
            lock (this)
            {
                foreach (String item in CacheOutputs)
                {
                    sb.AppendLine(item);
                }
                CacheOutputs.Clear();
            }
            return sb.ToString();
        }

        void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrEmpty(e.Data))
            {
                lock (this)
                {
                    CacheOutputs.Add(e.Data);
                    Console.WriteLine(e.Data);
                    if (OnMsg != null) OnMsg(e.Data);
                }
            }
        }

        void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrEmpty(e.Data))
            {
                lock (this)
                {
                    CacheOutputs.Add(e.Data);
                    Console.WriteLine(e.Data);
                    if (OnErr != null) OnErr(e.Data);
                }
            }
        }

        public void Dispose()
        {
            this.KillProcess();
        }
    }
}

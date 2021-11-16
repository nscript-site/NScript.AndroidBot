using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace NScript.AndroidBot
{
    public class Controller
    {
        public Socket ControlSocket { get; private set; }
        public Queue<ControlMsg> queue { get; set; } = new Queue<ControlMsg>();
        private Object syncRoot = new object();

        public bool Stopped { get; set; }
        private bool ForceRunningTaskExit { get; set; }

        public bool Push(ControlMsg msg)
        {
            if (msg == null) return false;
            lock(syncRoot)
                queue.Enqueue(msg);
            return true;
        }

        private Task task;

        private byte[] serialized_msg = new byte[Setting.CONTROL_MSG_MAX_SIZE];

        public unsafe bool ProcessMsg(ControlMsg msg)
        {
            if(msg is WaitMsg) {
                int miniSeconds = ((WaitMsg)msg).MiniSeconds;
                System.Threading.Thread.Sleep(miniSeconds);
                return true; 
            }
            fixed(Byte* pBuff = serialized_msg)
            {
                int len = msg.Serialize(pBuff);
                ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(pBuff, len);
                int sendLen = NetUtils.SendAll(ControlSocket, span);
                return sendLen == len;
            }
        }

        public void Bind(Socket socket)
        {
            if(task != null)
            {
                ForceRunningTaskExit = true;
                queue.Clear();
                task.Wait();
            }

            ForceRunningTaskExit = false;
            this.ControlSocket = socket;
            task = new Task(RunCore);
            task.Start();
        }

        private void RunCore()
        {
            while(true)
            {
                if (ForceRunningTaskExit == true) break;

                if(Stopped == true || queue.Count == 0)
                {
                    System.Threading.Thread.Sleep(200);
                    continue;
                }

                ControlMsg msg = null;
                lock(syncRoot)
                {
                    if(queue.Count > 0)
                        msg = queue.Dequeue();
                }

                if(msg != null)
                {
                    try
                    {
                        ProcessMsg(msg);
                    }
                    catch(SocketException ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }
    }
}

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
        public Socket control_socket { get; set; }
        public bool Stopped { get; set; }
        public Queue<ControlMsg> queue { get; set; } = new Queue<ControlMsg>();
        public Controller(Socket socket)
        {
            this.control_socket = socket;
        }

        private Object syncRoot = new object();

        public bool Push(ControlMsg msg)
        {
            if (msg == null) return false;
            lock(syncRoot)
                queue.Enqueue(msg);
            return true;
        }

        private Task task;

        private byte[] serialized_msg = new byte[Setting.CONTROL_MSG_MAX_SIZE];

        public unsafe bool process_msg(ControlMsg msg)
        {
            fixed(Byte* pBuff = serialized_msg)
            {
                int len = msg.Serialize(pBuff);
                ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(pBuff, len);
                int sendLen = NetUtils.net_send_all(control_socket, span);
                return sendLen == len;
            }
        }

        public void Start()
        {
            task = new Task(RunCore);
            task.Start();
        }

        private void RunCore()
        {
            while(true)
            {
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
                    process_msg(msg);
            }
        }
    }
}

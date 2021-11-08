using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    public class NetUtils
    {
        public static Socket Connect(IPAddress addr, UInt16 port)
        {
            Socket sock = new Socket(SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint sin = new IPEndPoint(addr, port);
            try
            {
                sock.Connect(sin);
            }
            catch
            {
                sock.Close();
                throw;
            }
            return sock;
        }

        public static Socket Listen(IPAddress addr, UInt16 port, int backlog)
        {
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            IPEndPoint sin = new IPEndPoint(addr, port);
            try
            {
                sock.Bind(sin);
                sock.Listen(backlog);
            }
            catch
            {
                sock.Close();
                throw;
            }
            return sock;
        }

        public static int RecvAll(Socket socket, Byte[] buff)
        {
            return RecvAll(socket, new Span<Byte>(buff));
        }

        public static int RecvAll(Socket socket, Span<Byte> buff)
        {
            Span<Byte> data = buff;

            int len = 0;
            while(len < data.Length)
            {
                len += socket.Receive(data, SocketFlags.None);
                data = buff.Slice(len);
            }

            return len;
        }

        public static int SendAll(Socket socket, ReadOnlySpan<Byte> buff)
        {
            int w = 0;
            int len = buff.Length;

            ReadOnlySpan<Byte> tmpBuff = buff;
            while (len > 0)
            {
                w = socket.Send(tmpBuff);
                if (w == -1)
                {
                    return -1;
                }
                len -= w;
                tmpBuff = tmpBuff.Slice(w);
            }
            return w;
        }

        public static bool ShutDown(Socket socket, SocketShutdown how)
        {
            bool result = true;
            try
            {
                socket.Shutdown(how);
            }
            catch(Exception ex)
            {
                result = false;
            }
            return result;
        }
    }
}

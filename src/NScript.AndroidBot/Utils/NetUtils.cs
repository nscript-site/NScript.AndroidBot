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
        public static Socket net_connect(UInt32 addr, UInt16 port)
        {
            Socket sock = new Socket(SocketType.Stream, ProtocolType.IPv4);
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

        public static Socket net_listen(IPAddress addr, UInt16 port, int backlog)
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

        public static Socket net_accept(Socket server_socket)
        {
            return server_socket.Accept();
        }

        public static int net_recv(Socket socket, Span<Byte> buff)
        {
            int len = socket.Receive(buff);
            return len;
        }

        public static int net_recv_all(Socket socket, Byte[] buff)
        {
            int len = socket.Receive(buff, buff.Length, SocketFlags.None);
            return len;
        }

        public static int net_recv_all(Socket socket, Span<Byte> buff)
        {
            int len = socket.Receive(buff, SocketFlags.None);
            return len;
        }

        public static int net_send(Socket socket, ReadOnlySpan<Byte> buff)
        {
            return socket.Send(buff);
        }

        public static int net_send_all(Socket socket, ReadOnlySpan<Byte> buff)
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

        public static bool net_shutdown(Socket socket, SocketShutdown how)
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

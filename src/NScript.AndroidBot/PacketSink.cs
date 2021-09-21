using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    using FFmpeg.AutoGen;

    public unsafe abstract class PacketSink
    {
        public abstract bool Open(AVCodec* codec);
        public abstract void Close();
        public abstract bool Push(AVPacket* packet);
    }
}

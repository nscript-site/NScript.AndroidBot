using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    using FFmpeg.AutoGen;

    public unsafe class Stream
    {
        public Socket Socket { get; private set; }
        private List<PacketSink> Sinks { get; set; } = new List<PacketSink>();
        public AVCodecContext* codec_ctx;
        public AVCodecParserContext* parser;
        public AVPacket* pending;
        public void* CbsUserData;
        public Action<IntPtr> OnCbs { get; set; }
        public Action<String> OnMsg { get; set; }

        public String Name { get; set; } = String.Empty;

        public void Cbs(void* userData)
        {
            OnCbs?.Invoke((IntPtr)userData);
        }

        public void AddSink(PacketSink sink)
        {
            Sinks.Add(sink);
        }

        #region 二进制操作

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static UInt16 buffer_read16be(Byte* buf)
        {
            return (UInt16)((buf[0] << 8) | buf[1]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static UInt32 buffer_read32be(Byte* buf)
        {
            return (UInt32)((buf[0] << 24) | (buf[1] << 16) | (buf[2] << 8) | buf[3]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static UInt64 buffer_read64be(Byte* buf)
        {
            UInt32 msb = buffer_read32be(buf);
            UInt32 lsb = buffer_read32be(&buf[4]);
            return ((UInt64)msb << 32) | lsb;
        }

        #endregion

        public bool stream_recv_packet(AVPacket* packet)
        {
            // The video stream contains raw packets, without time information. When we
            // record, we retrieve the timestamps separately, from a "meta" header
            // added by the server before each raw packet.
            //
            // The "meta" header length is 12 bytes:
            // [. . . . . . . .|. . . .]. . . . . . . . . . . . . . . ...
            //  <-------------> <-----> <-----------------------------...
            //        PTS        packet        raw packet
            //                    size
            //
            // It is followed by <packet_size> bytes containing the packet/frame.
            const int HEADER_SIZE = 12;
            Byte* pHeader = stackalloc byte[HEADER_SIZE];
            Span<Byte> header = new Span<byte>(pHeader, HEADER_SIZE);

            int r = NetUtils.RecvAll(Socket, header);
            if (r < HEADER_SIZE)
            {
                OnMsg?.Invoke($"[{Name}] packet header RecvAll fail");
                return false;
            }

            UInt64 pts = buffer_read64be(pHeader);
            int len = (int)buffer_read32be(&pHeader[8]);

            //assert(pts == NO_PTS || (pts & 0x8000000000000000) == 0);
            //assert(len);

            if (ffmpeg.av_new_packet(packet, len) != 0)
            {
                OnMsg?.Invoke($"[{Name}] could not allocate packet of {len} bytes");
                throw new BotException("Could not allocate packet");
            }

            Span<Byte> data = new Span<byte>(packet->data, len);
            r = NetUtils.RecvAll(Socket, data);
            if (r < 0 || r < len)
            {
                ffmpeg.av_packet_unref(packet);
                OnMsg?.Invoke($"[{Name}] packet content of {len} bytes RecvAll fail");
                return false;
            }

            unchecked
            {
                const UInt64 NO_PTS = (UInt64)(-1);
                packet->pts = pts != NO_PTS ? (Int64)pts : ffmpeg.AV_NOPTS_VALUE;
            }

            return true;
        }

        public bool push_packet_to_sinks(AVPacket* packet)
        {
            for (int i = 0; i < this.Sinks.Count; ++i)
            {
                var sink = Sinks[i];
                if (!sink.Push(packet))
                {
                    //LOGE("Could not send config packet to sink %d", i);
                    return false;
                }
            }

            return true;
        }

        public bool stream_parse(AVPacket* packet)
        {
            Byte* in_data = packet->data;
            int in_len = packet->size;
            Byte* out_data = null;
            int out_len = 0;
            int r = ffmpeg.av_parser_parse2(parser, codec_ctx,
                                     &out_data, &out_len, in_data, in_len,
                                     ffmpeg.AV_NOPTS_VALUE, ffmpeg.AV_NOPTS_VALUE, -1);

            if (parser->key_frame == 1)
            {
                packet->flags |= ffmpeg.AV_PKT_FLAG_KEY;
            }

            packet->dts = packet->pts;

            //Span<Byte> datas = new Span<byte>(packet->data, packet->size);
            //Console.WriteLine(datas.ToString());

            bool ok = this.push_packet_to_sinks(packet);
            if (!ok)
            {
                //LOGE("Could not process packet");
                return false;
            }

            return true;
        }

        public bool stream_push_packet(AVPacket* packet)
        {
            bool is_config = packet->pts == ffmpeg.AV_NOPTS_VALUE;

            // A config packet must not be decoded immediately (it contains no
            // frame); instead, it must be concatenated with the future data packet.
            if (pending != null || is_config)
            {
                Int64 offset;
                if (pending != null)
                {
                    offset = pending->size;
                    if (ffmpeg.av_grow_packet(pending, packet->size) != 0)
                    {
                        //LOGE("Could not grow packet");
                        return false;
                    }
                }
                else
                {
                    offset = 0;
                    pending = ffmpeg.av_packet_alloc();
                    if (pending == null)
                    {
                        //LOGE("Could not allocate packet");
                        return false;
                    }
                    if (ffmpeg.av_new_packet(pending, packet->size) != 0)
                    {
                        //LOGE("Could not create packet");
                        FFmpegUtils.av_packet_free(ref pending);
                        return false;
                    }
                }

                // smemcpy(stream->pending->data + offset, packet->data, packet->size);
                Span<Byte> source = new Span<byte>(packet->data, packet->size);
                Span<Byte> dest = new Span<byte>(pending->data + offset, source.Length);
                source.CopyTo(dest);

                if (!is_config)
                {
                    // prepare the concat packet to send to the decoder
                    pending->pts = packet->pts;
                    pending->dts = packet->dts;
                    pending->flags = packet->flags;
                    packet = pending;
                }
            }

            if (is_config)
            {
                // config packet
                bool ok = this.push_packet_to_sinks(packet);
                if (!ok)
                {
                    return false;
                }
            }
            else
            {
                // data packet
                bool ok = this.stream_parse(packet);

                if (pending != null)
                {
                    // the pending packet must be discarded (consumed or error)
                    ffmpeg.av_packet_unref(pending);
                    FFmpegUtils.av_packet_free(ref pending);
                }

                if (!ok)
                {
                    return false;
                }
            }
            return true;
        }


        public void CloseSinks()
        {
            this.CloseSinks(Sinks.Count);
        }

        public void CloseSinks(int count)
        {
            count = Math.Max(count, Sinks.Count);
            while (count > 0)
            {
                count--;
                var sink = Sinks[count];
                sink.Close();
            }
        }

        public bool OpenSinks(AVCodec* codec, AVPixelFormat fmt)
        {
            for (int i = 0; i < Sinks.Count; i++)
            {
                var item = Sinks[i];
                if (item.Open(codec, fmt) == false)
                {
                    CloseSinks(i);
                    return false;
                    //throw new BotException("Could not open packet sink " + i);
                }
            }
            return true;
        }

        public int RunBackgroundWork()
        {
            OnMsg?.Invoke($"[{Name}] start video decoding");

            AVCodec* codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
            Exception ex = null;
            if (codec == null)
            {
                ex = new BotException("H.264 decoder not found");
                goto end;
            }

            codec_ctx = ffmpeg.avcodec_alloc_context3(codec);
            if (codec_ctx == null)
            {
                ex = new BotException("Could not allocate codec context");
                goto end;
            }

            if (this.OpenSinks(codec, AVPixelFormat.AV_PIX_FMT_YUV420P) == false)
            {
                ex = new BotException("Could not open stream sinks");
                goto finally_free_codec_ctx;
            }

            parser = ffmpeg.av_parser_init((int)AVCodecID.AV_CODEC_ID_H264);
            if (parser == null)
            {
                ex = new BotException("Could not initialize parser");
                goto finally_close_sinks;
            }

            // We must only pass complete frames to av_parser_parse2()!
            // It's more complicated, but this allows to reduce the latency by 1 frame!
            parser->flags |= ffmpeg.PARSER_FLAG_COMPLETE_FRAMES;

            AVPacket* packet = ffmpeg.av_packet_alloc();
            if (packet == null)
            {
                OnMsg?.Invoke($"[{Name}] could not allocate packet");
                ex = new BotException("Could not allocate packet");
                goto finally_close_parser;
            }

            for (; ; )
            {
                bool ok = this.stream_recv_packet(packet);
                if (!ok)
                {
                    OnMsg?.Invoke($"[{Name}] video stream eof");
                    break;  // eof
                }

                ok = this.stream_push_packet(packet);
                ffmpeg.av_packet_unref(packet);
                if (!ok)
                {
                    OnMsg?.Invoke($"[{Name}] cannot process packet");
                    // cannot process packet (error already logged)
                    break;
                }
            }

            //LOGD("End of frames");

            if (pending != null)
            {
                ffmpeg.av_packet_unref(pending);
                FFmpegUtils.av_packet_free(ref pending);
            }

            FFmpegUtils.av_packet_free(ref packet);
        finally_close_parser:
            ffmpeg.av_parser_close(parser);
        finally_close_sinks:
            this.CloseSinks();
        finally_free_codec_ctx:
            FFmpegUtils.avcodec_free_context(ref codec_ctx);
        end:
            Cbs(CbsUserData);

            OnMsg?.Invoke($"[{Name}] stop video decoding");

            return 0;
        }

        public Task Task { get; private set; }

        public bool Receive(Socket socket) {
            if (socket == null) return false;

            this.Socket = socket;
            Task = new Task(() =>
            {
                RunBackgroundWork();
            });
            this.Task.Start();
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    using FFmpeg.AutoGen;

    public unsafe class FrameSink
    {
        public bool Open()
        {
            return true;
        }
        public void Close() { }
        public bool Push(AVFrame* frame)
        {
            return true;
        }
    }

    public unsafe class Decoder : PacketSink
    {
        public override unsafe bool Open(AVCodec* codec)
        {
            decoder_open(codec);
            return true;
        }

        public override unsafe bool Push(AVPacket* packet)
        {
            decoder_push(packet);
            return true;
        }

        public override void Close()
        {
            decoder_close();
        }

        public void AddSink(FrameSink sink)
        {
            this.FrameSinks.Add(sink);
        }

        AVCodecContext* codec_ctx;
        AVFrame* frame;

        private List<FrameSink> FrameSinks { get; set; } = new List<FrameSink>();

        public void CloseFrameSinks()
        {
            this.CloseFrameSinks(FrameSinks.Count);
        }

        public void CloseFrameSinks(int count)
        {
            count = Math.Max(count, FrameSinks.Count);
            while (count > 0)
            {
                count--;
                var frameSink = FrameSinks[count];
                frameSink.Close();
            }
        }

        public void OpenFrameSinks()
        {
            for (int i = 0; i < FrameSinks.Count; i++)
            {
                var item = FrameSinks[i];
                if (item.Open() == false)
                {
                    CloseFrameSinks(i);
                    throw new BotException("Could not open frame sink " + i);
                }
            }
        }

        public void decoder_open(AVCodec* codec)
        {
            this.codec_ctx = ffmpeg.avcodec_alloc_context3(codec);
            if (codec_ctx == null)
            {
                throw new BotException("Could not allocate decoder context");
            }

            if (ffmpeg.avcodec_open2(codec_ctx, codec, null) < 0)
            {
                FFmpegUtils.avcodec_free_context(ref codec_ctx);
                throw new BotException("Could not open codec");
            }

            frame = ffmpeg.av_frame_alloc();
            if (frame == null)
            {
                ffmpeg.avcodec_close(codec_ctx);
                FFmpegUtils.avcodec_free_context(ref codec_ctx);
                throw new BotException("Could not create decoder frame");
            }

            try
            {
                this.OpenFrameSinks();
            }
            catch
            {
                FFmpegUtils.av_frame_free(ref frame);
                ffmpeg.avcodec_close(codec_ctx);
                FFmpegUtils.avcodec_free_context(ref codec_ctx);
                throw;
            }
        }

        public void decoder_close()
        {
            this.CloseFrameSinks();
            FFmpegUtils.av_frame_free(ref frame);
            ffmpeg.avcodec_close(codec_ctx);
            FFmpegUtils.avcodec_free_context(ref codec_ctx);
        }

        public bool push_frame_to_sinks(AVFrame* frame)
        {
            for (int i = 0; i < this.FrameSinks.Count; ++i)
            {
                var sink = FrameSinks[i];
                if (sink.Push(frame) == false)
                {
                    //LOGE("Could not send frame to sink %d", i);
                    return false;
                }
            }

            return true;
        }

        public void decoder_push(AVPacket* packet)
        {
            bool is_config = packet->pts == ffmpeg.AV_NOPTS_VALUE;
            if (is_config) return;

            int ret = ffmpeg.avcodec_send_packet(codec_ctx, packet);
            if (ret < 0 && ret != ffmpeg.AVERROR(ffmpeg.EAGAIN))
            {
                
                return;
                //throw new BotException("Could not send video packet: " + ret);
            }

            ret = ffmpeg.avcodec_receive_frame(codec_ctx, frame);
            if (ret == 0)
            {
                // a frame was received
                bool ok = push_frame_to_sinks(frame);
                // A frame lost should not make the whole pipeline fail. The error, if
                // any, is already logged.
                ffmpeg.av_frame_unref(frame);
            }
            else if (ret != ffmpeg.AVERROR(ffmpeg.EAGAIN))
            {
                return;
                //throw new BotException("Could not receive video frame: %d" + ret);
            }
        }
    }
}

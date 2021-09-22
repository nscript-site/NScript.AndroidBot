using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    using FFmpeg.AutoGen;
    using Geb.Image;

    public unsafe class FrameSink
    {
        private SwsContextHolder m_sws = null;

        public bool Open()
        {
            return true;
        }

        public int Width { get; set; }
        public int Height { get; set; }

        public AVPixelFormat PixelFormat { get; set; }

        private Object syncRoot = new object();

        public Action<ImageBgr24> OnRender { get; set; }

        public void Close() { }

        public bool Push(AVFrame* frame)
        {
            if (Width > 0 && Height > 0 && frame != null)
            {
                ImageBgr24 img = new ImageBgr24(Width, Height);
                WriteToFrame(frame, (Byte*)img.Start, img.Width * 3, AVPixelFormat.AV_PIX_FMT_BGR24, Width, Height);
                if (Image != null) Image.Dispose();
                Image = img;
                OnRender?.Invoke(img);
            }
            return true;
        }

        public ImageBgr24 Image { get; set; }

        public unsafe bool WriteToFrame(AVFrame* m_avFrame, byte* frameData, int stride, AVPixelFormat frameFmt, int width, int height)
        {
            if (m_avFrame == null || m_avFrame->data[0] == null) return false;

            SwsContextHolder sws = GetSwsContextHolder(this.Width, this.Height, this.PixelFormat, width, height, frameFmt, ffmpeg.SWS_BILINEAR);
            byte*[] dstData = { frameData };
            int[] dstLinesize = { stride };

            ffmpeg.sws_scale(sws.Context, m_avFrame->data,
               m_avFrame->linesize, 0, Height, dstData,
               dstLinesize);
            return true;
        }

        private SwsContextHolder GetSwsContextHolder(int srcW, int srcH, AVPixelFormat srcFmt, int dstW, int dstH, AVPixelFormat dstFmt, int flags)
        {
            if (m_sws == null)
            {
                m_sws = new SwsContextHolder(srcW, srcH, srcFmt, dstW, dstH, dstFmt, flags);
                return m_sws;
            }
            else if (m_sws.Match(srcW, srcH, srcFmt, dstW, dstH, dstFmt, flags) == true)
            {
                return m_sws;
            }
            else
            {
                m_sws.Dispose();
                m_sws = new SwsContextHolder(srcW, srcH, srcFmt, dstW, dstH, dstFmt, flags);
                return m_sws;
            }
        }
    }

    public unsafe class Decoder : PacketSink
    {
        public override unsafe bool Open(AVCodec* codec, AVPixelFormat fmt)
        {
            decoder_open(codec);
            foreach (var item in this.FrameSinks)
                item.PixelFormat = fmt;
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

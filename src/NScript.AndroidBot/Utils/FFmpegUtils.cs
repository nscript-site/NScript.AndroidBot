using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    using FFmpeg.AutoGen;

    public static unsafe class FFmpegUtils
    {
        public static void avcodec_free_context(ref AVCodecContext* codec_ctx)
        {
            AVCodecContext* pTmp = codec_ctx;
            ffmpeg.avcodec_free_context(&pTmp);
            codec_ctx = pTmp;
        }

        public static void av_frame_free(ref AVFrame* p)
        {
            AVFrame* pTmp = p;
            ffmpeg.av_frame_free(&pTmp);
            p = pTmp;
        }

        public static void av_packet_free(ref AVPacket* p)
        {
            AVPacket* pTmp = p;
            ffmpeg.av_packet_free(&pTmp);
            p = pTmp;
        }
    }
}

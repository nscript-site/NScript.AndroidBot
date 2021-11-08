using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    using FFmpeg.AutoGen;

    public class SwsContextHolder : IDisposable
    {
        public int SrcW, SrcH, DstW, DstH;
        public AVPixelFormat SrcFmt, DstFmt;
        public int Flags;

        public unsafe SwsContext* Context;

        public unsafe SwsContextHolder(int srcW, int srcH, AVPixelFormat srcFmt, int dstW, int dstH, AVPixelFormat dstFmt, int flags)
        {
            SrcW = srcW;
            SrcH = srcH;
            SrcFmt = srcFmt;
            DstW = dstW;
            DstH = dstH;
            DstFmt = dstFmt;
            Flags = flags;
            Context = ffmpeg.sws_getContext(srcW, srcH, srcFmt, dstW, dstH,
                dstFmt, flags, null, null, null);
        }

        public Boolean Match(int srcW, int srcH, AVPixelFormat srcFmt, int dstW, int dstH, AVPixelFormat dstFmt, int flags)
        {
            return SrcW == srcW && SrcH == srcH && SrcFmt == srcFmt && DstW == dstW && DstH == dstH && DstFmt == dstFmt && Flags == flags;
        }

        public unsafe void Dispose()
        {
            if (Context != null)
            {
                ffmpeg.sws_freeContext(Context);
                Context = null;
            }
        }

        ~SwsContextHolder()
        {
            Dispose();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    using Geb.Image;

    public class BotClient
    {
        #region Config FFmpeg
        private static bool IsFFmpegConfigged;

        private static void ConfigFFmpeg()
        {
            if (IsFFmpegConfigged == true) return;
            String[] searchPaths = { "./lib", "./ffmpeg", "./lib/ffmpeg", "c://lib/ffmpeg", "d://lib/ffmpeg", "e://lib//ffmpeg" };
            foreach (var path in searchPaths)
            {
                if (ConfigFFmpeg(path) != null) break;
            }
            IsFFmpegConfigged = true;
        }

        private static String ConfigFFmpeg(String dirPath)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
            if (dirInfo.Exists == false) return null;
            FileInfo[] files = dirInfo.GetFiles("avcodec*.dll");
            if (files == null || files.Length == 0) return null;
            FFmpeg.AutoGen.ffmpeg.RootPath = dirInfo.FullName;
            return dirInfo.FullName;
        } 
        #endregion

        private Server Server { get; set; }
        private Stream Stream { get; set; }
        private Decoder Decoder { get; set; }
        private FrameSink FrameSink { get; set; }
        private Controller Controller { get; set; }

        /// <summary>
        /// 将 UI 界面 dump 出来。结果为 xml 布局文件。
        /// </summary>
        /// <returns></returns>
        public String DumpUI()
        {
            return Server.DumpUI();
        }

        /// <summary>
        /// Bot 选项
        /// </summary>
        public BotOptions Options { get; set; } = new BotOptions();

        /// <summary>
        /// 显示 Bot 相关信息
        /// </summary>
        public Action<String> OnMsg { get; set; }

        /// <summary>
        /// 显示画面的回调函数
        /// </summary>
        public Action<ImageBgr24> OnRender { get; set; }

        /// <summary>
        /// 获取当前画面截图
        /// </summary>
        /// <returns></returns>
        public ImageBgr24 GetFameImage() { return FrameSink?.GetFrameImage(); }

        /// <summary>
        /// 向 Bot 发送消息
        /// </summary>
        /// <param name="msg"></param>
        public void Push(ControlMsg msg)
        {
            if (msg == null) return;
            Controller?.Push(msg);
        }

        /// <summary>
        /// 启动 Bot 运行
        /// </summary>
        public void Run()
        {
            ConfigFFmpeg();

            BotOptions options = Options;
            if (options == null) options = new BotOptions();
            ServerParams serverParams = options.ToServerParams();
            this.Server = new Server();
            this.Server.OnMsg = this.OnMsg;
            this.Server.Start(serverParams);
            this.Decoder = new Decoder();
            this.FrameSink = new FrameSink();
            this.FrameSink.Width = this.Server.FrameSize.Width;
            this.FrameSink.Height = this.Server.FrameSize.Height;
            this.FrameSink.OnRender = this.OnRender;
            this.Decoder.AddSink(this.FrameSink);
            this.Stream = new Stream();
            this.Stream.OnMsg = this.OnMsg;
            this.Stream.AddSink(this.Decoder);
            this.Stream.Receive(this.Server.VideoSocket);
            this.Server.OnVideoSocketConnected = () => {
                this.Stream.Receive(this.Server.VideoSocket);
            };
            this.Controller = new Controller(this.Server.ControlSocket);
            this.Controller.Start();
        }
    }
}

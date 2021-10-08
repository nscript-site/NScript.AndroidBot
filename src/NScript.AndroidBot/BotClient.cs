using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    using Geb.Image;

    public class BotClient
    {
        private Server Server { get; set; }
        private Stream Stream { get; set; }
        private Decoder Decoder { get; set; }
        private FrameSink FrameSink { get; set; }
        private Controller Controller { get; set; }

        public BotOptions Options { get; set; } = new BotOptions();

        public Action<String> OnMsg { get; set; }

        public Action<ImageBgr24> OnRender { get; set; }

        public void Push(ControlMsg msg)
        {
            if (msg == null) return;
            Controller?.Push(msg);
        }

        public void Run()
        {
            FFmpeg.AutoGen.ffmpeg.RootPath = @"C:\Lib\ffmpeg";

            BotOptions options = Options;
            if (options == null) options = new BotOptions();
            ServerParams serverParams = options.ToServerParams();
            this.Server = new Server();
            this.Server.OnMsg = this.OnMsg;
            this.Server.Start(serverParams);
            this.Server.Connect();
            this.Decoder = new Decoder();
            this.FrameSink = new FrameSink();
            this.FrameSink.Width = this.Server.FrameSize.Width;
            this.FrameSink.Height = this.Server.FrameSize.Height;
            this.FrameSink.OnRender = this.OnRender;
            this.Decoder.AddSink(this.FrameSink);
            this.Stream = new Stream(this.Server.video_socket);
            this.Stream.AddSink(this.Decoder);
            this.Stream.Start();
            this.Controller = new Controller(this.Server.control_socket);
            this.Controller.Start();
        }
    }
}

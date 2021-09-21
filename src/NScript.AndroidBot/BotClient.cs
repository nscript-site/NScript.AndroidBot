using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    public class BotClient
    {
        private Server Server { get; set; }
        private Stream Stream { get; set; }
        private Decoder Decoder { get; set; }
        private FrameSink FrameSink { get; set; }

        public BotOptions Options { get; set; } = new BotOptions();

        public Action<String> OnMsg { get; set; }

        public void Run()
        {
            FFmpeg.AutoGen.ffmpeg.RootPath = @"C:\Lib\ffmpeg";
            FFmpeg.AutoGen.ffmpeg.av_register_all();

            BotOptions options = Options;
            if (options == null) options = new BotOptions();
            ServerParams serverParams = options.ToServerParams();
            this.Server = new Server();
            this.Server.OnMsg = this.OnMsg;
            this.Server.server_start(serverParams);
            this.Server.ServerConnect();
            this.Decoder = new Decoder();
            this.FrameSink = new FrameSink();
            this.Decoder.AddSink(this.FrameSink);
            this.Stream = new Stream(this.Server.video_socket);
            this.Stream.AddSink(this.Decoder);
            this.Stream.Start();
        }
    }
}

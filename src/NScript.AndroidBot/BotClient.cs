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

        public System.Drawing.Size FrameSize { get { return Server == null ? default(System.Drawing.Size) : Server.FrameSize;  } }

        /// <summary>
        /// 向 Bot 发送消息
        /// </summary>
        /// <param name="msg"></param>
        public BotClient Push(ControlMsg msg)
        {
            if (msg == null) return this;
            Controller?.Push(msg);
            return this;
        }

        /// <summary>
        /// 发送鼠标事件
        /// </summary>
        /// <param name="type"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public BotClient Send(MouseEventType type, int x, int y)
        {
            return this.Push(new InjectTouchEventMsg(type, new Position(x, y, this.FrameSize)));
        }

        /// <summary>
        /// 发送鼠标事件
        /// </summary>
        /// <param name="type"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public BotClient Send(MouseEventType type, System.Drawing.Point point)
        {
            return this.Push(new InjectTouchEventMsg(type, new Position(point.X, point.Y, this.FrameSize)));
        }

        public BotClient SendTouchMove(System.Drawing.Point pointFrom, System.Drawing.Point pointTo, double steps = 20)
        {
            this.Push(new InjectTouchEventMsg(MouseEventType.Down, new Position(pointFrom, this.FrameSize)));
            double deltaX = (pointTo.X - pointFrom.X)/steps;
            double deltaY = (pointTo.Y - pointFrom.Y)/steps;
            this.SendWait(10);
            for(int i = 0; i <= steps; i++)
            {
                int x = (int)Math.Round(pointFrom.X + deltaX * i);
                int y = (int)Math.Round(pointFrom.Y + deltaY * i);
                this.Push(new InjectTouchEventMsg(MouseEventType.Move, new Position(new System.Drawing.Point(x,y), this.FrameSize)));
                this.SendWait(10);
            }
            this.Push(new InjectTouchEventMsg(MouseEventType.Up, new Position(pointFrom, this.FrameSize)));
            return this;
        }

        /// <summary>
        /// 发送点击事件
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public BotClient SendClick(int x, int y)
        {
            this.Push(new InjectTouchEventMsg(MouseEventType.Down, new Position(x, y, this.FrameSize)));
            this.Push(new InjectTouchEventMsg(MouseEventType.Up, new Position(x, y, this.FrameSize)));
            return this;
        }

        public BotClient SendText(String content)
        {
            if (content == null) throw new ArgumentNullException("content");
            return this.Push(new InjectTextMsg(content));
        }

        /// <summary>
        /// 发送点击事件
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public BotClient SendClick(System.Drawing.Point point)
        {
            this.Push(new InjectTouchEventMsg(MouseEventType.Down, new Position(point.X, point.Y, this.FrameSize)));
            this.Push(new InjectTouchEventMsg(MouseEventType.Up, new Position(point.X, point.Y, this.FrameSize)));
            return this;
        }

        /// <summary>
        /// 发送剪贴板消息
        /// </summary>
        /// <param name="content"></param>
        /// <param name="post">设为true，则直接将剪贴板消息显示在当前框中</param>
        /// <returns></returns>
        public BotClient SendClipboardMsg(String content, bool post)
        {
            if (content == null) throw new ArgumentNullException("content");
            SetClipboardMsg msg = new SetClipboardMsg(content, post);
            return this.Push(msg);
        }

        /// <summary>
        /// 后退
        /// </summary>
        /// <returns></returns>
        public BotClient SendBack()
        {
            this.Push(new BackOrScreenOnMsg() { Action = AndroidKeyeventAction.AKEY_EVENT_ACTION_DOWN });
            this.Push(new BackOrScreenOnMsg() { Action = AndroidKeyeventAction.AKEY_EVENT_ACTION_UP });
            return this;
        }

        /// <summary>
        /// 等待
        /// </summary>
        /// <param name="miniSeconds"></param>
        /// <returns></returns>
        public BotClient SendWait(int miniSeconds)
        {
            return this.Push(new WaitMsg(miniSeconds));
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
            if(options.Display == true)
            {
                this.FrameSink = new FrameSink();
                this.FrameSink.Width = this.Server.FrameSize.Width;
                this.FrameSink.Height = this.Server.FrameSize.Height;
                this.FrameSink.OnRender = this.OnRender;
                this.Decoder.AddSink(this.FrameSink);
            }
            this.Stream = new Stream();
            this.Stream.OnMsg = this.OnMsg;
            this.Stream.AddSink(this.Decoder);
            this.Stream.Receive(this.Server.VideoSocket);
            this.Server.OnVideoSocketConnected = () => {
                this.Stream.Receive(this.Server.VideoSocket);
            };
            this.Controller = new Controller();
            this.Controller.Bind(this.Server.ControlSocket);
            this.Server.OnControlSocketConnected = () => {
                this.Controller.Bind(this.Server.ControlSocket);
            };
        }
    }
}

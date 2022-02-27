using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    using Geb.Image;
    using System.Xml;
    using System.Xml.XPath;
    using System.Drawing;

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
        private MediaStream Stream { get; set; }
        private Decoder Decoder { get; set; }
        private FrameSink FrameSink { get; set; }
        private Controller Controller { get; set; }

        /// <summary>
        /// 获得 xml 描述的当前界面内容。
        /// </summary>
        /// <param name="ignoreTextlessControls">是否忽略没有文本的控件</param>
        /// <returns></returns>
        public PageLayout GetScreenXml(bool ignoreTextlessControls = true)
        {
            String content = Server.DumpUI();
            if (ignoreTextlessControls == true) content = LayoutUtils.ClearXmlContent(content);
            return new PageLayout(content);
        }

        static char[] splitters = new char[] { '[', ']', ',' };

        /// <summary>
        /// 获得指定节点的 Bounds
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public Nullable<Rectangle> GetNodeBounds(XPathNavigator node)
        {
            // <node text="" resource-id="" content-desc="4G 手机信号满格。" bounds="[190,15][251,57]"></node>

            if (node == null) return null;
            String attr = node.GetAttribute("bounds", "");
            if (attr == null) return null;
            float scale = (float)Server.GetScale();
            String[] terms = attr.Split(splitters, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length != 4) return null;
            float x0, y0 = 0;
            float x1, y1 = 0;
            float.TryParse(terms[0], out x0);
            float.TryParse(terms[1], out y0);
            float.TryParse(terms[2], out x1);
            float.TryParse(terms[3], out y1);
            
            return new Rectangle((int)Math.Round(x0 * scale), (int)Math.Round(y0 * scale), (int)Math.Round((x1 - x0)*scale), (int)Math.Round((y1 - y0)*scale));
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
        /// 媒体切片器
        /// </summary>
        public MediaSlicer Slicer { get; private set; } = new MediaSlicer();

        /// <summary>
        /// 显示画面的回调函数
        /// </summary>
        public Action<ImageBgr24> OnRender { get; set; }

        /// <summary>
        /// 音频数据的回调函数
        /// </summary>
        public Action<Byte[]> OnAudioDataReceive { get; set; }

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
        /// 在指定的 node 上发送鼠标事件
        /// </summary>
        /// <param name="type"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        public BotClient Send(MouseEventType type, XPathNavigator node)
        {
            var bounds = GetNodeBounds(node);
            int x = bounds.Value.X + bounds.Value.Width / 2;
            int y = bounds.Value.Y + bounds.Value.Height / 2;
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

        /// <summary>
        /// 在指定的 node 上发送点击事件
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public BotClient SentClick(XPathNavigator node)
        {
            var bounds = GetNodeBounds(node);
            int x = bounds.Value.X + bounds.Value.Width / 2;
            int y = bounds.Value.Y + bounds.Value.Height / 2;
            return SendClick(x, y);
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
            this.Slicer.FrameRate = serverParams.max_fps;
            this.Decoder = new Decoder();
            if(options.Display == true)
            {
                this.FrameSink = new FrameSink();
                this.FrameSink.Width = this.Server.FrameSize.Width;
                this.FrameSink.Height = this.Server.FrameSize.Height;
                this.FrameSink.OnRender = (t) => { this.Slicer.Receive(t); this.OnRender?.Invoke(t); } ;
                this.Decoder.AddSink(this.FrameSink);
            }
            this.Stream = new MediaStream();
            this.Stream.OnMsg = this.OnMsg;
            this.Stream.AddSink(this.Decoder);
            this.Stream.ReceiveVideoSocket(this.Server.VideoSocket);
            this.Stream.ReceiveAudioSocket(this.Server.AudioSocket, Options.MaxFps);

            this.Stream.OnAudioDataReceive = (data) => { this.Slicer.Receive(data); };

            this.Server.OnVideoSocketConnected = () => {
                this.Stream.ReceiveVideoSocket(this.Server.VideoSocket);
            };
            this.Server.OnAudioSocketConnected = () =>
            {
                this.Stream.ReceiveAudioSocket(this.Server.AudioSocket, Options.MaxFps);
            };
            this.Controller = new Controller();
            this.Controller.Bind(this.Server.ControlSocket);
            this.Server.OnControlSocketConnected = () => {
                this.Controller.Bind(this.Server.ControlSocket);
            };
        }
    }
}

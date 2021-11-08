using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NScript.AndroidBot.WpfUI
{
    using Geb.Image;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        BotClient Client;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Run(() => {
                Client = new BotClient();
                // Client.Options.Display = false; // 不显示画面
                Client.Options.MaxSize = 640;   // 修改最大画面
                Client.OnMsg = OnMsg;           // 监听程序
                Client.OnRender = OnRender;     // 如果不需要显示画面，可以不设置 OnRender
                Client.Run();                   // 启动 BotClient
            });
        }

        private void OnMsg(String msg)
        {
            if (msg == null) return;
            this.Dispatcher.InvokeAsync(() => {
                DateTime now = DateTime.Now;
                this.tbMsgs.AppendText($"[{now.ToShortTimeString()}]"+  msg + Environment.NewLine);
                this.tbMsgs.ScrollToEnd();
            });
        }

        private ImageBgr24 imgCache;
        private WriteableBitmap bmpCache;

        private void OnRender(ImageBgr24 img)
        {
            if (img == null) return;
            if (imgCache == null) imgCache = img.Clone();
            lock(imgCache)
                imgCache.CloneFrom(img);
            this.Dispatcher.InvokeAsync(Render);
        }

        private void Render()
        {
            ImageBgr24 imgFrame = imgCache;
            if (imgFrame == null) return;

            if (bmpCache == null)
            {
                bmpCache = new WriteableBitmap(imgCache.Width, imgCache.Height, 96.0, 96.0, PixelFormats.Bgr24, null);
                this.cvs.Source = bmpCache;
            }
            lock (imgCache)
                bmpCache.WritePixels(new Int32Rect(0, 0, imgCache.Width, imgCache.Height), imgCache.StartIntPtr, imgCache.ByteCount, imgCache.Stride);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SetClipboardMsg msg = new SetClipboardMsg("Hello!", false);
            Client.Push(msg);
        }

        private void ButtonSnap_Click(object sender, RoutedEventArgs e)
        {
            var img = Client.GetFameImage();
            if(img == null)
            {
                MessageBox.Show("没有截取到手机画面");
            }
            else
            {
                String fileName = DateTime.Now.ToFileTimeUtc().ToString() + ".png";
                System.IO.FileInfo fileInfo = new System.IO.FileInfo(fileName);
                img.Save(fileName);
                MessageBox.Show("截图保存在: " + fileInfo.FullName);
            }
        }

        private void ButtonGetLayout_Click(object sender, RoutedEventArgs e)
        {
            this.tbLayouts.Text = String.Empty;

            Task.Run(() => {
                String msg = String.Empty;
                try
                {
                    msg = Client.DumpUI();
                }
                catch (Exception ex)
                {
                    msg = ex.Message;
                }
                this.Dispatcher.Invoke(() => {
                    this.tbLayouts.Text = msg;
                });
            });
        }
    }
}

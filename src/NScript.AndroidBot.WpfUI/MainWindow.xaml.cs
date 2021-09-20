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
            Client = new BotClient();
            Client.OnMsg = OnMsg;
            Client.Run();
        }

        private void OnMsg(String msg)
        {
            if (msg == null) return;
            this.Dispatcher.InvokeAsync(() => {
                this.tbMsgs.AppendText(msg + Environment.NewLine);
                this.tbMsgs.ScrollToEnd();
            });
        }
    }
}

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

        public BotOptions Options { get; set; } = new BotOptions();

        public Action<String> OnMsg { get; set; }

        public void Run()
        {
            BotOptions options = Options;
            if (options == null) options = new BotOptions();
            ServerParams serverParams = options.ToServerParams();
            this.Server = new Server();
            this.Server.OnMsg = this.OnMsg;
            this.Server.server_start(serverParams);
        }
    }
}

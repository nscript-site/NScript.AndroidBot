using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NScript.AndroidBot
{
    using RestSharp;
    using Newtonsoft.Json;

    public class AtxAgentRequest
    {
        public String jsonrpc { get; set; } = "2.0";
        public String id { get; set; } = Guid.NewGuid().ToString().ToLower().Replace("-", "");
        public String method { get; set; } = "dumpWindowHierarchy";
        public object[] Params { get; set; } = { false, null };
    }

    public class AtxAgentResult
    {
        public String jsonrpc { get; set; }
        public String id { get; set; }
        public String result { get; set; }
    }

    public class UIAutomatorStatus
    {
        public bool running { get; set; }
        public bool success { get; set; }
    }

    public class AtxAgentServer
    {
        public UInt16 Port { get; private set; }
        
        private String ServerUrl;
        private RestClient RpcClient;
        private RestClient UIAutomatorClient;

        public AtxAgentServer(UInt16 port)
        {
            this.Port = port;
            ServerUrl = "http://127.0.0.1:" + port;
            RpcClient = new RestClient(ServerUrl + "/jsonrpc/0");
            UIAutomatorClient = new RestClient(ServerUrl + "/services/uiautomator");
        }

        public const String NoData = "[NO Data]";

        public String DumpUI()
        {
            AtxAgentRequest req = new AtxAgentRequest();
            String reqStr = JsonConvert.SerializeObject(req).Replace("Params","params");

            RestRequest request = new RestRequest(Method.POST);
            request.RequestFormat = DataFormat.Json;
            request.AddHeader("Content-Type", "application/json");
            request.AddBody(reqStr);
            var response = RpcClient.Execute(request);
            if (response.ErrorException != null) throw response.ErrorException;
            AtxAgentResult result = JsonConvert.DeserializeObject<AtxAgentResult>(response.Content);
            String str = result == null ? String.Empty : result.result;
            if (String.IsNullOrEmpty(str)) str = NoData;
            return str;
        }

        public bool Start()
        {
            RestRequest request = new RestRequest(Method.POST);
            var response = UIAutomatorClient.Execute(request);
            return response.ErrorException == null;
        }

        public void Stop()
        {
            RestRequest request = new RestRequest(Method.POST);
            var response = UIAutomatorClient.Execute(request);
            Console.WriteLine(response.Content);
        }

        public bool IsRunning()
        {
            try
            {
                RestRequest request = new RestRequest(Method.GET);
                var response = UIAutomatorClient.Execute(request);
                if (response.ErrorException != null) throw response.ErrorException;
                Console.WriteLine(response.Content);
                UIAutomatorStatus status = JsonConvert.DeserializeObject<UIAutomatorStatus>(response.Content);
                return status.running;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return false;
        }

        public bool WaitRunning(int timeOutMiniSeconds)
        {
            DateTime start = DateTime.Now;
            while(true)
            {
                try
                {
                    bool isRunning = IsRunning();
                    if (isRunning == true) return true;
                    else
                        Start();
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                TimeSpan ts = DateTime.Now - start;
                if (ts.TotalMilliseconds > timeOutMiniSeconds) return false;
                System.Threading.Thread.Sleep(3000);
            }
        }
    }
}

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

    public class AtxAgentServer
    {
        public UInt16 Port { get; private set; }
        
        private String ServerUrl;
        private RestClient Client;

        public AtxAgentServer(UInt16 port)
        {
            this.Port = port;
            ServerUrl = "http://127.0.0.1:" + port + "/jsonrpc/0";
            Client = new RestClient("http://127.0.0.1:8000/jsonrpc/0");
        }

        public String DumpUI()
        {
            AtxAgentRequest req = new AtxAgentRequest();
            String reqStr = JsonConvert.SerializeObject(req).Replace("Params","params");

            RestRequest request = new RestRequest(Method.POST);
            request.RequestFormat = DataFormat.Json;
            request.AddHeader("Content-Type", "application/json");
            request.AddBody(reqStr);
            var response = Client.Execute(request);
            if (response.ErrorException != null) throw response.ErrorException;
            AtxAgentResult result = JsonConvert.DeserializeObject<AtxAgentResult>(response.Content);
            return result.result;
        }
    }
}

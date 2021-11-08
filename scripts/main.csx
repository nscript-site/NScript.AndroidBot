#r "nuget: RestSharp, 106.13.0"
#r "nuget: Newtonsoft.Json, 13.0.1"

using RestSharp;
using Newtonsoft.Json;

String jsonStr = "{\"jsonrpc\": \"2.0\", \"id\": \""+ Guid.NewGuid().ToString().ToLower().Replace("-","") +"\", \"method\": \"dumpWindowHierarchy\", \"params\": [false, null]}";

class UiAmRequest
{
    public String jsonrpc { get;set; } = "2.0";
    public String id { get;set; } =  Guid.NewGuid().ToString().ToLower().Replace("-","");
    public String method { get;set; } = "dumpWindowHierarchy";
    public object[] Params { get;set; } = { false, null };
}

Console.WriteLine(jsonStr);

UiAmRequest ur = new UiAmRequest();
String urStr = JsonConvert.SerializeObject(ur).Replace("Params","params");
Console.WriteLine(urStr);

RestClient rc = new RestClient("http://127.0.0.1:8000/jsonrpc/0");
rc.AddDefaultHeader("Content-Type","application/json");
RestRequest request = new RestRequest(Method.POST);
request.RequestFormat = DataFormat.Json;
request.AddHeader("Content-Type", "application/json");
request.AddBody(urStr);
// request.AddJsonBody(ur);
// request.AddObject(urStr);
var response = rc.Execute(request);
if(response.ErrorException != null)
Console.WriteLine(response.ErrorException.ToString());

Console.WriteLine(response.Content);


Console.WriteLine("Hello world!");

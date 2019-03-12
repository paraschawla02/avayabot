
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Net;
using System.Threading;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Web;
using System.Net.Http;
using WebSocketSharp;
using Newtonsoft.Json.Linq;

namespace Microsoft.BotBuilderSamples
{
    public class WSConnection
    {
        private const string url = "ws://192.168.210.49:8081/CustomerControllerWeb/chat";
        private string authenticationKey;
        private ClientWebSocket ws;


        public WSConnection()
        {
            this.ws = new ClientWebSocket();
        }

        public async Task Connect()
        {
            while (ws.State != System.Net.WebSockets.WebSocketState.Open)
            {
                await ws.ConnectAsync(new Uri(url), CancellationToken.None);
                Console.WriteLine("Web socket : " + ws.State);
                Console.WriteLine("Sending connect request...");

                // Send the connect request and wait for the response
                await Send("connect");
                await Receive();
            }
        }

        public async Task Send(string type)
        {
            StringBuilder message = new StringBuilder();

            // We send a connect request
            if (type == "connect")
            {
              

             
                message.Append("{\"apiVersion\":\"1.0\",\"type\":\"request\",\"body\":{\"method\":\"requestChat\",\"guid\":null,\"authenticationKey\":null,\"deviceType\":\"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.110 Safari/537.36\",\"requestTranscript\":false,\"intrinsics\":{\"email\":\"\",\"name\":\"paras\",\"country\":\"+7 840\",\"area\":\"\",\"phoneNumber\":\"\",\"skillset\":\"WC_Default_Skillset\",\"customFields\":[{\"title\":\"address\",\"value\":\"\"}]}}}");


            }
            else
            {
                Console.WriteLine("No valid message type");
                return;
            }

            Console.WriteLine("Send message : " + message.ToString());
            var sendBuffer = new ArraySegment<Byte>(Encoding.UTF8.GetBytes(message.ToString()));
            await ws.SendAsync(sendBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task Receive()
        {
            ArraySegment<byte> receivedBytes = new ArraySegment<byte>(new byte[1024]);
          //  await dc.Context.SendActivityAsync("Please wait,we are connecting you to live agent.");
            WebSocketReceiveResult result = await ws.ReceiveAsync(receivedBytes, CancellationToken.None);
            Console.WriteLine(Encoding.UTF8.GetString(receivedBytes.Array, 0, result.Count));
        }
    }
}

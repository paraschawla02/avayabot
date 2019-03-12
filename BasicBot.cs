// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
    /// <summary>
    /// Main entry point and orchestration for bot.
    /// </summary>
    public class BasicBot : IBot
    {
        // Supported LUIS Intents
        static int agentchatstarted ;
        
        public const string GreetingIntent = "Greeting";
        public const string CancelIntent = "Cancel";
        public const string HelpIntent = "Help";
        public const string NoneIntent = "None";
        public const string AgentIntent = "Agent";
        public const string ResetPasswordIntent = "Password_reset";
        public const string TicketStatusIntent = "Ticket_Status";
        private static UTF8Encoding encoding = new UTF8Encoding();
        
        /// <summary>
        /// Key in the bot config (.bot file) for the LUIS instance.
        /// In the .bot file, multiple instances of LUIS can be configured.
        /// </summary>
        /// 
      private const string url = "ws://122.176.109.48:8081/CustomerControllerWeb/chat";
       // private const string url = "ws://192.168.210.49:8081/CustomerControllerWeb/chat";
        private const string queueurl = "http://122.176.109.48:8081/CustomerControllerWeb/currentqueue";
        private string authenticationKey;
        private static ClientWebSocket ws =new ClientWebSocket();

        public static readonly string LuisConfiguration = "BasicBotLuisApplication";

        private readonly IStatePropertyAccessor<PasswordResetState> _passwordresetStateAccessor;
        private readonly IStatePropertyAccessor<TicketStatusState> _ticketstatusStateAccessor;
        private readonly IStatePropertyAccessor<GreetingState> _greetingStateAccessor;
        private readonly IStatePropertyAccessor<DialogState> _dialogStateAccessor;
        private readonly UserState _userState;
        private readonly ConversationState _conversationState;
        private readonly BotServices _services;
       
        /// <summary>
        /// Initializes a new instance of the <see cref="BasicBot"/> class.
        /// </summary>
        /// <param name="botServices">Bot services.</param>
        /// <param name="accessors">Bot State Accessors.</param>
        public BasicBot(BotServices services, UserState userState, ConversationState conversationState, ILoggerFactory loggerFactory)
        {
            
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _userState = userState ?? throw new ArgumentNullException(nameof(userState));
            _conversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));
            
            _greetingStateAccessor = _userState.CreateProperty<GreetingState>(nameof(GreetingState));
            _dialogStateAccessor = _conversationState.CreateProperty<DialogState>(nameof(DialogState));
            _passwordresetStateAccessor = _userState.CreateProperty<PasswordResetState>(nameof(PasswordResetState));
            _ticketstatusStateAccessor = _userState.CreateProperty<TicketStatusState>(nameof(TicketStatusState));

            // Verify LUIS configuration.
            if (!_services.LuisServices.ContainsKey(LuisConfiguration))
            {
                throw new InvalidOperationException($"The bot configuration does not contain a service type of `luis` with the id `{LuisConfiguration}`.");
            }

            Dialogs = new DialogSet(_dialogStateAccessor);
            Dialogs.Add(new GreetingDialog(_greetingStateAccessor, loggerFactory));
            Dialogs.Add(new PasswordResetDialog(_passwordresetStateAccessor, loggerFactory));
            Dialogs.Add(new TicketStatusDialog(_ticketstatusStateAccessor, loggerFactory));
        }

        private DialogSet Dialogs { get; set; }

        /// <summary>
        /// Run every turn of the conversation. Handles orchestration of messages.
        /// </summary>
        /// <param name="turnContext">Bot Turn Context.</param>
        /// <param name="cancellationToken">Task CancellationToken.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var activity = turnContext.Activity;

            // Create a dialog context
            
              var  dc = await Dialogs.CreateContextAsync(turnContext);
            
            if (agentchatstarted == 1)
            {
                try
                {
                    await Send("data", turnContext.Activity.Text.ToString(), dc);
                   await Receive(dc);
                    return;
                }
                catch(Exception ex)
                {


                }
            }
            if (activity.Type == ActivityTypes.Message)
            {
                // Perform a call to LUIS to retrieve results for the current activity message.
                var luisResults = await _services.LuisServices[LuisConfiguration].RecognizeAsync(dc.Context, cancellationToken);
                agentchatstarted = 0;
                // If any entities were updated, treat as interruption.
                // For example, "no my name is tony" will manifest as an update of the name to be "tony".
                var topScoringIntent = luisResults?.GetTopScoringIntent();

                var topIntent = topScoringIntent.Value.intent;

                // update greeting state with any entities captured
                await UpdateGreetingState(luisResults, dc.Context);
                await UpdatePasswordResetState(luisResults, dc.Context);
                // Handle conversation interrupts first.
                var interrupted = await IsTurnInterruptedAsync(dc, topIntent);
                if (interrupted)
                {
                    // Bypass the dialog.
                    // Save state before the next turn.
                    await _conversationState.SaveChangesAsync(turnContext);
                    await _userState.SaveChangesAsync(turnContext);
                    return;
                }

                // Continue the current dialog
                var dialogResult = await dc.ContinueDialogAsync();

                // if no one has responded,
                if (!dc.Context.Responded)
                {
                    // examine results from active dialog
                    switch (dialogResult.Status)
                    {
                        case DialogTurnStatus.Empty:
                            switch (topIntent)
                            {
                                case GreetingIntent:
                                    await dc.BeginDialogAsync(nameof(GreetingDialog));
                                    break;
                                case ResetPasswordIntent:
                                    await dc.BeginDialogAsync(nameof(PasswordResetDialog));
                                    break;
                                case TicketStatusIntent:
                                    await dc.BeginDialogAsync(nameof(TicketStatusDialog));
                                    break;
                                case NoneIntent:
                                default:
                                    // Help or no intent identified, either way, let's provide some help.
                                    // to the user
                                    await dc.Context.SendActivityAsync("I didn't understand what you just said to me.");
                                    break;
                            }

                            break;

                        case DialogTurnStatus.Waiting:
                            // The active dialog is waiting for a response from the user, so do nothing.
                            break;

                        case DialogTurnStatus.Complete:
                            await dc.EndDialogAsync();
                            break;

                        default:
                            await dc.CancelAllDialogsAsync();
                            break;
                    }
                }
            }
            else if (activity.Type == ActivityTypes.ConversationUpdate)
            {
                if (activity.MembersAdded != null)
                {
                    // Iterate over all new members added to the conversation.
                    foreach (var member in activity.MembersAdded)
                    {
                        // Greet anyone that was not the target (recipient) of this message.
                        // To learn more about Adaptive Cards, see https://aka.ms/msbot-adaptivecards for more details.
                        if (member.Id != activity.Recipient.Id)
                        {
                            var welcomeCard = CreateAdaptiveCardAttachment();
                            var response = CreateResponse(activity, welcomeCard);
                            await dc.Context.SendActivityAsync(response);
                        }
                    }
                }
            }

            await _conversationState.SaveChangesAsync(turnContext);
            await _userState.SaveChangesAsync(turnContext);
        }

        // Determine if an interruption has occurred before we dispatch to any active dialog.
        private async Task<bool> IsTurnInterruptedAsync(DialogContext dc, string topIntent)
        {
            // See if there are any conversation interrupts we need to handle.
            if (topIntent.Equals(CancelIntent))
            {
                if (dc.ActiveDialog != null)
                {
                    await dc.CancelAllDialogsAsync();
                    await dc.Context.SendActivityAsync("Ok. I've canceled our last activity.");
                }
                else
                {
                    await dc.Context.SendActivityAsync("I don't have anything to cancel.");
                }

                return true;        // Handled the interrupt.
            }

            if (topIntent.Equals(HelpIntent))
            {
                await dc.Context.SendActivityAsync("Let me try to provide some help.");
                await dc.Context.SendActivityAsync("I understand greetings, being asked for help, or being asked to cancel what I am doing.");
            
                if (dc.ActiveDialog != null)
                {
                    await dc.RepromptDialogAsync();
                }

                return true;        // Handled the interrupt.
            }
            if (topIntent.Equals(AgentIntent))
            {
               

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(queueurl);
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";
                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string json = "{\"skillset\":\"" + "" + "\"}";
                    streamWriter.Write(json);
                    streamWriter.Flush();
                    streamWriter.Close();
                }
                try
                {
                    var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                    var result = "";
                    using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                         result = streamReader.ReadToEnd();
                    }
                    var jsonObj = JObject.Parse(result);
                    var values = (JArray)jsonObj["body"]["metrics"];
                    int agentinqueue=0;
                    foreach (var value in values)
                    {
                        agentinqueue = (int)value["availableAgentsInQueue"];
                        if(agentinqueue>0)
                        {
                            await dc.Context.SendActivityAsync("Please wait,we are connecting you to live agent.");
                            try
                            {

                                Connect(dc);

                            }
                            catch (Exception ex)
                            {

                            }
                        }
                        else
                        {
                            await dc.Context.SendActivityAsync("No agent available this time.Please try again later.");
                          
                        }
                    };
                }
                catch(Exception ex)
                { }
                if (dc.ActiveDialog != null)
                {
                    await dc.RepromptDialogAsync();
                }

                return true;        // Handled the interrupt.
            }
            return false;           // Did not handle the interrupt.
        }

    

        // Create an attachment message response.
        private Activity CreateResponse(Activity activity, Attachment attachment)
        {
            var response = activity.CreateReply();
            response.Attachments = new List<Attachment>() { attachment };
            return response;
        }

        // Load attachment from file.
        private Attachment CreateAdaptiveCardAttachment()
        {
            var adaptiveCard = File.ReadAllText(@".\Dialogs\Welcome\Resources\welcomeCard.json");
            return new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(adaptiveCard),
            };
        }

        /// <summary>
        /// Helper function to update greeting state with entities returned by LUIS.
        /// </summary>
        /// <param name="luisResult">LUIS recognizer <see cref="RecognizerResult"/>.</param>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        private async Task UpdateGreetingState(RecognizerResult luisResult, ITurnContext turnContext)
        {
            if (luisResult.Entities != null && luisResult.Entities.HasValues)
            {
                // Get latest GreetingState
                var greetingState = await _greetingStateAccessor.GetAsync(turnContext, () => new GreetingState());
                var entities = luisResult.Entities;

                // Supported LUIS Entities
                string[] userNameEntities = { "userName", "userName_patternAny" };
                string[] userLocationEntities = { "userLocation", "userLocation_patternAny" };

                // Update any entities
                // Note: Consider a confirm dialog, instead of just updating.
                foreach (var name in userNameEntities)
                {
                    // Check if we found valid slot values in entities returned from LUIS.
                    if (entities[name] != null)
                    {
                        // Capitalize and set new user name.
                        var newName = (string)entities[name][0];
                        greetingState.Name = char.ToUpper(newName[0]) + newName.Substring(1);
                        break;
                    }
                }

                foreach (var city in userLocationEntities)
                {
                    if (entities[city] != null)
                    {
                        // Capitalize and set new city.
                        var newCity = (string)entities[city][0];
                        greetingState.City = char.ToUpper(newCity[0]) + newCity.Substring(1);
                        break;
                    }
                }

                // Set the new values into state.
                await _greetingStateAccessor.SetAsync(turnContext, greetingState);
            }
        }

        /// <summary>
        /// Helper function to update greeting state with entities returned by LUIS.
        /// </summary>
        /// <param name="luisResult">LUIS recognizer <see cref="RecognizerResult"/>.</param>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        private async Task UpdatePasswordResetState(RecognizerResult luisResult, ITurnContext turnContext)
        {
            if (luisResult.Entities != null && luisResult.Entities.HasValues)
            {
                // Get latest GreetingState
                var passwordresetstate = await _passwordresetStateAccessor.GetAsync(turnContext, () => new PasswordResetState());
                var entities = luisResult.Entities;

                // Supported LUIS Entities
                string[] userotpEntities = { "OTP" };

                // Update any entities
                // Note: Consider a confirm dialog, instead of just updating.
                foreach (var otp in userotpEntities)
                {
                    // Check if we found valid slot values in entities returned from LUIS.
                    if (entities[otp] != null)
                    {
                        // Capitalize and set new user name.
                        var newotp = (string)entities[otp][0];
                        passwordresetstate.OTP = char.ToUpper(newotp[0]) + newotp.Substring(1);
                        break;
                    }
                }

             

                // Set the new values into state.
                await _passwordresetStateAccessor.SetAsync(turnContext, passwordresetstate);
            }
        }

        /// <summary>
        /// Helper function to update greeting state with entities returned by LUIS.
        /// </summary>
        /// <param name="luisResult">LUIS recognizer <see cref="RecognizerResult"/>.</param>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        private async Task UpdateTicketStatusState(RecognizerResult luisResult, ITurnContext turnContext)
        {
            if (luisResult.Entities != null && luisResult.Entities.HasValues)
            {
                // Get latest GreetingState
                var ticketstatusstate = await _ticketstatusStateAccessor.GetAsync(turnContext, () => new TicketStatusState());
                var entities = luisResult.Entities;

                // Supported LUIS Entities
                string[] userticketstatusEntities = { "TicketNumber" };


                // Update any entities
                // Note: Consider a confirm dialog, instead of just updating.
                foreach (var ticketnumber in userticketstatusEntities)
                {
                    // Check if we found valid slot values in entities returned from LUIS.
                    if (entities[ticketnumber] != null)
                    {
                        // Capitalize and set new user name.
                        var newticketnumber = (string)entities[ticketnumber][0];
                        ticketstatusstate.TicketNumber = char.ToUpper(newticketnumber[0]) + newticketnumber.Substring(1);
                        break;
                    }
                }



                // Set the new values into state.
                await _ticketstatusStateAccessor.SetAsync(turnContext, ticketstatusstate);
            }
        }

        public async Task Connect(DialogContext dc)
        {
            while (ws.State != System.Net.WebSockets.WebSocketState.Open)
            {
                try
                {
                    if(ws.State.ToString()=="Closed")
                    {
                        ws = new ClientWebSocket();
                    }
                    await ws.ConnectAsync(new Uri(url), CancellationToken.None);
                    Console.WriteLine("Web socket : " + ws.State);
                    await Task.WhenAll(Receive(dc), Send("connect", "dummy", dc));
                    await Receive(dc);
                    Console.WriteLine("Sending connect request...");
                }
                catch(Exception ex)
                {

                }

                while (true)
                {
                    Thread.Sleep(500);
                    await Send("ping", "dummy", dc);
                    await Receive(dc);
                }


            }
        }

        public async Task Send(string type,string senddata, DialogContext dc)
        {
            StringBuilder message = new StringBuilder();
          
            // We send a connect request
            if (type == "connect")
            {
                message.Append("{\"apiVersion\":\"1.0\",\"type\":\"request\",\"body\":{\"method\":\"requestChat\",\"guid\":null,\"authenticationKey\":null,\"deviceType\":\"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.110 Safari/537.36\",\"requestTranscript\":false,\"intrinsics\":{\"email\":\"\",\"name\":\"paras\",\"country\":\"+7 840\",\"area\":\"\",\"phoneNumber\":\"\",\"skillset\":\"WC_Default_Skillset\",\"customFields\":[{\"title\":\"address\",\"value\":\"\"}]}}}");
                agentchatstarted = 1;
                Console.WriteLine("Send message : " + message.ToString());
                var sendBuffer = new ArraySegment<Byte>(Encoding.UTF8.GetBytes(message.ToString()));
                await ws.SendAsync(sendBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                
                type = "ping";

            }
         
            if (type == "ping")
            {
                message.Append("{\"apiVersion\":\"1.0\",\"type\":\"request\",\"body\":{\"method\":\"ping\"}}");
                Console.WriteLine("Send message : " + message.ToString());
                var sendBuffer = new ArraySegment<Byte>(Encoding.UTF8.GetBytes(message.ToString()));
                await ws.SendAsync(sendBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                
                
            }
            if(type=="data")
            {
                try
                {
                    message.Append("{\"apiVersion\":\"1.0\",\"type\":\"request\",\"body\":{\"method\":\"newMessage\",\"message\":\""+senddata.ToString()+"\"}}");
                    Console.WriteLine("Send message : " + message.ToString());
                    var sendBuffer = new ArraySegment<Byte>(Encoding.UTF8.GetBytes(message.ToString()));
                    Console.WriteLine("Web socket : " + ws.State);
                    
                    await ws.SendAsync(sendBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Error in send data : " + ex.ToString());
                }
            }
            else
            {
                Console.WriteLine("No valid message type");
                return;
            }

            
        }

        public async Task Receive(DialogContext dc)
        {
            try
             {
                ArraySegment<byte> receivedBytes = new ArraySegment<byte>(new byte[1024]);
                WebSocketReceiveResult result = await ws.ReceiveAsync(receivedBytes, CancellationToken.None);

                Console.WriteLine(Encoding.UTF8.GetString(receivedBytes.Array, 0, result.Count));
                var jsonObj = JObject.Parse(Encoding.UTF8.GetString(receivedBytes.Array, 0, result.Count));
                string method = JObject.Parse(jsonObj.ToString())["body"]["method"].ToString();
               // string method = Convert.ToString((JArray)jsonObj["body"]["method"]);
              
               
                    if (method.ToString() == "newMessage")
                {
                    var message = JObject.Parse(jsonObj.ToString())["body"]["message"].ToString();
                    await dc.Context.SendActivityAsync(message.ToString());
                }
                else
                if (method.ToString() == "participantLeave")
                {
                    agentchatstarted = 0;
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure,"closing WS",CancellationToken.None);
                    Console.WriteLine("WebSocket closed."+ ws.CloseStatus);
                    ws.Abort();
                    
                    await dc.Context.SendActivityAsync("chat has been closed");
                }
                else
                    if (method.ToString() == "newParticipant")
                {
                    await dc.Context.SendActivityAsync("Agent joined.");
                }
                
            }
            catch (Exception ex)
            { }
        }
    }
}

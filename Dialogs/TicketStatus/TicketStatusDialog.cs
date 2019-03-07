// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace Microsoft.BotBuilderSamples
{
    /// <summary>
    /// Demonstrates the following concepts:
    /// - Use a subclass of ComponentDialog to implement a multi-turn conversation
    /// - Use a Waterflow dialog to model multi-turn conversation flow
    /// - Use custom prompts to validate user input
    /// - Store conversation and user state.
    /// </summary>
    public class TicketStatusDialog : ComponentDialog
    {
        // User state for greeting dialog
        private const string TicketStatusProperty = "ticketstatusState";
        private const string TicketNumber = "TicketNumber";
        private const string MobileNumber = "MobileNumber";

        // Prompts names
        private const string TicketNumberPrompt = "TicketNumberPrompt";
        private const string MobileNumberPrompt = "MobileNumberPrompt";

        // Minimum length requirements for city and name
        private const int ticketnumberminlen = 6;
        private const int mobilenumberminlen = 10;

        // Dialog IDs
        private const string ticketstatusDialog = "ticketstatusDialog";

        /// <summary>
        /// Initializes a new instance of the <see cref="PasswordResetDialog"/> class.
        /// </summary>
        /// <param name="botServices">Connected services used in processing.</param>
        /// <param name="botState">The <see cref="UserState"/> for storing properties at user-scope.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> that enables logging and tracing.</param>
        public TicketStatusDialog(IStatePropertyAccessor<TicketStatusState> ticketstatusStateAccessor, ILoggerFactory loggerFactory)
            : base(nameof(TicketStatusDialog))
        {
            TicketStatusAccessor = ticketstatusStateAccessor ?? throw new ArgumentNullException(nameof(ticketstatusStateAccessor));

            // Add control flow dialogs
            var waterfallSteps = new WaterfallStep[]
            {
                    InitializeStateStepAsync,
                    PromptforMobileNumberAsync,
                    PromptForTicketNumberAsync,
                   DisplayticketstatusStepAsync,

            };
            AddDialog(new WaterfallDialog(ticketstatusDialog, waterfallSteps));
            AddDialog(new TextPrompt(TicketNumberPrompt, ValidateTicketNumber));
            AddDialog(new TextPrompt(MobileNumberPrompt, ValidateMobileNumber));
        }

        public IStatePropertyAccessor<TicketStatusState> TicketStatusAccessor { get; }

        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var ticketstatus = await TicketStatusAccessor.GetAsync(stepContext.Context, () => null);
            if (ticketstatus == null)
            {
                var ticketstatusOpt = stepContext.Options as TicketStatusState;
                if (ticketstatusOpt != null)
                {
                    await TicketStatusAccessor.SetAsync(stepContext.Context, ticketstatusOpt);
                }
                else
                {
                    await TicketStatusAccessor.SetAsync(stepContext.Context, new TicketStatusState());
                }
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> PromptForTicketNumberAsync(
                                                WaterfallStepContext stepContext,
                                                CancellationToken cancellationToken)
        {
            var ticketstatusstate = await TicketStatusAccessor.GetAsync(stepContext.Context);

            // if we have everything we need, greet user and return.
            if (ticketstatusstate != null && !string.IsNullOrWhiteSpace(ticketstatusstate.TicketNumber))
            {
                return await TicketNumberSuccessfull(stepContext);
            }

            if (string.IsNullOrWhiteSpace(ticketstatusstate.TicketNumber))
            {
                // prompt for name, if missing
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = "Please enter Ticket Number to know its status ?",
                    },
                };
                return await stepContext.PromptAsync(TicketNumberPrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> PromptforMobileNumberAsync(
                                               WaterfallStepContext stepContext,
                                               CancellationToken cancellationToken)
        {
            var ticketstatusstate = await TicketStatusAccessor.GetAsync(stepContext.Context);

            // if we have everything we need, greet user and return.
            if (ticketstatusstate != null && !string.IsNullOrWhiteSpace(ticketstatusstate.MobileNumber))
            {
                return await MobileNumbersuccessful(stepContext);
            }

            if (string.IsNullOrWhiteSpace(ticketstatusstate.MobileNumber))
            {
                // prompt for name, if missing
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = "Please enter your registered Mobile Number with us ?",
                    },
                };
                return await stepContext.PromptAsync(MobileNumberPrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> DisplayticketstatusStepAsync(
                                                    WaterfallStepContext stepContext,
                                                    CancellationToken cancellationToken)
        {
            // Save city, if prompted.
            var ticketstatusstate = await TicketStatusAccessor.GetAsync(stepContext.Context);

            var lowerCaseCity = stepContext.Result as string;
            if (string.IsNullOrWhiteSpace(ticketstatusstate.TicketNumber) )
            {
               
                await TicketStatusAccessor.SetAsync(stepContext.Context, ticketstatusstate);
            }

            return await TicketNumberSuccessfull(stepContext);
        }



        /// <summary>
        /// Validator function to verify if the user name meets required constraints.
        /// </summary>
        /// <param name="promptContext">Context for this prompt.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        private async Task<bool> ValidateTicketNumber(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            // Validate that the user entered a minimum length for their name.
            var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;
            if (value.Length >= ticketnumberminlen)
            {
                promptContext.Recognized.Value = value;
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"OTP needs to be at least 6 characters long.");
                return false;
            }
        }

        /// <summary>
        /// Validator function to verify if the user name meets required constraints.
        /// </summary>
        /// <param name="promptContext">Context for this prompt.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        private async Task<bool> ValidateMobileNumber(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            // Validate that the user entered a minimum length for their name.
            var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;
            if (value.Length >= mobilenumberminlen)
            {
                promptContext.Recognized.Value = value;
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"Mobile Number needs to be at least 10 characters long.");
                return false;
            }
        }


        // Helper function to greet user with information in GreetingState.
        private async Task<DialogTurnResult> TicketNumberSuccessfull(WaterfallStepContext stepContext)
        {
            var context = stepContext.Context;
            var ticketstatusstate = await TicketStatusAccessor.GetAsync(context);

            // Display their profile information and end dialog.
            await context.SendActivityAsync($"your ticket is under process.Kindly contact +91XXXX for more details.Let me know in case of any query.");
            return await stepContext.EndDialogAsync();
        }

        // Helper function to greet user with information in GreetingState.
        private async Task<DialogTurnResult> MobileNumbersuccessful(WaterfallStepContext stepContext)
        {
            var context = stepContext.Context;
            var ticketstatusstate = await TicketStatusAccessor.GetAsync(context);

            // Display their profile information and end dialog.
            await context.SendActivityAsync($"Your number is registered with us.");
            return await stepContext.EndDialogAsync();
        }
    }
}

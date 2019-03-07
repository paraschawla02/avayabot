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
    public class PasswordResetDialog : ComponentDialog
    {
        // User state for greeting dialog
        private const string PasswordResetProperty = "passwordresetState";
        private const string OTP = "OTP";
        private const string MobileNumber = "MobileNumber";

        // Prompts names
        private const string OTPPrompt = "otpPrompt";
        private const string MobileNumberPrompt = "MobileNumberPrompt";

        // Minimum length requirements for city and name
        private const int passminlen = 6;
        private const int mobilenumberminlen = 10;

        // Dialog IDs
        private const string passwrdresetdialog = "passwordresetDialog";

        /// <summary>
        /// Initializes a new instance of the <see cref="PasswordResetDialog"/> class.
        /// </summary>
        /// <param name="botServices">Connected services used in processing.</param>
        /// <param name="botState">The <see cref="UserState"/> for storing properties at user-scope.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> that enables logging and tracing.</param>
        public PasswordResetDialog(IStatePropertyAccessor<PasswordResetState> passwordresetStateAccessor, ILoggerFactory loggerFactory)
            : base(nameof(PasswordResetDialog))
        {
            PassworsResetAccessor = passwordresetStateAccessor ?? throw new ArgumentNullException(nameof(passwordresetStateAccessor));

            // Add control flow dialogs
            var waterfallSteps = new WaterfallStep[]
            {
                    InitializeStateStepAsync,
                    PromptforMobileNumberAsync,
                    PromptForOTPStepAsync,
                   DisplaypasswordresetstateStepAsync,

            };
            AddDialog(new WaterfallDialog(passwrdresetdialog, waterfallSteps));
            AddDialog(new TextPrompt(OTPPrompt, ValidateOTP));
            AddDialog(new TextPrompt(MobileNumberPrompt, ValidateMobileNumber));
        }

        public IStatePropertyAccessor<PasswordResetState> PassworsResetAccessor { get; }

        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var passwordresetState = await PassworsResetAccessor.GetAsync(stepContext.Context, () => null);
            if (passwordresetState == null)
            {
                var passwordResetStateOpt = stepContext.Options as PasswordResetState;
                if (passwordResetStateOpt != null)
                {
                    await PassworsResetAccessor.SetAsync(stepContext.Context, passwordResetStateOpt);
                }
                else
                {
                    await PassworsResetAccessor.SetAsync(stepContext.Context, new PasswordResetState());
                }
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> PromptForOTPStepAsync(
                                                WaterfallStepContext stepContext,
                                                CancellationToken cancellationToken)
        {
            var passwordresetstate = await PassworsResetAccessor.GetAsync(stepContext.Context);

            // if we have everything we need, greet user and return.
            if (passwordresetstate != null && !string.IsNullOrWhiteSpace(passwordresetstate.OTP))
            {
                return await OTPsuccessful(stepContext);
            }

            if (string.IsNullOrWhiteSpace(passwordresetstate.OTP))
            {
                // prompt for name, if missing
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = "Please enter OTP sent to registered mobile number/Email ?",
                    },
                };
                return await stepContext.PromptAsync(OTPPrompt, opts);
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
            var passwordresetstate = await PassworsResetAccessor.GetAsync(stepContext.Context);

            // if we have everything we need, greet user and return.
            if (passwordresetstate != null && !string.IsNullOrWhiteSpace(passwordresetstate.MobileNumber))
            {
                return await MobileNumbersuccessful(stepContext);
            }

            if (string.IsNullOrWhiteSpace(passwordresetstate.MobileNumber))
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

        private async Task<DialogTurnResult> DisplaypasswordresetstateStepAsync(
                                                    WaterfallStepContext stepContext,
                                                    CancellationToken cancellationToken)
        {
            // Save city, if prompted.
            var passwordresetstate = await PassworsResetAccessor.GetAsync(stepContext.Context);

            var lowerCaseCity = stepContext.Result as string;
            if (string.IsNullOrWhiteSpace(passwordresetstate.OTP) )
            {
               
                await PassworsResetAccessor.SetAsync(stepContext.Context, passwordresetstate);
            }

            return await OTPsuccessful(stepContext);
        }



        /// <summary>
        /// Validator function to verify if the user name meets required constraints.
        /// </summary>
        /// <param name="promptContext">Context for this prompt.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        private async Task<bool> ValidateOTP(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            // Validate that the user entered a minimum length for their name.
            var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;
            if (value.Length >= passminlen)
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
        private async Task<DialogTurnResult> OTPsuccessful(WaterfallStepContext stepContext)
        {
            var context = stepContext.Context;
            var passwordresetState = await PassworsResetAccessor.GetAsync(context);

            // Display their profile information and end dialog.
            await context.SendActivityAsync($"your Password reset successfully.Is there any other query?");
            return await stepContext.EndDialogAsync();
        }

        // Helper function to greet user with information in GreetingState.
        private async Task<DialogTurnResult> MobileNumbersuccessful(WaterfallStepContext stepContext)
        {
            var context = stepContext.Context;
            var passwordresetState = await PassworsResetAccessor.GetAsync(context);

            // Display their profile information and end dialog.
            await context.SendActivityAsync($"Your number is registered with us.");
            return await stepContext.EndDialogAsync();
        }
    }
}

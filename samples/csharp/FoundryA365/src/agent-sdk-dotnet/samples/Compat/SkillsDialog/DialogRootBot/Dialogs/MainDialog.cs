// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Dialogs;
using Microsoft.Agents.Builder.Dialogs.Choices;
using Microsoft.Agents.Builder.Dialogs.Prompts;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Client;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Extensions.Configuration;

namespace DialogRootBot.Dialogs
{
    /// <summary>
    /// The main dialog for this bot. It uses a <see cref="SkillDialog"/> to call skills.
    /// </summary>
    public class MainDialog : ComponentDialog
    {
        // Constants used for selecting actions on the skill.
        private const string SkillActionBookFlight = "BookFlight";
        private const string SkillActionBookFlightWithInputParameters = "BookFlight with input parameters";
        private const string SkillActionGetWeather = "GetWeather";
        private const string SkillActionMessage = "Message";

        public static readonly string ActiveSkillPropertyName = $"{typeof(MainDialog).FullName}.ActiveSkillProperty";
        private readonly IStatePropertyAccessor<string> _activeSkillProperty;
        private readonly IAgentHost _agentHost;
        private readonly ConversationState _conversationState;
        
        private readonly string _selectedSkillKey = $"{typeof(MainDialog).FullName}.SelectedSkillKey";

        // Dependency injection uses this constructor to instantiate MainDialog.
        public MainDialog(IAgentHost agentHost, ConversationState conversationState)
            : base(nameof(MainDialog))
        {
            _agentHost = agentHost ?? throw new ArgumentNullException(nameof(agentHost));
            _conversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));

            // Use helper method to add SkillDialog instances for the configured skills.
            AddSkillDialogs();

            // Add ChoicePrompt to render available skills.
            AddDialog(new ChoicePrompt("SkillPrompt"));

            // Add ChoicePrompt to render skill actions.
            AddDialog(new ChoicePrompt("SkillActionPrompt", SkillActionPromptValidator));

            // Add main waterfall dialog for this bot.
            var waterfallSteps = new WaterfallStep[]
            {
                SelectSkillStepAsync,
                SelectSkillActionStepAsync,
                CallSkillActionStepAsync,
                FinalStepAsync
            };
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));

            // Create state property to track the active skill.
#pragma warning disable CS0618 // Type or member is obsolete
            _activeSkillProperty = conversationState.CreateProperty<string>(ActiveSkillPropertyName);
#pragma warning restore CS0618 // Type or member is obsolete

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        protected override async Task<DialogTurnResult> OnContinueDialogAsync(DialogContext innerDc, CancellationToken cancellationToken = default)
        {
            // This is an example on how to cancel a SkillDialog that is currently in progress from the parent bot.
            var activeSkill = await _activeSkillProperty.GetAsync(innerDc.Context, () => null, cancellationToken);
            var activity = innerDc.Context.Activity;
            if (activeSkill != null && activity.Type == ActivityTypes.Message && activity.Text.Equals("abort", StringComparison.OrdinalIgnoreCase))
            {
                // Cancel all dialogs when the user says abort.
                // The SkillDialog automatically sends an EndOfConversation message to the skill to let the
                // skill know that it needs to end its current dialogs, too.
                await innerDc.CancelAllDialogsAsync(cancellationToken);
                return await innerDc.ReplaceDialogAsync(InitialDialogId, "Canceled! \n\n What skill would you like to call?", cancellationToken);
            }

            return await base.OnContinueDialogAsync(innerDc, cancellationToken);
        }

        // Render a prompt to select the skill to call.
        private async Task<DialogTurnResult> SelectSkillStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Create the PromptOptions from the skill configuration which contain the list of configured skills.
            var messageText = stepContext.Options?.ToString() ?? "What skill would you like to call?";
            var repromptMessageText = "That was not a valid choice, please select a valid skill.";
            var options = new PromptOptions
            {
                Prompt = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput),
                RetryPrompt = MessageFactory.Text(repromptMessageText, repromptMessageText, InputHints.ExpectingInput),
                Choices = _agentHost.GetAgents().Select(skill => new Choice(skill.Name)).ToList()
            };

            // Prompt the user to select a skill.
            return await stepContext.PromptAsync("SkillPrompt", options, cancellationToken);
        }

        // Render a prompt to select the action for the skill.
        private async Task<DialogTurnResult> SelectSkillActionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Get the skill info based on the selected skill.
            var selectedSkillName = ((FoundChoice)stepContext.Result).Value;

            // Remember the skill selected by the user.
            stepContext.Values[_selectedSkillKey] = selectedSkillName;

            // Create the PromptOptions with the actions supported by the selected skill.
            var messageText = $"Select an action # to send to **{selectedSkillName}** or just type in a message and it will be forwarded to the skill";
            var options = new PromptOptions
            {
                Prompt = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput),
                Choices = GetSkillActions(selectedSkillName)
            };

            // Prompt the user to select a skill action.
            return await stepContext.PromptAsync("SkillActionPrompt", options, cancellationToken);
        }

        // This validator defaults to Message if the user doesn't select an existing option.
        private Task<bool> SkillActionPromptValidator(PromptValidatorContext<FoundChoice> promptContext, CancellationToken cancellationToken)
        {
            if (!promptContext.Recognized.Succeeded)
            {
                // Assume the user wants to send a message if an item in the list is not selected.
                promptContext.Recognized.Value = new FoundChoice { Value = SkillActionMessage };
            }

            return Task.FromResult(true);
        }

        // Starts the SkillDialog based on the user's selections.
        private async Task<DialogTurnResult> CallSkillActionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var selectedSkill = (string) stepContext.Values[_selectedSkillKey];

            IActivity skillActivity;
            switch (selectedSkill)
            {
                case "DialogSkillBot":
                    skillActivity = CreateDialogSkillBotActivity(((FoundChoice)stepContext.Result).Value, stepContext.Context);
                    break;

                // We can add other case statements here if we support more than one skill.
                default:
                    throw new Exception($"Unknown target skill id: {selectedSkill}.");
            }

            // Create the BeginSkillDialogOptions and assign the activity to send.
            var skillDialogArgs = new BeginSkillDialogOptions { Activity = skillActivity };

            // Save active skill in state.
            await _activeSkillProperty.SetAsync(stepContext.Context, selectedSkill, cancellationToken);

            // Start the skillDialog instance with the arguments. 
            return await stepContext.BeginDialogAsync(selectedSkill, skillDialogArgs, cancellationToken);
        }

        // The SkillDialog has ended, render the results (if any) and restart MainDialog.
        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var activeSkill = await _activeSkillProperty.GetAsync(stepContext.Context, () => null, cancellationToken);

            // Check if the skill returned any results and display them.
            if (stepContext.Result != null)
            {
                var message = $"Skill \"{activeSkill}\" invocation complete.";
                message += $" Result: {ProtocolJsonSerializer.ToJson(stepContext.Result)}";
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(message, message, inputHint: InputHints.IgnoringInput), cancellationToken: cancellationToken);
            }

            // Clear the skill selected by the user.
            stepContext.Values[_selectedSkillKey] = null;

            // Clear active skill in state.
            await _activeSkillProperty.DeleteAsync(stepContext.Context, cancellationToken);

            // Restart the main dialog with a different message the second time around.
            return await stepContext.ReplaceDialogAsync(InitialDialogId, $"Done with \"{activeSkill}\". \n\n What skill would you like to call?", cancellationToken);
        }

        // Helper method that creates and adds SkillDialog instances for the configured skills.
        private void AddSkillDialogs()
        {
            foreach (var skillInfo in _agentHost.GetAgents())
            {
                // Create the dialog options.
                var skillDialogOptions = new SkillDialogOptions
                {
                    AgentHost = _agentHost,
                    ConversationState = _conversationState,
                    Skill = skillInfo.Name
                };

                // Add a SkillDialog for the selected skill.
                AddDialog(new SkillDialog(skillDialogOptions, skillInfo.Name));
            }
        }

        // Helper method to create Choice elements for the actions supported by the skill.
        private IList<Choice> GetSkillActions(string skillName)
        {
            // Note: the bot would probably render this by reading the skill manifest.
            // We are just using hardcoded skill actions here for simplicity.

            var choices = new List<Choice>();
            switch (skillName)
            {
                case "DialogSkillBot":
                    choices.Add(new Choice(SkillActionBookFlight));
                    choices.Add(new Choice(SkillActionBookFlightWithInputParameters));
                    choices.Add(new Choice(SkillActionGetWeather));
                    break;
            }

            return choices;
        }

        // Helper method to create the activity to be sent to the DialogSkillBot using selected type and values.
        private IActivity CreateDialogSkillBotActivity(string selectedOption, ITurnContext turnContext)
        {
            // Note: in a real bot, the dialogArgs will be created dynamically based on the conversation
            // and what each action requires; here we hardcode the values to make things simpler.

            IActivity activity = null;
            // Just forward the message activity to the skill with whatever the user said. 
            if (selectedOption.Equals(SkillActionMessage, StringComparison.OrdinalIgnoreCase))
            {
                // Note message activities also support input parameters but we are not using them in this example.
                // Return a deep clone of the activity so we don't risk altering the original one 
                activity = turnContext.Activity.Clone();
            }

            // Send an event activity to the skill with "BookFlight" in the name.
            if (selectedOption.Equals(SkillActionBookFlight, StringComparison.OrdinalIgnoreCase))
            {
                activity = (Activity)Activity.CreateEventActivity();
                activity.Name = SkillActionBookFlight;
            }

            // Send an event activity to the skill with "BookFlight" in the name and some testing values.
            if (selectedOption.Equals(SkillActionBookFlightWithInputParameters, StringComparison.OrdinalIgnoreCase))
            {
                activity = (Activity)Activity.CreateEventActivity();
                activity.Name = SkillActionBookFlight;
                activity.Value = new { origin = "New York", destination = "Seattle" };
            }

            // Send an event activity to the skill with "GetWeather" in the name and some testing values.
            if (selectedOption.Equals(SkillActionGetWeather, StringComparison.OrdinalIgnoreCase))
            {
                activity = (Activity)Activity.CreateEventActivity();
                activity.Name = SkillActionGetWeather;
                activity.Value = new { latitude = 47.614891, longitude = -122.195801};
            }

            if (activity == null)
            {
                throw new Exception($"Unable to create a skill activity for \"{selectedOption}\".");
            }

            // We are manually creating the activity to send to the skill; ensure we add the ChannelData and Properties 
            // from the original activity so the skill gets them.
            // Note: this is not necessary if we are just forwarding the current activity from context. 
            activity.ChannelData = turnContext.Activity.ChannelData;
            activity.Properties = turnContext.Activity.Properties;

            return activity;
        }
    }
}

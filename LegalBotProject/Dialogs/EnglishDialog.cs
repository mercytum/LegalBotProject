using LegalBotProject.Models;
using LegalBotProject.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema.Teams;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LegalBotProject.Dialogs
{
    public class EnglishDialog : ComponentDialog
    {
        #region Variables
        private readonly BotStateService _botStateService;
        #endregion

        public EnglishDialog(string dialogId, BotStateService botStateService) : base(dialogId)
        {
            _botStateService = botStateService ?? throw new System.ArgumentNullException(nameof(botStateService));

            InitializeWaterfallDialog();
        }

        private void InitializeWaterfallDialog()
        {
            //Create Waterfall Steps
            var waterfallSteps = new WaterfallStep[]
            {
                InitialStepAsync,
                CountyStepAsync,
                SubCountyStepAsync,
                WardStepAsync,
                PhoneNumberStepAsync,
                FinalStepAsync
            };

            //Add Named Dialogs
            AddDialog(new WaterfallDialog($"{nameof(EnglishDialog)}.mainFlow", waterfallSteps));
            AddDialog(new TextPrompt($"{nameof(EnglishDialog)}.name"));
            AddDialog(new ChoicePrompt($"{nameof(EnglishDialog)}.county"));
            AddDialog(new ChoicePrompt($"{nameof(EnglishDialog)}.subcounty"));
            AddDialog(new ChoicePrompt($"{nameof(EnglishDialog)}.ward"));
            AddDialog(new TextPrompt($"{nameof(EnglishDialog)}.phoneNumber", MobileNumberValidation));



            //Set the starting Dialog
            InitialDialogId = $"{nameof(EnglishDialog)}.mainFlow";
        }

        private async Task<DialogTurnResult> InitialStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            UserProfile userProfile = await _botStateService.UserProfileAccessor.GetAsync(stepContext.Context, () => new UserProfile());

            if (string.IsNullOrEmpty(userProfile.Name))
            {
                return await stepContext.PromptAsync($"{nameof(EnglishDialog)}.name",
                    new PromptOptions
                    {

                        Prompt = MessageFactory.Text("Welcome to our service, we would like to ask you a few questions for the purpose of registration." +
                                                     " What is your full name?")
                    }, cancellationToken);
            }
            else
            {
                return await stepContext.NextAsync(null, cancellationToken);
            }
        }


        private async Task<DialogTurnResult> CountyStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["name"] = (string)stepContext.Result;
            var counties = new List<string>();
            var filterkey = "county";
            counties = GetLocations(filterkey);
            return await stepContext.PromptAsync($"{nameof(EnglishDialog)}.county",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Which county do you live in?"),
                    Choices = ChoiceFactory.ToChoices(counties)
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> SubCountyStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var subcounties = new List<string>();
            var filterkey = "constituency";
            var filtervalue = "county";
            var value = ((FoundChoice)stepContext.Result).Value;
            subcounties = GetLocations(filterkey, filtervalue, value);

            stepContext.Values["county"] = value;


            return await stepContext.PromptAsync($"{nameof(EnglishDialog)}.subcounty", new PromptOptions
            {
                Prompt = MessageFactory.Text("Which subcounty are you located at?"),
                Choices = ChoiceFactory.ToChoices(subcounties),
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> WardStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var wards = new List<string>();
            var filterkey = "ward";
            var filtervalue = "constituency";
            var value = ((FoundChoice)stepContext.Result).Value;

            wards = GetLocations(filterkey, filtervalue, value);

            stepContext.Values["subcounty"] = value;

            return await stepContext.PromptAsync($"{nameof(EnglishDialog)}.ward",
                 new PromptOptions
                 {
                     Prompt = MessageFactory.Text("Which ward do you live in?"),
                     Choices = ChoiceFactory.ToChoices(wards),
                 }, cancellationToken);

        }

        private async Task<DialogTurnResult> PhoneNumberStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["ward"] = ((FoundChoice)stepContext.Result).Value;

            return await stepContext.PromptAsync($"{nameof(EnglishDialog)}.phoneNumber",
                new PromptOptions
                {

                    Prompt = MessageFactory.Text("Please enter the primary phone number that we can use to get in touch with you"),
                    RetryPrompt = MessageFactory.Text("Please enter a valid  phone number"),
                }, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["phonenumber"] = (string)stepContext.Result;

            UserProfile userProfile = await _botStateService.UserProfileAccessor.GetAsync(stepContext.Context, () => new UserProfile());

            //save all the data inside the user profile
            userProfile.Name = (string)stepContext.Values["name"];
            userProfile.County = (string)stepContext.Values["county"];
            userProfile.SubCounty = (string)stepContext.Values["subcounty"];
            userProfile.Ward = (string)stepContext.Values["ward"];
            userProfile.PhoneNumber = (string)stepContext.Values["phonenumber"];

            //output message to user.

            var message = $"Congratulations {userProfile.Name}! You are now registered to use our service...Please choose(1. MAIN MENU) to continue using the service";


            await stepContext.Context.SendActivityAsync(MessageFactory.Text(message), cancellationToken);

            //send user details to a json file
            var userDetails = JsonConvert.SerializeObject(userProfile, Formatting.Indented);
            var filePath = @".\JSON\UserDetails.json";
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, userDetails);
            }
            else
            {
                File.AppendAllText(filePath, userDetails);
            }

            //save data in usersate
            await _botStateService.UserProfileAccessor.SetAsync(stepContext.Context, userProfile);





            //waterfallstep always finishes with the end of the waterfall or with another dialog, here it is end
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);

        }

        //method to get location county/constituency/wards
        private List<string> GetLocations(string administrativeKey, string filterKey = null, string filterValue = null)
        {
            //Source


            var file = @".\JSON\location.json";

            //Read values and convert to json

            var locations = JsonConvert.DeserializeObject<JArray>(File.ReadAllText(file));

            //list to contain location after filtering
            var items = new List<string>();

            //Loop through array

            foreach (var obj in locations)
            {
                if (filterKey != null && obj.Value<string>(filterKey) != filterValue)
                {
                    continue;
                }

                items.Add(obj.Value<string>(administrativeKey));
            }

            return items.Distinct().ToList();

        }

        //method to validate the phone number
        private async Task<bool> MobileNumberValidation(PromptValidatorContext<string> promptcontext, CancellationToken cancellationtoken)
        {
            if (!promptcontext.Recognized.Succeeded)
            {
                await promptcontext.Context.SendActivityAsync("Hello, Please enter a valid mobile number",
                    cancellationToken: cancellationtoken);

                return false;
            }

            int count = Convert.ToString(promptcontext.Recognized.Value).Length;
            if (count != 10)
            {
                await promptcontext.Context.SendActivityAsync("Hello , you are missing some numbers. Please try again.",
                    cancellationToken: cancellationtoken);
                return false;
            }

            return true;
        }

    }

}




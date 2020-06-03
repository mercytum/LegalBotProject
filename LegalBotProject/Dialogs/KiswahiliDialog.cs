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
    public class KiswahiliDialog : ComponentDialog
    {
        #region Variables
        private readonly BotStateService _botStateService;
        #endregion

        public KiswahiliDialog(string dialogId, BotStateService botStateService) : base(dialogId)
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
            AddDialog(new WaterfallDialog($"{nameof(KiswahiliDialog)}.mainFlow", waterfallSteps));
            AddDialog(new TextPrompt($"{nameof(KiswahiliDialog)}.name"));
            AddDialog(new ChoicePrompt($"{nameof(KiswahiliDialog)}.county"));
            AddDialog(new ChoicePrompt($"{nameof(KiswahiliDialog)}.subcounty"));
            AddDialog(new ChoicePrompt($"{nameof(KiswahiliDialog)}.ward"));
            AddDialog(new TextPrompt($"{nameof(KiswahiliDialog)}.phoneNumber", MobileNumberValidation));



            //Set the starting Dialog
            InitialDialogId = $"{nameof(KiswahiliDialog)}.mainFlow";
        }

        private async Task<DialogTurnResult> InitialStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            UserProfile userProfile = await _botStateService.UserProfileAccessor.GetAsync(stepContext.Context, () => new UserProfile());

            if (string.IsNullOrEmpty(userProfile.Name))
            {
                return await stepContext.PromptAsync($"{nameof(KiswahiliDialog)}.name",
                    new PromptOptions
                    {

                        Prompt = MessageFactory.Text("Karibu katika huduma yetu, tunapenda kukuuliza maswali machache kwa madhumuni ya usajili." +
                                                     "Jina lako kamili ni nini ? ")
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
            return await stepContext.PromptAsync($"{nameof(KiswahiliDialog)}.county",
                new PromptOptions
                {
                    Prompt = MessageFactory.Text("Unaishi kaunti gani?"),
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


            return await stepContext.PromptAsync($"{nameof(KiswahiliDialog)}.subcounty", new PromptOptions
            {
                Prompt = MessageFactory.Text("Je! Uko kaunti gani ndogo?"),
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

            return await stepContext.PromptAsync($"{nameof(KiswahiliDialog)}.ward",
                 new PromptOptions
                 {
                     Prompt = MessageFactory.Text("Unaishi katika wadi gani?"),
                     Choices = ChoiceFactory.ToChoices(wards),
                 }, cancellationToken);

        }

        private async Task<DialogTurnResult> PhoneNumberStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["ward"] = ((FoundChoice)stepContext.Result).Value;

            return await stepContext.PromptAsync($"{nameof(KiswahiliDialog)}.phoneNumber",
                new PromptOptions
                {

                    Prompt = MessageFactory.Text("Tafadhali ingiza nambari ya simu ya msingi ambayo tunaweza kutumia kuwasiliana nawe"),
                    RetryPrompt = MessageFactory.Text("Tafadhali ingiza nambari halali ya simu"),
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

            var message = $"Hongera { userProfile.Name}! Umesajiliwa kutumia huduma yetu ... Tafadhali chagua (1. MAIN MENU) kuendelea kutumia huduma";


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
                await promptcontext.Context.SendActivityAsync("Habari, Tafadhali ingiza nambari ya simu halali",
                    cancellationToken: cancellationtoken);

                return false;
            }

            int count = Convert.ToString(promptcontext.Recognized.Value).Length;
            if (count != 10)
            {
                await promptcontext.Context.SendActivityAsync("Unakosa nambari kadhaa. Tafadhali jaribu tena.",
                    cancellationToken: cancellationtoken);
                return false;
            }

            return true;
        }

    }

}




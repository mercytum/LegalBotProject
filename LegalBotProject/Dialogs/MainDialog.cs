using LegalBotProject.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LegalBotProject.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        #region Variables
        private readonly BotStateService _botStateService;
        #endregion

        public MainDialog(BotStateService botStateService) : base(nameof(MainDialog))
        {
            _botStateService = botStateService ?? throw new System.ArgumentNullException(nameof(botStateService));

            InitializeWaterfallDialog();
        }

        public void InitializeWaterfallDialog()
        {
            //Create Waterfall Steps
            var waterfallSteps = new WaterfallStep[]
            {
                InitialStepAsync,
                FinalStepAsync,
            };

            //Add Named Dialogs
            AddDialog(new EnglishDialog($"{nameof(MainDialog)}.greetingEnglish", _botStateService));
            AddDialog(new KiswahiliDialog($"{nameof(MainDialog)}.greetingKiswahili", _botStateService));

            AddDialog(new WaterfallDialog($"{nameof(MainDialog)}.mainFlow", waterfallSteps));

            //Set the starting Dialog
            InitialDialogId = $"{nameof(MainDialog)}.mainFlow";
        }

        private async Task<DialogTurnResult> InitialStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (Regex.Match(stepContext.Context.Activity.Text.ToLower(), "2").Success)
            {
                return await stepContext.BeginDialogAsync($"{nameof(MainDialog)}.greetingEnglish", null, cancellationToken);
            }
            else if (Regex.Match(stepContext.Context.Activity.Text.ToLower(), "1").Success)
            {

                return await stepContext.BeginDialogAsync($"{nameof(MainDialog)}.greetingKiswahili", null, cancellationToken);
                
            }
            else
            {
                return await stepContext.NextAsync(null, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.EndDialogAsync(null, cancellationToken);
        }
    }
}

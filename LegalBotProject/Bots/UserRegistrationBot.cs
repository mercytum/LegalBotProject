using LegalBotProject.Helpers;
using LegalBotProject.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LegalBotProject.Bots
{
    public class UserRegistrationBot<T> : ActivityHandler where T : Dialog
    {
        #region Variables
        protected readonly Dialog _dialog;
        protected readonly BotStateService _botStateService;
        protected readonly ILogger _logger;
        #endregion

        public UserRegistrationBot(BotStateService botStateService, T dialog, ILogger<UserRegistrationBot<T>> logger)
        {
            _botStateService = botStateService ?? throw new System.ArgumentNullException(nameof(botStateService));
            _dialog = dialog ?? throw new System.ArgumentNullException(nameof(dialog));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));

        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync($"Hi thereHujambo, karibu katika huduma yetu. Tafadhali chagua lugha inayokufaa\n " +
                                                        $"\n(1. KISWAHILI), (2. KINGEREZA)\n\n " +

                                                        $"\n\nHello, welcome to our service.Please choose your preferred language\n" +
                                                        $"\n(1.KISWAHILI), (2.ENGLISH)", 
                                                        cancellationToken: cancellationToken);
                }
            }
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            //Save any state changes that might have occured during the turn
            await _botStateService.UserState.SaveChangesAsync(turnContext, false, cancellationToken);
            await _botStateService.ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Running dialog with Message activity.");

            //Run the Dialog with the new message Activity
            await _dialog.Run(turnContext, _botStateService.DialogStateAccessor, cancellationToken);
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with EchoBot .NET Template version v4.17.1

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using System;
using Azure.AI.OpenAI;

namespace EchoBot.Bots
{
    public class EchoBot : ActivityHandler
    {
        private readonly OpenAIClient chatClient;
        private readonly ConversationState conversationState;

        public EchoBot(IConfiguration configuration, ConversationState conversationState)
        {
            this.chatClient = new OpenAIClient("{{api-key}}");
            this.conversationState = conversationState;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var accessor = this.conversationState.CreateProperty<List<Tuple<string, string>>>(nameof(ChatMessage));
            var messages = await accessor.GetAsync(turnContext, () => new(), cancellationToken);
            while (messages.Count > 8)
            {
                messages.RemoveAt(0);
            }
            var chatCompletionsOptions = new ChatCompletionsOptions();
            chatCompletionsOptions.Messages.Add(new ChatMessage(
                ChatRole.System,
                "あなたは Microsoft Bot Framework から呼び出されるアシスタントです。ユーザーからの質問に回答してください。"
            ));
            foreach (var message in messages)
            {
                chatCompletionsOptions.Messages.Add(new ChatMessage(
                    new ChatRole(message.Item1),
                    message.Item2
                ));
            }
            chatCompletionsOptions.Messages.Add(new ChatMessage(ChatRole.User, turnContext.Activity.Text));
            var chatCompletion = await this.chatClient.GetChatCompletionsAsync(
                "gpt-3.5-turbo",
                chatCompletionsOptions,
                cancellationToken
            );
            var replyText = chatCompletion.Value.Choices[0].Message.Content;
            await turnContext.SendActivityAsync(MessageFactory.Text(replyText, replyText), cancellationToken);
            messages.Add(new Tuple<string, string>(ChatRole.User.ToString(), turnContext.Activity.Text));
            messages.Add(new Tuple<string, string>(ChatRole.Assistant.ToString(), replyText));
            await accessor.SetAsync(turnContext, messages, cancellationToken);
            await this.conversationState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Hello and welcome!";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }
    }
}

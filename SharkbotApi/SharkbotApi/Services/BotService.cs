﻿using ChatAnalyzer.Services;
using ChatModels;
using ConversationDatabase.Services;
using SharkbotApi.Models;
using SharkbotReplier.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SharkbotApi.Services
{
    public class BotService
    {
        private ConversationService conversationService;
        private AnalyzationService analyzationService;
        private ResponseService responseService;
        private ConversationUpdateService covnersationUpdateService;
        private UserService.UserService userService;

        public BotService()
        {
            conversationService = new ConversationService();
            analyzationService = new AnalyzationService();
            responseService = new ResponseService();
            covnersationUpdateService = new ConversationUpdateService();
            userService = new UserService.UserService();
        }

        public ChatResponse GetResponse(ChatRequest chat)
        {
            if (chat.requestTime == null)
            {
                chat.requestTime = DateTime.Now;
            }

            var queueItem = new ConversationQueueItem { ConversationName = chat.conversationName, RequestTime = chat.requestTime };

            if(!ConversationTracker.activeConversationNames.Contains(queueItem))
            {
                ConversationTracker.activeConversationNames.Add(queueItem);
            }

            if (ConversationTracker.activeConversationNames.Any(i => i.ConversationName == chat.conversationName && i.RequestTime < chat.requestTime))
            {
                return Task.Delay(1000).ContinueWith((task) => { return GetResponse(chat); }).Result;
            }

            var processedChat = ProcessChat(chat);

            ConversationTracker.activeConversationNames.Remove(queueItem);

            return processedChat;
        }

        public bool UpdateConversation(ChatRequest chat)
        {
            if (chat.requestTime == null)
            {
                chat.requestTime = DateTime.Now;
            }

            var queueItem = new ConversationQueueItem { ConversationName = chat.conversationName, RequestTime = chat.requestTime };

            if (!ConversationTracker.activeConversationNames.Contains(queueItem))
            {
                ConversationTracker.activeConversationNames.Add(queueItem);
            }

            if (ConversationTracker.activeConversationNames.Any(i => i.ConversationName == chat.conversationName && i.RequestTime < chat.requestTime))
            {
                return Task.Delay(1000).ContinueWith((task) => { return UpdateConversation(chat); }).Result;
            }

            var updated = UpdateDatabases(chat);

            ConversationTracker.activeConversationNames.Remove(queueItem);

            return updated;
        }

        private ChatResponse ProcessChat(ChatRequest chat)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var conversation = conversationService.GetConversation(chat);
            stopwatch.Stop();
            Debug.WriteLine("GetConversation " + stopwatch.Elapsed);

            stopwatch.Start();
            var analyzedConversation = analyzationService.AnalyzeConversation(conversation);
            stopwatch.Stop();
            Debug.WriteLine("AnalyzeConversation " + stopwatch.Elapsed);

            stopwatch.Start();
            var conversationUdpdated = covnersationUpdateService.UpdateConversation(analyzedConversation, chat.type);
            stopwatch.Stop();
            Debug.WriteLine("UpdateConversation " + stopwatch.Elapsed);

            stopwatch.Start();
            userService.UpdateUsers(analyzedConversation.responses.Last());
            stopwatch.Stop();
            Debug.WriteLine("UpdateUser " + stopwatch.Elapsed);

            stopwatch.Start();
            var response = responseService.GetResponse(analyzedConversation);
            stopwatch.Stop();
            Debug.WriteLine("GetResponse " + stopwatch.Elapsed);

            return response;
        }

        private bool UpdateDatabases(ChatRequest chat)
        {
            var conversation = conversationService.GetConversation(chat);
            var analyzedConversation = analyzationService.AnalyzeConversation(conversation);
            var conversationUdpdated = covnersationUpdateService.UpdateConversation(analyzedConversation, chat.type);
            userService.UpdateUsers(analyzedConversation.responses.Last());

            return conversationUdpdated;
        }
    }
}
﻿using ChatModels;
using ConversationMatcher.Services;
using System.Collections.Generic;
using System.Linq;

namespace SharkbotReplier.Services
{
    public class ConversationMatchService
    {
        private BestMatchService bestMatchService;

        public ConversationMatchService()
        {
            bestMatchService = new BestMatchService();
        }

        public MatchChat GetConversationMatch(Conversation conversation, List<string> excludedTypes)
        {
            var conversationLists = ConversationDatabase.ConversationDatabase.conversationDatabase.Where(cl => !excludedTypes.Any(t => cl.type == t)).ToList();
            var conversationMatchRequest = new ConversationMatchRequest { conversation = conversation, conversationLists = conversationLists };
            return bestMatchService.GetBestMatch(conversationMatchRequest.conversation, conversationMatchRequest.conversationLists);
        }

        public MatchChat GetConversationMatch(Conversation conversation, List<string> requiredTypes, List<string> requiredProperyMatches, List<string> excludedTypes)
        {
            var conversationLists = ConversationDatabase.ConversationDatabase.conversationDatabase.Where(cl => !excludedTypes.Any(t => cl.type == t)).ToList();
            if (requiredTypes.Count > 0)
            {
                conversationLists = conversationLists.Where(cl => requiredTypes.Any(t => cl.type == t)).ToList();
            }
            
            if(requiredProperyMatches.Count > 0)
            {
                var userData = UserDatabase.UserDatabase.userDatabase.Where(user => user.userName == conversation.responses.Last().chat.user && requiredProperyMatches.All(requiredProperty => user.derivedProperties.Any(dp => dp.name == requiredProperty))).FirstOrDefault();
                if(userData != null)
                {
                    var propertiesToMatch = userData.derivedProperties.Where(dp => requiredProperyMatches.Contains(dp.name));
                    var matchingUsers = UserDatabase.UserDatabase.userDatabase.Where(user => propertiesToMatch.All(ptm => user.derivedProperties.Any(dp => dp.name == ptm.name && dp.value == ptm.value))).ToList();

                    var matchRequest = new ConversationMatchRequest { conversation = conversation, conversationLists = conversationLists };
                    return bestMatchService.GetBestMatch(matchRequest.conversation, matchRequest.conversationLists, matchingUsers);
                }             
            }
            var conversationMatchRequest = new ConversationMatchRequest { conversation = conversation, conversationLists = conversationLists };
            return bestMatchService.GetBestMatch(conversationMatchRequest.conversation, conversationMatchRequest.conversationLists);
        }
    }
}

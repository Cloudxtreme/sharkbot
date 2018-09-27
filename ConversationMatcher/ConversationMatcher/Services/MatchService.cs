﻿using ChatModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConversationMatcher.Services
{
    public class MatchService
    {
        private SubjectConfidenceService subjectConfidenceService;
        private ScoreService scoreService;
        private MatchConfidenceService matchConfidenceService;
        private GroupChatConfidenceService groupChatConfidenceService;
        private UniqueConfidenceService uniqueConfidenceService;
        private ReadingLevelConfidenceService readingLevelConfidenceService;

        public MatchService()
        {
            subjectConfidenceService = new SubjectConfidenceService();
            scoreService = new ScoreService();
            matchConfidenceService = new MatchConfidenceService();
            groupChatConfidenceService = new GroupChatConfidenceService();
            uniqueConfidenceService = new UniqueConfidenceService();
            readingLevelConfidenceService = new ReadingLevelConfidenceService();
        }

        public List<AnalyzedChat> GetResponseChat(List<MatchChat> conversation, int targetIndex)
        {
            var response = new List<AnalyzedChat>();

            var replyIndex = targetIndex + 1;
            response.Add(conversation[replyIndex].analyzedChat);
            replyIndex++;

            while(replyIndex < conversation.Count)
            {
                if (IsChainedReply(response.Last().chat, conversation[replyIndex].analyzedChat.chat))
                {
                    response.Add(conversation[replyIndex].analyzedChat);
                    replyIndex++;
                }
                else
                {
                    return response;
                }
            }

            return response;
        }
        
        private bool IsChainedReply(Chat chat, Chat reply)
        {
            var maximumReplyTime = 60000;
            return chat.user == reply.user && chat.time + maximumReplyTime > reply.time;
        }

        public List<ConversationMatchList> GetConversationMatchLists(Conversation targetConversation, List<ConversationList> conversationLists)
        {
            var conversationMatchLists = new List<ConversationMatchList>();

            foreach (var conversationList in conversationLists)
            {
                var conversationMatchList = new ConversationMatchList { type = conversationList.type, matchConversations = new ConcurrentBag<MatchConversation>() };

                Parallel.ForEach(conversationList.conversations, (conversation) =>
                {
                    if (conversation.name != targetConversation.name)
                    {
                        var matchConversation = new MatchConversation
                        {
                            name = conversation.name,
                            responses = new List<MatchChat>()
                        };

                        var subjectMatchConfidence = subjectConfidenceService.getSubjectMatchConfidence(targetConversation, conversation);
                        var readingLevelMatchConfidence = readingLevelConfidenceService.getReadingLevelConfidence(targetConversation.readingLevel, conversation.readingLevel);

                        var responseUsers = conversation.responses.Select(r => r.chat.user).Distinct().ToList();
                        var targetUsers = targetConversation.responses.Select(r => r.chat.user).Distinct().ToList();
                        for(var index = 0; index < conversation.responses.Count(); index++)
                        {
                            var userlessReply = string.Empty;
                            if(index + 1 < conversation.responses.Count())
                            {
                                userlessReply = conversation.responses[index + 1].naturalLanguageData.userlessMessage;
                            }
                            var matchChat = GetMatch(targetConversation, conversation.responses[index], subjectMatchConfidence, readingLevelMatchConfidence, conversation.groupChat, userlessReply);
                            matchConversation.responses.Add(matchChat);
                        }

                        conversationMatchList.matchConversations.Add(matchConversation);
                    }
                });

                conversationMatchLists.Add(conversationMatchList);
            }

            return conversationMatchLists;
        }

        private const double replyMatchRatio = .5;
        private const double conversationProximityRatio = .35;
        private const double subjectMatchRatio = .05;
        private const double readingLevelMatchRatio = .05;
        private const double groupChatRatio = .05;

        private MatchChat GetMatch(Conversation targetConversation, AnalyzedChat existingResponse, double subjectMatchConfidence, double readingLevelMatchConfidence, bool existingGroupChat, string userlessReply)
        {
            //TODO: add the ability to pass in a required user property that the responding user has to have, or that the matching response has to have
            //TODO: add user comparison and user similarity to the algorithm for confidence
            var targetResponse = targetConversation.responses.Last();

            var matchChat = new MatchChat
            {
                matchConfidence = 0,
                analyzedChat = existingResponse
            };

            if(existingResponse.naturalLanguageData.responseConfidence == 0) //TODO: if the responding user name is in the excluded list set confidence to 0 and return
            {
                return matchChat;
            }

            var uniqueConfidence = uniqueConfidenceService.getUniqueConfidence(userlessReply, targetConversation);
            var replyConfidence = existingResponse.naturalLanguageData.responseConfidence;
            var confidenceMultiplier = uniqueConfidence * replyConfidence;

            var replyMatchConfidence = matchConfidenceService.getMatchConfidence(targetResponse.naturalLanguageData, existingResponse.naturalLanguageData, targetResponse.botName);
            var replyPartScore = replyMatchConfidence * replyMatchRatio;

            var conversationProximityScore = subjectConfidenceService.getConversationProximityMatchConfidence(targetResponse.naturalLanguageData.proximitySubjects, existingResponse.naturalLanguageData.proximitySubjects);
            var proximityPartScore = conversationProximityScore * conversationProximityRatio;

            var subjectMatchPartScore = subjectMatchConfidence * subjectMatchRatio;

            var readingLevelMatchPartScore = readingLevelMatchConfidence * readingLevelMatchRatio;

            var groupChatMatchConfidence = groupChatConfidenceService.getGroupChatConfidence(targetConversation.groupChat, existingGroupChat);
            var groupChatPartScore = groupChatMatchConfidence * groupChatRatio;

            var confidence = (replyPartScore + proximityPartScore + subjectMatchPartScore + readingLevelMatchPartScore + groupChatPartScore) * confidenceMultiplier;

            var length = targetResponse.naturalLanguageData.sentences.SelectMany(s => s.tokens).Count();
            if(length < 5)
            {
                var exponent = 6 - length;
                confidence = Math.Pow(confidence, exponent);
            }

            matchChat.matchConfidence = confidence;

            return matchChat;
        }
    }
}

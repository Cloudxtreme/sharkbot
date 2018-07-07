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
        private TripletScoreService tripletScoreService;
        private TokenScoreService tokenScoreService;
        private InterogativeScoreService interogativeScoreService;
        private ScoreService scoreService;
        private MatchConfidenceService matchConfidenceService;
        private GroupChatConfidenceService groupChatConfidenceService;
        private UniqueConfidenceService uniqueConfidenceService;

        public MatchService()
        {
            subjectConfidenceService = new SubjectConfidenceService();
            tripletScoreService = new TripletScoreService();
            tokenScoreService = new TokenScoreService();
            interogativeScoreService = new InterogativeScoreService();
            scoreService = new ScoreService();
            matchConfidenceService = new MatchConfidenceService();
            groupChatConfidenceService = new GroupChatConfidenceService();
            uniqueConfidenceService = new UniqueConfidenceService();
        }

        public MatchChat GetBestMatch(Conversation targetConversation, List<ConversationList> conversationLists)
        {
            var bestMatch = new MatchChat { matchConfidence = 0 };

            var conversationMatchLists = GetConversationMatchLists(targetConversation, conversationLists);
            foreach (var conversationMatchList in conversationMatchLists)
            {
                foreach (var conversation in conversationMatchList.matchConversations)
                {
                    for (var index = 0; index < conversation.responses.Count; index++)
                    {
                        if (conversation.responses[index].matchConfidence > bestMatch.matchConfidence)
                        {
                            bestMatch.matchConfidence = conversation.responses[index].matchConfidence;
                            bestMatch.analyzedChat = conversation.responses[index].analyzedChat;
                            if (index + 1 < conversation.responses.Count)
                            {
                                bestMatch.responseChat = conversation.responses[index + 1].analyzedChat;
                            }
                        }
                    }
                }
            }

            return bestMatch;
        }

        private List<ConversationMatchList> GetConversationMatchLists(Conversation targetConversation, List<ConversationList> conversationLists)
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

                        var responseUsers = conversation.responses.Select(r => r.chat.user).Distinct().ToList();
                        var targetUsers = targetConversation.responses.Select(r => r.chat.user).Distinct().ToList();
                        for(var index = 0; index < conversation.responses.Count(); index++)
                        {
                            var userlessReply = string.Empty;
                            if(index + 1 < conversation.responses.Count())
                            {
                                userlessReply = conversation.responses[index + 1].naturalLanguageData.userlessMessage;
                            }
                            var matchChat = GetMatch(targetConversation, conversation.responses[index], subjectMatchConfidence, conversation.groupChat, userlessReply);
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
        private const double conversationProximityRatio = .4;
        private const double subjectMatchRatio = .05;
        private const double groupChatRatio = .05;

        private MatchChat GetMatch(Conversation targetConversation, AnalyzedChat existingResponse, double subjectMatchConfidence, bool existingGroupChat, string userlessReply)
        {
            var targetResponse = targetConversation.responses.Last();

            var matchChat = new MatchChat
            {
                matchConfidence = 0,
                analyzedChat = existingResponse
            };

            if(existingResponse.naturalLanguageData.responseConfidence == 0)
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

            var groupChatMatchConfidence = groupChatConfidenceService.getGroupChatConfidence(targetConversation.groupChat, existingGroupChat);
            var groupChatPartScore = groupChatMatchConfidence * groupChatRatio;

            var confidence = (replyPartScore + proximityPartScore + subjectMatchPartScore + groupChatPartScore) * confidenceMultiplier;

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

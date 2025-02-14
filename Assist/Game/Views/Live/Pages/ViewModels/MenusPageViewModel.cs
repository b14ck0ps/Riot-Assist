﻿using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Assist.Game.Controls.Live;
using Assist.Game.Models;
using Assist.Game.Services;
using Assist.Objects.RiotSocket;
using Assist.ViewModels;
using Avalonia.Collections;
using Avalonia.Threading;
using ReactiveUI;
using Serilog;
using ValNet.Objects.Exceptions;
using ValNet.Objects.Local;

namespace Assist.Game.Views.Live.Pages.ViewModels
{
    internal class MenusPageViewModel : ViewModelBase
    {
        public string CurrentPartyId;

        private List<LiveMenuPartyUser> _currentUsers = new List<LiveMenuPartyUser>();

        public List<LiveMenuPartyUser> CurrentUsers
        {
            get => _currentUsers;
            set => this.RaiseAndSetIfChanged(ref _currentUsers, value);
        }

        private string? queueName;

        public string QueueName
        {
            get => queueName;
            set => this.RaiseAndSetIfChanged(ref queueName, value);
        }

        private string? partySize;

        public string PartySize
        {
            get => partySize;
            set => this.RaiseAndSetIfChanged(ref partySize, value);
        }


        public async Task Setup(PresenceV4Message start = null)
        {
            AssistApplication.Current.RiotWebsocketService.UserPresenceMessageEvent += RiotWebsocketServiceOnUserPresenceMessageEvent;

            if (start != null)
            {
                RiotWebsocketServiceOnUserPresenceMessageEvent(start);
            }
        }

        private async void RiotWebsocketServiceOnUserPresenceMessageEvent(PresenceV4Message obj)
        {
            // On User Update 
            var data = await GetPresenceData(obj.data.presences[0]);

            if (data != null)
            {
                if(data.sessionLoopState != "MENUS")
                    return;

                if (CurrentUsers.Count == 0)
                {
                    AddUserToList(
                    
                        new LiveMenuPartyUser()
                        {
                            PlayerId = obj.data.presences[0].puuid,
                            PlayerName = $"{obj.data.presences[0].game_name}#{obj.data.presences[0].game_tag}",
                            Playercard = $"https://content.assistapp.dev/playercards/{data.playerCardId}_LargeArt.png",
                            PlayerReady = true,
                        }
                    );
                }

                if (data.partySize == 1)
                {
                    for (int i = 0; i < CurrentUsers.Count; i++)
                    {
                        if (CurrentUsers[i].PlayerId != AssistApplication.Current.CurrentUser.UserData.sub)
                            RemoveUserToList(CurrentUsers[i]);
                    }
                }

                CurrentPartyId = data.partyId;
                UpdateGeneralPartyInformation(data,obj);

                if (data.partySize > 1)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => { HandleMoreThanOneParty(data, obj); });
                }
            }
        }

        public async void UpdateGeneralPartyInformation(PlayerPresence data, PresenceV4Message obj)
        {
            var currentUserBtn = CurrentUsers.ToList().Find(member => member.PlayerId == obj.data.presences[0].puuid);
            QueueName = GetQueueId(data.queueId).ToUpper();
            PartySize = $"{data.partySize}/{data.maxPartySize}";

            if (data.maxPartySize > 5)
            {
                QueueName = "CUSTOM GAME (Not currently supported within Assist)";
            }

            if (currentUserBtn != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    currentUserBtn.Playercard = $"https://content.assistapp.dev/playercards/{data.playerCardId}_LargeArt.png";
                    
                });
            }

            // Update UI Elements
            
        }

        private string GetQueueId(string dataQueueId)
        {
            switch (dataQueueId)
            {
                case "ggteam":
                    return "Escalation";
                case "deathmatch":
                    return "Deathmatch";
                case "spikerush":
                    return "SpikeRush";
                case "competitive":
                    return "Competitive";
                case "unrated":
                    return "Unrated";
                case "onefa":
                    return "Replication";
                case "swiftplay":
                    return "Swiftplay";
                case "snowball":
                    return "Snowball";
                case "lotus":
                    return "Lotus";
                default:
                    return "VALORANT";
            }
        }

        public async void HandleMoreThanOneParty(PlayerPresence data, PresenceV4Message obj)
        {
            try
            {
                var partyData = await AssistApplication.Current.CurrentUser.Party.FetchParty();
                var pres = await AssistApplication.Current.CurrentUser.Presence.GetPresences();
                if (partyData != null)
                {
                    if(partyData.Members.Count <= 1)
                        return;

                    for (int i = 0; i < partyData.Members.Count; i++)
                    {
                        if(partyData.Members[i].Subject == AssistApplication.Current.CurrentUser.UserData.sub)
                            continue;

                        var member = partyData.Members[i];
                        var currentUserBtn = CurrentUsers.ToList().Find(member => member.PlayerId == partyData.Members[i].Subject);
                        var gameName = pres.presences.Find(pres => pres.puuid == member.Subject);
                        var p = await GetPresenceData(gameName);
                        if (currentUserBtn == null)
                        {
                            Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                AddUserToList(new LiveMenuPartyUser()
                                {
                                    PlayerName = $"{gameName.game_name}#{gameName.game_tag}",
                                    PlayerId = member.Subject,
                                    Playercard = $"https://content.assistapp.dev/playercards/{p.playerCardId}_LargeArt.png"
                                });
                            });
                            // This means this is a new Party Member
                            
                        }


                    }

                    for (int i = 0; i < CurrentUsers.Count; i++)
                    {
                        var d = partyData.Members.Find(member => member.Subject == CurrentUsers[i].PlayerId);
                        if (d == null)
                        {
                            RemoveUserToList(CurrentUsers[i]);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Fatal("Failed to get Party");
                Log.Fatal(e.Message);

                if (e is RequestException)
                {
                    var test = e as RequestException;

                    if (test != null)
                    {
                        Log.Fatal(test.StatusCode.ToString());
                        Log.Fatal(test.Content);
                    }
                }
            }
        }
        public async void AddUserToList(LiveMenuPartyUser u)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var newTempList = new List<LiveMenuPartyUser>();
                newTempList.Add(u);
                var l = CurrentUsers.Concat(newTempList).ToList();
                CurrentUsers = l;
            });
        }

        public async void RemoveUserToList(LiveMenuPartyUser u)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var newTempList = new List<LiveMenuPartyUser>();
                CurrentUsers.ForEach(user =>
                {
                    if(user.PlayerId != u.PlayerId)
                        newTempList.Add(user);

                });

                CurrentUsers = newTempList;


            });
        }

        public async Task<PlayerPresence> GetPresenceData(PresenceV4Message.Presence data)
        {
            if (string.IsNullOrEmpty(data.Private))
                return new PlayerPresence();
            byte[] stringData = Convert.FromBase64String(data.Private);
            string decodedString = Encoding.UTF8.GetString(stringData);
            return JsonSerializer.Deserialize<PlayerPresence>(decodedString);
        }

        public async Task<PlayerPresence> GetPresenceData(ChatV4PresenceObj.Presence data)
        {
            if (string.IsNullOrEmpty(data.Private))
                return new PlayerPresence();
            byte[] stringData = Convert.FromBase64String(data.Private);
            string decodedString = Encoding.UTF8.GetString(stringData);
            return JsonSerializer.Deserialize<PlayerPresence>(decodedString);
        }
    }
}

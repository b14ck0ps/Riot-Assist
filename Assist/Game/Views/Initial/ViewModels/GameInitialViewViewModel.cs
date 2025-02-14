﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Assist.Game.Services;
using Assist.Services.Popup;
using Assist.Settings;
using Assist.ViewModels;
using Avalonia.Controls;
using ReactiveUI;
using Serilog;
using ValNet;
using ValNet.Enums;
using ValNet.Objects;
using ValNet.Objects.Exceptions;

namespace Assist.Game.Views.Initial.ViewModels
{
    internal class GameInitialViewViewModel : ViewModelBase
    {

        private string _message = "Loading";

        public string Message
        {
            get => _message;
            set => this.RaiseAndSetIfChanged(ref _message, value);
        }

        public async Task Setup()
        {
            PopupSystem.KillPopups();
            // Start Setup

            // Check if you are in Design Mode
            if(Design.IsDesignMode)
                return;


            while (!IsValorantRunning())
            {
                Message = "Please Run Assist, When Valorant is Open. Checking in 5 seconds.";
                await Task.Delay(5000);
            }

            // Connect to Valorant Websocket Through Socket Service.
            Message = "Connecting to Game...";
            await ConnectToGame();
            Message = "Connecting to Live Data Socket...";
            await StartSocketConnection();
            
            new DodgeService();
            
            // Introduce Authentication
            if (string.IsNullOrEmpty(AssistSettings.Current.AssistUserCode))
            {
                AssistApplication.Current.OpenAssistAuthenticationView();
                return;
            }

            // Authenticate User with Code
            try
            {
                var authResp = await AssistApplication.Current.AssistUser.AuthenticateWithRefreshToken(AssistSettings.Current
                    .AssistUserCode);
                AssistSettings.Current.AssistUserCode = authResp.RefreshToken;
                AssistSettings.Save();
                await AssistApplication.Current.AssistUser.GetUserInfo();
            }
            catch (Exception e)
            {
                Log.Fatal("Account Token is not Valid");
                Log.Fatal("Opening Auth View");
                AssistApplication.Current.OpenAssistAuthenticationView();
                return;
            }

            if (GameSettings.Current.DiscordPresenceEnabled)
            {
                await DiscordPresenceController.ControllerInstance.Initalize();
            }
            AssistApplication.Current.OpenGameView();
        }

        private bool IsValorantRunning()
        {
            var processlist = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Where(process => process.Id != Process.GetCurrentProcess().Id).ToList();
            processlist.AddRange(Process.GetProcessesByName("VALORANT-Win64-Shipping"));
            
            return processlist.Any();
        }

        private async Task StartSocketConnection()
        {
            AssistApplication.Current.RiotWebsocketService.RecieveMessageEvent += delegate (object o) {  };
            await AssistApplication.Current.RiotWebsocketService.Connect();
            Message = "Connected to Live Data Socket.";
            
        }

        private async Task ConnectToGame()
        {
            RiotUser user = new RiotUserBuilder().WithSettings(new RiotUserSettings()
            {
                AuthenticationMethod = AuthenticationMethod.CURL,
            }).Build();

            try
            {
                var r = await user.Authentication.AuthenticateWithLocal();
            }
            catch (Exception e)
            {
                Log.Error("Error On Local Auth Connection");
                if (e is ValNetException)
                {
                    var ex = e as ValNetException;
                    Log.Fatal("ERROR:" + ex.Message);
                }

                throw e;
            }
            Message = "Connection Successful";

            AssistApplication.Current.CurrentUser = user;

            


        }
    }
}


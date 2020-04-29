using GuessMyNumber.API.Helper;
using GuessMyNumber.Model;
using GuessMyNumber.Provider;
using GuessMyNumber.Web.Models;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GuessMyNumber.Web.Hubs
{
    public class GameHub : Hub<IChatClient>
    {
        // CnnId -> Username
        private static IDictionary<string, string> _connectionUserDict = new ConcurrentDictionary<string, string>();
        private static Random _random = new Random();
        private const string ServerUsername = "SERVER";
        private readonly IGameProvider _gameProvider;
        
        public GameHub(IGameProvider gameProvider)
        {
            _gameProvider = gameProvider;
        }

        public async Task SendMessage(string message)
        {
            var username = _connectionUserDict[GetConnectionId()];
            var command = ParseCommand(message);
            if (command != null)
            {
                await TryProcessCommand(username, command);
            }
            else
            {
                // Not a command, just a message
                await Clients.All.ReceiveUserMessage(username, $"<span style='color:gray;'>_{username}_</span> {message}");
            }
        }

        private async Task TryProcessCommand(string username, Command command)
        {
            try
            {
                await ProcessCommand(username, command);
            }
            catch (Exception ex)
            {
                await Clients.All.ReceiveServerMessage($"<span style='color:red;'>**{username} cannot {command.Name}. _Exception_**: {ex.Message}</span>");
                throw;
            }
        }

        public override Task OnConnectedAsync()
        {
            var username = Context.GetHttpContext().Request.Cookies["PlayerName"];
            _connectionUserDict[Context.ConnectionId] = username;
            Clients.All.UserListChanged(_connectionUserDict.Values.Distinct().OrderBy(v => v).ToArray());
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            _connectionUserDict.Remove(Context.ConnectionId);
            Clients.All.UserListChanged(_connectionUserDict.Values.Distinct().OrderBy(v => v).ToArray());
            return base.OnDisconnectedAsync(exception);
        }

        public string GetConnectionId()
        {
            return Context.ConnectionId;
        }

        // <COMMANDS>
        private Command ParseCommand(string message)
        {
            if (message.Length > 1 && message.StartsWith('/') && !message.StartsWith("//"))
            {
                var words = message.Substring(1).Split(' ');
                if (Enum.TryParse<CommandName>(words[0], true, out CommandName cmd))
                {
                    // It's a command
                    return new Command()
                    {
                        Name = cmd,
                        Parameters = words.Length > 1 ? words[1..] : new string[] { }
                    };
                }
            }
            return null;
        }
        private async Task<CommandResponse> ProcessCommand(string username, Command command)
        {
            GameResponse gameResponse = null;
            var response = new CommandResponse();
            string number;
            switch (command.Name)
            {
                case CommandName.None:
                    throw new ArgumentException("Invalid command", nameof(command));
                case CommandName.Create:
                    // ***** Create Game *****
                    if (command.Parameters.Length < 1)
                    {
                        throw new ArgumentException("Invalid command parameters", nameof(command));
                    }
                    number = command.Parameters[0];
                    bool isAuto = false;
                    if (number.All(c => c == '*'))
                    {
                        // Random number
                        number = GenerateRandomNumber(number.Length);
                        isAuto = true;
                    }
                    int? maxTries = null;
                    if (command.Parameters.Length > 1 && int.TryParse(command.Parameters[1], out int mt))
                    {
                        maxTries = mt;
                    }
                    gameResponse = _gameProvider.CreateGame(isAuto ? ServerUsername : username, number, maxTries);
                    if (gameResponse.HasErrors)
                    {
                        response.Error = gameResponse.Error;
                        response.ErrorCode = gameResponse.ErrorCode;
                    }
                    else
                    {
                        if (isAuto)
                        {
                            _gameProvider.StartGame(ServerUsername, gameResponse.Game.GameId);
                            _gameProvider.JoinGame(username, gameResponse.Game.GameId);
                        }
                        // set message for create game successfully
                        response.OutputMessage = $"<span style='color:purple;'>**{(isAuto ? ServerUsername : username)}** created game **`{gameResponse.Game.GameId}`** with **`{gameResponse.Game.Digits}`** digits";
                        if (gameResponse.Game.MaxTries.HasValue)
                        {
                            response.OutputMessage += $" and a maximum of **`{gameResponse.Game.MaxTries.Value}`** tries";
                        }
                        response.OutputMessage += "</span>";
                    }
                    break;
                case CommandName.Join:
                    // ***** Join Game *****
                    if (command.Parameters.Length < 1)
                    {
                        throw new ArgumentException("Invalid command parameters", nameof(command));
                    }
                    gameResponse = _gameProvider.JoinGame(username, command.Parameters[0].ToUpper(), true);
                    if (gameResponse.HasErrors)
                    {
                        response.Error = gameResponse.Error;
                        response.ErrorCode = gameResponse.ErrorCode;
                    }
                    else
                    {
                        // set message for join game successfully
                        response.OutputMessage = $"<span style='color:purple;'>**{username}** has joined game **`{gameResponse.Game.GameId}`**</span>";
                    }
                    break;
                case CommandName.Start:
                    // ***** Start Game *****
                    if (command.Parameters.Length < 1)
                    {
                        throw new ArgumentException("Invalid command parameters", nameof(command));
                    }
                    gameResponse = _gameProvider.StartGame(username, command.Parameters[0].ToUpper());
                    if (gameResponse.HasErrors)
                    {
                        response.Error = gameResponse.Error;
                        response.ErrorCode = gameResponse.ErrorCode;
                    }
                    else
                    {
                        // set message for start game successfully
                        if (gameResponse.Game.Status == GameStatus.Started)
                        {
                            var players = string.Join(", ", gameResponse.Game.Players.Select(p => p.Role == Role.Host ? p.PlayerId + "*" : p.PlayerId));
                            response.OutputMessage = $"<span style='color:magenta;'>Game **`{gameResponse.Game.GameId}`** with **`{gameResponse.Game.Digits}`** digits has started. Next turn: **{gameResponse.Game.NextTurnPlayerId}**</span>";
                        }
                        else
                        {
                            response.OutputMessage = $"<span style='color:magenta;'>Player **{username}** is ready to play game **`{gameResponse.Game.GameId}`** with **`{gameResponse.Game.Digits}`** digits</span>";
                        }
                    }
                    break;
                case CommandName.Play:
                    // ***** Play Turn *****
                    if (command.Parameters.Length < 2)
                    {
                        throw new ArgumentException("Invalid command parameters", nameof(command));
                    }
                    gameResponse = _gameProvider.Play(username, command.Parameters[0].ToUpper(), command.Parameters[1]);
                    if (gameResponse.HasErrors)
                    {
                        response.Error = gameResponse.Error;
                        response.ErrorCode = gameResponse.ErrorCode;
                    }
                    else
                    {
                        // set message for play game successfully
                        number = gameResponse.Game.Number.Decrypt();
                        var playerGame = gameResponse.Game.PlayersDict[username];
                        var hostPlayer = gameResponse.Game.HostPlayer.PlayerId;

                        response.OutputMessage = $"<span style='color:indigo;'>Game **`{gameResponse.Game.GameId}`** **{username}** try **`#{playerGame.TriesCount}`** number **`{command.Parameters[1]}`** response **`{(gameResponse as PlayGameResponse).Answer.AnswerString}`**</span>";

                        if ((gameResponse as PlayGameResponse).Answer.CorrectCount == gameResponse.Game.Digits)
                        {
                            // Guesser won
                            response.OutputMessage += $"<br /><span style='color:darkgreen;'>**{username}** has won the game **`{gameResponse.Game.GameId}`** against **{hostPlayer}** in **`{playerGame.TriesCount}`** moves. Number was **`{number}`**</span>";
                        }
                        else
                        {
                            if (gameResponse.Game.WinnerPlayerId == gameResponse.Game.HostPlayer.PlayerId)
                            {
                                // Host won
                                response.OutputMessage += $"<br /><span style='color:darkred;'>**{hostPlayer}** has won the game **`{gameResponse.Game.GameId}`** as host after `{playerGame.TriesCount}` moves. Number was **`{number}`**</span>";
                            }
                            else
                            {
                                // Next turn
                                if (!gameResponse.Game.NextTurnPlayerId.Equals(username, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    response.OutputMessage += $"<span style='color:gray;'>. Next turn: **{gameResponse.Game.NextTurnPlayerId}**</span>";
                                }
                            }
                        }
                        if (gameResponse.Game.Status == GameStatus.Finished)
                        {
                            response.OutputMessage += $"<br /><span style='color:green;'> WINNER **{gameResponse.Game.WinnerPlayerId}**</span>";
                        }
                    }
                    break;
                case CommandName.Abandon:
                    if (command.Parameters.Length < 1)
                    {
                        throw new ArgumentException("Invalid command parameters", nameof(command));
                    }
                    gameResponse = _gameProvider.Abandon(username, command.Parameters[0].ToUpper());
                    if (gameResponse.HasErrors)
                    {
                        response.Error = gameResponse.Error;
                        response.ErrorCode = gameResponse.ErrorCode;
                    }
                    else
                    {
                        response.OutputMessage = $"<span style='color:darkred;'> {username} abandoned the game  **{gameResponse.Game.GameId}**</span>";
                    }
                    break;
                case CommandName.Cancel:
                    // TODO: Implement cancel and abandon
                    throw new NotImplementedException();

            }

            // Send output message to clients
            if (response.HasErrors)
            {
                await Clients.All.ReceiveServerMessage($"<span style='color:red;'>**{username} cannot {command.Name}. _Error_**: {response.Error}</span>");
            }
            else
            {
                await Clients.All.ReceiveServerMessage(response.OutputMessage);
            }
            
            // Send command output to clients
            await SendCommandToClient(username, command, response, gameResponse);

            return response;
        }

        private string GenerateRandomNumber(int digits)
        {
            if (digits < 1 || digits > 10)
            {
                throw new IndexOutOfRangeException("Digits out of range");
            }
            var numbers = Enumerable.Range(0, 10).ToList();
            var number = "";
            for(int i = 0; i < digits; i++)
            {
                var randomIndex = _random.Next(0, 10 - i);
                number += numbers[randomIndex].ToString();
                numbers.RemoveAt(randomIndex);
            }
            return number;
        }

        private async Task SendCommandToClient(string user, Command command, CommandResponse commandResponse, GameResponse gameResponse)
        {
            if (command.Name == CommandName.Create && command.Parameters.Length > 0 && !command.Parameters[0].StartsWith('*'))
            {
                command.Parameters[0] = command.Parameters[0].Encrypt();
            }
            await Clients.All.ReceiveCommand(user, command.Name.ToString(), command.Parameters, commandResponse, gameResponse);
        }
        // </COMMANDS>

    }
}

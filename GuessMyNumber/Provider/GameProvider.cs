using GuessMyNumber.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using GuessMyNumber.API.Helper;
using CachingFramework.Redis.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace GuessMyNumber.Provider
{
    public class GameProvider : IGameProvider
    {
        #region fields
        private static IContext _redis;
        internal IDictionary<string, Game> Games { get; set; }
        internal IDictionary<string, Game> GamesHistory { get; set; }
        internal IDictionary<string, List<string>> PlayerGames { get; set; }
        internal static object LockerGames = new object();
        internal static object LockerPlayers = new object();
        internal static object LockerLoader = new object();
        private static char[] Digits = "0123456789".ToCharArray();
        private static char[] AnswerStringChars;
        private readonly AppSettings _settings;
        private const string StatsDictKey = "player-{0}-stats";
        #endregion fields

        #region ctor
        public GameProvider(IOptions<AppSettings> settings)
        {
            _settings = settings.Value;
            lock (LockerLoader)
            {
                if (_redis == null)
                {
                    _redis = new CachingFramework.Redis.RedisContext(_settings.RedisConnectionString);
                    AnswerStringChars = _settings.AnswerStringChars.ToCharArray();
                }
            }
            Games = _redis.Collections.GetRedisDictionary<string, Game>($"games"); // key: Game ID
            GamesHistory = _redis.Collections.GetRedisDictionary<string, Game>($"games-history"); // key: Game ID
            PlayerGames = _redis.Collections.GetRedisDictionary<string, List<string>>($"players"); // key: Player ID - value: list of Game ID
        }
        internal GameProvider(string postfix)
        {
            Games = _redis.Collections.GetRedisDictionary<string, Game>($"games-{postfix}"); // key: Game ID
            GamesHistory = _redis.Collections.GetRedisDictionary<string, Game>($"games-history-{postfix}"); // key: Game ID
            PlayerGames = _redis.Collections.GetRedisDictionary<string, List<string>>($"players-{postfix}"); // key: Player ID - value: list of Game ID
        }
        #endregion

        #region Private
        private void ValidatePlayerIdFormat(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException($"{nameof(playerId)} cannot be Null or Empty");
            }
            if (playerId.Length < _settings.MinPlayerIdLength)
            {
                throw new ArgumentException($"{nameof(playerId)} must be of at least {_settings.MinPlayerIdLength} chars");
            }
        }

        private void ValidateNumberFormat(string number)
        {
            if (string.IsNullOrWhiteSpace(number))
            {
                throw new ArgumentException($"{nameof(number)} cannot be Null or Empty");
            }
            if (number.Length < _settings.MinDigits || number.Length > _settings.MaxDigits)
            {
                throw new ArgumentException($"{nameof(number)} must be of at least {_settings.MinDigits} digits and at most {_settings.MaxDigits} digits");
            }
            if (number.Any(d => !Digits.Contains(d)))
            {
                throw new ArgumentException($"Not a number");
            }
            if (number.Length != number.Distinct().Count())
            {
                throw new ArgumentException($"Number cannot contain duplicated digits");
            }
        }

        private void ValidateGameIdFormat(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                throw new ArgumentException($"{nameof(gameId)} cannot be Null or Empty");
            }
            if (gameId.Length != 5)
            {
                throw new ArgumentException($"{nameof(gameId)} must be of 5 chars");
            }
            if (gameId != gameId.ToUpper())
            {
                throw new ArgumentException($"{nameof(gameId)} must be upper cased");
            }
        }

        private void ValidatePlayerGameCount(string playerId)
        {
            var gamesForPlayer = GetCurrentGames(playerId);
            if (gamesForPlayer.GameCount > _settings.MaxGamesPerPlayer)
            {
                throw new ArgumentException($"Max. games per player exceeded for player {playerId}");
            }
        }

        private string NewGameId()
        {
            return Proquint.Quint32.NewQuint().ToString().Substring(0, 5).ToUpper();
        }

        internal string GetAnswerString(int correctCount, int regularCount, int wrongCount)
        {
            var sb = new StringBuilder();
            if (correctCount > 0)
            {
                sb.Append($"{correctCount}{AnswerStringChars[0]} ");
            }
            if (regularCount > 0)
            {
                sb.Append($"{regularCount}{AnswerStringChars[2]} ");
            }
            if (correctCount == 0 && regularCount == 0)
            {
                sb.Append($"{wrongCount}{AnswerStringChars[1]}");
            }
            return sb.ToString().Trim();
        }

        internal Answer GetAnswer(string numberToTry, Game game, int tryNumber)
        {
            var gameNumber = game.Number.Decrypt();
            var answer = new Answer();
            // Count
            for (int i = 0; i < numberToTry.Length; i++)
            {
                if (numberToTry[i] == gameNumber[i])
                {
                    answer.CorrectCount++;
                }
                else if (gameNumber.Contains(numberToTry[i]))
                {
                    answer.RegularCount++;
                }
                else
                {
                    answer.WrongCount++;
                }
            }
            answer.AnswerString = GetAnswerString(answer.CorrectCount, answer.RegularCount, answer.WrongCount);
            answer.TryNumber = tryNumber;
            return answer;
        }

        internal string GetNextTurn(Game game, string currentPlayerId)
        {
            int index = 0;
            for (int i = 0; i < game.Players.Count; i++)
            {
                if (game.Players[i].PlayerId == currentPlayerId)
                {
                    index = i;
                    break;
                }
            }
            // Look forward
            for(int i = index + 1; i < game.Players.Count; i++)
            {
                if (game.Players[i].Role == Role.Guesser && game.Players[i].Status == PlayerGameStatus.Playing)
                {
                    return game.Players[i].PlayerId;
                }
            }
            // Look from beginning
            for (int i = 0; i < index; i++)
            {
                if (game.Players[i].Role == Role.Guesser && game.Players[i].Status == PlayerGameStatus.Playing)
                {
                    return game.Players[i].PlayerId;
                }
            }
            return currentPlayerId;
        }
        #endregion Private

        #region Public
        public GameResponse CreateGame(string playerId, string number, int? maxTries, bool autoStart = false)
        {
            // Validate ID format
            ValidatePlayerIdFormat(playerId);
            
            // Validate number format
            ValidateNumberFormat(number);

            // Validate player is not playing more than MaxGamesPerPlayer games
            ValidatePlayerGameCount(playerId);

            if (maxTries.HasValue && (maxTries.Value < 1 || maxTries.Value > 100))
            {
                throw new ArgumentException($"Max. tries {maxTries.Value} is out of range");
            }

            // Create the game
            var gameId = NewGameId();
            var game = new Game()
            {
                GameId = gameId,
                Number = number.Encrypt(),
                Players = new List<PlayerGameRole>()
                {
                    new PlayerGameRole()
                    {
                        PlayerId = playerId,
                        Role = Role.Host,
                        Status = autoStart ? PlayerGameStatus.Playing : PlayerGameStatus.Ready
                    }
                },
                Status = GameStatus.Created,
                MaxTries = maxTries
            };
            this.Games.Add(gameId, game);

            // add the player to the players dictionary
            lock (LockerPlayers)
            {
                if (this.PlayerGames.ContainsKey(playerId))
                {
                    var list = this.PlayerGames[playerId];
                    list.Add(gameId);
                    this.PlayerGames[playerId] = list;
                }
                else
                {
                    this.PlayerGames[playerId] = new List<string>() { gameId };
                }
            }

            return new GameResponse
            {
                Game = game
            };
        }

        public GameResponse JoinGame(string playerId, string gameId, bool startGame = false)
        {
            // Validate ID format
            ValidatePlayerIdFormat(playerId);

            // Validate Game ID
            ValidateGameIdFormat(gameId);

            // Validate player is not playing more than MaxGamesPerPlayer games
            ValidatePlayerGameCount(playerId);

            // Validate game exists
            Game game = null;
            if (!this.Games.TryGetValue(gameId, out game))
            {
                return new GameResponse() { Error = $"Game {gameId} does not exists", ErrorCode = 100 };
            }

            lock (LockerGames)
            {
                // Validate the player is not already joined to the game
                if (game.PlayersDict.ContainsKey(playerId))
                {
                    return new GameResponse() { Error = "Already joined to the game", ErrorCode = 101 };
                }

                // Add the player to the game
                game.Players.Add(new PlayerGameRole()
                {
                    PlayerId = playerId,
                    Role = Role.Guesser,
                    Status = PlayerGameStatus.Ready,
                    Guesses = new List<Guess>()
                });
            }

            // Update turn
            game.NextTurnPlayerId = playerId;

            // Update game
            this.Games[gameId] = game;

            // add the player to the players dictionary
            lock (LockerPlayers)
            {
                if (this.PlayerGames.ContainsKey(playerId))
                {
                    var list = this.PlayerGames[playerId];
                    list.Add(gameId);
                    this.PlayerGames[playerId] = list;
                }
                else
                {
                    this.PlayerGames[playerId] = new List<string>() { gameId };
                }
            }

            if (startGame)
            {
                var startGameResponse = StartGame(playerId, gameId);
                if (startGameResponse.HasErrors)
                {
                    return startGameResponse;
                }
                game = startGameResponse.Game;
            }

            return new GameResponse()
            {
                Game = game
            };
        }

        public GameResponse StartGame(string playerId, string gameId)
        {
            // Validate ID format
            ValidatePlayerIdFormat(playerId);

            // Validate Game ID
            ValidateGameIdFormat(gameId);

            // Validate game exists
            Game game = null;
            if (!this.Games.TryGetValue(gameId, out game))
            {
                return new GameResponse() { Error = $"Game {gameId} does not exists", ErrorCode = 100 };
            }

            // Validate player is in the game 
            if (!game.PlayersDict.ContainsKey(playerId))
            {
                return new GameResponse() { Error = $"Player {playerId} have not joined game {gameId}", ErrorCode = 102 };
            }

            // Validate player status in game
            var playerGame = game.PlayersDict[playerId];
            if (playerGame.Status != PlayerGameStatus.Ready)
            {
                return new GameResponse() { Error = $"Player {playerId} is not ready. Current status is {playerGame.Status}", ErrorCode = 103 };
            }

            // Change status and start the game
            playerGame.Status = PlayerGameStatus.Playing;

            // If all players have started, let's start
            if (game.Players.Count > 1 && !game.Players.Any(p => p.Status == PlayerGameStatus.Ready))
            {
                game.Status = GameStatus.Started;
                game.StartedOn = DateTime.UtcNow;
            }

            // Update game
            this.Games[gameId] = game;

            return new GameResponse()
            {
                Game = game
            };
        }

        public PlayGameResponse Play(string playerId, string gameId, string number)
        {
            // Validate ID format
            ValidatePlayerIdFormat(playerId);

            // Validate number format
            ValidateNumberFormat(number);

            // Validate game exists
            Game game = null;
            if (!this.Games.TryGetValue(gameId, out game))
            {
                return new PlayGameResponse() { Error = $"Game {gameId} does not exists", ErrorCode = 100 };
            }

            // Validate game status
            if (game.Status != GameStatus.Started)
            {
                return new PlayGameResponse() { Error = $"Game {gameId} is in status {game.Status}", ErrorCode = 104 };
            }

            // Validate player is in the game 
            if (!game.PlayersDict.ContainsKey(playerId))
            {
                return new PlayGameResponse() { Error = $"Player {playerId} have not joined game {gameId}", ErrorCode = 102 };
            }

            // Validate player status in game
            var playerGame = game.PlayersDict[playerId];
            if (playerGame.Status != PlayerGameStatus.Playing)
            {
                return new PlayGameResponse() { Error = $"Player {playerId} is not playing. Current status is {playerGame.Status}", ErrorCode = 105 };
            }

            // Validate player turn in game
            if (game.NextTurnPlayerId != playerId)
            {
                return new PlayGameResponse() { Error = $"Not player {playerId} turn. It's {game.NextTurnPlayerId}'s turn.", ErrorCode = 106 };
            }

            // Validete digits
            if (number.Length != game.Digits)
            {
                return new PlayGameResponse() { Error = $"Incorrect number length. Must be of {game.Digits} digits", ErrorCode = 107 };
            }

            // Validate the number was not a previous guess
            if (playerGame.Guesses.Any(g => g.Number == number))
            {
                return new PlayGameResponse() { Error = $"Duplicated guess {number} with answer {playerGame.Guesses.First(g => g.Number == number).AnswerString}", ErrorCode = 109 };
            }

            // Guess
            var guess = GetAnswer(number, game, playerGame.Guesses.Count + 1);

            lock (LockerPlayers)
            {
                playerGame.Guesses.Add(new Guess()
                {
                    Number = number,
                    AnswerString = guess.AnswerString
                });
            }

            if (guess.CorrectCount == game.Digits)
            {
                // Win !
                game.WinnerPlayerId = playerId;
                game.NextTurnPlayerId = null;
                game.Status = GameStatus.Finished;
                lock (LockerGames)
                {
                    foreach (var p in game.Players)
                    {
                        if (p.PlayerId == playerId)
                        {
                            p.Status = PlayerGameStatus.Winner;
                            InsertGameFinishedToStats(p.PlayerId, true, p.Role, false, p.TriesCount);
                        }
                        else
                        {
                            p.Status = PlayerGameStatus.Loser;
                            InsertGameFinishedToStats(p.PlayerId, false, p.Role, false);
                        }
                    }
                }
                game.FinishedOn = DateTime.UtcNow;
                
            }
            else
            {
                if (game.MaxTries.HasValue && playerGame.Guesses.Count >= game.MaxTries.Value)
                {
                    // Lose !
                    var hostPlayer = game.HostPlayer.PlayerId;
                    game.WinnerPlayerId = hostPlayer;
                    game.NextTurnPlayerId = null;
                    game.Status = GameStatus.Finished;
                    lock (LockerGames)
                    {
                        foreach (var p in game.Players)
                        {
                            if (p.PlayerId == hostPlayer)
                            {
                                p.Status = PlayerGameStatus.Winner;
                                InsertGameFinishedToStats(p.PlayerId, true, p.Role, false);
                            }
                            else
                            {
                                p.Status = PlayerGameStatus.Loser;
                                InsertGameFinishedToStats(p.PlayerId, false, p.Role, false);
                            }
                        }
                    }
                    game.FinishedOn = DateTime.UtcNow;
                }
                else
                {
                    // Next turn
                    if (game.Players.Count(p => p.Role == Role.Guesser) > 1)
                    {
                        // More than one guesser player, turn to the next
                        game.NextTurnPlayerId = GetNextTurn(game, playerId);
                    }
                }
            }

            if (game.Status == GameStatus.Finished)
            {
                // Move to history
                this.GamesHistory[gameId] = game;
                this.Games.Remove(gameId);
            }
            else
            {
                // Update game
                this.Games[gameId] = game;
            }

            return new PlayGameResponse()
            {
                Answer = guess,
                Game = game,
                PlayerId = playerId
            };
        }

        public GetGamesResponse GetCurrentGames(string playerId)
        {
            List<string> gameIds;
            lock (LockerPlayers)
            {
                if (!this.PlayerGames.TryGetValue(playerId, out gameIds))
                {
                    // No games
                    return new GetGamesResponse()
                    {
                        Games = new List<Game>()
                    };
                }
            }
            lock (LockerGames)
            {
                return new GetGamesResponse()
                {
                    Games = gameIds
                    .Select(gid => this.Games[gid] as Game)
                        .Where(g => g != null && g.Status != GameStatus.Finished)
                        .ToList()
                };
            }
        }

        public GetGamesResponse GetJoinableGames(string playerId)
        {
            return new GetGamesResponse()
            {
                Games = this.Games.Values
                    .Where(g => g.Status == GameStatus.Created && !g.Players.Any(p => p.PlayerId == playerId))
                    .Select(g => g as Game)
                    .ToList()
            };
        }

        public GetGamesResponse GetJoinedGames(string playerId)
        {
            return new GetGamesResponse()
            {
                Games = this.Games.Values
                    .Where(g => g.Status != GameStatus.Finished && g.Players.Any(p => p.PlayerId == playerId))
                    .Select(g => g as Game)
                    .ToList()
            };
        }

        public GetGamesResponse GetPastGames(string playerId)
        {
            return new GetGamesResponse()
            {
                Games = this.GamesHistory.Values
                    .Where(g => g.Status == GameStatus.Finished && g.Players.Any(p => p.PlayerId == playerId))
                    .Select(g => g as Game)
                    .ToList()
            };
        }

        public Game GetGame(string gameId)
        {
            return this.Games[gameId] ?? this.GamesHistory[gameId];
        }

        public GameResponse Abandon(string playerId, string gameId)
        {
            // Validate ID format
            ValidatePlayerIdFormat(playerId);

            // Validate Game ID
            ValidateGameIdFormat(gameId);

            // Validate game exists
            Game game = null;
            if (!this.Games.TryGetValue(gameId, out game))
            {
                throw new Exception($"Game {gameId} does not exists");
            }

            if (!game.PlayersDict.ContainsKey(playerId))
            {
                throw new Exception($"Player {playerId} not in game");
            }

            if (game.PlayersDict[playerId].Status != PlayerGameStatus.Playing)
            {
                throw new Exception($"Player {playerId} not in status playing");
            }

            game.PlayersDict[playerId].Status = PlayerGameStatus.Abandoned;

            if (game.PlayersDict[playerId].Role == Role.Host)
            {
                // Host abandoned, last guesser wins (if any)
                game.Status = GameStatus.Finished;
                game.WinnerPlayerId = null;
                game.NextTurnPlayerId = null;
                game.FinishedOn = DateTime.UtcNow;

                var winnerPlayerId = game.Players.Where(p => p.Role == Role.Guesser && p.Status == PlayerGameStatus.Playing).OrderByDescending(p => p.TriesCount).FirstOrDefault()?.PlayerId;
                if (winnerPlayerId != null)
                {
                    // There is a winner
                    game.WinnerPlayerId = winnerPlayerId;
                    lock (LockerGames)
                    {
                        foreach (var p in game.Players)
                        {
                            if (p.PlayerId == winnerPlayerId)
                            {
                                p.Status = PlayerGameStatus.Winner;
                                InsertGameFinishedToStats(p.PlayerId, true, p.Role, false);
                            }
                            else
                            {
                                p.Status = PlayerGameStatus.Loser;
                                InsertGameFinishedToStats(p.PlayerId, false, p.Role, true);
                            }
                        }
                    }
                }
            }
            else
            {
                // Guesser abandon
                var otherGuessers = game.Players.Where(p => p.Role == Role.Guesser && p.Status == PlayerGameStatus.Playing && p.PlayerId != playerId).ToList();
                if (otherGuessers.Count > 0)
                {
                    // Game not finished, there are more players
                    game.NextTurnPlayerId = GetNextTurn(game, playerId);
                }
                else
                {
                    // Last guesser abandons, host wins
                    game.Status = GameStatus.Finished;
                    game.WinnerPlayerId = game.HostPlayer.PlayerId;
                    game.NextTurnPlayerId = null;
                    game.FinishedOn = DateTime.UtcNow;
                    lock (LockerGames)
                    {
                        foreach (var p in game.Players)
                        {
                            if (p.PlayerId == game.HostPlayer.PlayerId)
                            {
                                p.Status = PlayerGameStatus.Winner;
                                InsertGameFinishedToStats(p.PlayerId, true, p.Role, false);
                            }
                            else
                            {
                                p.Status = PlayerGameStatus.Loser;
                                InsertGameFinishedToStats(p.PlayerId, false, p.Role, true);
                            }
                        }
                    }
                }

            }

            if (game.Status == GameStatus.Finished)
            {
                // Move to history
                this.GamesHistory[gameId] = game;
                this.Games.Remove(gameId);
            }
            else
            {
                // Update game
                this.Games[gameId] = game;
            }

            return new GameResponse()
            {
                Game = game
            };
        }

        public GameResponse GetGame(string playerId, string gameId)
        {
            // Validate ID format
            ValidatePlayerIdFormat(playerId);

            // Validate Game ID
            ValidateGameIdFormat(gameId);

            // Validate game exists
            Game game = this.Games[gameId] ?? this.GamesHistory[gameId];
            if (game == null)
            {
                return new GameResponse() { Error = $"Game {gameId} does not exists", ErrorCode = 100 };
            }

            if (game.PlayersDict.ContainsKey(playerId))
            {
                return new GameResponse()
                {
                    Game = game
                };
            }
            else
            {
                return new GameResponse() { Error = $"Player {playerId} not in game", ErrorCode = 108 };
            }
        }

        public PlayerStats GetStatsForPlayer(string playerId)
        {
            // Validate ID format
            ValidatePlayerIdFormat(playerId);

            var redisDict = _redis.Collections.GetRedisDictionary<string, string>(string.Format(StatsDictKey, playerId));
            return new PlayerStats()
            {
                PlayerId = playerId,
                LostAsGuess = int.Parse(redisDict[StatInfoType.LostAsGuess.ToString()] ?? "0"),
                LostAsHost = int.Parse(redisDict[StatInfoType.LostAsHost.ToString()] ?? "0"),
                WonAsGuess = int.Parse(redisDict[StatInfoType.WonAsGuess.ToString()] ?? "0"),
                WonAsHost = int.Parse(redisDict[StatInfoType.WonAsHost.ToString()] ?? "0"),
                TotalTries = int.Parse(redisDict[StatInfoType.TotalTriesCount.ToString()] ?? "0"),
                Abandons = int.Parse(redisDict[StatInfoType.Abandons.ToString()] ?? "0")
            };
        }

        #region Stats
        /// <summary>Inserts the data for the player stats</summary>
        private void InsertGameFinishedToStats(string playerId, bool isWon, Role role, bool isAbandon, int triesCount = 0)
        {
            var redisDict = _redis.Collections.GetRedisDictionary<string, string>(string.Format(StatsDictKey, playerId));
            if (role == Role.Host)
            {
                if (isWon)
                {
                    // Host won
                    redisDict.IncrementBy(StatInfoType.WonAsHost.ToString(), 1);
                }
                else
                {
                    // Host lost
                    redisDict.IncrementBy(StatInfoType.LostAsHost.ToString(), 1);
                }
            }
            else
            {
                if (isWon)
                {
                    // Guesser won
                    redisDict.IncrementBy(StatInfoType.WonAsGuess.ToString(), 1);
                    redisDict.IncrementBy(StatInfoType.TotalTriesCount.ToString(), triesCount);
                }
                else
                {
                    // Guesser lost
                    redisDict.IncrementBy(StatInfoType.LostAsGuess.ToString(), 1);
                }
            }
            if (isAbandon && !isWon)
            {
                // Abandon count
                redisDict.IncrementBy(StatInfoType.Abandons.ToString(), 1);
            }

        }

        private enum StatInfoType
        {
            LostAsGuess = -2,
            LostAsHost = -1,
            WonAsHost = 1,
            WonAsGuess = 2,
            // To keep track of the number of abandons (already counted on Losses)
            Abandons = -3,
            // To store the total tries for games won as guesser
            TotalTriesCount = 10
        }
        #endregion Stats

        #endregion Public

    }
}

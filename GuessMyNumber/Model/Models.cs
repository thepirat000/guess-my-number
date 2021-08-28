using GuessMyNumber.API.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GuessMyNumber.Model
{
    public enum GameStatus
    {
        Created = 0, // waiting for players
        Started = 1,
        Finished = 2
    }
    public enum Role
    {
        Host = 0,
        Guesser = 1
    }
    public enum PlayerGameStatus
    {
        Ready = 0,
        Playing = 1,
        Winner = 2,
        Loser = 3,
        Abandoned = 4
    }

    public class Game
    {
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? StartedOn { get; set; } = DateTime.UtcNow;
        public DateTime? FinishedOn { get; set; }
        public string GameId { get; set; }
        public GameStatus Status { get; set; }
        public string WinnerPlayerId { get; set; }
        public string Number { get; set; }
        public int Digits { get { return Number?.Decrypt().Length ?? 0; } }
        public int? MaxTries { get; set; }
        public bool IsAutoStart { get; set; }
        public string NextTurnPlayerId { get; set; }
        public List<PlayerGameRole> Players { get; set; }
        [JsonIgnore]
        public Dictionary<string, PlayerGameRole> PlayersDict { get { return Players?.ToDictionary(k => k.PlayerId); } }
        public PlayerGameRole HostPlayer { get { return Players.FirstOrDefault(p => p.Role == Role.Host); } }

    }

    public class PlayerGameRole
    {
        public string PlayerId { get; set; }
        public Role Role { get; set; }
        public PlayerGameStatus Status { get; set; }
        public int TriesCount { get { return Guesses?.Count ?? 0; } }
        public List<Guess> Guesses { get; set; }
    }

    public class Guess
    {
        [JsonPropertyName("N")]
        public string Number { get; set; }
        [JsonPropertyName("A")]
        public string AnswerString { get; set; }
    }

    public abstract class Response
    {
        public bool HasErrors { get { return Error != null; } }
        public string Error { get; set; }
        public int ErrorCode { get; set; }
    }

    public class GameResponse : Response
    {
        public Game Game { get; set; }
    }

    public class Answer
    {
        public string AnswerString { get; set; }
        public byte CorrectCount { get; set; }
        public byte RegularCount { get; set; }
        public byte WrongCount { get; set; }
        public int TryNumber { get; set; }
    }

    public class PlayGameResponse : GameResponse
    {
        public string PlayerId { get; set; }
        public Answer Answer { get; set; }
    }

    public class GetGamesResponse : Response
    {
        public int GameCount { get { return Games?.Count ?? 0; } }
        public List<Game> Games { get; set; }
    }

    public class PlayerStats
    {
        public string PlayerId { get; set; }
        public int WonAsGuess { get; set; }
        public int LostAsGuess { get; set; }
        public int WonAsHost { get; set; }
        public int LostAsHost { get; set; }
        public int TotalTries { get; set; }

        public int Abandons { get; set; }

        public double TriesAverage
        {
            get
            {
                return WonAsGuess == 0 ? 0 : (double)TotalTries / WonAsGuess;
            }
        }

        public int TotalWon
        {
            get
            {
                return WonAsGuess + WonAsHost;
            }
        }

        public int TotalLost
        {
            get
            {
                return LostAsGuess + LostAsHost;
            }
        }
    }
}

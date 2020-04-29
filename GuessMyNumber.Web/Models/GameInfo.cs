using GuessMyNumber.API.Helper;
using GuessMyNumber.Model;
using System.Collections.Generic;
using System.Linq;

namespace GuessMyNumber.Web.Models
{
    public abstract class GameInfo
    {
        public Game Game { get; set; }
        public string GameStatus { get { return Game?.Status.ToString(); } }
        public int? TriesLeft
        {
            get
            {
                if (Game?.MaxTries != null)
                {
                    return Game.MaxTries - Game.Players.Max(p => p.TriesCount);
                }
                else
                {
                    return null;
                }
            }
        }
        public IEnumerable<PlayerTry> PlayerTries
        {
            get
            {
                if (Game?.Players != null)
                {
                    foreach (var p in Game.Players)
                    {
                        if (p.Guesses != null)
                        {
                            for (int i = 0; i < p.Guesses.Count; i++)
                            {
                                yield return new PlayerTry()
                                {
                                    Answer = p.Guesses[i].AnswerString,
                                    Number = p.Guesses[i].Number,
                                    PlayerId = p.PlayerId,
                                    TryNumber = i + 1
                                };
                            }
                        }
                    }
                }

            }
        }

    }
    public class HostGameInfo : GameInfo
    {
        public string GameNumber { get { return Game?.Number.Decrypt(); } }
    }

    public class GuessGameInfo : GameInfo
    {
    }
}

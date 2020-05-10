using GuessMyNumber.API.Helper;
using GuessMyNumber.Model;
using GuessMyNumber.Provider;
using GuessMyNumber.Web.Handler;
using GuessMyNumber.Web.Helper;
using GuessMyNumber.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GuessMyNumber.Web.Controller
{

    [BasicAuthorize("gmn.eastus.cloudapp.azure.com")]
    [ApiController]
    [Route("[controller]")]
    public class GameController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly IGameProvider _gameProvider;
        public GameController(IGameProvider gameProvider)
        {
            _gameProvider = gameProvider;
        }

        /// <summary>Main page</summary>
        [HttpGet("Index")]
        public IActionResult Index([FromQuery(Name = "j")] string gameToJoin = null)
        {
            var user = this.GetCurrentUser();
            var username = user?.Username;
            if (username == null)
            {
                return this.BadRequest();
            }

            Response.Cookies.Append("PlayerName", username, new Microsoft.AspNetCore.Http.CookieOptions() { Path = "/" });

            string postCommand = null;
            if (!string.IsNullOrEmpty(gameToJoin))
            {
                gameToJoin = gameToJoin.ToUpper();
                var game = _gameProvider.GetGame(gameToJoin);
                if (game == null)
                {
                    return this.BadRequest($"{gameToJoin} does not exists");
                }

                // User can load
				if (game.PlayersDict.ContainsKey(username))
				{
					var player = game.PlayersDict[username];
					if (player.Role == Role.Host)
                    {
                        // player is hosting, just load
                        postCommand = "/host " + game.GameId;
                    }
                    else if (player.Role == Role.Guesser)
                    {
                        // player already joined as guesser
                        postCommand = "/guess " + game.GameId;
                    }
				}
                else 
                {
                    if (game.Status != GameStatus.Created)
                    {
                        return this.BadRequest($"{gameToJoin} is in status {game.Status}");
                    }
                    else
                    {
                        // player can join
                        postCommand = "/join " + game.GameId;
                    }
                }
            }

            var model = new PlayerGameModel()
            {
                Player = user,
                PostCommand = postCommand
            };

            return View(model);
        }

        /// <summary>Get data for a hosted game</summary>
        [HttpGet("GameAsHost")]
        public IActionResult GameAsHost([FromQuery] string gameId)
        {
            var username = this.GetCurrentUser()?.Username;
            if (username == null)
            {
                return this.BadRequest();
            }

            var game = _gameProvider.GetGame(gameId);

            if (game == null)
            {
                return this.BadRequest($"{gameId} does not exists");
            }

            if (game.HostPlayer.PlayerId != username)
            {
                return this.BadRequest($"{username} is not the host of game {gameId}");
            }

            var model = new HostGameInfo()
            {
                Game = game
            };

            return Json(model);
        }

        /// <summary>Get data for a guessing game</summary>
        [HttpGet("GameAsGuess")]
        public IActionResult GameAsGuess([FromQuery] string gameId)
        {
            var username = this.GetCurrentUser()?.Username;
            if (username == null)
            {
                return this.BadRequest();
            }

            var game = _gameProvider.GetGame(gameId);

            if (game == null)
            {
                return this.BadRequest($"{gameId} does not exists");
            }

            if (game.PlayersDict[username].Role != Role.Guesser)
            {
                return this.BadRequest($"{username} is not guesser of game {gameId}");
            }

            var model = new GuessGameInfo()
            {
                Game = game
            };

            return Json(model);
        }

        /// <summary>Get statistics for the given player(s)</summary>
        [HttpGet("Stats")]
        public IActionResult StatsForPlayer([FromQuery] string player = null, [FromQuery] string players = null)
        {
            var result = new List<PlayerStats>();
            var username = this.GetCurrentUser()?.Username;
            if (username == null)
            {
                return this.BadRequest();
            }
            if (!string.IsNullOrEmpty(player))
            {
                // just one, given
                result.Add(_gameProvider.GetStatsForPlayer(player));
            }
            else if (!string.IsNullOrEmpty(players))
            {
                // multiple
                foreach(var p in players.Split(new string[] { " ", ",", ";", "+" }, StringSplitOptions.RemoveEmptyEntries)
                    .OrderBy(pid => pid == username ? "A" + pid : "B" + pid))
                {
                    result.Add(_gameProvider.GetStatsForPlayer(p));
                }
            }
            else
            {
                // just one, current
                result.Add(_gameProvider.GetStatsForPlayer(username));
            }
            return Json(result);
        }


        /// <summary>Return the loadable games (games that can be joined or loaded)</summary>
        [HttpGet("GetLoadableGames")]
        public IActionResult GetLoadableGames([FromQuery]string type)
        {
            var username = this.GetCurrentUser()?.Username;
            if (username == null)
            {
                return this.BadRequest();
            }

            var games = new List<Game>();

            // get created games to which the player does not belongs
            var joinables = _gameProvider.GetJoinableGames(username)?.Games;
            if (joinables != null)
            {
                games.AddRange(joinables);
            }
            // get current games playing
            var currents = _gameProvider.GetCurrentGames(username)?.Games;
            if (currents != null)
            {
                games.AddRange(currents);
            }

            var result = games
                .Where(g => type == null ? true : 
                            (type == "host" && g.HostPlayer.PlayerId == username 
                             || type == "guess" && g.HostPlayer.PlayerId != username))
                .OrderBy(g => g.HostPlayer?.PlayerId == username ? "A" : "B")
                .ThenByDescending(g => g.CreatedOn);

            return Json(result);

        }
    }
}
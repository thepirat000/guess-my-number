/* NOT USED
 * 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GuessMyNumber.Model;
using GuessMyNumber.Provider;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GuessMyNumber.Controllers
{
    [ApiController]
    [Produces("application/json")]
    [Route("[controller]")]
    public class GameController : ControllerBase
    {
        private readonly ILogger<GameController> _logger;
        private readonly IGameProvider _gameProvider;

        public GameController(ILogger<GameController> logger, IGameProvider gameProvider)
        {
            _logger = logger;
            _gameProvider = gameProvider;
        }

        /// <summary>
        /// Creates a new game as a host
        /// </summary>
        /// <param name="playerId">The host player ID</param>
        /// <param name="number">The number to be guessed</param>
        [HttpGet("Create")]
        [ProducesResponseType(typeof(void), 400)]
        [ProducesResponseType(typeof(GameResponse), 200)]
        public ActionResult<GameResponse> CreateGame(string playerId, string number, int maxTries)
        {
            try
            {
                var response = _gameProvider.CreateGame(playerId?.ToUpper(), number, maxTries);
                if (response.HasErrors)
                {
                    return BadRequest(response.Error);
                }
                return Ok(response);
            }
            catch (Exception ex)
            {
                return this.Problem(ex.Message);
            }
        }

        /// <summary>
        /// Joins an existing game as a guesser
        /// </summary>
        /// <param name="playerId">The player ID</param>
        /// <param name="gameId">The existing game ID to join</param>
        [HttpGet("Join")]
        [ProducesResponseType(typeof(void), 400)]
        [ProducesResponseType(typeof(GameResponse), 200)]
        public ActionResult<GameResponse> JoinGame(string playerId, string gameId)
        {
            try
            {
                gameId = gameId?.ToUpper();
                var response = _gameProvider.JoinGame(playerId?.ToUpper(), gameId?.ToUpper());
                if (response.HasErrors)
                {
                    return BadRequest(response.Error);
                }
                return Ok(response);
            }
            catch (Exception ex)
            {
                return this.Problem(ex.Message);
            }
        }

        /// <summary>
        /// Signal the start for a player on an existing game 
        /// </summary>
        /// <param name="playerId">The player ID</param>
        /// <param name="gameId">The game ID to start</param>
        [HttpGet("Start")]
        [ProducesResponseType(typeof(void), 400)]
        [ProducesResponseType(typeof(GameResponse), 200)]
        public ActionResult<GameResponse> StartGame(string playerId, string gameId)
        {
            try
            {
                gameId = gameId?.ToUpper();
                var response = _gameProvider.StartGame(playerId?.ToUpper(), gameId?.ToUpper());
                if (response.HasErrors)
                {
                    return BadRequest(response.Error);
                }
                return Ok(response);
            }
            catch (Exception ex)
            {
                return this.Problem(ex.Message);
            }
        }

        /// <summary>
        /// Plays a turn
        /// </summary>
        /// <param name="playerId">The player ID</param>
        /// <param name="gameId">The game ID</param>
        /// <param name="number">The number to test</param>
        [HttpGet("Play")]
        [ProducesResponseType(typeof(void), 400)]
        [ProducesResponseType(typeof(PlayGameResponse), 200)]
        public ActionResult<PlayGameResponse> PlayGame(string playerId, string gameId, string number)
        {
            try
            {
                gameId = gameId?.ToUpper();
                var response = _gameProvider.Play(playerId?.ToUpper(), gameId?.ToUpper(), number);
                if (response.HasErrors)
                {
                    return BadRequest(response.Error);
                }
                return Ok(response);
            }
            catch (Exception ex)
            {
                return this.Problem(ex.Message);
            }
        }

        /// <summary>
        /// Get a list of joinable games for a given player
        /// </summary>
        /// <param name="playerId">The player ID</param>
        [HttpGet("List/Joinable")]
        [ProducesResponseType(typeof(void), 400)]
        [ProducesResponseType(typeof(GetGamesResponse), 200)]
        public ActionResult<GetGamesResponse> GetJoinableGames(string playerId)
        {
            try
            {
                var response = _gameProvider.GetJoinableGames(playerId?.ToUpper());
                if (response.HasErrors)
                {
                    return BadRequest(response.Error);
                }
                return Ok(response);
            }
            catch (Exception ex)
            {
                return this.Problem(ex.Message);
            }
        }

        /// <summary>
        /// Get a list of joinable games for a given player
        /// </summary>
        /// <param name="playerId">The player ID</param>
        [HttpGet("List/Joined")]
        [ProducesResponseType(typeof(void), 400)]
        [ProducesResponseType(typeof(GetGamesResponse), 200)]
        public ActionResult<GetGamesResponse> GetJoinedGames(string playerId)
        {
            try
            {
                var response = _gameProvider.GetJoinedGames(playerId?.ToUpper());
                if (response.HasErrors)
                {
                    return BadRequest(response.Error);
                }
                return Ok(response);
            }
            catch (Exception ex)
            {
                return this.Problem(ex.Message);
            }
        }

        /// <summary>
        /// Get a list of past games for a given player
        /// </summary>
        /// <param name="playerId">The player ID</param>
        [HttpGet("List/Past")]
        [ProducesResponseType(typeof(void), 400)]
        [ProducesResponseType(typeof(GetGamesResponse), 200)]
        public ActionResult<GetGamesResponse> GetPastGames(string playerId)
        {
            try
            {
                var response = _gameProvider.GetPastGames(playerId?.ToUpper());
                if (response.HasErrors)
                {
                    return BadRequest(response.Error);
                }
                return Ok(response);
            }
            catch (Exception ex)
            {
                return this.Problem(ex.Message);
            }
        }
    }
}*/
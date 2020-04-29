using GuessMyNumber.Model;

namespace GuessMyNumber.Provider
{
    public interface IGameProvider
    {
        /// <summary>
        /// Creates a new game (host)
        /// </summary>
        /// <param name="playerId">The player ID</param>
        /// <param name="number">The number to be guessed</param>
        GameResponse CreateGame(string playerId, string number, int? maxTries);

        /// <summary>
        /// Joins a created game (guesser)
        /// </summary>
        /// <param name="playerId">The player ID</param>
        /// <param name="gameId">The game ID to join</param>
        GameResponse JoinGame(string playerId, string gameId, bool startGame = false);

        /// <summary>
        /// Starts a created game (host and guesser)
        /// </summary>
        /// <param name="playerId">The player ID</param>
        /// <param name="gameId">The game ID to join</param>
        GameResponse StartGame(string playerId, string gameId);

        /// <summary>
        /// Gets the game status for a game and a player
        /// </summary>
        /// <param name="playerId">The player ID</param>
        /// <param name="gameId">The game ID</param>
        GameResponse GetGame(string playerId, string gameId);

        /// <summary>
        /// Get the current games for a player (not finished)
        /// </summary>
        /// <param name="playerId">The player ID</param>
        GetGamesResponse GetCurrentGames(string playerId);

        /// <summary>
        /// Get the games that can be joined for a player
        /// </summary>
        /// <param name="playerId">The player ID</param>
        GetGamesResponse GetJoinableGames(string playerId);

        /// <summary>
        /// Get the games to which the player is already joined
        /// </summary>
        /// <param name="playerId">The player ID</param>
        GetGamesResponse GetJoinedGames(string playerId);

        /// <summary>
        /// Get the games the player has been on
        /// </summary>
        /// <param name="playerId">The player ID</param>
        GetGamesResponse GetPastGames(string playerId);

        /// <summary>
        /// Play the game (1 try for guesser)
        /// </summary>
        /// <param name="playerId">Player ID</param>
        /// <param name="number">Gussing Number</param>
        PlayGameResponse Play(string playerId, string gameId, string number);

        /// <summary>
        /// Abandon the game
        /// </summary>
        GameResponse Abandon(string playerId, string gameId);

        /// <summary>
        /// Get a game info
        /// </summary>
        Game GetGame(string gameId);

        /// <summary>
        /// Get a player statistics
        /// </summary>
        /// <param name="playerId">The player id</param>
        PlayerStats GetStatsForPlayer(string playerId);
    }
}

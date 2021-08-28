using GuessMyNumber.API.Helper;
using GuessMyNumber.Model;
using GuessMyNumber.Provider;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace UnitTest
{
    [TestClass]
    public class UnitTests
    {
        // TODO: 
        //      - Test more than one guessers (include abandons)
        //      - Test autoStart parameter

        [TestMethod]
        public void CompleteGame_OneGuesser()
        {
            IGameProvider p = new GameProvider("test");

            var createResponse = p.CreateGame("FEDE_TEST", "123", 10);
            var game = createResponse?.Game;
            var gameId = game?.GameId;
         
            Assert.IsNotNull(gameId);
            Assert.AreEqual(GameStatus.Created, game.Status);
            Assert.AreEqual(1, game.Players.Count);
            Assert.AreEqual("123".Encrypt(), game.Number);
            Assert.AreEqual("123", game.Number.Decrypt());
            Assert.AreEqual(3, game.Digits);
            Assert.IsNull(game.WinnerPlayerId);
            Assert.AreEqual("FEDE_TEST", game.Players[0].PlayerId);
            Assert.AreEqual(Role.Host, game.Players[0].Role);
            Assert.AreEqual(PlayerGameStatus.Ready, game.Players[0].Status);
            Assert.AreEqual(0, game.Players[0].TriesCount);

            var joinFailResponse = p.JoinGame("FEDE_TEST", gameId);

            Assert.IsTrue(joinFailResponse.HasErrors);
            Assert.AreEqual("Already joined to the game", joinFailResponse.Error);

            var joinResponse = p.JoinGame("ADRI_TEST", gameId);
            game = joinResponse?.Game;

            Assert.IsNotNull(game);
            Assert.AreEqual(gameId, game.GameId);
            Assert.IsFalse(joinResponse.HasErrors);
            Assert.AreEqual(GameStatus.Created, game.Status);
            Assert.AreEqual(2, game.Players.Count);
            Assert.AreEqual("FEDE_TEST", game.Players[0].PlayerId);
            Assert.AreEqual("ADRI_TEST", game.Players[1].PlayerId);
            Assert.AreEqual(Role.Guesser, game.Players[1].Role);
            Assert.AreEqual(PlayerGameStatus.Ready, game.Players[1].Status);
            Assert.AreEqual(0, game.Players[1].TriesCount);


            var startHostResponse = p.StartGame("FEDE_TEST", gameId);
            game = startHostResponse?.Game;

            Assert.IsFalse(startHostResponse.HasErrors);
            Assert.AreEqual(GameStatus.Created, game.Status);
            Assert.AreEqual(2, game.Players.Count);
            Assert.AreEqual("FEDE_TEST", game.Players[0].PlayerId);
            Assert.AreEqual("ADRI_TEST", game.Players[1].PlayerId);
            Assert.AreEqual(PlayerGameStatus.Playing, game.Players[0].Status);
            Assert.AreEqual(PlayerGameStatus.Ready, game.Players[1].Status);

            var startGuesserResponse = p.StartGame("ADRI_TEST", gameId);
            game = startGuesserResponse?.Game;

            Assert.IsFalse(startGuesserResponse.HasErrors);
            Assert.AreEqual(GameStatus.Started, game.Status);
            Assert.AreEqual(2, game.Players.Count);
            Assert.AreEqual("FEDE_TEST", game.Players[0].PlayerId);
            Assert.AreEqual("ADRI_TEST", game.Players[1].PlayerId);
            Assert.AreEqual(PlayerGameStatus.Playing, game.Players[0].Status);
            Assert.AreEqual(PlayerGameStatus.Playing, game.Players[1].Status);

            var playFail = p.Play("FEDE_TEST", gameId, "123");
            Assert.IsTrue(playFail.HasErrors);

            var guess1 = p.Play("ADRI_TEST", gameId, "456");
            game = guess1.Game;

            Assert.IsFalse(guess1.HasErrors);
            Assert.AreEqual(GameStatus.Started, game.Status);
            Assert.AreEqual("3M", guess1.Answer.AnswerString);
            Assert.AreEqual(0, guess1.Answer.CorrectCount);
            Assert.AreEqual(0, guess1.Answer.RegularCount);
            Assert.AreEqual(3, guess1.Answer.WrongCount);
            Assert.AreEqual(1, game.Players.First(p => p.PlayerId == guess1.PlayerId).TriesCount);
            Assert.AreEqual("ADRI_TEST", guess1.PlayerId);

            var guess2 = p.Play("ADRI_TEST", gameId, "319");
            game = guess2.Game;

            Assert.IsFalse(guess2.HasErrors);
            Assert.AreEqual(GameStatus.Started, game.Status);
            Assert.AreEqual("2R", guess2.Answer.AnswerString);
            Assert.AreEqual(0, guess2.Answer.CorrectCount);
            Assert.AreEqual(2, guess2.Answer.RegularCount);
            Assert.AreEqual(1, guess2.Answer.WrongCount);
            Assert.AreEqual(2, game.Players.First(p => p.PlayerId == guess2.PlayerId).TriesCount);

            var guess3 = p.Play("ADRI_TEST", gameId, "130");
            game = guess3.Game;

            Assert.IsFalse(guess3.HasErrors);
            Assert.AreEqual(GameStatus.Started, game.Status);
            Assert.AreEqual("1B 1R", guess3.Answer.AnswerString);
            Assert.AreEqual(1, guess3.Answer.CorrectCount);
            Assert.AreEqual(1, guess3.Answer.RegularCount);
            Assert.AreEqual(1, guess3.Answer.WrongCount);
            Assert.AreEqual(3, game.Players.First(p => p.PlayerId == guess3.PlayerId).TriesCount);

            var guess4 = p.Play("ADRI_TEST", gameId, "132");
            game = guess4.Game;

            Assert.IsFalse(guess4.HasErrors);
            Assert.AreEqual(GameStatus.Started, game.Status);
            Assert.AreEqual("1B 2R", guess4.Answer.AnswerString);
            Assert.AreEqual(1, guess4.Answer.CorrectCount);
            Assert.AreEqual(2, guess4.Answer.RegularCount);
            Assert.AreEqual(0, guess4.Answer.WrongCount);
            Assert.AreEqual(4, game.Players.First(p => p.PlayerId == guess4.PlayerId).TriesCount);

            var guess5 = p.Play("ADRI_TEST", gameId, "123");
            game = guess5.Game;

            Assert.IsFalse(guess4.HasErrors);
            Assert.AreEqual(GameStatus.Finished, game.Status);
            Assert.AreEqual("3B", guess5.Answer.AnswerString);
            Assert.AreEqual(3, guess5.Answer.CorrectCount);
            Assert.AreEqual(0, guess5.Answer.RegularCount);
            Assert.AreEqual(0, guess5.Answer.WrongCount);
            Assert.AreEqual(5, game.Players.First(p => p.PlayerId == guess5.PlayerId).TriesCount);
            Assert.AreEqual(PlayerGameStatus.Winner, game.Players.First(p => p.PlayerId == guess5.PlayerId).Status);
            Assert.AreEqual(PlayerGameStatus.Loser, game.Players.First(p => p.PlayerId != guess5.PlayerId).Status);

            playFail = p.Play("ADRI_TEST", gameId, "123");
            Assert.IsTrue(playFail.HasErrors);
        }
    }
}

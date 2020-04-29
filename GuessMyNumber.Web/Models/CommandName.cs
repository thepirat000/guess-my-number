namespace GuessMyNumber.Web.Models
{
    public enum CommandName
    {
        None = 0,
        Create = 1, // Params: Number|***, MaxTries?
        Join = 2,   // Params: GameId
        Start = 3,  // Params: GameId
        Play = 4,   // Params: GameId, Number
        Abandon = 5,// Params: GameId
        Cancel = 6  // Params: GameId
    }
}

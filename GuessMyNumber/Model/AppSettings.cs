namespace GuessMyNumber.Model
{
    public class AppSettings
    {
        public string RedisConnectionString { get; set; }
        public string AnswerStringChars { get; set; }
        public int MinPlayerIdLength { get; set; }
        public int MaxGamesPerPlayer { get; set; }
        public int MinDigits { get; set; }
        public int MaxDigits { get; set; }

    }
}
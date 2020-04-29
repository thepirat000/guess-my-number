namespace GuessMyNumber.Web.Models
{
    public class Command
    {
        public CommandName Name { get; set; }
        public string[] Parameters { get; set; }
    }
}

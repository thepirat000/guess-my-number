using GuessMyNumber.Model;
using GuessMyNumber.Web.Models;
using System.Threading.Tasks;

namespace GuessMyNumber.Web.Hubs
{
    public interface IChatClient
    {
        Task ReceiveUserMessage(string user, string message);
        Task ReceiveServerMessage(string message);

        Task ReceiveCommand(string user, string commandName, string[] parameters, CommandResponse commandResponse, GameResponse gameResponse);

        Task UserListChanged(string[] connectedUsers);
    }
}

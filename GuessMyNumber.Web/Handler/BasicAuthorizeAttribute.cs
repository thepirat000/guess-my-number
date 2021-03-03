using GuessMyNumber.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuessMyNumber.Web.Handler
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class BasicAuthorizeAttribute : TypeFilterAttribute
    {
        public BasicAuthorizeAttribute(string realm = null)
            : base(typeof(BasicAuthorizeFilter))
        {
            Arguments = new object[]
            {
                realm
            };
        }
    }

    public class BasicAuthorizeFilter : IAuthorizationFilter
    {
        private readonly static List<User> Users = new List<User>
        {
            new User { Id = 1, FirstName = "Federico", LastName = "Colombo", Username = "fede", Password = "fede" },
            new User { Id = 3, FirstName = "Luciana", LastName = "Staiano", Username = "luciana", Password = "luciana" },
            new User { Id = 4, FirstName = "Adriano", LastName = "Colombo", Username = "adri", Password = "adri" },
            new User { Id = 5, FirstName = "Antonino", LastName = "Colombo", Username = "anto", Password = "anto" },
            new User { Id = 6, FirstName = "Lupita", LastName = "García", Username = "lupi", Password = "lupi" }
        };

        private readonly string realm;
        public BasicAuthorizeFilter(string realm = null)
        {
            this.realm = realm;
        }
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            string authHeader = context.HttpContext.Request.Headers["Authorization"];
            if (authHeader != null && authHeader.StartsWith("Basic "))
            {
                // Get the encoded username and password
                var encodedUsernamePassword = authHeader.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[1]?.Trim();
                // Decode from Base64 to string
                var decodedUsernamePassword = Encoding.UTF8.GetString(Convert.FromBase64String(encodedUsernamePassword));
                // Split username and password
                var username = decodedUsernamePassword.Split(':', 2)[0];
                var password = decodedUsernamePassword.Split(':', 2)[1];
                // Check if login is correct
                if (IsAuthorized(username, password))
                {
                    return;
                }
            }
            // Return authentication type (causes browser to show login dialog)
            context.HttpContext.Response.Headers["WWW-Authenticate"] = "Basic";
            // Add realm if it is not null
            if (!string.IsNullOrWhiteSpace(realm))
            {
                context.HttpContext.Response.Headers["WWW-Authenticate"] += $" realm=\"{realm}\"";
            }
            // Return unauthorized
            context.Result = new UnauthorizedResult();
        }
        
        public bool IsAuthorized(string username, string password)
        {
            // Check that username and password are correct
            return username != null && password != null
                && username.Length > 3
                && username.Length == password.Length
                && username.ToLowerInvariant() == password.ToLowerInvariant();
            //return Users.Any(u => u.Username.Equals(username, StringComparison.InvariantCultureIgnoreCase) && u.Password == password);
        }

        internal static User GetUser(string username)
        {
            var user = Users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.InvariantCultureIgnoreCase));
            if (user == null)
            {
                int id = new Random(username.GetHashCode()).Next(1000, 9999);
                return new User { Id = id, FirstName = username, LastName = "", Username = username, Password = username };
            }
            return user;
        }
    }
}

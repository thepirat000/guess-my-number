using GuessMyNumber.Web.Handler;
using GuessMyNumber.Web.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuessMyNumber.Web.Helper
{
    public static class HttpRequestExtensions
    {
        public static User GetCurrentUser (this Microsoft.AspNetCore.Mvc.Controller controller)
        {
            return GetCurrentUser(controller.Request);
        }

        public static User GetCurrentUser(this HttpRequest request)
        {
            return GetCurrentUser(request.Headers);
        }

        public static User GetCurrentUser(this IHeaderDictionary headers)
        {
            string username = string.Empty;
            string password = string.Empty;

            if (headers.TryGetValue("Authorization", out Microsoft.Extensions.Primitives.StringValues authToken))
            {
                string authHeader = authToken.First();
                string encodedUsernamePassword = authHeader.Substring("Basic ".Length).Trim();
                Encoding encoding = Encoding.GetEncoding("iso-8859-1");
                string usernamePassword = encoding.GetString(Convert.FromBase64String(encodedUsernamePassword));
                int seperatorIndex = usernamePassword.IndexOf(':');
                username = usernamePassword.Substring(0, seperatorIndex);
                password = usernamePassword.Substring(seperatorIndex + 1);
            }
            else
            {
                return null;
            }

            var user = BasicAuthorizeFilter.GetUser(username);

            return user;
        }
    }
}

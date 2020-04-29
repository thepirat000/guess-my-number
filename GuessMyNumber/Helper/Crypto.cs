using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuessMyNumber.API.Helper
{
    public static class Crypto
    {
        public static string Encrypt(this string value)
        {
            if (value == null)
            {
                return null;
            }
            return Convert.ToBase64String(value.ToCharArray().Select(x => (byte)x).ToArray());
        }
        public static string Decrypt(this string value)
        {
            if (value == null)
            {
                return null;
            }
            return Encoding.ASCII.GetString(Convert.FromBase64String(value));
        }
    }
}

using GuessMyNumber.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GuessMyNumber.Web.Models
{
    public class PlayerGameModel
    {
        public User Player { get; set; }
        // To return a command to be executed when loading the page
        public string PostCommand { get; set; }
    }

}

using GuessMyNumber.Provider;
using Microsoft.AspNetCore.Mvc;
using System;

namespace GuessMyNumber.Web.Controller
{
    [ApiController]
    [Route("[controller]")]
    public class GuestController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly IGameProvider _gameProvider;
        public GuestController(IGameProvider gameProvider)
        {
            _gameProvider = gameProvider;
        }

        [HttpGet()]
        public IActionResult Index([FromQuery(Name = "j")] string gameToJoin = null)
        {
            throw new NotImplementedException();
        }
    }
}
using GownApi.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;


namespace GownApi
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {

        private readonly ILogger<AuthController> _logger;
        private readonly GownDb _db;


        public AuthController(ILogger<AuthController> logger, GownDb db)
        {
            _logger = logger;
            _db = db;
        }


        /* [HttpPost("login")]
         public IActionResult Login([FromBody] LoginRequest request)
         {
             if (request.Username != "user" || request.Password != "password")
                 return Unauthorized();

             var tokenHandler = new JwtSecurityTokenHandler();
             var key = Encoding.ASCII.GetBytes("iREWTEWGfgweGERWgtGWgwET$#%q34GG#$%%3$##%GHBNBsgfdgwe345");
             var tokenDescriptor = new SecurityTokenDescriptor
             {
                 Subject = new ClaimsIdentity(new Claim[]
                 {
                 new Claim(ClaimTypes.Name, request.Username)
                 }),
                 Expires = DateTime.UtcNow.AddHours(1),
                 SigningCredentials = new SigningCredentials(
                     new SymmetricSecurityKey(key),
                     SecurityAlgorithms.HmacSha256Signature
                 )
             };
             var token = tokenHandler.CreateToken(tokenDescriptor);
             return Ok(new { token = tokenHandler.WriteToken(token) });
         } */

        // JWT
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            
            var user = await _db.Set<User>()
                .FirstOrDefaultAsync(u => u.Name == request.Username);

            if (user == null)
                return Unauthorized("Invalid username or password.");

            if (user.Active != true)
                return Unauthorized("User is inactive.");

            var hasher = new PasswordHasher<User>();
            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (result != PasswordVerificationResult.Success)
                return Unauthorized("Invalid username or password.");
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes("iREWTEWGfgweGERWgtGWgwET$#%q34GG#$%%3$##%GHBNBsgfdgwe345");

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.Name),
        new Claim(ClaimTypes.Email, user.Email ?? ""),
        new Claim(ClaimTypes.Role, user.Role ?? "user"), 
        new Claim("userId", user.Id.ToString())
    };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);

            return Ok(new
            {
                token = tokenHandler.WriteToken(token),
                role = user.Role,
                name = user.Name
            });
        }

        // JWT

        [HttpGet("me")]
        [Authorize]
        public IActionResult Me()
        {
            return Ok(new
            {
                name = User.Identity?.Name,
                roles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList()
            });
        }

        [HttpGet("manager-only")]
        [Authorize(Roles = "manager")]
        public IActionResult ManagerOnly()
        {
            return Ok("OK - manager access");
        }


    }

    public record LoginRequest(string Username, string Password);
}


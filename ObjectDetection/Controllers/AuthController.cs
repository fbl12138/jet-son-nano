using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ObjectDetection.Data;
using ObjectDetection.Dtos;
using ObjectDetection.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ObjectDetection.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly AppDbContext _db;
        public AuthController(AppDbContext db,IConfiguration cfg)
        {
            _db = db;
            _cfg = cfg;
        }
        private (string Token, DateTime ExpiresAt) CreateToken(User user)
        {
            var jwt = _cfg.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiresAt = DateTime.UtcNow.AddMinutes(int.Parse(jwt["ExpireMinutes"]!));
            var claims = new[]
            {
                new Claim("sub",user.Id.ToString()),
                new Claim("name",user.Username),
                new Claim("role",user.Role)
            };
            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds);
            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);

        }
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")]   // 登录是暴力破解的第一目标：单独限流，和设备上报互不影响
        public async Task<ActionResult> Login([FromBody] LoginRequestDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(c => c.Username == dto.Username);
            if (user is null) return Unauthorized(new { message = "用户名或密码错误" });
            var hasher = new PasswordHasher<User>();
            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized(new { message = "用户名或者密码错误" });
            if(result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = hasher.HashPassword(user, dto.Password);
                await _db.SaveChangesAsync();
            }
            var (token, expiresAt) = CreateToken(user);
            return Ok(new
            {
                token,
                expiresAt,
                username = user.Username,
                role = user.Role
            });
        }

    }
}

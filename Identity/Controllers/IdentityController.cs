using Identity.Data.Account;
using Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;

namespace Identity.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class IdentityController : ControllerBase
    {
        private readonly ILogger<IdentityController> _logger;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly SMTPSettings _smtpSettings;

        public IdentityController(ILogger<IdentityController> logger, UserManager<User> userManager, SignInManager<User> signInManager, IOptions<SMTPSettings> smtpSettings)
        {
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _smtpSettings = smtpSettings.Value;
        }

        [HttpPost()]
        public async Task<IActionResult> CreateUser(NewUserModel userModel)
        {
            var user = new User
            {
                Email = userModel.Email,
                UserName = userModel.Name,
            };

            var departmentClaim = new Claim("Department", userModel.Department);
            var positionClaim = new Claim("Position", userModel.Position);

            var result = await _userManager.CreateAsync(user, userModel.Password);

            if (result.Succeeded) 
            {
                await _userManager.AddClaimAsync(user, departmentClaim);
                await _userManager.AddClaimAsync(user, positionClaim);

                var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var encodedToken = WebUtility.UrlEncode(confirmationToken);

                var confirmationLink = Url.Link(nameof(ConfirmEmail), values: new { userId = user.Id, token = encodedToken });

                //TODO:Move to separate file
                var message = new MailMessage("desteronererer@gmail.com", user.Email, "Please confirm your email", $"Please click on this link to confirm your email address: {confirmationLink}");
                
                using (var emailClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port))
                {
                    emailClient.Credentials = new NetworkCredential(_smtpSettings.User, _smtpSettings.Password);

                    await emailClient.SendMailAsync(message);
                }

                return Ok("User successfully created");
            }

            return BadRequest(new
            {
                Errors = result.Errors.Select(e => e.Description)
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginModel loginModel)
        {
            var result = await _signInManager.PasswordSignInAsync(loginModel.Email, loginModel.Password, loginModel.RememberMe, false);

            if (result.Succeeded)
            {
                return Ok();
            }

            if (result.RequiresTwoFactor)
            {
                var user = await _userManager.FindByEmailAsync(loginModel.Email);

                if (user == null)
                    return NotFound("User not found.");

                var securityCode = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");

                //TODO:Move to separate file
                var message = new MailMessage("desteronererer@gmail.com", user.Email, "Please confirm your login", $"Here is your code: {securityCode}");

                using var emailClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port);
                emailClient.Credentials = new NetworkCredential(_smtpSettings.User, _smtpSettings.Password);

                await emailClient.SendMailAsync(message);
            }

            if (result.IsLockedOut)
            {
                return BadRequest("You are locked out");
            }

            return BadRequest("Failed to Login");
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
           await _signInManager.SignOutAsync();

           return Ok();
        }

        [HttpPost("mfa")]
        public async Task<IActionResult> MfaLogin(MfaModel model)
        {
            var result = await _signInManager.TwoFactorSignInAsync("Email", model.SecurityCode, model.RememberMe, false);

            if (result.Succeeded)
            {
                return Ok();
            }

            if (result.IsLockedOut)
            {
                return BadRequest("You are locked out");
            }

            return BadRequest("Failed to Login");
        }

        [HttpGet("confirm-email", Name = nameof(ConfirmEmail))]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
                return BadRequest("Invalid email confirmation request.");

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound("User not found.");

            var decodedToken = WebUtility.UrlDecode(token);
            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

            if (result.Succeeded)
                return Ok("Email confirmed successfully.");
            
            return BadRequest("Email confirmation failed.");
        }
    }
}


using Auth.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Splitwise_Back.Data;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Services;


namespace Splitwise_Back.Controllers

{
    [ApiController]
    [Route("[controller]")]
    public class UserController : Controller
    {
        private readonly ILogger<UserController> _logger;
        private readonly CloudinaryService _cloudinary;
        private readonly ITokenService _tokenService;

        private readonly UserManager<IdentityUser> _userManager;
        private readonly AppDbContext _context;

        public UserController(ILogger<UserController> logger, CloudinaryService cloudinary, UserManager<IdentityUser> userManager, ITokenService tokenService, AppDbContext context)
        {
            _logger = logger;
            _cloudinary = cloudinary;
            _userManager = userManager;
            _tokenService = tokenService;
            _context = context;
        }

        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> RegisterUser([FromForm] UserRegistrationDto newUser, [FromForm(Name = "Image")] IFormFile image)
        {
            if (image is null)
            {
                return BadRequest(new AuthResults()
                {
                    Result = false,
                    Errors = ["No image recived."]
                });
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(new AuthResults()
                {
                    Result = false,
                    Errors = ["Invalid Payload"]
                });
            }
            var user = new IdentityUser()
            {
                UserName = newUser.Name,
                Email = newUser.Email,
            };
            try
            {
                var downloadUrl = await _cloudinary.UploadImage(image);
                var isUserCreated = await _userManager.CreateAsync(user, newUser.Password);
                if (!isUserCreated.Succeeded)
                {
                    return BadRequest(new AuthResults()
                    {
                        Result = false,
                        Errors = IdentityErrorHandler.GetErrors(isUserCreated)
                    });
                }
                var Token = await _tokenService.GenerateJwtToken(user);

                if (!Token.Result)
                {
                    return StatusCode(500, Token);
                }
                Token.DownloadUrl = downloadUrl;
                return Ok(Token);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
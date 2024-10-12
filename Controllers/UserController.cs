
using Auth.Helpers;
using Auth.Models.DTOs;
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
            var user = new CustomUser()
            {
                UserName = newUser.Name,
                Email = newUser.Email,
            };
            try
            {
                var downloadUrl = await _cloudinary.UploadImage(image);
                user.ImageUrl = downloadUrl;
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

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto userLogin)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AuthResults()
                {
                    Result = false,
                    Errors = ["Invalid payload"]
                });
            }
            var existing_user = await _userManager.FindByEmailAsync(userLogin.Email);
            if (existing_user is null)
            {
                return BadRequest(new AuthResults()
                {
                    Result = false,
                    Errors = ["Email Not Registered"]
                });
            }
            var isCorrect = await _userManager.CheckPasswordAsync(existing_user, userLogin.Password);
            if (!isCorrect)
            {
                return BadRequest(new AuthResults()
                {
                    Result = false,
                    Errors = ["Invalid Passwored"]
                });
            }
            return Ok(await _tokenService.GenerateJwtToken(existing_user));
        }

        [HttpDelete]
        [Route("delete/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user is null)
            {
                return BadRequest(new AuthResults()
                {
                    Result = false,
                    Errors = ["Email Not Registered"]
                });
            }
            var isDeleted = await _userManager.DeleteAsync(user);
            if(!isDeleted.Succeeded){
                return StatusCode(500,new AuthResults()
                {
                    Result = false,
                    Errors = ["Server Error"]
                });
            }
            return Ok("User Deleted sucessfully");
        }
        [HttpPut]
        [Route("update")]

        public async Task<IActionResult> UpdateUser(int id,[FromBody] IFormFile Image, UserRegistrationDto updateUser){
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user is null)
            {
                return BadRequest(new AuthResults()
                {
                    Result = false,
                    Errors = ["Email Not Registered"]
                });
            }
            if(Image is null){

            }

            return Ok();
        }
    }
}
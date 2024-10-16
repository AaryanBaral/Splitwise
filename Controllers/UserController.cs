using Auth.Helpers;
using Splitwise_Back.Models.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        private readonly UserManager<CustomUser> _userManager;
        private readonly AppDbContext _context;

        public UserController(ILogger<UserController> logger, CloudinaryService cloudinary, UserManager<CustomUser> userManager, ITokenService tokenService, AppDbContext context)
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
        public async Task<IActionResult> DeleteUser(string id)
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
            if (!isDeleted.Succeeded)
            {
                return StatusCode(500, new AuthResults()
                {
                    Result = false,
                    Errors = ["Server Error"]
                });
            }
            return Ok("User Deleted sucessfully");
        }

        // Api testing Remaining for update
        [HttpPut]
        [Route("update/{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromForm] UserRegistrationDto updateUser,[FromForm(Name = "Image")] IFormFile? Image = null)
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
            user.Email = updateUser.Email;
            user.UserName = updateUser.Name;
            if (Image is not null)
            {
                user.ImageUrl = await _cloudinary.UploadImage(Image);
            }
            var isUpdated = await _userManager.UpdateAsync(user);
            if (!isUpdated.Succeeded)
            {
                foreach (var error in isUpdated.Errors)
                {
                    // Log or display the error descriptions
                    Console.WriteLine($"Error: {error.Code} - {error.Description}");
                }
                return StatusCode(500, new AuthResults()
                {
                    Result = false,
                    Errors = [$"{isUpdated.Errors}"]
                });
            }

            return Ok("User Updated Sucessfully.");
        }

        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            return Ok(await _userManager.Users.ToListAsync());
        }
        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> GetUserById(string id)
        {
            return Ok(await _userManager.FindByIdAsync(id));
        }
    }
}
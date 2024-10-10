using System.Reflection.Metadata.Ecma335;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
        
        private readonly UserManager<IdentityUser> _userManager;

        public UserController(ILogger<UserController> logger,CloudinaryService cloudinary,UserManager<IdentityUser> userManager )
        {
            _logger = logger;
            _cloudinary = cloudinary;
            _userManager = userManager;
        }

        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> RegisterUser([FromForm] UserRegistrationDto newUser, [FromForm(Name = "Image")] IFormFile image)
        {
            if (image is null)
            {
                return BadRequest("No image file received.");
            }
            if(!ModelState.IsValid){
                return BadRequest("");
            }
            var user = new IdentityUser(){
                UserName = newUser.Name,
                Email = newUser.Email,
            };
            try
            {
                var downloadUrl = await _cloudinary.UploadImage(image);
                var isUserCreated = await _userManager.CreateAsync(user,newUser.Password);
                if(!isUserCreated.Succeeded){
                    return StatusCode(500,"Server Error");
                }
                return Ok(new { DownloadUrl = downloadUrl });
            }
            catch (System.Exception ex)
            {
                 return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}
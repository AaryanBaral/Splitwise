using Auth.Helpers;
using Splitwise_Back.Models.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Splitwise_Back.Data;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Services.ExternalServices;
using Splitwise_Back.Services.User;


namespace Splitwise_Back.Controllers

{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : Controller
    {
        private readonly IUserService _userService;
        

        public UserController(   IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> RegisterUser([FromForm] UserRegistrationDto newUser, [FromForm(Name = "Image")] IFormFile image)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AuthResults()
                {
                    Result = false,
                    Errors = ["Invalid Payload"]
                });
            }
            var results = await _userService.CreateUserAsync(newUser, image);
            return StatusCode(results.StatusCode, new
            {
                Success = results.Success,
                Data = results.Data,
                Errors = results.Errors,
            });
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
            var results = await _userService.LoginUser(userLogin);
            return StatusCode(results.StatusCode, new
            {
                Success = results.Success,
                Data = results.Data,
                Errors = results.Errors,
            });
            
        }

        [HttpDelete]
        [Route("delete/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var results = await _userService.DeleteUser(id);
            return StatusCode(results.StatusCode, new
            {
                Success = results.Success,
                Data = results.Data,
                Errors = results.Errors,
            });
        }

        // Api testing Remaining for update
        [HttpPut]
        [Route("update/{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromForm] UpdateUserDto updateUser,[FromForm(Name = "Image")] IFormFile? Image = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AuthResults()
                {
                    Result = false,
                    Errors = ["Invalid payload"]
                });
            }
            var results = await _userService.UpdateUser(id, updateUser, Image);
            return StatusCode(results.StatusCode, new
            {
                Success = results.Success,
                Data = results.Data,
                Errors = results.Errors,
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var results = _userService.GetAllUsers();
            return StatusCode(results.StatusCode, new
            {
                Success = results.Success,
                Data = results.Data,
                Errors = results.Errors,
            });
        }
        
        
        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> GetUserById(string id)
        {
            var results = await _userService.GetSingleUserAsync(id);
            return StatusCode(results.StatusCode, new
            {
                Success = results.Success,
                Data = results.Data,
                Errors = results.Errors,
            });
        }
    }
}
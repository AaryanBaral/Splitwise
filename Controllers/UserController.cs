using System.Security.Claims;
using Splitwise_Back.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Splitwise_Back.Data;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Services.User;


namespace Splitwise_Back.Controllers

{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : Controller
    {
        private readonly IUserService _userService;
        private readonly AppDbContext _context;


        public UserController(IUserService userService, AppDbContext context)
        {
            _userService = userService;
            _context = context;
        }

        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> RegisterUser([FromForm] UserRegistrationDto newUser,
            [FromForm(Name = "Image")] IFormFile image)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AuthResults()
                {
                    Result = false,
                    Errors = ["Invalid Payload"]
                });
            }

            var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var results = await _userService.CreateUserAsync(newUser, image);
                await transaction.CommitAsync();
                return StatusCode(results.StatusCode, new
                {
                    Success = results.Success,
                    Data = results.Data,
                    Errors = results.Errors,
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
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

            var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var results = await _userService.LoginUser(userLogin);
                await transaction.CommitAsync();
                return StatusCode(results.StatusCode, new
                {
                    Success = results.Success,
                    Data = results.Data,
                    Errors = results.Errors,
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpDelete]
        [Route("delete/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var userId = User.FindFirstValue("Id");
            if (userId is null)
            {
                return StatusCode(401, "Not authorized");
            }

            var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var results = await _userService.DeleteUser(id, userId);
                await transaction.CommitAsync();
                return StatusCode(results.StatusCode, new
                {
                    Success = results.Success,
                    Data = results.Data,
                    Errors = results.Errors,
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Api testing Remaining for update
        [HttpPut]
        [Route("update/{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromForm] UpdateUserDto updateUser,
            [FromForm(Name = "Image")] IFormFile? Image = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AuthResults()
                {
                    Result = false,
                    Errors = ["Invalid payload"]
                });
            }

            var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var results = await _userService.UpdateUser(id, updateUser, Image);
                await transaction.CommitAsync();
                return StatusCode(results.StatusCode, new
                {
                    Success = results.Success,
                    Data = results.Data,
                    Errors = results.Errors,
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpGet]
        public IActionResult GetAllUsers()
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
            var results = await _userService.GetSingleUsersAsync(id);
            return StatusCode(results.StatusCode, new
            {
                Success = results.Success,
                Data = results.Data,
                Errors = results.Errors,
            });
        }
    }
}
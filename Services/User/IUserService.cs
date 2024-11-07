using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Models.DTOs;

namespace Splitwise_Back.Services.User;

public interface IUserService
{
    Task<ResponseResults<ReadUserDto>> GetSingleUserAsync(string userId);
    Task<CustomUsers> DoesUserExistsEmailAsync(string userId);
    Task<CustomUsers> GetUserIdOrThrowAsync(string userId);
    Task<ResponseResults<AuthResults>> CreateUserAsync(UserRegistrationDto newUser, IFormFile image);
    Task<ResponseResults<AuthResults>> LoginUser(UserLoginDto userLogin);
    Task<ResponseResults<string>> UpdateUser(string userId, UpdateUserDto updateUserDto, IFormFile? image);
    Task<ResponseResults<string>> DeleteUser(string userId);
    ResponseResults<List<ReadUserDto>> GetAllUsers();

}
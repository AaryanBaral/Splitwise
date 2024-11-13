using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Models.DTOs;
using Splitwise_Back.Services.Group;

namespace Splitwise_Back.Services.User;

public interface IUserService
{
    Task<ResponseResults<AuthResults>> CreateUserAsync(UserRegistrationDto newUser, IFormFile image);
    Task<ResponseResults<AuthResults>> LoginUser(UserLoginDto userLogin);
    Task<ResponseResults<string>> UpdateUser(string userId, UpdateUserDto updateUserDto, IFormFile? image);
    Task<ResponseResults<string>> DeleteUser(string userId, string currentUserId);
    ResponseResults<List<ReadUserDto>> GetAllUsers();
    Task<ResponseResults<ReadUserDto>> GetSingleUsersAsync(string id);
    Task<CustomUsers> GetUserIdOrThrowAsync(string userId);

}
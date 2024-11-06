using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;

namespace Splitwise_Back.Services.User;

public interface IUserService
{
    Task<ResponseResults<ReadUserDto>> GetUserByIdAsync(string userId);
    Task<CustomUsers?> GetSingleUserAsync(string userId);
}
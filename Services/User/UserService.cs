using Auth.Helpers;
using Microsoft.AspNetCore.Identity;
using Splitwise_Back.Controllers;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Services.ExternalServices;

namespace Splitwise_Back.Services.User;

public class UserService : IUserService
{
    private readonly ILogger<UserController> _logger;
    private readonly CloudinaryService _cloudinary;
    private readonly ITokenService _tokenService;
    private readonly UserManager<CustomUsers> _userManager;

    public UserService(ILogger<UserController> logger, CloudinaryService cloudinary,
        UserManager<CustomUsers> userManager, ITokenService tokenService)
    {
        _logger = logger;
        _cloudinary = cloudinary;
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<ResponseResults<AuthResults>> CreateUserAsync(UserRegistrationDto newUser, IFormFile image)
    {
        try
        {
            var user = new CustomUsers()
            {
                UserName = newUser.Name,
                Email = newUser.Email,
            };
            var downloadUrl = await _cloudinary.UploadImage(image);
            user.ImageUrl = downloadUrl;
            var isUserCreated = await _userManager.CreateAsync(user, newUser.Password);
            if (!isUserCreated.Succeeded)
            {
                return new ResponseResults<AuthResults>()
                {
                    Success = false,
                    StatusCode = 400,
                    Errors = IdentityErrorHandler.GetErrors(isUserCreated).ToString()
                };
            }

            var token = await _tokenService.GenerateJwtToken(user);
            token.Id = user.Id;

            if (!token.Result)
            {
                return new ResponseResults<AuthResults>()
                {
                    Success = false,
                    StatusCode = 400,
                    Errors = "Error occurred while generating JWT token"
                };
            }

            token.DownloadUrl = downloadUrl;
            return new ResponseResults<AuthResults>()
            {
                Success = true,
                StatusCode = 200,
                Data = token
            };
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex.Message, ex.Errors);
            return new ResponseResults<AuthResults>()
            {
                Success = false,
                StatusCode = ex.StatusCode,
                Errors = ex.Errors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return new ResponseResults<AuthResults>()
            {
                Success = false,
                StatusCode = 500,
                Errors = $"{ex.Message}"
            };
        }
    }
    

    public async Task<CustomUsers?> GetSingleUserAsync(string userId)
    {
        return await _userManager.FindByIdAsync(userId);
    }

    public async Task<ResponseResults<ReadUserDto>> GetUserByIdAsync(string userId)
    {
        var user = await GetSingleUserAsync(userId);
        if (user == null)
        {
            return new ResponseResults<ReadUserDto>()
            {
                Success = false,
                Errors = "User does not exist",
                StatusCode = 404,
            };
        }

        if (user.UserName == null || user.Email == null)
        {
            return new ResponseResults<ReadUserDto>()
            {
                Success = false,
                StatusCode = 400,
                Errors = "User is not valid"
            };
        }

        return new ResponseResults<ReadUserDto>()
        {
            StatusCode = 200,
            Success = true,
            Data = new ReadUserDto()
            {
                Id = user.Id,
                UserName = user.UserName,
                ImageUrl = user.ImageUrl,
                Email = user.Email,
            },
        };
    }

    public async Task<ResponseResults<string>> UpdateUser(string userId, UpdateUserDto updateUserDto, IFormFile? image)
    {
        try
        {
            var user = await GetSingleUserAsync(userId);
            if (user is null)
            {
                return new ResponseResults<string>()
                {
                    Success = false,
                    StatusCode = 400,
                    Errors = "User does not exist"
                };
            }

            user.Email = updateUserDto.Email;
            user.UserName = updateUserDto.Name;
            if (image is null)
            {
                await _userManager.UpdateAsync(user);
                return new ResponseResults<string>()
                {
                    StatusCode = 200,
                    Success = true,
                    Data = "User Updated Successfully.",
                };
            }

            if (user.ImageUrl != null)
            {
                await _cloudinary.DeleteImageByPublicIc(user.ImageUrl);
            }

            user.ImageUrl = await _cloudinary.UploadImage(image);
            await _userManager.UpdateAsync(user);
            return new ResponseResults<string>()
            {
                StatusCode = 200,
                Success = true,
                Data = "User Updated Successfully.",
            };
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex.Message, ex.Errors);
            return new ResponseResults<string>()
            {
                StatusCode = 500,
                Errors = ex.Message,
                Success = false,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return new ResponseResults<string>()
            {
                StatusCode = 500,
                Errors = ex.Message,
                Success = false,
            };
        }
    }
}
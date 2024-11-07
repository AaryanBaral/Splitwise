using Auth.Helpers;
using Microsoft.AspNetCore.Identity;
using Splitwise_Back.Controllers;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Models.DTOs;
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
    
    public async Task<CustomUsers> DoesUserExistsEmailAsync(string email)
    {
        return await _userManager.FindByEmailAsync(email) ?? throw new CustomException()
        {
            StatusCode = 400,
            Errors = "Email address is invalid"
        };
    }

    public async Task<ResponseResults<ReadUserDto>> GetSingleUserAsync(string email)
    {
        var user = await DoesUserExistsEmailAsync(email);

        if (user.UserName == null || user.Email == null)
        {
            throw new CustomException()
            {
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
            var user = await DoesUserExistsEmailAsync(userId);
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


    public async Task<ResponseResults<AuthResults>> LoginUser(UserLoginDto userLogin)
    {
        try
        {
            var user = await DoesUserExistsEmailAsync(userLogin.Email);
            var isCorrect = await _userManager.CheckPasswordAsync(user, userLogin.Password);
            if (!isCorrect)
            {
                return new ResponseResults<AuthResults>()
                {
                    Data = new AuthResults()
                    {
                        Result = false,
                        Errors = ["Invalid username or password."]
                    },
                    StatusCode = 400,
                    Success = false,
                };
            }
            var result = await _tokenService.GenerateJwtToken(user);
            result.Id = user.Id;
            return new ResponseResults<AuthResults>()
            {
                Success = true,
                Data = result,
                StatusCode = 200,
            };
            
        }
        catch (CustomException ex)
        {
            return new ResponseResults<AuthResults>()
            {
                Success = false,
                StatusCode = 404,
                Errors = "User does not exist"
            };
        }
    }

    public async Task<CustomUsers> GetUserIdOrThrowAsync(string userId)
    {
        return await _userManager.FindByIdAsync(userId) ?? throw new CustomException()
        {
            StatusCode = 404,
            Errors = "User does not exist"
        };
    }

    public async Task<ResponseResults<string>> DeleteUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new ResponseResults<string>()
            {
                Data = "Email Not Registered",
                StatusCode = 404,
                Success = false,
            };
        }
        var isDeleted = await _userManager.DeleteAsync(user);
        if (!isDeleted.Succeeded)
        {
            return new ResponseResults<string>()
            {
                Data = "Server Error.",
                StatusCode = 500,
                Success = false,
            };
        }

        return new ResponseResults<string>()
        {
            Data = "User Deleted Successfully.",
            StatusCode = 200,
            Success = true,
        };
    }

    public ResponseResults<List<ReadUserDto>> GetAllUsers()
    {
        try
        {
            var allUsers =  _userManager.Users.ToList();
            var readUserDto = allUsers.Select(e =>
            {

                if (e.Email is null || e.UserName is null)
                {
                    throw new CustomException()
                    {
                        StatusCode = 400,
                        Errors = "Email address is invalid"
                    };
                }

                return new ReadUserDto()
                {
                    Id = e.Id,
                    Email = e.Email,
                    UserName = e.UserName,
                };
            }).ToList();
            return new ResponseResults<List<ReadUserDto>>()
            {
                StatusCode = 200,
                Data = readUserDto,
                Success = true,
            };
        }
        catch (CustomException ex)
        {
            _logger.LogError(ex.Message, ex.Errors);
            return new ResponseResults<List<ReadUserDto>>()
            {
                StatusCode = 500,
                Errors = ex.Message,
                Success = false,
            };
        }
    }
}
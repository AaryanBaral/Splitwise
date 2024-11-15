using Auth.Helpers;
using Azure;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Splitwise_Back.Controllers;
using Splitwise_Back.Data;
using Splitwise_Back.Events.UserEvents;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;
using Splitwise_Back.Models.DTOs;
using Splitwise_Back.Services.ExternalServices;
using Splitwise_Back.Services.UserBalance;

namespace Splitwise_Back.Services.User;

public class UserService : IUserService
{
    private readonly IMediator _mediator;
    private readonly CloudinaryService _cloudinary;
    private readonly ITokenService _tokenService;
    private readonly UserManager<CustomUsers> _userManager;
    private readonly IUserBalanceService _userBalanceService;

    public UserService(
        IMediator mediator,
        CloudinaryService cloudinary,
        UserManager<CustomUsers> userManager,
        ITokenService tokenService,
        IUserBalanceService userBalanceService
    )
    {
        _mediator = mediator;
        _cloudinary = cloudinary;
        _userManager = userManager;
        _tokenService = tokenService;
        _userBalanceService = userBalanceService;
    }

    public async Task<ResponseResults<AuthResults>> CreateUserAsync(UserRegistrationDto newUser, IFormFile image)
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
            throw new Exception(IdentityErrorHandler.GetErrors(isUserCreated).ToString());
        }

        var token = await _tokenService.GenerateJwtToken(user);
        token.Id = user.Id;

        if (!token.Result)
        {
            throw new Exception("Error occurred while generating JWT token");
        }

        token.DownloadUrl = downloadUrl;
        return new ResponseResults<AuthResults>()
        {
            Success = true,
            StatusCode = 200,
            Data = token
        };
    }

    private async Task<CustomUsers> DoesUserExistsEmailAsync(string email)
    {
        return await _userManager.FindByEmailAsync(email) ?? throw new KeyNotFoundException("Email address is invalid");
    }

    public async Task<ResponseResults<string>> UpdateUser(string userId, UpdateUserDto updateUserDto, IFormFile? image)
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

    public async Task<ResponseResults<AuthResults>> LoginUser(UserLoginDto userLogin)
    {
        var user = await DoesUserExistsEmailAsync(userLogin.Email);
        var isCorrect = await _userManager.CheckPasswordAsync(user, userLogin.Password);
        if (!isCorrect)
        {
            throw new AuthenticationFailureException("Invalid username or password.");
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

    public async Task<ResponseResults<string>> DeleteUser(string userId, string currentUserId)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new ArgumentException("Email doesn't exists");

        var isUserSettled = await _userBalanceService.ValidateUserBalances(userId);
        if (!isUserSettled)
            throw new RequestFailedException("User not Settled");

        var userDeleteEvent = new UserDeleteEvent(userId, currentUserId);
        await _mediator.Publish(userDeleteEvent);
        // remove the user from every group

        if (user.ImageUrl != null)
            await _cloudinary.DeleteImageByPublicIc(user.ImageUrl);

        var isDeleted = await _userManager.DeleteAsync(user);
        if (!isDeleted.Succeeded)
            throw new Exception("Server Error.");

        return new ResponseResults<string>()
        {
            Data = "User Deleted Successfully.",
            StatusCode = 200,
            Success = true,
        };
    }

    public ResponseResults<List<ReadUserDto>> GetAllUsers()
    {
        var allUsers = _userManager.Users.ToList();
        var readUserDto = allUsers.Select(e =>
        {
            if (e.Email is null || e.UserName is null)
            {
                throw new ArgumentNullException(e.Email, e.UserName);
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

    public async Task<ResponseResults<ReadUserDto>> GetSingleUsersAsync(string id)
    {
        var user = await GetUserIdOrThrowAsync(id);
        
        if (user.Email is null || user.UserName is null)
            throw new KeyNotFoundException("Email address is invalid");

        return new ResponseResults<ReadUserDto>()
        {
            Data = new ReadUserDto()
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName
            },
            Success = true,
            StatusCode = 200,
        };
    }

    public async Task<CustomUsers> GetUserIdOrThrowAsync(string userId)
    {
        return await _userManager.FindByIdAsync(userId) ?? throw new KeyNotFoundException("User does not exist");
    }
}
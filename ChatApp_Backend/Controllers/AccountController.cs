using AutoMapper;
using ChatApp_Backend.DTO;
using ChatApp_Backend.DTO.Account;
using ChatApp_Backend.DTO.Group;
using ChatApp_Backend.Models;
using ChatApp_Backend.Repository;
using ChatApp_Backend.Services;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;

namespace ChatApp_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public class AccountController : ControllerBase
    {
        private readonly IUserRepository userRepository;
        private readonly IMapper mapper;
        private readonly ITokenService tokenService;
        private readonly IUserService userService;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IConfiguration configuration;

        public AccountController(IUserRepository userRepository, IMapper mapper, ITokenService tokenService, IUserService userService, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            this.userRepository = userRepository;
            this.mapper = mapper;
            this.tokenService = tokenService;
            this.userService = userService;
            this.httpContextAccessor = httpContextAccessor;
            this.configuration = configuration;
        }
        [HttpPost("Register")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<RegisterLoginResponseDTO>), 200)]
        public async Task<IActionResult> Register(RegisterRequestDTO request)
        {
            var user = await userRepository.GetUserByEmail(request.Email);
            if ( user != null)
            {
                return BadRequest(ApiResponse<object>.FailResponse("This Email is Already Exist"));
            }
            AppUser newUser = mapper.Map<AppUser>(request);
            RefreshTokenHash refreshTokenHash = await tokenService.GetRefreshToken();
            newUser.RefreshToken = refreshTokenHash.HashedRefreshToken;
            newUser.RefreshTokenExpiration = tokenService.GetRefreshTokenExp();
            newUser.UserName = await userService.GenerateUserName(newUser.FirstName, newUser.LastName);
            IdentityResult result = await userRepository.AddUser(newUser, request.Password);
            if (!result.Succeeded)
            {
                return BadRequest(ApiResponse<object>.FailResponse("Can Not Create User", result.Errors.Select(e => e.Description.ToString()).ToList()));
            }
            RegisterLoginResponseDTO responseDTO = mapper.Map<RegisterLoginResponseDTO>(newUser);
            responseDTO.AccessToken = await tokenService.GenerateAccessToken(newUser);
            responseDTO.RefreshToken = refreshTokenHash.RefreshToken;
            return Ok(ApiResponse<RegisterLoginResponseDTO>.SuccessResponse(responseDTO));
        }
        [HttpPost("Login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<RegisterLoginResponseDTO>), 200)]
        public async Task<IActionResult> Login(LoginRequestDTO loginRequest)
        {
            AppUser? user = await userRepository.GetUserByEmail(loginRequest.Email);
            if(user == null)
            {
                return BadRequest(ApiResponse<object>.FailResponse("Email or Password Invalid"));
            }
            bool isPasswordValid = await userRepository.CheckUserPassword(user, loginRequest.Password);
            if (!isPasswordValid)
            {
                return BadRequest(ApiResponse<object>.FailResponse("Email or Password Invalid"));
            }
            RefreshTokenHash refreshTokenHash = await tokenService.GetRefreshToken();
            user.RefreshToken = refreshTokenHash.HashedRefreshToken;
            user.RefreshTokenExpiration = tokenService.GetRefreshTokenExp();
            IdentityResult result = await userRepository.UpdateUser(user);
            if (!result.Succeeded)
            {
                return BadRequest(ApiResponse<object>.FailResponse("Can Not Create User", result.Errors.Select(e => e.Description.ToString()).ToList()));
            }
            RegisterLoginResponseDTO responseDTO = mapper.Map<RegisterLoginResponseDTO>(user);
            responseDTO.AccessToken = await tokenService.GenerateAccessToken(user);
            responseDTO.RefreshToken = refreshTokenHash.RefreshToken;
            return Ok(ApiResponse<RegisterLoginResponseDTO>.SuccessResponse(responseDTO));
        }
        [HttpGet("CurrentUser")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<RegisterLoginResponseDTO>), 200)]
        public async Task<IActionResult> GetCurrentUser()
        {
            Claim? claim = httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
            if(claim == null)
            {
                return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));
            }
            AppUser? appuser = await userRepository.GetUserByEmail(claim.Value);
            if(appuser == null)
            {
                return NotFound(ApiResponse<object>.FailResponse("User Not Found"));
            }
            RegisterLoginResponseDTO userResponse = mapper.Map<RegisterLoginResponseDTO>(appuser);
            return Ok(ApiResponse<RegisterLoginResponseDTO>.SuccessResponse(userResponse));
        }

        [HttpPost("NewAccessToken")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<NewAccessTokenResponse>), 200)]
        public async Task<IActionResult> NewAccessToken(AccessTokenRequest request)
        {
            string hashedRefreshToken = tokenService.HashRefreshToken(request.RefreshToken);
            AppUser? user = await userRepository.GetUserByRefreshToken(hashedRefreshToken);
            if(user == null)
            {
                return NotFound(ApiResponse<object>.FailResponse("RefreshToken Not Exist"));
            }
            else if(user.RefreshTokenExpiration < DateTime.Now)
            {
                return BadRequest(ApiResponse<object>.FailResponse("RefreshToken Expired"));
            }
            string accessToken = await tokenService.GenerateAccessToken(user);
            NewAccessTokenResponse response = new NewAccessTokenResponse { AccessToken = accessToken };
            return Ok(ApiResponse<NewAccessTokenResponse>.SuccessResponse(response));
        }

        [HttpGet("/auth/google")]
        [AllowAnonymous]
        [ProducesResponseType(302)]
        public IActionResult GetGoogleAuthUri()
        {
            GoogleUrlBuilderDTO googleUrlBuilder = tokenService.BuildGoogleAuthUrl();
            return Redirect(googleUrlBuilder.ToString());
        }
        [HttpGet("/auth/google/callback")]
        [ProducesResponseType(typeof(ApiResponse<RegisterLoginResponseDTO>), 200)]
        public async Task<IActionResult> GetCodeResponse([FromQuery]string code)
        {
            GoogleUrlBuilderDTO googleUrlBuilder = tokenService.BuildGoogleAuthUrl();
            GoogleTokenResponseDTO? tokenData;
            GoogleJsonWebSignature.Payload payload;
            try
            {
                tokenData = await tokenService.GetGoogleToken(code);
                if (tokenData?.IdToken == null)
                    return BadRequest(ApiResponse<object>.FailResponse("Failed to get token from Google"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.FailResponse("Can Not Authenticate with Google", [ex.Message]));
            }
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { googleUrlBuilder.ClientID }
                };
                payload =
                    await GoogleJsonWebSignature.ValidateAsync(tokenData.IdToken, settings);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.FailResponse("Validation Failled", [ex.Message]));
            }
            AppUser? user = await userRepository.GetUserByEmail(payload.Email);
            var refreshTokenHashed = await tokenService.GetRefreshToken();
            if(user == null)
            {
                user = new AppUser();
                user.Email = payload.Email;
                user.FirstName = payload.GivenName;
                user.LastName = payload.FamilyName;
                user.UserName = payload.Email.Split("@")[0];
                user.RefreshToken = refreshTokenHashed.HashedRefreshToken;
                user.RefreshTokenExpiration = tokenService.GetRefreshTokenExp();
                var result = await userRepository.AddUserNoPass(user);
                if (!result.Succeeded)
                {
                    return BadRequest(ApiResponse<object>.FailResponse("Failed to Create User", result.Errors.Select(e => e.Description.ToString()).ToList()));
                }
            }
            else
            {
                user.RefreshToken = refreshTokenHashed.HashedRefreshToken;
                user.RefreshTokenExpiration = tokenService.GetRefreshTokenExp();
                var result = await userRepository.UpdateUser(user);
                if (!result.Succeeded)
                {
                    return BadRequest(ApiResponse<object>.FailResponse("Failed to Get User", result.Errors.Select(e => e.Description.ToString()).ToList()));
                }
            }
            RegisterLoginResponseDTO responseDTO = mapper.Map<RegisterLoginResponseDTO>(user);
            responseDTO.AccessToken = await tokenService.GenerateAccessToken(user);
            responseDTO.RefreshToken = refreshTokenHashed.RefreshToken;

            var frontendBaseUrl = configuration["FrontendBaseUrl"] ?? "http://localhost:4200";
            var redirectUrl = $"{frontendBaseUrl}/auth/callback" +
                $"?accessToken={Uri.EscapeDataString(responseDTO.AccessToken)}" +
                $"&refreshToken={Uri.EscapeDataString(responseDTO.RefreshToken)}" +
                $"&id={Uri.EscapeDataString(responseDTO.Id)}" +
                $"&firstName={Uri.EscapeDataString(responseDTO.FirstName)}" +
                $"&lastName={Uri.EscapeDataString(responseDTO.LastName)}" +
                $"&email={Uri.EscapeDataString(responseDTO.Email ?? "")}" +
                $"&userName={Uri.EscapeDataString(responseDTO.UserName ?? "")}";
            return Redirect(redirectUrl);
        }

        [HttpGet("SearchUsers")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<List<UserSearchResultDTO>>), 200)]
        public async Task<IActionResult> SearchUsers([FromQuery] string query)
        {
            Claim? claim = httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (claim == null)
                return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return Ok(ApiResponse<List<UserSearchResultDTO>>.SuccessResponse(new List<UserSearchResultDTO>()));

            var users = await userRepository.SearchUsers(query, claim.Value);
            var results = mapper.Map<List<UserSearchResultDTO>>(users);
            return Ok(ApiResponse<List<UserSearchResultDTO>>.SuccessResponse(results));
        }

        [HttpGet("logout")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> Logout()
        {
            Claim? claim = httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
            if (claim == null)
            {
                return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));
            }
            AppUser? appuser = await userRepository.GetUserByEmail(claim.Value);
            if (appuser == null)
            {
                return NotFound(ApiResponse<object>.FailResponse("User Not Found"));
            }
            appuser.RefreshToken = null;
            appuser.RefreshTokenExpiration = null;
            var result = await userRepository.UpdateUser(appuser);
            if (!result.Succeeded)
            {
                return BadRequest(ApiResponse<object>.FailResponse("Can Not Revoke the Refresh Token", result.Errors.Select(e => e.Description).ToList()));
            };
            return Ok(ApiResponse<object>.SuccessResponse(null, message: "User Logged out Successfully"));
        }

    }
}

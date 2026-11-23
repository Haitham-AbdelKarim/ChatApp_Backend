using ChatApp_Backend.Context;
using ChatApp_Backend.DTO;
using ChatApp_Backend.DTO.Account;
using ChatApp_Backend.DTO.Group;
using ChatApp_Backend.DTO.Message;
using ChatApp_Backend.Exceptions;
using ChatApp_Backend.Hubs;
using ChatApp_Backend.Models;
using ChatApp_Backend.RealTime.Options;
using ChatApp_Backend.RealTime.Sockets;
using ChatApp_Backend.Repository;
using ChatApp_Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ChatApp_Backend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .SelectMany(x => x.Value.Errors)
                        .Select(x => x.ErrorMessage)
                        .ToList();
                    var response = ApiResponse<object>.FailResponse("Validation failed", errors);
                    return new BadRequestObjectResult(response);
                };
            });
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            builder.Services.AddCors(setup =>
            {
                setup.AddPolicy("AllowAll", policy =>
                {
                    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                        ?? new[] { "http://localhost:4200", "http://localhost:5238" };

                    policy
                        .WithOrigins(allowedOrigins)
                        .SetIsOriginAllowed(_ => true)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });


            builder.Services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ChatAppDbContext>();

            builder.Services.AddDbContext<ChatAppDbContext>(option =>
            {
                option.UseSqlServer(builder.Configuration.GetConnectionString("cs"));
            });

            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IGroupRepositroy, GroupRepositroy>();
            builder.Services.AddScoped<IMessageRepository, MessageRepository>();
            builder.Services.AddScoped<IUserConnectionIDsRepository, UserConnectionIDsRepository>();

            builder.Services.AddAutoMapper(config =>
            {
                config.CreateMap<RegisterRequestDTO, AppUser>();
                config.CreateMap<AppUser, RegisterLoginResponseDTO>();
                config.CreateMap<CreateGroupRequestDTO, Group>();
                config.CreateMap<Group, GroupResponseDTO>();
                config.CreateMap<Messages, MessageDTO>();
                config.CreateMap<AppUser, UserSearchResultDTO>();

            });

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(option =>
            {
                //option.UseSecurityTokenValidators = true;
                option.RequireHttpsMetadata = false;
                option.SaveToken = true;
                option.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["JwtSetting:Issuer"],
                    ValidAudience = builder.Configuration["JwtSetting:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSetting:Key"] ?? string.Empty))
                };

                option.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/chatHub") || path.StartsWithSegments("/videoHub")))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });
            builder.Services.AddSignalR();
            builder.Services.AddHttpClient();

            builder.Services.Configure<SocketServerOptions>(builder.Configuration.GetSection("SocketServer"));
            builder.Services.AddHostedService<SocketServerHostedService>();

            builder.Services.AddEndpointsApiExplorer();
            var app = builder.Build();

            app.MapHub<ChatHub>("/chatHub");
            app.MapHub<VideoHub>("/videoHub");
            app.UseCors("AllowAll");
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseExceptionHandler(o => { });
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapFallbackToFile("index.html");
            app.MapControllers();
            app.Run();
        }
    }
}

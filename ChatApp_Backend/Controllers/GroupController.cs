using AutoMapper;
using ChatApp_Backend.DTO;
using ChatApp_Backend.DTO.Group;
using ChatApp_Backend.Models;
using ChatApp_Backend.Repository;
using ChatApp_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatApp_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GroupController : ControllerBase
    {
        private readonly IMapper mapper;
        private readonly IGroupRepositroy groupRepositroy;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IUserRepository userRepository;
        private readonly IUserService userService;

        public GroupController(IMapper mapper, IGroupRepositroy groupRepositroy, IHttpContextAccessor httpContextAccessor, IUserRepository userRepository, IUserService userService)
        {
            this.mapper = mapper;
            this.groupRepositroy = groupRepositroy;
            this.userRepository = userRepository;
            this.userService = userService;
        }
        [HttpPost("CreateGroup")]
        public async Task<IActionResult> CreateGroup(CreateGroupRequestDTO request)
        {
            AppUser? appuser = await userService.GetCurrentUser(httpContextAccessor);
            if (appuser == null) return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));
            Group group = mapper.Map<Group>(request);
            group.CreatedAt = DateTime.Now;
            group.Users.Add(appuser);
            try
            {
                groupRepositroy.CreateGroup(group);
                groupRepositroy.SaveChanges();
            }
            catch (Exception ex)
            {
                    return BadRequest(ApiResponse<object>.FailResponse("Can Not Create the Group", [ex.Message]));
            }
            GroupResponseDTO groupResponseDTO = mapper.Map<GroupResponseDTO>(group);
            groupResponseDTO.NumOFUsers = group.Users.Count;
            return Ok(ApiResponse<GroupResponseDTO>.SuccessResponse(groupResponseDTO));
        }
        [HttpPost("JoinGroup/{id}")]
        public async Task<IActionResult> JoinGroup(int id)
        {
            AppUser? appuser = await userService.GetCurrentUser(httpContextAccessor);
            if (appuser == null) return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));
            Group? group = groupRepositroy.GetGroupById(id);
            if (group == null) return BadRequest(ApiResponse<object>.FailResponse("Group Not Found"));
            group.Users.Add(appuser);
            try{ groupRepositroy.SaveChanges();}
            catch (Exception ex){ return BadRequest(ApiResponse<object>.FailResponse("Can Not Join the Group", [ex.Message]));}
            return Ok(ApiResponse<object>.SuccessResponse(null));
        }
        [HttpDelete("LeaveGroup/{id}")]
        public async Task<IActionResult> LeaveGroup(int id)
        {
            AppUser? appuser = await userService.GetCurrentUser(httpContextAccessor);
            if (appuser == null) return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));
            Group? group = groupRepositroy.GetGroupById(id);
            if (group == null) return BadRequest(ApiResponse<object>.FailResponse("Group Not Found"));
            if (group.Users.FirstOrDefault(u => u.Id == appuser.Id) == null) return BadRequest(ApiResponse<object>.FailResponse("Curren User Is Not in the Group"));
            group.Users.Remove(appuser);
            if (group.Users.Count == 0)
                groupRepositroy.DeleteGroup(group);
            try { groupRepositroy.SaveChanges(); }
            catch (Exception ex) { return BadRequest(ApiResponse<object>.FailResponse("Can Not Leave the Group", [ex.Message])); }
            return NoContent();
        }
        [HttpDelete("DeleteGroup/{id}")]
        public async Task<IActionResult> DeleteGroup(int id)
        {
            AppUser? appuser = await userService.GetCurrentUser(httpContextAccessor);
            if (appuser == null) return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));
            Group? group = groupRepositroy.GetGroupById(id);
            if (group == null) return BadRequest(ApiResponse<object>.FailResponse("Group Not Found"));
            if (group.Users.FirstOrDefault(u => u.Id == appuser.Id) == null) return BadRequest(ApiResponse<object>.FailResponse("Curren User Is Not in the Group"));
            try { groupRepositroy.DeleteGroup(group); groupRepositroy.SaveChanges(); }
            catch (Exception ex) { return BadRequest(ApiResponse<object>.FailResponse("Can Not Join the Group", [ex.Message])); }
            return NoContent();
        }

        [HttpGet("NumOFUser/{id}")]
        public IActionResult GetNumOfUserInGroup(int id)
        {
            Group? group = groupRepositroy.GetGroupById(id);
            if (group == null) return BadRequest(ApiResponse<object>.FailResponse("Group Not Found"));
            int num = group.Users.Count;
            return Ok(ApiResponse<int>.SuccessResponse(num));
        }
    }
}

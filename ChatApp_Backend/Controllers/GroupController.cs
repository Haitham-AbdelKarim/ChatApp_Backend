using AutoMapper;
using ChatApp_Backend.DTO;
using ChatApp_Backend.DTO.Group;
using ChatApp_Backend.Hubs;
using ChatApp_Backend.Models;
using ChatApp_Backend.Repository;
using ChatApp_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ChatApp_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public class GroupController : ControllerBase
    {
        private readonly IMapper mapper;
        private readonly IGroupRepositroy groupRepositroy;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IUserRepository userRepository;
        private readonly IUserService userService;
        private readonly IHubContext<ChatHub> hubContext;
        private readonly IUserConnectionIDsRepository userConnectionIDsRepository;

        public GroupController(IMapper mapper, IGroupRepositroy groupRepositroy, IHttpContextAccessor httpContextAccessor, IUserRepository userRepository, IUserService userService, IHubContext<ChatHub> hubContext, IUserConnectionIDsRepository userConnectionIDsRepository)
        {
            this.mapper = mapper;
            this.groupRepositroy = groupRepositroy;
            this.httpContextAccessor = httpContextAccessor;
            this.userRepository = userRepository;
            this.userService = userService;
            this.hubContext = hubContext;
            this.userConnectionIDsRepository = userConnectionIDsRepository;
        }
        [HttpPost("CreateGroup")]
        [ProducesResponseType(typeof(ApiResponse<GroupResponseDTO>), 200)]
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
            var connectionIds = userConnectionIDsRepository.GetUserConnectionID(appuser.Id);
            foreach (var cid in connectionIds)
                await hubContext.Groups.AddToGroupAsync(cid, group.Id.ToString());
            return Ok(ApiResponse<GroupResponseDTO>.SuccessResponse(groupResponseDTO));
        }
        [HttpPost("JoinGroup/{id}")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> JoinGroup(int id)
        {
            AppUser? appuser = await userService.GetCurrentUser(httpContextAccessor);
            if (appuser == null) return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));
            Group? group = groupRepositroy.GetGroupById(id);
            if (group == null) return BadRequest(ApiResponse<object>.FailResponse("Group Not Found"));
            group.Users.Add(appuser);
            try{ groupRepositroy.SaveChanges();}
            catch (Exception ex){ return BadRequest(ApiResponse<object>.FailResponse("Can Not Join the Group", [ex.Message]));}
            var connectionIds = userConnectionIDsRepository.GetUserConnectionID(appuser.Id);
            foreach (var cid in connectionIds)
                await hubContext.Groups.AddToGroupAsync(cid, id.ToString());
            return Ok(ApiResponse<object>.SuccessResponse(null));
        }
        [HttpDelete("LeaveGroup/{id}")]
        [ProducesResponseType(204)]
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
        [ProducesResponseType(204)]
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
        [ProducesResponseType(typeof(ApiResponse<int>), 200)]
        public IActionResult GetNumOfUserInGroup(int id)
        {
            Group? group = groupRepositroy.GetGroupById(id);
            if (group == null) return BadRequest(ApiResponse<object>.FailResponse("Group Not Found"));
            int num = group.Users.Count;
            return Ok(ApiResponse<int>.SuccessResponse(num));
        }

        [HttpGet("GetUserGroups")]
        [ProducesResponseType(typeof(ApiResponse<List<GroupResponseDTO>>), 200)]
        public async Task<IActionResult> GetUserGroups()
        {
            AppUser? appuser = await userService.GetCurrentUser(httpContextAccessor);
            if (appuser == null) return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));
            List<GroupResponseDTO> response = new List<GroupResponseDTO>();
            foreach (var group in appuser.Groups)
            {
                GroupResponseDTO gRes = mapper.Map<GroupResponseDTO>(group);
                gRes.NumOFUsers = group.Users.Count;
                response.Add(gRes);
            }
            return Ok(ApiResponse<List<GroupResponseDTO>>.SuccessResponse(response));
        }

        [HttpGet("GetAllGroups")]
        [ProducesResponseType(typeof(ApiResponse<List<GroupResponseDTO>>), 200)]
        public async Task<IActionResult> GetAllGroups()
        {
            AppUser? appuser = await userService.GetCurrentUser(httpContextAccessor);
            if (appuser == null) return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));
            List<Group> groups = groupRepositroy.GetAllGroups();
            var userGroupIds = appuser.Groups.Select(g => g.Id).ToHashSet();
            var response = groups.Select(g =>
            {
                var dto = mapper.Map<GroupResponseDTO>(g);
                dto.NumOFUsers = g.Users.Count;
                dto.IsMember = userGroupIds.Contains(g.Id);
                return dto;
            }).ToList();
            return Ok(ApiResponse<List<GroupResponseDTO>>.SuccessResponse(response));
        }
    }
}

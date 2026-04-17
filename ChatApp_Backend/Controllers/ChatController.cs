using ChatApp_Backend.DTO;
using ChatApp_Backend.DTO.Message;
using ChatApp_Backend.Models;
using ChatApp_Backend.Repository;
using ChatApp_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IMessageRepository messageRepository;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IUserService userService;

        public ChatController(IMessageRepository messageRepository, IHttpContextAccessor httpContextAccessor, IUserService userService)
        {
            this.messageRepository = messageRepository;
            this.httpContextAccessor = httpContextAccessor;
            this.userService = userService;
        }
        [HttpGet("GetUserChats")]
        public async Task<IActionResult> GetUserChat()
        {
            AppUser? appuser = await userService.GetCurrentUser(httpContextAccessor);
            if (appuser == null) return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));
            List<GetChatsDTO> chats = messageRepository.GetUserChats(appuser);
            return Ok(ApiResponse<List<GetChatsDTO>>.SuccessResponse(chats));
        }
        public async Task<IActionResult> GetDirectChatMessage(string id)
        {
            AppUser? appuser = await userService.GetCurrentUser(httpContextAccessor);
            if (appuser == null) return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));
            List<MessageDTO> chat = messageRepository.GetDirectChatMessage(appuser.Id, id);
            return Ok(ApiResponse<List<MessageDTO>>.SuccessResponse(chat));
        }

    }
}

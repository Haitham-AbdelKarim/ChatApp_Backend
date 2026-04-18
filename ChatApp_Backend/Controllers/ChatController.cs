using ChatApp_Backend.DTO;
using ChatApp_Backend.DTO.Group;
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
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
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
        [ProducesResponseType(typeof(ApiResponse<List<GetChatsDTO>>), 200)]
        public async Task<IActionResult> GetUserChat()
        {
            AppUser? appuser = await userService.GetCurrentUser(httpContextAccessor);
            if (appuser == null) return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));
            List<GetChatsDTO> chats = messageRepository.GetUserChats(appuser);
            return Ok(ApiResponse<List<GetChatsDTO>>.SuccessResponse(chats));
        }
        [HttpGet("GetDirectChatMessage/{id}")]
        [ProducesResponseType(typeof(ApiResponse<List<MessageDTO>>), 200)]
        public async Task<IActionResult> GetDirectChatMessage(string id)
        {
            AppUser? appuser = await userService.GetCurrentUser(httpContextAccessor);
            if (appuser == null) return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));
            List<MessageDTO> chat = messageRepository.GetDirectChatMessage(appuser.Id, id);
            return Ok(ApiResponse<List<MessageDTO>>.SuccessResponse(chat));
        }

        [HttpGet("GetGroupChatMessage/{id}")]
        [ProducesResponseType(typeof(ApiResponse<List<MessageDTO>>), 200)]
        public async Task<IActionResult> GetGroupChatMessage(int id)
        {
            AppUser? appuser = await userService.GetCurrentUser(httpContextAccessor);
            if (appuser == null) return BadRequest(ApiResponse<object>.FailResponse("Can Not Get The Current User"));
            List<MessageDTO> chat = messageRepository.GetGroupChatMessage(id);
            return Ok(ApiResponse<List<MessageDTO>>.SuccessResponse(chat));
        }

        [HttpPost("upload")]
        [ProducesResponseType(typeof(ApiResponse<string>), 200)]
        public async Task<IActionResult> Upload([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file");

            var folder = Path.Combine("wwwroot", "uploads");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(folder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"/uploads/{fileName}";

            return Ok(ApiResponse<string>.SuccessResponse(url));
        }

    }
}

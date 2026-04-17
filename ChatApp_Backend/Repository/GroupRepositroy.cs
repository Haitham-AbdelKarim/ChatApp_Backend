using ChatApp_Backend.Context;
using ChatApp_Backend.Models;

namespace ChatApp_Backend.Repository
{
    public class GroupRepositroy : IGroupRepositroy
    {
        private readonly ChatAppDbContext chatAppDbContext;

        public GroupRepositroy(ChatAppDbContext chatAppDbContext)
        {
            this.chatAppDbContext = chatAppDbContext;
        }
        public void CreateGroup(Group group)
        {
            chatAppDbContext.Groups.Add(group);
        }

        public void DeleteGroup(Group group)
        {
            chatAppDbContext.Groups.Remove(group);
        }

        public List<Group> GetAllGroups()
        {
            return chatAppDbContext.Groups.ToList();
        }

        public Group? GetGroupById(int id)
        {
            return chatAppDbContext.Groups.FirstOrDefault(g => g.Id == id);
        }

        public List<Group> GetGroupsByName(string groupName)
        {
            return chatAppDbContext.Groups.Where(g => g.Name == groupName).ToList();
        }

        public void SaveChanges()
        {
            chatAppDbContext.SaveChanges();
        }
    }
}

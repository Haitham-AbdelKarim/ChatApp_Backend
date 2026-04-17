using ChatApp_Backend.Models;

namespace ChatApp_Backend.Repository
{
    public interface IGroupRepositroy
    {
        public Group? GetGroupById(int id);
        public List<Group> GetAllGroups();
        public void DeleteGroup(Group group);
        public void CreateGroup(Group group);
        public List<Group> GetGroupsByName(string groupName);
        public void SaveChanges();
    }
}

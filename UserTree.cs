using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TQServer
{
    public static class UserTree
    {
        public static User Anonymous { get; private set; }
        public static List<User> Users = new List<User>();
        public static void Init()
        {
            Anonymous = new User();
            Anonymous.Username = "anonymous";
            if (Config.AllowAnonymous) { Anonymous.AddScope(Config.AnonymousScope); }
        }

        public static bool AddUser(User user)
        {
            if(Users.Contains(user)) return false;

            Users.Add(user);
            return true;
        }

        public static User GetUserByName(string name)
        {
            name = name.ToLower();

            foreach (User user in Users)
            {
                if(user.Username == name) return user;
            }

            return null;
        }
    }
}

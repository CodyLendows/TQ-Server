using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TQServer
{
    public class Scope
    {
        /* 
         
            Used permissions:

            Read     - read files and databases
            Traverse - read filesystem

            Write    - self explanatory
            Net      - run network-related commands

            Exec     - run operating system programs and commands. dangerous, allows privesc and escaping shell.
            Admin    - matches all. give this to the Anonymous scope if you want your server to get hatefucked

        */

        public string Name { get; set; }
        public List<string> Permissions { get; set; }
        public Scope(string name, IEnumerable<string> permissions = null)
        {
            Name = name;
            Permissions = permissions != null ? new List<string>(permissions) : new List<string>();
        }

        // Add a permission to the scope
        public void AddPermission(string permission)
        {
            if (!Permissions.Contains(permission))
            {
                Permissions.Add(permission);
            }
        }

        // Remove a permission from the scope
        public bool RemovePermission(string permission)
        {
            return Permissions.Remove(permission);
        }

        // Check if a permission exists within the scope
        public bool HasPermission(string permission)
        {
            return Permissions.Contains(permission) || Permissions.Contains("Admin");
        }

        public bool ContainsAny(List<string> permissions)
        {
            foreach (string permission in Permissions)
            {
                if (permissions.Contains(permission)) return true;
            }
            return false;
        }

        public override string ToString()
        {
            return $"{Name}[{string.Join(", ", Permissions)}]";
        }
    }

}

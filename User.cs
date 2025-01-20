using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TQServer
{
    public class User
    {
        public List<Scope> Scopes { get; set; } = new List<Scope>();
        public string Username { get; set; }
        public string PasswdPath { get; set; }

        // SCOPES
        public void AddScope(Scope scope)
        {
            if (scope == null) { throw new ArgumentNullException(nameof(scope), "Scope cannot be null"); }

            if (Scopes == null) { Scopes = new List<Scope>(); }

            if (!Scopes.Contains(scope))
            {
                Scopes.Add(scope);
            }
        }


        public bool RemoveScope(Scope scope)
        {
            return Scopes.Remove(scope);
        }

        // Check if the client has a specific permission in any scope
        public bool HasPermission(string permission)
        {
            if(Scopes == null || Scopes.Count == 0) { return false; }

            foreach (var scope in Scopes)
            {
                if (scope != null && scope.HasPermission(permission)) 
                {
                    return true;
                }
            }
            return false;
        }
    }
}

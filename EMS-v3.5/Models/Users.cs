using Microsoft.AspNetCore.Identity;

namespace EMS_v2._2.Models
{
    public class Users : IdentityUser
    {
        public string FullName { get; set; }

        // Add building type field Industrial or Residential
        public string BuildingType { get; set; }
    }
}

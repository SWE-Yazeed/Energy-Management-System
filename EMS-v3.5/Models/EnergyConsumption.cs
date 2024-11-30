using System;
using System.ComponentModel.DataAnnotations;

namespace EMS_v2._2.Models
{
    public class EnergyConsumption
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Date required")]
        public DateTime Date { get; set; }

        [Range(0.1, double.MaxValue, ErrorMessage = "Power consumption must be a positive value")]
        public double? EnergyUsed { get; set; }

        [Required(ErrorMessage = "Location required")]
        public string Location { get; set; }

        [Required(ErrorMessage = "Building type required")]
        public string BuildingType { get; set; }

        // Additional fields for each type of building
        public int? RoomCount { get; set; }
        public int? PersonCount { get; set; }
        public int? ApplianceCount { get; set; }
        public int? MachineCount { get; set; }

        [ScaffoldColumn(false)]
        public string UserId { get; set; }
        public bool IsHighConsumption { get; set; }
    }
}

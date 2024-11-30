using EMS_v2._2.Data;
using EMS_v2._2.Models;
using EMS_v2._2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace EMS_v2._2.Controllers
{
    [Authorize]
    public class EnergyController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly PredictionService _predictionService;
        private readonly UserManager<Users> _userManager;

        public EnergyController(ApplicationDbContext db, PredictionService predictionService, UserManager<Users> userManager)
        {
            _db = db;
            _predictionService = predictionService;
            _userManager = userManager;

            // Train models for each location
            _predictionService.TrainModelsByLocation(_db.EnergyConsumptions.ToList());
            Console.WriteLine("Model training for each location attempted.");
        }

        // Display the user's dashboard
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "You must be logged in to view the control panel.";
                return RedirectToAction("Login", "Account");
            }

            // Fetch the user's consumption data from the database
            var consumptions = await _db.EnergyConsumptions
                                        .Where(e => e.UserId == user.Id)
                                        .ToListAsync() ?? new List<EnergyConsumption>();

            // Calculate the total power consumption
            double totalConsumption = consumptions.Sum(e => e.EnergyUsed ?? 0);
            ViewBag.TotalConsumption = totalConsumption;

            // Calculate the consumption of each record and store it in ViewData
            ViewData["CalculatedConsumptions"] = consumptions.Any()
                ? consumptions.ToDictionary(
                    consumption => consumption.Id,
                    consumption => CalculateConsumption(consumption)
                  )
                : new Dictionary<int, double>();

            // Calculate consumption forecasts for each location based on its individual characteristics
            var predictionsByLocation = consumptions.Any()
                ? consumptions.GroupBy(e => e.Location)
                    .Select(g =>
                    {
                        int roomCount = g.Sum(e => e.RoomCount ?? 0);
                        string buildingType = g.FirstOrDefault()?.BuildingType ?? "Unknown";
                        int personCount = g.Sum(e => e.PersonCount ?? 0);
                        int applianceCount = g.Sum(e => e.ApplianceCount ?? 0);
                        int machineCount = g.Sum(e => e.MachineCount ?? 0);
                        string location = g.Key; // Add Location

                        float predictedUsage;

                        try
                        {
                            // Use the appropriate template for the location
                            predictedUsage = _predictionService.PredictEnergyUsage(
                                roomCount,
                                buildingType,
                                personCount,
                                applianceCount,
                                machineCount,
                                location // Pass Location
                            );
                        }
                        catch (InvalidOperationException ex)
                        {
                            // If no form exists for this location
                            Debug.WriteLine($"Prediction failed for location {location}: {ex.Message}");
                            predictedUsage = 0; // Default value 
                        }

                        return new
                        {
                            Location = location,
                            RecordCount = g.Count(),
                            PredictedUsage = predictedUsage
                        };
                    })
                    .ToList<dynamic>()
                : new List<dynamic>();

            // Store forecasts by location in ViewData to display in the interface
            ViewData["PredictionsByLocation"] = predictionsByLocation;

            // Determine whether each consumption is high or not
            foreach (var consumption in consumptions)
            {
                consumption.IsHighConsumption = IsHighConsumption(consumption);
            }

            // Generate technical recommendations based on the user's latest consumption history
            ViewBag.TechRecommendations = consumptions.Any()
                ? GenerateTechRecommendations(consumptions.Last())
                : "There are no technical recommendations currently.";

            // Display the Dashboard's View
            return View("Dashboard", consumptions);
        }


        // Display the form for adding a new consumption
        public IActionResult AddConsumption()
        {
            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
            {
                TempData["Error"] = "You must be logged in to add consumption.";
                return RedirectToAction("Login", "Account");
            }

            var energy = new EnergyConsumption
            {
                BuildingType = user.BuildingType
            };

            ViewData["Action"] = "AddConsumption";
            ViewData["Title"] = "Add consumption";
            return View("ConsumptionForm", energy);
        }

        // Add a new consumption (POST function)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddConsumption(EnergyConsumption energy)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "You must be logged in to add consumption.";
                return RedirectToAction("Login", "Account");
            }

            // Set the user ID for the new record
            energy.UserId = user.Id;
            energy.BuildingType = user.BuildingType ?? "Residential"; // Set a default value if it is null

            ModelState.Remove("UserId"); // Remove UserId verification because we set it manually

            // Validate additional fields based on building type
            if (energy.BuildingType == "Residential" && (!energy.RoomCount.HasValue || !energy.PersonCount.HasValue || !energy.ApplianceCount.HasValue))
            {
                ModelState.AddModelError("", "Enter the number of rooms, people, and devices for the apartment building.");
            }
            else if (energy.BuildingType == "Industrial" && !energy.MachineCount.HasValue)
            {
                ModelState.AddModelError("", "Make sure to enter the number of machines for the industrial building.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Add the following line to comment out any calculation of the EnergyUsed value
                    if (energy.EnergyUsed == null)
                    {
                        energy.EnergyUsed = CalculateConsumption(energy);
                    }

                    _db.EnergyConsumptions.Add(energy);
                    await _db.SaveChangesAsync();

                    TempData["Success"] = "New consumption added successfully!";
                    return RedirectToAction("Dashboard");
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"An error occurred while saving: {ex.Message}";
                }
            }

            TempData["Error"] = "There are errors in the form, please check the data entered.";
            ViewData["Action"] = "AddConsumption";
            ViewData["Title"] = "Add Consumption";
            return View("ConsumptionForm", energy);
        }

        //Display an existing consumption modification form
        public async Task<IActionResult> EditConsumption(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "You must be logged in to edit consumption.";
                return RedirectToAction("Login", "Account");
            }

            var consumption = await _db.EnergyConsumptions
                                       .FirstOrDefaultAsync(e => e.Id == id && e.UserId == user.Id);
            if (consumption == null)
            {
                TempData["Error"] = "The record was not found or you do not have permission to modify it.";
                return RedirectToAction("Dashboard");
            }

            ViewData["Action"] = "EditConsumption";
            ViewData["Title"] = "Edit Consumption";
            return View("ConsumptionForm", consumption);
        }

        // modify consumption (POST processing function)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditConsumption(EnergyConsumption energy)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "You must be logged in to modify consumption.";
                return RedirectToAction("Login", "Account");
            }

            ModelState.Remove("UserId"); // Remove UserId verification because we don't allow it to be modified

            if (ModelState.IsValid)
            {
                try
                {
                    // Fetch the existing record and verify its ownership by the current user
                    var existingConsumption = await _db.EnergyConsumptions
                                                       .FirstOrDefaultAsync(e => e.Id == energy.Id && e.UserId == user.Id);
                    if (existingConsumption == null)
                    {
                        TempData["Error"] = "The record was not found or you do not have permission to modify it.";
                        return RedirectToAction("Dashboard");
                    }

                    // Update values ​​in existing object
                    existingConsumption.Date = energy.Date;
                    /*    existingConsumption.Cost = energy.Cost;*/
                    existingConsumption.Location = energy.Location;
                    existingConsumption.RoomCount = energy.RoomCount;
                    existingConsumption.PersonCount = energy.PersonCount;
                    existingConsumption.ApplianceCount = energy.ApplianceCount;
                    existingConsumption.MachineCount = energy.MachineCount;
                    existingConsumption.BuildingType = energy.BuildingType;

                    // If EnergyUsed is null, calculate it using CalculateConsumption
                    if (existingConsumption.EnergyUsed == null)
                    {
                        existingConsumption.EnergyUsed = CalculateConsumption(existingConsumption);
                    }
                    else
                    {
                        existingConsumption.EnergyUsed = energy.EnergyUsed; // Update EnergyUsed with the entered value
                    }

                    // Tells Entity Framework that the record has been modified
                    _db.Entry(existingConsumption).State = EntityState.Modified;

                    await _db.SaveChangesAsync();

                    TempData["Success"] = "Consumption updated successfully!";
                    return RedirectToAction("Dashboard");
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"An error occurred while updating: {ex.Message}";
                }
            }

            TempData["Error"] = "There are errors in the form, please check the data entered.";
            ViewData["Action"] = "EditConsumption";
            ViewData["Title"] = "Edit Consumption";
            return View("ConsumptionForm", energy);
        }

        // Delete consumption
        [HttpPost]
        public async Task<IActionResult> DeleteConsumption(int id)
        {
            var consumption = await _db.EnergyConsumptions.FindAsync(id);
            if (consumption == null)
            {
                TempData["Error"] = "Record not found.";
                return RedirectToAction("Dashboard");
            }

            _db.EnergyConsumptions.Remove(consumption);
            await _db.SaveChangesAsync();

            TempData["Success"] = "The record has been deleted successfully!";
            return RedirectToAction("Dashboard");
        }

        // Calculate consumption based on building type
        private double CalculateConsumption(EnergyConsumption energy)
        {
            switch (energy.BuildingType)
            {
                case "Residential":
                    return (energy.RoomCount ?? 0) * 100 + (energy.PersonCount ?? 0) * 50 + (energy.ApplianceCount ?? 0) * 30;
                case "Industrial":
                    return (energy.MachineCount ?? 0) * 500 + (energy.PersonCount ?? 0) * 100;
                default:
                    throw new InvalidOperationException("The type of building is unknown.");
            }
        }

        // Check whether the consumption is high or not
        private bool IsHighConsumption(EnergyConsumption energy)
        {
            double baseThreshold = energy.BuildingType switch
            {
                "Residential" => 500 + (energy.RoomCount ?? 0) * 50 + (energy.ApplianceCount ?? 0) * 20 + (energy.PersonCount ?? 0) * 30,
                "Industrial" => 3000 + (energy.MachineCount ?? 0) * 200 + (energy.PersonCount ?? 0) * 50,
                _ => 1000
            };

            return energy.EnergyUsed > baseThreshold;
        }

        // Generate technical recommendations to reduce energy consumption
        private string GenerateTechRecommendations(EnergyConsumption energy)
        {
            if (IsHighConsumption(energy))
            {
                return energy.BuildingType switch
                {
                    "Residential" => "⚡ Use smart plugs and thermostats to save energy! 📱 Track and adjust consumption with energy apps in real-time.",
                    "Industrial" => "🤖 Deploy AI-powered EMS to optimize machinery! ⚙️ Invest in efficient equipment and automation to cut energy waste.",
                    _ => "Use smart appliances and energy-efficient technologies to better monitor and control energy consumption."
                };
            }

            return "🌟 Great job! Your energy usage is on track. 💡 Keep up the good work with energy-efficient tech to maintain this balance!";
        }
    }
}
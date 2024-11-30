using EMS_v2._2.Models;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Microsoft.ML.Trainers.LightGbm;
using Microsoft.ML.Transforms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace EMS_v2._2.Services
{
    public class PredictionService
    {
        private readonly MLContext _mlContext;

        // Store records by location
        public Dictionary<string, ITransformer> ModelsByLocation { get; private set; }

        public PredictionService()
        {
            _mlContext = new MLContext();
            ModelsByLocation = new Dictionary<string, ITransformer>();
        }

        // Check if the model is trained
        public bool IsModelTrainedForLocation(string location)
        {
            return ModelsByLocation.ContainsKey(location ?? "Unknown");
        }

        public void TrainModelsByLocation(IEnumerable<EnergyConsumption> data)
        {
            if (data == null || !data.Any())
            {
                Console.WriteLine("No data available for training. Model training skipped.");
                return;
            }

            // Segment the data by location and train a model for each location
            var dataByLocation = data.GroupBy(d => d.Location ?? "Unknown");
            foreach (var group in dataByLocation)
            {
                var location = group.Key;
                var locationData = group.ToList();

                var trainingData = _mlContext.Data.LoadFromEnumerable(locationData.Select(d => new EnergyConsumptionInput
                {
                    RoomCount = (float)(d.RoomCount ?? 0),
                    BuildingType = d.BuildingType ?? "Unknown",
                    Location = d.Location ?? "Unknown",
                    EnergyUsed = (float)(d.EnergyUsed ?? 0),
                    PersonCount = (float)(d.PersonCount ?? 0),
                    ApplianceCount = (float)(d.ApplianceCount ?? 0),
                    MachineCount = (float)(d.MachineCount ?? 0)
                }));

                var pipeline = _mlContext.Transforms.Categorical.OneHotEncoding("BuildingType", outputKind: OneHotEncodingEstimator.OutputKind.Indicator)
                    .Append(_mlContext.Transforms.Categorical.OneHotEncoding("Location", outputKind: OneHotEncodingEstimator.OutputKind.Indicator))
                    .Append(_mlContext.Transforms.Concatenate("Features", "RoomCount", "BuildingType", "Location", "PersonCount", "ApplianceCount", "MachineCount"))
                    .Append(_mlContext.Transforms.CopyColumns("Label", "EnergyUsed"))
                    .Append(_mlContext.Regression.Trainers.LightGbm(new LightGbmRegressionTrainer.Options
                    {
                        NumberOfLeaves = 150,
                        MinimumExampleCountPerLeaf = 1,
                        LearningRate = 0.5,
                        NumberOfIterations = 300
                    }));

                // Train the model for this location
                var model = pipeline.Fit(trainingData);

                // Store the model in the dictionary
                ModelsByLocation[location] = model;
                Console.WriteLine($"Model trained for location: {location}");
            }
        }

        // Predict energy consumption
        public float PredictEnergyUsage(int roomCount, string buildingType, int personCount, int applianceCount, int machineCount, string location)
        {
            // Check if model is available for the requested location
            if (!IsModelTrainedForLocation(location))
            {
                throw new InvalidOperationException($"No trained model available for location: {location}. Please add more records for this location.");
            }

            // اختيار النموذج المناسب
            var model = ModelsByLocation[location ?? "Unknown"];
            var predictionEngine = _mlContext.Model.CreatePredictionEngine<EnergyConsumptionInput, ConsumptionPrediction>(model);

            // إعداد بيانات الإدخال للتنبؤ
            var input = new EnergyConsumptionInput
            {
                RoomCount = (float)roomCount,
                PersonCount = (float)personCount,
                ApplianceCount = (float)applianceCount,
                MachineCount = (float)machineCount,
                BuildingType = buildingType ?? "Unknown",
                Location = location ?? "Unknown"
            };

            // تنفيذ التنبؤ
            var prediction = predictionEngine.Predict(input);

            Debug.WriteLine($"Predicted Energy Usage for location '{location}': {prediction.EnergyUsed}");
            return prediction.EnergyUsed;
        }
    }

    // Represents the input data structure for the prediction model
    public class EnergyConsumptionInput
    {
        public float RoomCount { get; set; }
        public float PersonCount { get; set; }
        public float ApplianceCount { get; set; }
        public float MachineCount { get; set; }
        public string BuildingType { get; set; }
        public string Location { get; set; } // Add Location as a feature

        [ColumnName("EnergyUsed")]
        public float EnergyUsed { get; set; }
    }

    // Represents the output structure of the prediction model
    public class ConsumptionPrediction
    {
        [ColumnName("Score")]
        public float EnergyUsed { get; set; }
    }
}


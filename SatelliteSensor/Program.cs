using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace SatelliteSensor
{
    // 1. Define what the data looks like (The "Payload")
    public class TelemetryData
    {
        public string SensorId { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public DateTime Timestamp { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🛰️  Satellite Sensor Initialized.");

            // 2. SMART CONFIGURATION (Docker vs. Laptop)
            // If running in Docker, read the "RabbitMQHost" variable (set in docker-compose).
            // If running locally, that variable is null, so default to "localhost".
            var rabbitHostName = Environment.GetEnvironmentVariable("RabbitMQHost") ?? "localhost";
            Console.WriteLine($"Connecting to RabbitMQ at: {rabbitHostName}...");

            var factory = new ConnectionFactory { HostName = rabbitHostName };
            
            // 3. RETRY LOGIC (Crucial for Docker)
            // In Docker, the containers start at the same time. This app might wake up
            // BEFORE RabbitMQ is ready. loop until it connects.
            while (true)
            {
                try 
                {
                    // Attempt to connect
                    using var connection = await factory.CreateConnectionAsync();
                    using var channel = await connection.CreateChannelAsync();

                    // 4. Declare the Queue
                    // "durable: false" means messages are lost if RabbitMQ restarts
                    await channel.QueueDeclareAsync(queue: "telemetry_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);

                    var random = new Random();
                    var sensorId = "SAT-001";

                    Console.WriteLine("✅ Connected to RabbitMQ! Sending data...");

                    // 5. The Infinite Loop (Simulating a device that never sleeps)
                    while (true)
                    {
                        var data = new TelemetryData
                        {
                            SensorId = sensorId,
                            Temperature = 20 + (random.NextDouble() * 15),
                            Humidity = 40 + (random.NextDouble() * 20),
                            Timestamp = DateTime.UtcNow
                        };

                        // Serialize to JSON
                        string jsonString = JsonSerializer.Serialize(data);
                        
                        // Convert to bytes
                        var body = Encoding.UTF8.GetBytes(jsonString);

                        // Publish the message
                        await channel.BasicPublishAsync(exchange: string.Empty, routingKey: "telemetry_queue", body: body);

                        Console.WriteLine($"[SENT] {jsonString}");

                        // Wait 1 second before next reading
                        await Task.Delay(1000);
                    }
                }
                catch (Exception)
                {
                    // If RabbitMQ isn't ready yet, we catch the error and wait.
                    Console.WriteLine($"⏳ RabbitMQ not ready yet. Retrying in 3 seconds...");
                    await Task.Delay(3000); 
                }
            }
        }
    }
}
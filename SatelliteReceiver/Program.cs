using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; // The database magic
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SatelliteReceiver
{
    // 1. The Data Model (This becomes a Table in SQL Server)
    public class TelemetryData
    {
        public int Id { get; set; } // Primary Key
        public string SensorId { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // 2. The Database Context (The bridge between Code and SQL)
    public class TelemetryContext : DbContext
    {
        public DbSet<TelemetryData> TelemetryLogs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // SMART CONFIGURATION (Docker vs. Laptop)
            // We check the "SQLHost" environment variable.
            var sqlHost = Environment.GetEnvironmentVariable("SQLHost") ?? "localhost";
            
            // Build the connection string dynamically
            // Note: "TrustServerCertificate=True" is required for Docker SQL Server
            var connectionString = $"Server={sqlHost},1433;Database=TelemetryDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;";
            
            options.UseSqlServer(connectionString);
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("📡 Receiver Service Starting...");

            // 3. WAIT FOR SQL SERVER (Retry Logic)
            // SQL Server is heavy. It takes 10-20 seconds to start.
            // This app will crash if it tries to save data before SQL is ready.
            // We loop until the database is responding.
            bool dbReady = false;
            while (!dbReady)
            {
                try
                {
                    using (var db = new TelemetryContext())
                    {
                        Console.WriteLine("⏳ Checking Database connection...");
                        await db.Database.EnsureCreatedAsync();
                        Console.WriteLine("✅ Database Ready!");
                        dbReady = true;
                    }
                }
                catch
                {
                    Console.WriteLine("⚠️ SQL Server not ready. Waiting 5 seconds...");
                    await Task.Delay(5000);
                }
            }

            // 4. Connect to RabbitMQ (Also using Smart Config)
            var rabbitHost = Environment.GetEnvironmentVariable("RabbitMQHost") ?? "localhost";
            Console.WriteLine($"Connecting to RabbitMQ at: {rabbitHost}...");

            var factory = new ConnectionFactory { HostName = rabbitHost };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(queue: "telemetry_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);

            Console.WriteLine(" [*] Waiting for messages...");

            // 5. Create the Consumer (The "Worker")
            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try 
                {
                    // Deserialize JSON back to Object
                    var data = JsonSerializer.Deserialize<TelemetryData>(message);

                    // 6. SAVE TO SQL SERVER
                    using (var db = new TelemetryContext())
                    {
                        db.TelemetryLogs.Add(data);
                        await db.SaveChangesAsync(); // The actual "INSERT" command
                    }

                    Console.WriteLine($" [💾 SAVED] {data.SensorId} - Temp: {data.Temperature:F1}");

                    // 7. Acknowledge (The most important part!)
                    // Tell RabbitMQ: "I processed this safely. You can delete it now."
                    await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" [❌ ERROR] Could not save: {ex.Message}");
                    // We do NOT ack here. The message stays in the queue to be retried!
                }
            };

            // Start consuming
            await channel.BasicConsumeAsync(queue: "telemetry_queue", autoAck: false, consumer: consumer);

            // Keep the container running forever
            await Task.Delay(-1);
        }
    }
}
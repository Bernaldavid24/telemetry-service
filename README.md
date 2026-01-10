# 📡 Resilient Telemetry Ingestion Service

A high-throughput, fault-tolerant microservices architecture designed to ingest, buffer, and process simulated IoT telemetry data. Built with **.NET 8**, **RabbitMQ**, and **SQL Server**, fully containerized with **Docker**.

## 🏗 System Architecture

The system uses a **Producer-Consumer** pattern to decouple data generation from processing, ensuring zero data loss even during database outages.

**[Sensor]** ➔ 📨 **[RabbitMQ]** ➔ ⚙️ **[Worker Service]** ➔ 💾 **[SQL Database]**

1.  **Satellite Sensor (Producer):** Generates high-frequency telemetry data (Temperature/Humidity) and pushes it to the message queue.
2.  **RabbitMQ (Broker):** Acts as a durable buffer. It holds messages safely until the consumer is ready.
3.  **Receiver Service (Consumer):** An asynchronous worker that pulls messages, processes them, and commits them to storage using Entity Framework Core.
4.  **SQL Server (Storage):** Persists the telemetry logs for long-term analysis.

---

## 🚀 Key Features

* **⚡ Event-Driven Architecture:** Decoupled services allow the Sensor to keep running even if the Database is offline.
* **🛡️ Fault Tolerance:** Implements **Retry Policies** for database connections and Docker startup race conditions.
* **📦 Containerized:** Fully "Dockerized" environment. The entire stack (Apps + DB + Broker) spins up with a single command.
* **🔒 Data Safety:** Uses RabbitMQ manual acknowledgments (`autoAck: false`) to ensure messages are only removed from the queue *after* successful storage.

---

## 🛠 Tech Stack

* **Language:** C# / .NET 8 (Console & Worker Services)
* **Message Broker:** RabbitMQ (Management Plugin enabled)
* **Database:** Microsoft SQL Server 2022
* **ORM:** Entity Framework Core
* **Containerization:** Docker & Docker Compose

---

## 💻 How to Run (One-Click Setup)

Pre-requisites: [Docker Desktop](https://www.docker.com/products/docker-desktop/) must be installed.

1.  **Clone the Repository**
    ```bash
    git clone [https://github.com/Bernaldavid24/Telemetry-Service.git](https://github.com/Bernaldavid24/Telemetry-Service.git)
    cd Telemetry-Service
    ```

2.  **Launch the Stack**
    ```bash
    docker-compose up --build
    ```
    *This will compile the C# code, download SQL Server/RabbitMQ images, and link them together automatically.*

3.  **Verify It's Working**
    * **Console Logs:** You will see `[SENT]` and `[💾 SAVED]` messages scrolling.
    * **RabbitMQ Dashboard:** Open `http://localhost:15672` (User: `guest` / Pass: `guest`) to see the queue traffic in real-time.

4.  **Stop the System**
    Press `Ctrl + C` in the terminal to shut down the containers gracefully.

---

## 🧪 Simulation Details

The **SatelliteSensor** service mimics a remote IoT device with unstable connectivity:
* **Smart Configuration:** Automatically detects if it's running inside Docker (`rabbitmq` host) or on a local machine (`localhost`).
* **Resiliency:** If RabbitMQ is down, the sensor enters a "Wait & Retry" loop rather than crashing.

---

## 📄 License
This project is open source.
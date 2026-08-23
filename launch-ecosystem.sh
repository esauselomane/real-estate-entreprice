#!/bin/bash
# 🚀 TOTAL ARCHITECTURE AUTOMATED BOOT ENGINE

echo "===================================================="
echo "🎯 STARTING ARCHITECTURE INFRASTRUCTURE SETUP ENGINE"
echo "===================================================="

# 1. Start the Background Infrastructure Cluster Containers
echo "📡 Step 1: Launching SQL Server, Zookeeper, and Apache Kafka Brokers..."
docker compose up -d

# 2. Prevent race conditions by giving the database engine time to finalize socket binding
echo "⏳ Step 2: Pausing for 15 seconds to allow SQL Server to complete internal boot sequences..."
sleep 15

# 3. Spin Up Background Multi-Page Document Pipeline Consumers
echo "🏢 Step 3: Booting Asynchronous Document Pipeline Generation Background Worker..."
cd backend-services/BrochureGeneration.Worker
dotnet run &
cd ../..

# 4. Spin Up Microservice API Cluster Pools Concurrent Threads
echo "🔐 Step 4: Booting Identity and Core Access Control Microservice..."
cd backend-services/Identity.Microservice
dotnet run &
cd ../..

echo "🏠 Step 5: Booting Spatial Real Estate Asset Catalog Microservice..."
cd backend-services/Property.Microservice
dotnet run &
cd ../..

echo "🎯 Step 6: Booting Client Relationship and Automation Promotion Pipeline Microservice..."
cd backend-services/CRM.Microservice
dotnet run &
cd ../..

# 5. Boot Gateway Layer Receptacle Core Routing Rules Matrix
echo "🔀 Step 7: Booting Ocelot API Unified Microservice Gateway proxy..."
cd api-gateway
dotnet run &
cd ..

# 6. Spin Up Client User Presentation Layout Web Interface Containers
echo "🖼️ Step 8: Bundling and Launching Local Client Web Interface Portal..."
cd frontend-client
npm install
npm run dev

echo "===================================================="
echo "🎉 ALL PLATFORM SYSTEMS ONLINE AND CHANNELS RUNNING SAFELY"
echo "===================================================="

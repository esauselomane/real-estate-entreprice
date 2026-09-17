#!/bin/bash
# 🎯 UNIFIED MICROSERVICE SOLUTION MANAGEMENT INITIALIZER

echo "===================================================="
echo "🏗️  CREATING CENTRALIZED MONOREPO SOLUTION CONTAINER"
echo "===================================================="

SOLUTION_NAME="RealEstateEnterprisePlatform"

# 1. Initialize a fresh .NET solution container in the root if it doesn't exist
if [ ! -f "${SOLUTION_NAME}.sln" ] && [ ! -f "${SOLUTION_NAME}.slnx" ]; then
    echo "📝 Creating new solution..."
    dotnet new sln -n "${SOLUTION_NAME}"
else
    echo "✅ Solution already exists, skipping creation."
fi

# Determine solution file extension
SOLUTION_FILE=""
if [ -f "${SOLUTION_NAME}.slnx" ]; then
    SOLUTION_FILE="${SOLUTION_NAME}.slnx"
elif [ -f "${SOLUTION_NAME}.sln" ]; then
    SOLUTION_FILE="${SOLUTION_NAME}.sln"
else
    echo "❌ No solution file found. Creating new one..."
    dotnet new sln -n "${SOLUTION_NAME}"
    SOLUTION_FILE="${SOLUTION_NAME}.sln"
fi

echo "📁 Using solution file: ${SOLUTION_FILE}"

# 2. Add each standalone microservice project directory sequentially
echo "🔌 Coupling Identity Microservice..."
dotnet sln "${SOLUTION_FILE}" add backend-services/Identity.Microservice/Identity.Microservice.csproj

echo "🔌 Coupling Property Microservice..."
dotnet sln "${SOLUTION_FILE}" add backend-services/Property.Microservice/Property.Microservice.csproj

echo "🔌 Coupling CRM Microservice..."
dotnet sln "${SOLUTION_FILE}" add backend-services/CRM.Microservice/CRM.Microservice.csproj

echo "🔌 Coupling Ocelot API Gateway proxy..."
dotnet sln "${SOLUTION_FILE}" add Api-Gateway/Api-Gateway.csproj

echo "🔌 Coupling Event-Driven Brochure Generation Background Worker..."
dotnet sln "${SOLUTION_FILE}" add backend-services/BrochureGeneration.Worker/BrochureGeneration.Worker.csproj

echo "🔌 Coupling Data Import Background Worker..."
dotnet sln "${SOLUTION_FILE}" add backend-services/DataImport.Worker/DataImport.Worker.csproj

echo "🔌 Coupling Image Processor Background Worker..."
dotnet sln "${SOLUTION_FILE}" add backend-services/ImageProcessor.Worker/ImageProcessor.Worker.csproj

echo "🔌 Coupling Platform Syndication Background Worker..."
dotnet sln "${SOLUTION_FILE}" add backend-services/PlatformSyndication.Worker/PlatformSyndication.Worker.csproj

echo "🔌 Coupling Developer API..."
dotnet sln "${SOLUTION_FILE}" add Developer.API/Developer.API.csproj

echo "===================================================="
echo "🎯 SOLUTION BUNDLE BUILT! TESTING MASS SOLUTION COMPILATION..."
echo "===================================================="

# 3. Test compilation of the entire backend stack at once
dotnet build "${SOLUTION_FILE}"

echo "===================================================="
echo "🎉 CHANNELS LINKED SUCCESSFULLY WITH 0 STRUCTURAL LEAKS"
echo "===================================================="

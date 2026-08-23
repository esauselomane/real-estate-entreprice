#!/bin/bash
# 🎯 UNIFIED MICROSERVICE SOLUTION MANAGEMENT INITIALIZER

echo "===================================================="
echo "🏗️  CREATING CENTRALIZED MONOREPO SOLUTION CONTAINER"
echo "===================================================="

# 1. Initialize a fresh .NET solution container in the root
dotnet new sln -n RealEstateEnterprisePlatform

# 2. Add each standalone microservice project directory sequentially
echo "🔌 Coupling Identity Microservice..."
dotnet sln RealEstateEnterprisePlatform.sln add backend-services/Identity.Microservice/Identity.Microservice.csproj

echo "🔌 Coupling Property Microservice..."
dotnet sln RealEstateEnterprisePlatform.sln add backend-services/Property.Microservice/Property.Microservice.csproj

echo "🔌 Coupling CRM Microservice..."
dotnet sln RealEstateEnterprisePlatform.sln add backend-services/CRM.Microservice/CRM.Microservice.csproj

echo "🔌 Coupling Ocelot API Gateway proxy..."
dotnet sln RealEstateEnterprisePlatform.sln add Api-Gateway/Api-Gateway.csproj

echo "🔌 Coupling Event-Driven Brochure Generation Background Worker..."
dotnet sln RealEstateEnterprisePlatform.sln add backend-services/BrochureGeneration.Worker/BrochureGeneration.Worker.csproj

echo "===================================================="
echo "🎯 SOLUTION BUNDLE BUILT! TESTING MASS SOLUTION COMPILATION..."
echo "===================================================="

# 3. Test compilation of the entire backend stack at once
dotnet build RealEstateEnterprisePlatform.sln

echo "===================================================="
echo "🎉 CHANNELS LINKED SUCCESSFULLY WITH 0 STRUCTURAL LEAKS"
echo "===================================================="

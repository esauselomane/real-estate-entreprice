pkill -f "dotnet" 2>/dev/null
export PATH="$PATH:/usr/local/share/dotnet"
nohup /usr/local/share/dotnet/dotnet run --project /Users/uboomi/Desktop/real-estate-entreprice/backend-services/Identity.Microservice --urls http://localhost:5001 > /tmp/identity.log 2>&1 &
nohup /usr/local/share/dotnet/dotnet run --project /Users/uboomi/Desktop/real-estate-entreprice/backend-services/Property.Microservice --urls http://localhost:5002 > /tmp/property.log 2>&1 &
nohup /usr/local/share/dotnet/dotnet run --project /Users/uboomi/Desktop/real-estate-entreprice/backend-services/CRM.Microservice --urls http://localhost:5003 > /tmp/crm.log 2>&1 &
nohup /usr/local/share/dotnet/dotnet run --project /Users/uboomi/Desktop/real-estate-entreprice/api-gateway --urls http://localhost:5000 > /tmp/gateway.log 2>&1 &
nohup /usr/local/share/dotnet/dotnet run --project /Users/uboomi/Desktop/real-estate-entreprice/Developer.API/Developer.API.csproj --urls http://localhost:5006 > /tmp/developer.log 2>&1 &
# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app

# Copy only the .csproj file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the source code
COPY . ./

# Publish the application in Release mode
RUN dotnet publish -c Release -o out

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Copy the published output from the build stage
COPY --from=build /app/out .

# Expose the necessary port (adjust as needed)
EXPOSE 80

# Set the entry point to run the application
ENTRYPOINT ["dotnet", "Splitwise Back.dll"]

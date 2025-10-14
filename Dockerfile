# Use the official .NET SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy the project file and restore dependencies
COPY UserService.csproj ./
RUN dotnet restore

# Copy the entire project and build the application
COPY . ./
RUN dotnet publish -c Release -o out

# Use the official .NET runtime image for running the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# Copy the XML documentation file for Swagger from publish output
COPY --from=build-env /app/out/UserService.xml ./UserService.xml

# Expose the port and run the application
EXPOSE 8082
ENTRYPOINT ["dotnet", "UserService.dll"]
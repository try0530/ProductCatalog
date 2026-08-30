FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app

COPY . .

RUN dotnet restore

RUN dotnet publish -c Release -o /app/publish

WORKDIR /app/publish

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "ProductCatalog.dll"]
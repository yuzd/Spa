FROM mcr.microsoft.com/dotnet/aspnet:6.0
COPY . /publish
WORKDIR /publish
ENV ASPNETCORE_URLS=http://*:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "spa.dll"]
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["sekura/sekura.csproj", "sekura/"]
RUN dotnet restore "sekura/sekura.csproj"

COPY . .
WORKDIR "/src/sekura"
RUN dotnet publish "sekura.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
	ASPNETCORE_ENVIRONMENT=Production \
	Application__EnableHttpsRedirection=false \
	Application__PathBase=/ \
	Application__TimeZoneId=UTC \
	Application__AuthenticationSessionTimeoutMinutes=60 \
	Application__AuthenticationSlidingExpiration=true \
	Kestrel__Endpoints__Http__Url=http://+:8080 \
	Storage__Backend=sqlite \
	SqliteStorage__ConnectionString="Data Source=/app/data/sekura.db" \
	SqliteStorage__ApplyMigrationsOnStartup=true \
	SqlServerStorage__ConnectionString= \
	SqlServerStorage__ApplyMigrationsOnStartup=true \
	PostgresqlStorage__ConnectionString= \
	PostgresqlStorage__ApplyMigrationsOnStartup=true \
	AzureStorage__KeyVault__VaultUri= \
	AzureStorage__KeyVault__TenantId= \
	AzureStorage__KeyVault__ClientId= \
	AzureStorage__KeyVault__ClientSecret= \
	AzureStorage__KeyVault__SecretPrefix=sekura \
	AzureStorage__TableAudit__ServiceSasUrl= \
	AzureStorage__TableAudit__TableName=auditlogs \
	AzureStorage__TableAudit__PartitionKey=audit \
	AdminAuth__Username=admin \
	AdminAuth__PasswordHash= \
	LoginThrottle__FailedAttemptLimit=5 \
	LoginThrottle__PauseMinutes=15 \
	LoginThrottle__FailureWindowMinutes=60 \
	ForwardedHeaders__Enabled=false \
	OidcAuth__LocalLoginFallback=LoopbackOnly \
	OidcAuth__Enabled=false \
	OidcAuth__Authority= \
	OidcAuth__ClientId= \
	OidcAuth__ClientSecret= \
	OidcAuth__CallbackPath=/signin-oidc \
	OidcAuth__SignedOutCallbackPath=/signout-callback-oidc \
	OidcAuth__RequireHttpsMetadata=true \
	OidcAuth__Scopes__0=openid \
	OidcAuth__Scopes__1=profile \
	OidcAuth__Scopes__2=email \
	Encryption__Passphrase= \
	Share__DefaultExpiryHours=4 \
	Share__CleanupIntervalSeconds=60 \
	Logging__LogLevel__Default=Information \
	Logging__LogLevel__Microsoft__AspNetCore=Warning \
	AllowedHosts=*
EXPOSE 8080

COPY --from=build /app/publish .
RUN mkdir -p /app/data && chown -R app:app /app/data
USER app
ENTRYPOINT ["dotnet", "sekura.dll"]

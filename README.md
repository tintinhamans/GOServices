# Services
GeneralsOnline Game Services Code provides RESTful web services which act as a replacement and modernization of the GameSpy functionality found in the original release of Command & Conquer: Generals - Zero Hour

# Build Status: Windows
[![Windows (x64 + arm64)](https://github.com/GeneralsOnlineDevelopmentTeam/Services/actions/workflows/Windows.yml/badge.svg)](https://github.com/GeneralsOnlineDevelopmentTeam/Services/actions/workflows/Windows.yml)

# Build Status: Linux
[![Linux (x64 + arm64)](https://github.com/GeneralsOnlineDevelopmentTeam/Services/actions/workflows/Linux-MUSL.yml/badge.svg)](https://github.com/GeneralsOnlineDevelopmentTeam/Services/actions/workflows/Linux-MUSL.yml)
[![Linux MUSL (x64 + arm64)](https://github.com/GeneralsOnlineDevelopmentTeam/Services/actions/workflows/Linux.yml/badge.svg)](https://github.com/GeneralsOnlineDevelopmentTeam/Services/actions/workflows/Linux.yml)

# Build Status: MacOS
[![MacOS (x64 + arm64)](https://github.com/GeneralsOnlineDevelopmentTeam/Services/actions/workflows/MacOS.yml/badge.svg)](https://github.com/GeneralsOnlineDevelopmentTeam/Services/actions/workflows/MacOS.yml)

# Original Game Functionality Implemented
- Quick Match
- Custom Match
- Ladders
- Rooms & Chat
- Profiles
- Player Stats
- Friends / Buddies / Social System
- Global / Daily Stats
- Auto-update

# Required Dependencies
- Microsoft Visual Studio (2026 Community Edition recommended)
- MySQL/MariaDB server (MariaDB 12.1.2 or higher recommended)
- MySQL management tool or command line (e.g. HeidiSQL)
- .NET SDK 10

# Optional Dependencies (not required for development, required for live)
- Discord App ID
- S3 compatible storage
- STUN + TURN servers

# How To Build
- Sync the repository
- Open GenServices.sln
- Build the solution for x64, Windows (or your architecture & OS if different)
- Edit appsettings.json and fill out any TODO sections (e.g. token settings, database settings)
- Import the SQL structure to your database (GenOnlineService\Database_Structure\structure.sql)
- When upgrading an existing database, apply new `GenOnlineService\Database_Structure\upgrade_*.sql` files in date order before starting the updated service
- Run GenOnlineService.exe

# Configuration
The main settings are in [`appsettings.json`](GenOnlineService/appsettings.json). ASP.NET Core can also read environment variables. Use `__` between names, for example `Database__Host`. List positions start at `0`, so `Api__Keys__0` sets the first item.

<table>
  <thead>
    <tr>
      <th>Section</th>
      <th>Setting</th>
      <th>Description</th>
    </tr>
  </thead>
  <tbody>
    <tr><td rowspan="3"><code>Kestrel</code></td><td><code>EndpointDefaults:Protocols</code></td><td>HTTP protocols used by the server.</td></tr>
    <tr><td><code>Endpoints:HTTP:Url</code></td><td>HTTP address used by the server.</td></tr>
    <tr><td><code>Endpoints:HTTPS:Url</code></td><td>HTTPS address used by the server. Required during startup.</td></tr>
    <tr><td rowspan="2"><code>Logging</code></td><td><code>LogLevel:Default</code></td><td>Default log level.</td></tr>
    <tr><td><code>LogLevel:Microsoft.AspNetCore</code></td><td>Log level for ASP.NET Core messages.</td></tr>
    <tr><td>General</td><td><code>AllowedHosts</code></td><td>Host names accepted by the server. <code>"*"</code> accepts any host name.</td></tr>
    <tr><td rowspan="6"><code>JwtSettings</code></td><td><code>Key</code></td><td>JWT signing key. Must be at least 32 bytes and must not contain the default placeholder.</td></tr>
    <tr><td><code>Issuer</code></td><td>Name of the token issuer.</td></tr>
    <tr><td><code>Audience</code></td><td>Name of the token audience.</td></tr>
    <tr><td><code>SessionTokenLifetimeMinutes</code></td><td>Session token lifetime in minutes.</td></tr>
    <tr><td><code>RefreshTokenLifetimeMinutes</code></td><td>Refresh token lifetime in minutes.</td></tr>
    <tr><td><code>EnforceIPMatch</code></td><td>Requires the token to be used from the same IP address.</td></tr>
    <tr><td rowspan="7"><code>Core</code></td><td><code>WebSocketAddress</code></td><td>Secure WebSocket address returned to clients.</td></tr>
    <tr><td><code>InsecureWebSocketAddress</code></td><td>Plain-HTTP WebSocket address returned to clients that cannot use TLS.</td></tr>
    <tr><td><code>UseSystemCertificateStore</code></td><td>Uses the system or Kestrel certificate settings.</td></tr>
    <tr><td><code>CertificatePemPath</code></td><td>Certificate file used when <code>UseSystemCertificateStore</code> is false.</td></tr>
    <tr><td><code>CertificateKeyPath</code></td><td>Certificate key file used when <code>UseSystemCertificateStore</code> is false.</td></tr>
    <tr><td><code>EnforceHttps</code></td><td>Adds HSTS and redirects normal HTTP requests to HTTPS.</td></tr>
    <tr><td><code>DisableFullMeshCheck</code></td><td>Skips lobby connection checks.</td></tr>
    <tr><td><code>GeoIP</code></td><td><code>DatabasePath</code></td><td>Path to the MaxMind GeoIP City database.</td></tr>
    <tr><td rowspan="4"><code>Turn</code></td><td><code>Key</code></td><td>Cloudflare TURN key.</td></tr>
    <tr><td><code>Token</code></td><td>Cloudflare TURN API token.</td></tr>
    <tr><td><code>TokenTtl</code></td><td>TURN credential lifetime.</td></tr>
    <tr><td><code>AutomaticallyInvalidateTokens</code></td><td>Revokes old TURN credentials automatically.</td></tr>
    <tr><td rowspan="2"><code>Api</code></td><td><code>Keys</code></td><td>Values accepted in the <code>x-api-key</code> header. Letter case is ignored. An empty list disables partner API-key access.</td></tr>
    <tr><td><code>WebServerKey</code></td><td>Key used by the web server.</td></tr>
    <tr><td rowspan="2"><code>Monitor</code></td><td><code>Username</code></td><td>Monitoring login name.</td></tr>
    <tr><td><code>Password</code></td><td>Monitoring login password.</td></tr>
    <tr><td><code>RateLimiting</code></td><td><code>UseBuiltInRateLimiter</code></td><td>Enables the built-in limit for each user or IP address. Leave disabled if a reverse proxy handles rate limits.</td></tr>
    <tr><td rowspan="6"><code>Discord</code></td><td><code>Enabled</code></td><td>Enables the Discord bot.</td></tr>
    <tr><td><code>Token</code></td><td>Discord bot token.</td></tr>
    <tr><td><code>SendRoomChatToDiscord</code></td><td>Sends network-room chat messages to Discord.</td></tr>
    <tr><td><code>AdminUserIds</code></td><td>Discord user IDs allowed to use admin commands.</td></tr>
    <tr><td><code>NetworkRoomChatChannelId</code></td><td>Channel ID for network-room chat.</td></tr>
    <tr><td><code>AdminCommandsChannelId</code></td><td>Channel ID for admin commands.</td></tr>
    <tr><td rowspan="11"><code>Database</code></td><td><code>Host</code></td><td>MariaDB/MySQL server name or IP address.</td></tr>
    <tr><td><code>Name</code></td><td>Database name.</td></tr>
    <tr><td><code>Username</code></td><td>Database login name.</td></tr>
    <tr><td><code>Password</code></td><td>Database login password.</td></tr>
    <tr><td><code>Port</code></td><td>Database port.</td></tr>
    <tr><td><code>ConnectionTimeoutSeconds</code></td><td>Connection timeout in seconds.</td></tr>
    <tr><td><code>CommandTimeoutSeconds</code></td><td>Command timeout in seconds.</td></tr>
    <tr><td><code>MinimumPoolSize</code></td><td>Minimum number of pooled connections.</td></tr>
    <tr><td><code>MaximumPoolSize</code></td><td>Maximum number of pooled connections.</td></tr>
    <tr><td><code>UsePooling</code></td><td>Enables connection pooling.</td></tr>
    <tr><td><code>ResetConnections</code></td><td>Resets a connection before it is reused.</td></tr>
    <tr><td rowspan="6"><code>MatchData</code></td><td><code>UploadsEnabled</code></td><td>Enables replay and screenshot uploads.</td></tr>
    <tr><td><code>S3AccessKey</code></td><td>S3 access key.</td></tr>
    <tr><td><code>S3SecretKey</code></td><td>S3 secret key.</td></tr>
    <tr><td><code>S3BucketName</code></td><td>S3 bucket name.</td></tr>
    <tr><td><code>S3Endpoint</code></td><td>S3 service URL.</td></tr>
    <tr><td><code>S3UrlLifetimeMinutes</code></td><td>Lifetime of upload URLs in minutes.</td></tr>
    <tr><td rowspan="3"><code>Sentry</code></td><td><code>Enabled</code></td><td>Enables error reporting.</td></tr>
    <tr><td><code>Dsn</code></td><td>Sentry project address.</td></tr>
    <tr><td><code>Environment</code></td><td>Sentry environment name. Defaults to <code>production</code>.</td></tr>
    <tr><td rowspan="3"><code>Middleware</code></td><td><code>JwksEndpoint</code></td><td>Address used to download public signing keys.</td></tr>
    <tr><td><code>Audience</code></td><td>Required audience in middleware identity tokens.</td></tr>
    <tr><td><code>Issuer</code></td><td>Required issuer in middleware identity tokens.</td></tr>
    <tr><td rowspan="2"><code>AntiCheat</code></td><td><code>AllowedModules</code></td><td>Allowed module file names.</td></tr>
    <tr><td><code>AllowedModulePaths</code></td><td>Allowed folder prefixes.</td></tr>
    <tr><td rowspan="4"><code>ExternalLeaderboards</code></td><td><code>PostUrl</code></td><td>Address used to send leaderboard data.</td></tr>
    <tr><td><code>PostToken</code></td><td>Token used to send leaderboard data.</td></tr>
    <tr><td><code>GetUrl</code></td><td>Address used to read leaderboard data.</td></tr>
    <tr><td><code>GetToken</code></td><td>Token used to read leaderboard data.</td></tr>
  </tbody>
</table>

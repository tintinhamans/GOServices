# Services
GeneralsOnline Game Services Code provides RESTful web services which act as a replacement and modernization of the GameSpy functionality found in the original release of Command & Conquer: Generals - Zero Hour

## Build status

[![CI](https://github.com/GeneralsOnlineDevelopmentTeam/Services/actions/workflows/ci.yml/badge.svg)](https://github.com/GeneralsOnlineDevelopmentTeam/Services/actions/workflows/ci.yml)

CI builds the service in Release mode with warnings treated as errors and publishes framework-dependent artifacts for Linux (glibc and MUSL), macOS, and Windows on x64 and ARM64.

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
- Run GenOnlineService.exe

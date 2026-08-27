# Container deployment

This Compose project runs Generals Online Services with MariaDB. Neither
container publishes a host port by default. TLS and reverse-proxy configuration
are left to the operator.

Create the local deployment files:

```text
appsettings.json
config/
crcfiles/OutpostOnlineZH_60.exe
data/GeoLite2-City.mmdb
exceptions/
```

Create the four directories before starting Compose. On Linux, give UID 1654
write access to `config/` and `exceptions/`.

Copy `appsettings.example.json` to `appsettings.json`. Replace every
`TODO_CHANGE_ME` value in that file and `docker-compose.yml`.
`Database:Password` must match `MARIADB_PASSWORD`. MariaDB generates a random
root password during first initialization and writes it to the container log.
Configure `AllowedHosts`, `AllowedOrigins`, and `TrustedProxies` for the
deployment.

On first start, the service copies missing default files into `config/`.
Existing files are never overwritten.

Copy `GeoLite2-City.mmdb` into `data/`. Restart the service after updating the
file so it reopens the database.

MariaDB data is stored in a named volume. The database schema is imported when
that volume is initialized. Existing databases are not modified automatically.

Service configuration and crash reports are written to `config/` and
`exceptions/`. The service image runs as UID/GID 1654, so that user needs write
access to both directories on Linux hosts.

## Ports

To publish the API on all host interfaces, add this to the `services` service:

```yaml
ports:
  - "9001:9001"
```

To publish MariaDB, add this to the `mariadb` service:

```yaml
ports:
  - "3306:3306"
```

Build and start the stack from this directory:

```sh
docker compose up --detach --build
```

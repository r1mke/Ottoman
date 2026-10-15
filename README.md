# Ottoman

Sistem za upravljanje barbershopom: rezervacije termina, webshop proizvoda, recenzije, obavijesti i izvještaji.
Backend: ASP.NET Core (.NET 10) + SQL Server + RabbitMQ. Klijenti: Flutter desktop (admin) i mobile (klijent/zaposlenik).

Dokumentacija: [`docs/`](docs/README.md).

## Pokretanje backenda

```bash
cp deploy/.env.example deploy/.env     # popuniti lozinke
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
```

| Servis | Adresa |
|---|---|
| API (Scalar docs u Development) | http://localhost:5080/scalar/v1 |
| Health | http://localhost:5080/health/ready |
| RabbitMQ management | http://localhost:15672 |
| Mailpit (razvojni inbox) | http://localhost:8025 |

Demo korisnici (samo kad je `SEED_DEMO_DATA=true`): `kerim@ottoman.local`, `tarik@ottoman.local` (Employee), `klijent@ottoman.local` (Client); lozinka `SEED_DEMO_PASSWORD`. Admin: `SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD`.

## Razvoj

```bash
dotnet build Ottoman.slnx
dotnet test Ottoman.slnx          # integracijski testovi traže Docker (Testcontainers)
dotnet format Ottoman.slnx --verify-no-changes
```

Flutter: `cd apps/<app> && flutter pub get && flutter analyze && flutter test`.

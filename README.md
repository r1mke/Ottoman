# Ottoman

Sistem za upravljanje barbershopom: rezervacije termina, webshop proizvoda s plaćanjem karticom, recenzije i preporuke, obavijesti i podsjetnici, izvještaji.

- **Backend:** ASP.NET Core (.NET 10), EF Core + SQL Server, ASP.NET Identity + JWT, RabbitMQ (worker), Stripe.
- **Klijenti:** Flutter desktop (admin) i Flutter mobile (klijent / zaposlenik), zajednički paket `apps/packages/ottoman_core`.

Dokumentacija preporuka: [`recommender-dokumentacija.md`](recommender-dokumentacija.md).

## Brzi početak

```bash
cp deploy/.env.example deploy/.env     # popuniti lozinke i JWT ključ
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
```

| Servis | Adresa |
|---|---|
| API (Scalar docs u Development) | http://localhost:5080/scalar/v1 |
| Health | http://localhost:5080/health/ready |
| RabbitMQ management | http://localhost:15672 |
| Mailpit (razvojni inbox za mailove) | http://localhost:8025 |

Demo korisnici (samo kad je `SEED_DEMO_DATA=true`): `kerim@ottoman.local`, `tarik@ottoman.local` (zaposlenici), `klijent@ottoman.local` (klijent); lozinka `SEED_DEMO_PASSWORD`. Admin: `SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD`.

### Pristupni podaci za testiranje

| Uloga | Aplikacija | Korisničko ime (e-mail) | Lozinka |
|---|---|---|---|
| Administrator | desktop | `SEED_ADMIN_EMAIL` (`admin@ottoman.local`) | `SEED_ADMIN_PASSWORD` iz `.env` |
| Zaposlenik | mobile | `kerim@ottoman.local`, `tarik@ottoman.local` | `SEED_DEMO_PASSWORD` iz `.env` (primjer: `Test123!`) |
| Klijent | mobile | `klijent@ottoman.local` | `SEED_DEMO_PASSWORD` iz `.env` |

Stvarne vrijednosti `.env` datoteke isporučuju se odvojeno, u arhivi `.env-tajne.zip` (lozinka arhive se šalje zasebno); u repozitoriju je samo `deploy/.env.example`. Probna kartica za Stripe sandbox: `4242 4242 4242 4242`, bilo koji budući datum i CVC.

### Plaćanje karticom (Stripe)

U `deploy/.env` postavite `STRIPE_SECRET_KEY`, `STRIPE_PUBLISHABLE_KEY` i `STRIPE_WEBHOOK_SECRET`. Lokalno se webhook secret dobije s `stripe listen --api-key <sk_test_...> --print-secret`, a događaji se proslijede s `stripe listen --forward-to localhost:5080/api/v1/payments/webhook`. Bez ključeva narudžba karticom se ne može platiti (ostaje plaćanje pri preuzimanju). Aplikacije dobijaju publishable ključ od API-ja (`GET /api/v1/payments/config`), nije ugrađen u build.

### Pokretanje aplikacija

```bash
cd apps/ottoman_desktop && flutter run -d windows
cd apps/ottoman_mobile  && flutter run                 # emulator: API na http://10.0.2.2:5080
cd apps/ottoman_mobile  && flutter run --dart-define=API_BASE_URL=http://<IP računara>:5080   # pravi telefon
cd apps/ottoman_mobile  && flutter build apk --debug   # apps/ottoman_mobile/build/app/outputs/flutter-apk/app-debug.apk
```

## Razvoj

```bash
dotnet build Ottoman.slnx
dotnet test Ottoman.slnx          # integracijski testovi traže Docker (Testcontainers)
dotnet format Ottoman.slnx --verify-no-changes
```

Flutter: `cd apps/<app> && flutter pub get && flutter analyze && flutter test`.

Svaka faza je radila na vlastitoj grani spojenoj u `main`; 

## Produkcija — šta provjeriti

- API odbija start (`StartupGuards`) ako je `Jwt__SigningKey` primjer iz `.env.example`, ako je `Seed__DemoData=true` ili je admin lozinka placeholder.
- Iza reverse proxyja navedite pouzdane adrese u `Proxy:KnownProxies` (inače su rate limiti po IP-u beskorisni) i terminirajte TLS na proxyju.
- Dodajte pravi SMTP (`SMTP_HOST`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM`) i produkcijski Stripe webhook endpoint (njegov `whsec_`).
- QuestPDF (PDF izvoz) koristi Community licencu: besplatno za organizacije do 1 M USD godišnjeg prihoda.

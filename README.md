# Ottoman

Sistem za upravljanje barbershopom: rezervacije termina, webshop proizvoda s plaćanjem karticom, recenzije i preporuke, obavijesti i podsjetnici, izvještaji.

- **Backend:** ASP.NET Core (.NET 10), EF Core + SQL Server, ASP.NET Identity + JWT, RabbitMQ, Stripe. API upisuje događaje u outbox tabelu u istoj transakciji kao i promjenu, pa ih sam objavljuje na RabbitMQ (jedna konekcija, potvrde brokera); worker ih troši (e-mail, obavijesti, push) s ponovnim pokušajima i dead-letter redom.
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

Bez `ASPNETCORE_ENVIRONMENT` u `deploy/.env` compose pokreće API u **Production** okruženju bez demo podataka (`SEED_DEMO_DATA` je podrazumijevano `false`); `.env.example` zato za lokalni demo postavlja `ASPNETCORE_ENVIRONMENT=Development` i `SEED_DEMO_DATA=true`. SQL Server, RabbitMQ (i management) i Mailpit slušaju samo na `127.0.0.1`; API (port 5080) je dostupan i drugim uređajima u mreži (telefon).

### Pristupni podaci za testiranje

Vrijednosti iz `deploy/.env.example` (lokalni demo, `SEED_DEMO_DATA=true`). Ako `deploy/.env` postavlja druge lozinke, vrijede te.

| Uloga | Aplikacija | Korisničko ime (e-mail) | Lozinka |
|---|---|---|---|
| Administrator | desktop | `admin@ottoman.local` | `ChangeMe_Admin1!` |
| Zaposlenik (frizer) | mobile (i desktop) | `kerim@ottoman.local`, `tarik@ottoman.local`, `adin@ottoman.local` | `Test123!` |
| Klijent | mobile | `klijent@ottoman.local` | `Test123!` |

To su razvojne vrijednosti (`SEED_ADMIN_PASSWORD`, `SEED_DEMO_PASSWORD`); u produkciji API odbija start s demo podacima ili lozinkom iz primjera.

Stvarne vrijednosti `.env` datoteke isporučuju se odvojeno, u arhivi `.env-tajne.zip` (lozinka arhive se šalje zasebno); u repozitoriju je samo `deploy/.env.example`. Probna kartica za Stripe sandbox: `4242 4242 4242 4242`, bilo koji budući datum i CVC.

### Plaćanje karticom (Stripe)

U `deploy/.env` postavite `STRIPE_SECRET_KEY`, `STRIPE_PUBLISHABLE_KEY` i `STRIPE_WEBHOOK_SECRET`. Lokalno se webhook secret dobije s `stripe listen --api-key <sk_test_...> --print-secret`, a događaji se proslijede s `stripe listen --forward-to localhost:5080/api/v1/payments/webhook`. Bez ključeva narudžba karticom se ne može platiti (ostaje plaćanje pri preuzimanju). Aplikacije dobijaju publishable ključ od API-ja (`GET /api/v1/payments/config`), nije ugrađen u build. Nakon uspješnog plaćanja mobilna aplikacija poziva `POST /api/v1/orders/{id}/confirm-payment`: server sam pita Stripe za PaymentIntent (status, iznos, valuta, narudžba) i zaključuje narudžbu istim idempotentnim putem kao webhook, pa plaćanje radi i bez `stripe listen`; webhook ostaje kao drugi, nezavisni put (i za povrat novca).

### Pokretanje aplikacija

```bash
cd apps/ottoman_desktop && flutter run -d windows
cd apps/ottoman_mobile  && flutter run                 # emulator: API na http://10.0.2.2:5080
cd apps/ottoman_mobile  && flutter run --dart-define=API_BASE_URL=http://<IP računara>:5080   # pravi telefon
cd apps/ottoman_mobile  && flutter build apk --release --dart-define=API_BASE_URL=http://<IP računara>:5080
#   -> apps/ottoman_mobile/build/app/outputs/flutter-apk/app-release.apk (bez API_BASE_URL: emulator, http://10.0.2.2:5080)
cd apps/ottoman_desktop && flutter build windows --release   # -> build/windows/x64/runner/Release/
```

### Slike (Cloudinary)

Slike (proizvodi, usluge, frizeri, galerija, logo, novosti) čuvaju se na Cloudinaryju. U `deploy/.env` postavite `CLOUDINARY_CLOUD_NAME`, `CLOUDINARY_API_KEY` i `CLOUDINARY_API_SECRET`. Bez njih API provjerava upload (vrsta i veličina), ali sliku nigdje ne sprema, pa demo podaci i nove stavke ostaju bez slika (aplikacije tada prikazuju ikonu ili inicijale). S ključevima demo podaci pri startu dobiju generisane zamjenske slike.

### Push obavijesti (Firebase, opcionalno)

Push je dodatak: obavijesti u aplikaciji i mailovi rade i bez njega. Za push trebaju dvije stvari: `apps/ottoman_mobile/android/app/google-services.json` (Firebase konfiguracija aplikacije; bez nje se aplikacija normalno builda i radi, samo bez pusha) i `FIREBASE_SERVICE_ACCOUNT_JSON` u `deploy/.env` za worker (bez njega se push ne šalje, samo se zabilježi u memoriji). Obje datoteke su izvan gita.

## Razvoj

```bash
dotnet build Ottoman.slnx
dotnet test Ottoman.slnx          # integracijski testovi traže Docker (Testcontainers)
dotnet format Ottoman.slnx --verify-no-changes
```

Flutter: `cd apps/<app> && flutter pub get && flutter analyze && flutter test`.


## Produkcija — šta provjeriti

- API odbija start (`StartupGuards`) ako je `Jwt__SigningKey` primjer iz `.env.example`, ako je `Seed__DemoData=true` ili je admin lozinka placeholder.
- Iza reverse proxyja navedite pouzdane adrese u `Proxy:KnownProxies` (inače su rate limiti po IP-u beskorisni) i terminirajte TLS na proxyju.
- Dodajte pravi SMTP (`SMTP_HOST`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM`) i produkcijski Stripe webhook endpoint (njegov `whsec_`).
- QuestPDF (PDF izvoz) koristi Community licencu: besplatno za organizacije do 1 M USD godišnjeg prihoda.

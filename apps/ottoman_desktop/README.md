# Ottoman desktop aplikacija (administracija)

Flutter Windows aplikacija za admina: termini, usluge, uposlenici, proizvodi, narudžbe, recenzije, obavijesti, klijenti, izvještaji i dnevnik aktivnosti.

```bash
flutter pub get
flutter run -d windows --dart-define=API_BASE_URL=http://localhost:5080
flutter build windows --release        # build/windows/x64/runner/Release/
```

Adresa API-ja se čita iz `String.fromEnvironment('API_BASE_URL')`. Pokretanje cijelog sistema i test plan: `../../docs/11-pokretanje-i-testiranje.md`.

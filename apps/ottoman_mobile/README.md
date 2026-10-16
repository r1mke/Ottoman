# Ottoman mobilna aplikacija

Flutter aplikacija za klijente (rezervacije, webshop, obavijesti, recenzije, profil) i uposlenike (dnevni i sedmični raspored, usluge i cjenovnik, obavijesti, profil).

```bash
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5080      # Android emulator
flutter run --dart-define=API_BASE_URL=http://<IP računara>:5080 # pravi telefon na istoj mreži
flutter build apk --release                                       # build/app/outputs/flutter-apk/app-release.apk
```

Adresa API-ja se čita iz `String.fromEnvironment('API_BASE_URL')`. Pokretanje cijelog sistema i test plan: `../../docs/11-pokretanje-i-testiranje.md`.

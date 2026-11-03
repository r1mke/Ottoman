import 'package:flutter/material.dart';

/// One headed block of a text page.
class LegalSection {
  const LegalSection(this.heading, this.body);

  final String heading;
  final String body;
}

/// A scrollable text page: privacy policy, terms of use, about the app.
class LegalPage extends StatelessWidget {
  const LegalPage({super.key, required this.title, required this.sections});

  final String title;
  final List<LegalSection> sections;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: ListView(
        key: const Key('legal_page'),
        padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
        children: [
          for (final s in sections) ...[
            Padding(
              padding: const EdgeInsets.only(top: 20, bottom: 6),
              child: Text(
                s.heading,
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w800,
                  color: theme.colorScheme.primary,
                ),
              ),
            ),
            Text(
              s.body,
              style: theme.textTheme.bodyLarge?.copyWith(height: 1.5),
            ),
          ],
        ],
      ),
    );
  }
}

// The wording below is a working draft for the salon to review and adapt before the app is published.

const privacySections = [
  LegalSection(
    'Koje podatke prikupljamo',
    'Pri registraciji ime, prezime, e-mail adresu i, ako ga unesete, broj telefona. '
        'Dok koristite aplikaciju bilježimo vaše termine, narudžbe iz shopa i recenzije koje napišete. '
        'Ako dozvolite obavijesti, čuvamo i oznaku vašeg uređaja kako bismo vam mogli poslati podsjetnik na termin.',
  ),
  LegalSection(
    'Zašto ih koristimo',
    'Podatke koristimo da bismo zakazali i potvrdili termin, obradili narudžbu, poslali podsjetnik ili obavijest '
        'o promjeni i omogućili vam pregled vaše historije. Ne prodajemo ih i ne koristimo za oglašavanje trećih strana.',
  ),
  LegalSection(
    'Plaćanje karticom',
    'Kartično plaćanje obrađuje Stripe. Podaci o vašoj kartici ne prolaze kroz našu aplikaciju niti se čuvaju kod nas. '
        'Salon dobiva samo potvrdu da je plaćanje uspjelo.',
  ),
  LegalSection(
    'Ko još vidi podatke',
    'Vaše podatke vide uposlenici salona u mjeri u kojoj je to potrebno za vaš termin ili narudžbu. '
        'Za rad aplikacije koristimo usluge trećih strana (pohrana slika, slanje obavijesti i plaćanje), '
        'koje podatke obrađuju samo za tu svrhu.',
  ),
  LegalSection(
    'Zaštita podataka',
    'Lozinka se čuva isključivo u šifriranom obliku. Veza prema serveru je zaštićena, a pristup tuđim terminima i '
        'narudžbama nije moguć.',
  ),
  LegalSection(
    'Vaša prava',
    'Možete u svakom trenutku pregledati i ispraviti svoje podatke u dijelu Uredi profil. '
        'Za brisanje računa ili pitanja o podacima obratite se salonu na kontakte navedene na početnoj stranici.',
  ),
];

const termsSections = [
  LegalSection(
    'Opće odredbe',
    'Korištenjem aplikacije prihvatate ove uslove. Aplikacija služi za zakazivanje termina u salonu, '
        'pregled ponude i naručivanje proizvoda za preuzimanje u salonu.',
  ),
  LegalSection(
    'Zakazivanje termina',
    'Termin je potvrđen kada ga aplikacija prikaže među vašim rezervacijama. Prikazani su samo slobodni termini, '
        'a cijena se računa na serveru prema odabranim uslugama. Dva termina u isto vrijeme nije moguće zakazati.',
  ),
  LegalSection(
    'Otkazivanje i nedolazak',
    'Termin možete otkazati u rokovima koje aplikacija prikaže pri otkazivanju. Ako se na termin ne pojavite '
        'a niste ga otkazali, salon to može evidentirati kao nedolazak.',
  ),
  LegalSection(
    'Shop i plaćanje',
    'Proizvodi se preuzimaju u salonu. Možete platiti karticom unaprijed ili pri preuzimanju. '
        'Cijene su iskazane u konvertibilnim markama (KM). Narudžba koja ne bude plaćena karticom u predviđenom '
        'roku se poništava, a proizvodi se vraćaju u ponudu.',
  ),
  LegalSection(
    'Korisnički račun',
    'Odgovorni ste za tačnost unesenih podataka i čuvanje svoje lozinke. Salon može ograničiti pristup računu '
        'u slučaju zloupotrebe.',
  ),
  LegalSection(
    'Izmjene uslova',
    'Salon može izmijeniti ove uslove. Nastavak korištenja aplikacije nakon izmjene znači da ih prihvatate.',
  ),
];

const aboutAppSections = [
  LegalSection(
    'Ottoman',
    'Aplikacija za zakazivanje termina, pregled usluga i naručivanje proizvoda u vašem frizerskom salonu.',
  ),
  LegalSection(
    'Šta možete',
    'Zakazati termin kod odabranog majstora, pratiti i otkazati svoje rezervacije, pregledati cjenovnik i galeriju '
        'radova, naručiti proizvode za preuzimanje i ostaviti recenziju nakon posjete.',
  ),
  LegalSection('Verzija', '1.0.0'),
];

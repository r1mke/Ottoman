// ignore: unused_import
import 'package:intl/intl.dart' as intl;

import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for Bosnian (`bs`).
class AppLocalizationsBs extends AppLocalizations {
  AppLocalizationsBs([String locale = 'bs']) : super(locale);

  @override
  String get appName => 'OTTOMAN';

  @override
  String get login => 'Prijava';

  @override
  String get logout => 'Odjava';

  @override
  String get email => 'Email';

  @override
  String get password => 'Lozinka';

  @override
  String get retry => 'Pokušaj ponovo';

  @override
  String get cancel => 'Otkaži';

  @override
  String get save => 'Spremi';

  @override
  String get edit => 'Uredi';

  @override
  String get delete => 'Obriši';

  @override
  String get employees => 'Uposlenici';

  @override
  String get services => 'Usluge';

  @override
  String get appointments => 'Termini';

  @override
  String get products => 'Proizvodi';

  @override
  String get settings => 'Postavke';

  @override
  String get home => 'Početna';

  @override
  String get noData => 'Nema podataka';

  @override
  String get currencyShort => 'KM';

  @override
  String get register => 'Registracija';

  @override
  String get createAccount => 'Kreiraj račun';

  @override
  String get haveAccount => 'Već imate račun? Prijavite se';

  @override
  String get noAccount => 'Nemate račun? Registrujte se';

  @override
  String get forgotPassword => 'Zaboravljena lozinka?';

  @override
  String get resetEmailSent => 'Ako račun postoji, poslali smo upute na email.';

  @override
  String get firstName => 'Ime';

  @override
  String get lastName => 'Prezime';

  @override
  String get phone => 'Telefon';

  @override
  String get profile => 'Profil';

  @override
  String get saveChanges => 'Spremi izmjene';

  @override
  String get profileSaved => 'Profil je spremljen.';

  @override
  String get requiredField => 'Obavezno polje';

  @override
  String get invalidEmail => 'Unesite ispravan email';

  @override
  String get passwordRules =>
      'Najmanje 8 znakova, veliko i malo slovo, broj i poseban znak.';

  @override
  String get registrationDone => 'Račun je kreiran. Sada se možete prijaviti.';

  @override
  String get welcomeBack => 'Dobrodošli nazad';

  @override
  String get signInSubtitle => 'Prijavite se da nastavite';

  @override
  String get language => 'Jezik';

  @override
  String get overview => 'Pregled';

  @override
  String get clients => 'Klijenti';

  @override
  String get notifications => 'Notifikacije';

  @override
  String get reports => 'Izvještaji';

  @override
  String get earnings => 'Zarada';

  @override
  String get myProfile => 'Moj profil';

  @override
  String get comingSoon => 'Uskoro dostupno';

  @override
  String signedInAs(String name) {
    return 'Prijavljeni ste kao $name';
  }

  @override
  String get genericError => 'Došlo je do greške.';

  @override
  String get chooseEmployee => 'Odaberite uposlenika';

  @override
  String get chooseEmployeeHint =>
      'Za odabir usluga prvo odaberite nekog od naših uposlenika.';

  @override
  String get pricelist => 'Usluge & Cjenovnik';

  @override
  String minutesShort(int count) {
    return '$count min';
  }

  @override
  String get total => 'Ukupno';

  @override
  String get continueLabel => 'Nastavi';

  @override
  String get selectServices => 'Odaberite jednu ili više usluga';

  @override
  String get add => 'Dodaj';

  @override
  String get name => 'Naziv';

  @override
  String get description => 'Opis';

  @override
  String get price => 'Cijena';

  @override
  String get duration => 'Trajanje (min)';

  @override
  String get active => 'Aktivno';

  @override
  String get inactive => 'Neaktivno';

  @override
  String get title => 'Titula';

  @override
  String get bio => 'Opis / bio';

  @override
  String get workingHours => 'Radno vrijeme';

  @override
  String get dayOff => 'Neradni dan';

  @override
  String get breakLabel => 'Pauza';

  @override
  String get from => 'Od';

  @override
  String get to => 'Do';

  @override
  String get deactivate => 'Deaktiviraj';

  @override
  String get searchHint => 'Pretraga…';

  @override
  String get durationStepError => 'Trajanje mora biti višekratnik od 5 minuta.';

  @override
  String get invalidNumber => 'Unesite ispravan broj';
}

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
  String get invalidEmail => 'Unesite email u formatu ime@domena.ba';

  @override
  String get invalidPhone =>
      'Unesite ispravan broj telefona, npr. 061 123 456 ili +387 61 123 456.';

  @override
  String get invalidName =>
      'Unesite 2 do 50 slova (dozvoljeni su razmak, tačka, apostrof i crtica).';

  @override
  String get invalidPrice =>
      'Unesite cijenu veću od 0, najviše dvije decimale (npr. 12,50).';

  @override
  String invalidWholeNumber(int min, int max) {
    return 'Unesite cijeli broj od $min do $max.';
  }

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
      'Prikazani su uposlenici koji rade sve odabrane usluge.';

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

  @override
  String get book => 'Rezerviši';

  @override
  String get noSlots => 'Nema dostupnih termina za odabrani datum';

  @override
  String get showAllTimes => 'Prikaži sve termine';

  @override
  String get showFewerTimes => 'Prikaži manje termina';

  @override
  String get morning => 'Jutro';

  @override
  String get afternoon => 'Popodne';

  @override
  String get evening => 'Veče';

  @override
  String get note => 'Napomena';

  @override
  String get noteHint => 'Ostavite poruku';

  @override
  String get bookingConfirmed => 'Termin je rezervisan.';

  @override
  String get upcoming => 'Nadolazeći';

  @override
  String get past => 'Prošli';

  @override
  String get cancelBooking => 'Otkaži termin';

  @override
  String get cancelBookingQuestion =>
      'Da li sigurno želite otkazati ovaj termin?';

  @override
  String get no => 'Ne';

  @override
  String get yes => 'Da';

  @override
  String get statusPending => 'Na čekanju';

  @override
  String get statusConfirmed => 'Potvrđen';

  @override
  String get statusCompleted => 'Završen';

  @override
  String get statusCancelled => 'Otkazan';

  @override
  String get statusNoShow => 'Nije došao';

  @override
  String get complete => 'Završi';

  @override
  String get markNoShow => 'Nije došao';

  @override
  String get reschedule => 'Uredi termin';

  @override
  String get newAppointment => 'Novi termin';

  @override
  String get client => 'Klijent';

  @override
  String get employee => 'Uposlenik';

  @override
  String get date => 'Datum';

  @override
  String get time => 'Vrijeme';

  @override
  String get service => 'Usluga';

  @override
  String get status => 'Status';

  @override
  String get allEmployees => 'Svi uposlenici';

  @override
  String get today => 'Danas';

  @override
  String get weekly => 'Sedmično';

  @override
  String get monthly => 'Mjesečno';

  @override
  String get statUpcoming => 'Nadolazeći termini';

  @override
  String get statCompleted => 'Završeni termini';

  @override
  String get statCancelled => 'Otkazani termini';

  @override
  String get statClients => 'Ukupno mušterija';

  @override
  String get appointmentsOverview => 'Pregled termina';

  @override
  String get noAppointments => 'Nema termina';

  @override
  String get chooseClient => 'Odaberite klijenta';

  @override
  String get searchClient => 'Pretraži klijente';

  @override
  String get previousDay => 'Prethodni dan';

  @override
  String get nextDay => 'Sljedeći dan';

  @override
  String get slotTaken => 'Termin je upravo zauzet. Odaberite drugi.';

  @override
  String get cart => 'Korpa';

  @override
  String get addToCart => 'Dodaj u korpu';

  @override
  String get outOfStock => 'Nema na zalihi';

  @override
  String get lowStock => 'Niska zaliha';

  @override
  String get stock => 'Zaliha';

  @override
  String inStockCount(int count) {
    return 'Na zalihi: $count';
  }

  @override
  String get allCategories => 'Sve kategorije';

  @override
  String get sortBy => 'Sortiraj';

  @override
  String get sortName => 'Naziv';

  @override
  String get sortPriceAsc => 'Cijena: manja prvo';

  @override
  String get sortPriceDesc => 'Cijena: veća prvo';

  @override
  String get sortNewest => 'Najnovije';

  @override
  String get cartEmpty => 'Korpa je prazna';

  @override
  String get limitReached => 'Nema više komada na zalihi.';

  @override
  String get checkout => 'Naplata';

  @override
  String get pickup => 'Preuzimanje u salonu';

  @override
  String get delivery => 'Dostava';

  @override
  String get deliveryAddress => 'Adresa za dostavu';

  @override
  String get payByCard => 'Karticom';

  @override
  String get payOnReceipt => 'Pri preuzimanju';

  @override
  String get placeOrder => 'Naruči';

  @override
  String get orderPlaced => 'Narudžba je zaprimljena.';

  @override
  String get orderAwaitsPayment => 'Narudžba čeka plaćanje.';

  @override
  String get cardUnavailable =>
      'Plaćanje karticom trenutno nije dostupno u ovoj aplikaciji. Odaberite plaćanje pri preuzimanju.';

  @override
  String get payNow => 'Plati';

  @override
  String get myOrders => 'Moje narudžbe';

  @override
  String get noOrders => 'Nema narudžbi';

  @override
  String get orders => 'Narudžbe';

  @override
  String orderNumber(int id) {
    return 'Narudžba #$id';
  }

  @override
  String get orderStatusPendingPayment => 'Čeka plaćanje';

  @override
  String get orderStatusPlaced => 'Zaprimljena';

  @override
  String get orderStatusFulfilled => 'Preuzeta';

  @override
  String get orderStatusCancelled => 'Otkazana';

  @override
  String get orderStatusExpired => 'Istekla';

  @override
  String get cancelOrder => 'Otkaži narudžbu';

  @override
  String get cancelOrderQuestion => 'Da li sigurno želite otkazati narudžbu?';

  @override
  String get markFulfilled => 'Preuzeta / isporučena';

  @override
  String get restock => 'Dopuni zalihu';

  @override
  String get stockChange => 'Promjena zalihe (npr. 5 ili -2)';

  @override
  String get brand => 'Brend';

  @override
  String get brands => 'Brendovi';

  @override
  String get category => 'Kategorija';

  @override
  String get categories => 'Kategorije';

  @override
  String get lowStockOnly => 'Samo niska zaliha';

  @override
  String get newProduct => 'Novi proizvod';

  @override
  String get lowStockThreshold => 'Prag niske zalihe';

  @override
  String get initialStock => 'Početna zaliha';

  @override
  String get manage => 'Upravljaj';

  @override
  String get allStatuses => 'Svi statusi';

  @override
  String productsCount(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count proizvoda',
      few: '$count proizvoda',
      one: '$count proizvod',
    );
    return '$_temp0';
  }

  @override
  String get payment => 'Plaćanje';

  @override
  String get paymentStatusPending => 'Čeka uplatu';

  @override
  String get paymentStatusSucceeded => 'Plaćeno';

  @override
  String get paymentStatusFailed => 'Neuspjelo';

  @override
  String get paymentStatusRefunded => 'Vraćeno';

  @override
  String get paymentStatusRefundPending => 'Povrat u obradi';

  @override
  String get paymentNotConfirmed =>
      'Plaćanje nije potvrđeno. Narudžba čeka uplatu u „Moje narudžbe“; ako ostane neplaćena, sama se zatvara.';

  @override
  String get paymentSucceeded => 'Plaćanje je uspjelo. Hvala!';

  @override
  String get paymentCancelledPayLater =>
      'Plaćanje je otkazano. Narudžbu možete platiti kasnije.';

  @override
  String get none => 'Bez';

  @override
  String get noNotifications => 'Nema obavijesti';

  @override
  String get markAllRead => 'Označi sve kao pročitano';

  @override
  String get audienceAll => 'Svi';

  @override
  String get audienceClients => 'Klijenti';

  @override
  String get audienceEmployees => 'Uposlenici';

  @override
  String get audienceUser => 'Jedan korisnik';

  @override
  String get audience => 'Publika';

  @override
  String get messageTitle => 'Naslov';

  @override
  String get messageBody => 'Poruka';

  @override
  String get alsoSendEmail => 'Pošalji i na email';

  @override
  String get send => 'Pošalji';

  @override
  String get announcementSent => 'Poruka je poslana.';

  @override
  String get sentMessages => 'Poslane poruke';

  @override
  String recipientsCount(int count) {
    return '$count primalaca';
  }

  @override
  String get newMessage => 'Nova poruka';

  @override
  String get haveCode => 'Imam kod';

  @override
  String get resetCode => 'Kod iz maila';

  @override
  String get newPassword => 'Nova lozinka';

  @override
  String get passwordChanged => 'Lozinka je promijenjena. Prijavite se.';

  @override
  String get resetFailed => 'Kod nije ispravan ili je istekao.';

  @override
  String get close => 'Zatvori';

  @override
  String get daySunday => 'Nedjelja';

  @override
  String get dayMonday => 'Ponedjeljak';

  @override
  String get dayTuesday => 'Utorak';

  @override
  String get dayWednesday => 'Srijeda';

  @override
  String get dayThursday => 'Četvrtak';

  @override
  String get dayFriday => 'Petak';

  @override
  String get daySaturday => 'Subota';

  @override
  String get reviews => 'Recenzije';

  @override
  String get noReviews => 'Još nema recenzija';

  @override
  String get writeReview => 'Ocijeni';

  @override
  String get yourRating => 'Vaša ocjena';

  @override
  String get yourComment => 'Komentar (nije obavezan)';

  @override
  String get reviewSaved => 'Hvala na recenziji!';

  @override
  String get pickRating => 'Odaberite ocjenu.';

  @override
  String get recommendedForYou => 'Možda vam se sviđa';

  @override
  String get similarProducts => 'Slično';

  @override
  String get hideReview => 'Sakrij';

  @override
  String get showReview => 'Prikaži';

  @override
  String get reply => 'Odgovori';

  @override
  String get salonReply => 'Odgovor salona';

  @override
  String get hiddenReviews => 'Samo skrivene';

  @override
  String get replySaved => 'Odgovor je spremljen.';

  @override
  String get yourReply => 'Vaš odgovor';

  @override
  String get periodToday => 'Danas';

  @override
  String get period7 => '7 dana';

  @override
  String get period30 => '30 dana';

  @override
  String get periodThisMonth => 'Ovaj mjesec';

  @override
  String get periodLastMonth => 'Prošli mjesec';

  @override
  String get periodThisYear => 'Ova godina';

  @override
  String get periodCustom => 'Odaberi period';

  @override
  String get kpiRevenue => 'Ukupni prihod';

  @override
  String get kpiServices => 'Prihod od usluga';

  @override
  String get kpiProducts => 'Prihod od proizvoda';

  @override
  String get kpiAppointments => 'Završeni termini';

  @override
  String get kpiOrders => 'Narudžbe';

  @override
  String get kpiNewClients => 'Novi klijenti';

  @override
  String get kpiAverageTicket => 'Prosječan račun';

  @override
  String get kpiNoShow => 'Nije došlo';

  @override
  String get kpiCancelled => 'Otkazano';

  @override
  String get vsPrevious => 'u odnosu na prethodni period';

  @override
  String get revenueOverTime => 'Prihod kroz vrijeme';

  @override
  String get topServices => 'Najtraženije usluge';

  @override
  String get topProducts => 'Najprodavaniji proizvodi';

  @override
  String get employeePerformance => 'Učinak uposlenika';

  @override
  String get completedShort => 'Završeno';

  @override
  String get noShowShort => 'Nije došao';

  @override
  String get cancelledShort => 'Otkazano';

  @override
  String get revenueShort => 'Prihod';

  @override
  String get ratingShort => 'Ocjena';

  @override
  String get export => 'Izvezi';

  @override
  String get exportCsv => 'CSV';

  @override
  String get exportExcel => 'Excel';

  @override
  String get exportPdf => 'PDF';

  @override
  String savedTo(String path) {
    return 'Spremljeno: $path';
  }

  @override
  String get showInFolder => 'Prikaži';

  @override
  String get reportRevenue => 'Prihod';

  @override
  String get reportEmployees => 'Uposlenici';

  @override
  String get reportServices => 'Usluge';

  @override
  String get reportProducts => 'Proizvodi';

  @override
  String get reportReservations => 'Termini';

  @override
  String get reportOrders => 'Narudžbe';

  @override
  String get reportCustom => 'Prilagođeni izvještaj';

  @override
  String get chooseSource => 'Izvor podataka';

  @override
  String get chooseFields => 'Polja';

  @override
  String get runReport => 'Prikaži izvještaj';

  @override
  String get allStatusesOption => 'Svi statusi';

  @override
  String get noDataForPeriod => 'Nema podataka za odabrani period.';

  @override
  String get pictures => 'Slike';

  @override
  String get addPicture => 'Dodaj sliku';

  @override
  String get changePicture => 'Promijeni sliku';

  @override
  String get removePicture => 'Ukloni sliku';

  @override
  String get savePictureFirst =>
      'Slike se mogu dodati nakon što se stavka spremi.';

  @override
  String get clientSearchHint => 'Pretraži po imenu, emailu ili telefonu';

  @override
  String get clientRegistered => 'Registrovan';

  @override
  String get clientBlocked => 'Blokiran';

  @override
  String get clientBlock => 'Blokiraj';

  @override
  String get clientUnblock => 'Odblokiraj';

  @override
  String clientBlockQuestion(String name) {
    return 'Blokirati klijenta $name? Odmah će biti odjavljen i neće se moći prijaviti dok ga ne odblokirate.';
  }

  @override
  String clientUnblockQuestion(String name) {
    return 'Odblokirati klijenta $name?';
  }

  @override
  String get clientVisits => 'Posjete';

  @override
  String get clientNoShows => 'Nije došao';

  @override
  String get clientCancelled => 'Otkazano';

  @override
  String get clientUpcoming => 'Predstojeći termini';

  @override
  String get clientSpentServices => 'Potrošeno na usluge';

  @override
  String get clientSpentProducts => 'Potrošeno na proizvode';

  @override
  String get clientLastVisit => 'Zadnja posjeta';

  @override
  String get clientNever => 'Još nije dolazio';

  @override
  String get clientRecentReservations => 'Posljednji termini';

  @override
  String get clientRecentOrders => 'Posljednje narudžbe';

  @override
  String get previousPage => 'Prethodna stranica';

  @override
  String get nextPage => 'Sljedeća stranica';

  @override
  String get auditLog => 'Dnevnik aktivnosti';

  @override
  String get auditSearchHint => 'Pretraži po osobi, radnji ili zapisu';

  @override
  String get auditWhen => 'Vrijeme';

  @override
  String get auditWho => 'Ko';

  @override
  String get auditWhat => 'Radnja';

  @override
  String get auditTarget => 'Zapis';

  @override
  String get viewDay => 'Dan';

  @override
  String get viewWeek => 'Sedmica';

  @override
  String get previousWeek => 'Prethodna sedmica';

  @override
  String get changePassword => 'Promijeni lozinku';

  @override
  String get currentPassword => 'Trenutna lozinka';

  @override
  String get repeatPassword => 'Ponovite novu lozinku';

  @override
  String get passwordsDoNotMatch => 'Lozinke se ne podudaraju.';

  @override
  String get nextWeek => 'Sljedeća sedmica';

  @override
  String get cancelReasonLabel => 'Razlog otkazivanja';

  @override
  String get cancelReasonOptionalLabel => 'Razlog (nije obavezan)';

  @override
  String get cancelReasonRequired =>
      'Unesite razlog (najmanje 3 znaka). Klijent ga dobija u obavijesti.';

  @override
  String get pictureNotSaved => 'Slika nije spremljena';

  @override
  String get chooseAccount => 'Ko se prijavljuje?';

  @override
  String get addAccount => 'Dodaj račun';

  @override
  String get removeFromList => 'Ukloni s liste';

  @override
  String get backToAccounts => 'Nazad na račune';

  @override
  String get mainPicture => 'Glavna';

  @override
  String get pictureHint =>
      'JPEG, PNG ili WebP, do 5 MB. Ista slika se ne može dodati dvaput.';

  @override
  String get manageTitles => 'Uredi listu titula';

  @override
  String get newTitle => 'Nova titula';

  @override
  String titleUsedBy(int count) {
    return 'Koristi: $count';
  }

  @override
  String get titleInUseHint =>
      'Titulu koriste uposlenici, promijenite im je prije brisanja.';

  @override
  String get gender => 'Spol';

  @override
  String get genderMale => 'Muški';

  @override
  String get genderFemale => 'Ženski';

  @override
  String get done => 'Gotovo';

  @override
  String get manageAccounts => 'Uredi račune';

  @override
  String get removeAccountHint =>
      'Račun se samo uklanja s liste na ovom računaru.';

  @override
  String get zoom => 'Zoom';

  @override
  String get resetZoom => 'Resetuj';

  @override
  String get zoomHint =>
      'Slika se sama centrira na lice. Zoom određuje koliko se približava.';

  @override
  String get galleryTitle => 'Galerija radova';

  @override
  String get ourWork => 'Naši radovi';

  @override
  String get viewGallery => 'Pogledaj galeriju';

  @override
  String get allStyles => 'Sve';

  @override
  String get galleryEmpty => 'Još nema dodanih radova.';

  @override
  String get closedDay => 'Zatvoreno';

  @override
  String get openInMaps => 'Otvori u Google Maps';

  @override
  String get callSalon => 'Pozovi';

  @override
  String get findUs => 'Gdje smo';

  @override
  String get bookAppointment => 'Rezerviši termin';

  @override
  String get haircutStyle => 'Stil frizure';

  @override
  String get doneBy => 'Frizer';

  @override
  String get addWork => 'Dodaj rad';

  @override
  String get editWork => 'Uredi rad';

  @override
  String get captionOptional => 'Opis (nije obavezno)';

  @override
  String get showOnHome => 'Prikaži na početnoj';

  @override
  String get onHomeBadge => 'Na početnoj';

  @override
  String get noBarber => 'Bez uposlenika';

  @override
  String get styles => 'Stilovi frizura';

  @override
  String get addStyle => 'Dodaj stil';

  @override
  String get styleName => 'Naziv stila';

  @override
  String get chooseStyle => 'Odaberite stil frizure.';

  @override
  String get choosePicture => 'Odaberite sliku';

  @override
  String get deleteWorkQuestion => 'Obrisati ovaj rad iz galerije?';

  @override
  String get deleteStyleQuestion => 'Obrisati ovaj stil?';

  @override
  String get workAdded => 'Rad je dodan.';

  @override
  String get salonProfile => 'Salon';

  @override
  String get salonName => 'Naziv salona';

  @override
  String get address => 'Adresa';

  @override
  String get mapsLink => 'Google lokacija (link)';

  @override
  String get logo => 'Logo';

  @override
  String get openFrom => 'Od';

  @override
  String get openTo => 'Do';

  @override
  String get openDay => 'Radi';

  @override
  String get invalidUrl => 'Unesite ispravnu web adresu (https://...).';

  @override
  String get hoursOrder => 'Vrijeme otvaranja mora biti prije zatvaranja.';

  @override
  String get salonSaved => 'Podaci o salonu su spremljeni.';

  @override
  String get aboutSalon => 'O salonu';

  @override
  String get latitude => 'Geografska širina';

  @override
  String get longitude => 'Geografska dužina';

  @override
  String get coordinatesBoth => 'Unesite i širinu i dužinu, ili nijednu.';

  @override
  String get invalidCoordinate => 'Neispravna vrijednost.';

  @override
  String get chooseServicesHint =>
      'Odaberite jednu ili više usluga, a zatim uposlenika i termin.';

  @override
  String get noBarberForServices =>
      'Nijedan uposlenik ne radi sve odabrane usluge. Vratite se i promijenite odabir.';

  @override
  String get quantity => 'Količina';

  @override
  String addedToCartCount(int count) {
    return 'Dodano u korpu: $count';
  }

  @override
  String get allProducts => 'Svi proizvodi';

  @override
  String get salePrice => 'Akcijska cijena';

  @override
  String get salePriceHint => 'Ostavite prazno ako proizvod nije na akciji';

  @override
  String get salePriceTooHigh => 'Mora biti manja od redovne cijene';

  @override
  String get shopSectionsTitle => 'Početna shopa';

  @override
  String get shopSectionsHint =>
      'Redovi koje kupci vide na vrhu shopa. Povucite za promjenu redoslijeda.';

  @override
  String get addSection => 'Dodaj sekciju';

  @override
  String get editSection => 'Uredi sekciju';

  @override
  String get sectionTitle => 'Naslov';

  @override
  String get sectionKind => 'Šta prikazuje';

  @override
  String get kindNewArrivals => 'Novo u ponudi (zadnje dodani proizvodi)';

  @override
  String get kindOnSale => 'Akcije (proizvodi s akcijskom cijenom)';

  @override
  String get kindCategory => 'Proizvodi jedne kategorije i/ili brenda';

  @override
  String get kindManual => 'Ručni izbor proizvoda';

  @override
  String get sectionMaxItems => 'Najviše proizvoda';

  @override
  String get sectionVisible => 'Prikazano kupcima';

  @override
  String get sectionPickProducts => 'Odaberite proizvode';

  @override
  String get sectionSelected => 'Odabrano';

  @override
  String get noSections => 'Još nema sekcija. Dodajte prvu.';

  @override
  String get deleteSectionQuestion =>
      'Obrisati sekciju? Proizvodi ostaju u ponudi.';

  @override
  String get searchProducts => 'Pretraži proizvode';

  @override
  String get mapsLinkHint =>
      'Zalijepite link iz Google Maps (Dijeli). Pin za mapu u aplikaciji se očitava iz linka; ako ne uspije, unesite širinu i dužinu ispod.';

  @override
  String get paymentNotCompleted =>
      'Plaćanje nije završeno. Narudžba nije kreirana, a proizvodi su ostali u korpi.';

  @override
  String get groupWork => 'Rad';

  @override
  String get groupShop => 'Shop';

  @override
  String get groupSalon => 'Salon';

  @override
  String get groupClients => 'Klijenti';

  @override
  String get groupAnalytics => 'Analitika';

  @override
  String get groupAccount => 'Račun';

  @override
  String get any => 'Bilo koji';

  @override
  String get pickCategoryOrBrand => 'Odaberite kategoriju ili brend';

  @override
  String get instagramLink => 'Instagram (link)';

  @override
  String get facebookLink => 'Facebook (link)';

  @override
  String get connectWithUs => 'Povežite se s nama';

  @override
  String get findUsHere => 'Pronađite nas ovdje';

  @override
  String get callUs => 'Pozovi';

  @override
  String get instagram => 'Instagram';

  @override
  String get facebook => 'Facebook';

  @override
  String get eyebrowGallery => 'Galerija';

  @override
  String get eyebrowContact => 'Kontakt';

  @override
  String get eyebrowLocation => 'Lokacija';

  @override
  String get eyebrowSchedule => 'Raspored';

  @override
  String get eyebrowAbout => 'O nama';

  @override
  String get getToKnowUs => 'Upoznajte nas';

  @override
  String welcomeTo(String name) {
    return 'Dobrodošli u $name';
  }

  @override
  String get groupPersonalData => 'Lični podaci';

  @override
  String get groupAboutUs => 'O nama';

  @override
  String get editProfile => 'Uredi profil';

  @override
  String get manageAccount => 'Upravljanje računom';

  @override
  String get privacyPolicy => 'Pravila privatnosti';

  @override
  String get termsOfUse => 'Uslovi korištenja';

  @override
  String get aboutApp => 'O aplikaciji';

  @override
  String get chooseDate => 'Odaberite datum';

  @override
  String get chooseTime => 'Odaberite sate';

  @override
  String get unavailableMarkedRed =>
      'Nedostupni termini su označeni crvenom bojom';

  @override
  String get eyebrowAssortment => 'Asortiman';

  @override
  String get eyebrowBrands => 'Marke';

  @override
  String get brandsTitle => 'Brendovi';

  @override
  String get onSaleTitle => 'Akcije';

  @override
  String get newInStoreTitle => 'Novo u ponudi';

  @override
  String get newestProducts => 'Najnoviji proizvodi';

  @override
  String get searchResults => 'Rezultati pretrage';

  @override
  String discountUpTo(int percent) {
    return 'do -$percent%';
  }

  @override
  String get showAsCard => 'Prikaži kao karticu na ulaznoj stranici';

  @override
  String get cardPicture => 'Slika kartice';

  @override
  String get cardPictureAuto => 'Automatska slika (prvi proizvod u sekciji)';

  @override
  String get uploadPicture => 'Pošalji sliku';

  @override
  String get pickProductPicture => 'Izaberi sliku proizvoda';

  @override
  String get saveSectionFirst => 'Prvo spremite sekciju, pa joj dodajte sliku.';

  @override
  String get pickPictureTitle => 'Odaberite sliku proizvoda';

  @override
  String get noPicturesFound => 'Nema proizvoda sa slikama za ovu pretragu.';

  @override
  String get brandLogoUpload => 'Postavi logotip';

  @override
  String get brandLogoRemove => 'Ukloni logotip';

  @override
  String get sectionTitleHint => 'Kratak naslov koji lijepo stane na karticu';

  @override
  String sectionTitleTooShort(int min) {
    return 'Naslov mora imati najmanje $min znaka.';
  }

  @override
  String sectionTitleTooLong(int max) {
    return 'Naslov može imati najviše $max znaka.';
  }

  @override
  String get sectionLayout => 'Oblik sekcije';

  @override
  String get layoutRow => 'Red';

  @override
  String get layoutGrid => 'Mreža';

  @override
  String get layoutBanner => 'Veliki baner';

  @override
  String get layoutRowHint =>
      'Proizvodi u jednom redu koji se pomiče u stranu.';

  @override
  String get layoutGridHint => 'Proizvodi u dvije kolone, jedan ispod drugog.';

  @override
  String get layoutBannerHint =>
      'Široka slika s naslovom, a ispod nje red proizvoda.';

  @override
  String get previewTitle => 'Pregled u aplikaciji';

  @override
  String get previewCard => 'Kartica';

  @override
  String get previewSection => 'Sekcija';

  @override
  String get previewSectionTitlePlaceholder => 'Naslov sekcije';

  @override
  String get shopTab => 'Shop';

  @override
  String get sectionEyebrow => 'Mali tekst iznad naslova (žuti)';

  @override
  String get sectionEyebrowHint =>
      'Neobavezno, npr. KOLEKCIJA. Prikazuje se žutim slovima.';

  @override
  String get sectionTitleWhite => 'Naslov (bijeli tekst)';

  @override
  String get sectionEmptyWarning =>
      'Ova sekcija trenutno nema nijedan proizvod, pa je kupci ne vide. Promijenite odabir proizvoda.';

  @override
  String sectionHasProducts(int count) {
    return 'Trenutno prikazuje $count proizvoda';
  }

  @override
  String get sectionEmptyShort => 'Nema proizvoda: kupci ne vide ovu sekciju';

  @override
  String get seeAll => 'Vidi sve';

  @override
  String get kindCategoryTiles => 'Blok kategorija (pločice)';

  @override
  String get kindBrandTiles => 'Blok brendova (kartice)';

  @override
  String get kindRecommended => 'Preporuke („Možda vam se sviđa“)';

  @override
  String get styleTitle => 'Izgled';

  @override
  String get backgroundLabel => 'Pozadina sekcije';

  @override
  String get bgNone => 'Bez ploče';

  @override
  String get bgSoft => 'Tamna ploča';

  @override
  String get bgAmber => 'Jantarna ploča';

  @override
  String get bannerAlwaysPanel =>
      'Baner uvijek stoji na ploči, zajedno s proizvodima ispod.';

  @override
  String get alignLabel => 'Poravnanje naslova';

  @override
  String get alignLeft => 'Lijevo';

  @override
  String get alignCenter => 'Sredina';

  @override
  String get tileSizeLabel => 'Veličina proizvoda';

  @override
  String get tileCompact => 'Mali';

  @override
  String get tileNormal => 'Srednji';

  @override
  String get tileLarge => 'Veliki';

  @override
  String get showSeeAllLabel => 'Prikaži vezu „Vidi sve“ ispod sekcije';

  @override
  String get cardSpanLabel => 'Širina kartice';

  @override
  String get spanHalf => 'Pola širine';

  @override
  String get spanFull => 'Cijela širina';

  @override
  String get tileCountCategories => 'Broj kategorija (paran)';

  @override
  String get tileCountBrands => 'Broj brendova';

  @override
  String get tilesManual => 'Ručni izbor i redoslijed';

  @override
  String get tilesAutoHint =>
      'Automatski: prikazuju se samo oni koji imaju proizvoda, najpopunjeniji prvi.';

  @override
  String get tilesPickHint =>
      'Dodirnite da dodate, povucite da promijenite redoslijed. Prikazuju se samo oni koji imaju proizvoda.';

  @override
  String get tilesSelected => 'Odabrano';

  @override
  String get tilesAvailable => 'Dostupno';

  @override
  String get categoriesEvenError =>
      'Broj odabranih kategorija mora biti paran, da zadnji red bude pun.';

  @override
  String get blockNoDelete =>
      'Ovaj blok se ne briše, samo se sakriva prekidačem.';

  @override
  String sectionShows(int count) {
    return 'Prikazuje: $count';
  }

  @override
  String get previewSeeAll => 'Vidi sve';

  @override
  String get confirmOrderTitle => 'Potvrda narudžbe';

  @override
  String confirmOrderBody(String total, String payment) {
    return 'Narudžba na iznos $total. $payment. Preuzimanje u salonu.';
  }

  @override
  String get confirmBookingTitle => 'Potvrda rezervacije';

  @override
  String confirmBookingBody(String barber, String when, String total) {
    return 'Rezervisati termin kod $barber, $when, ukupno $total?';
  }

  @override
  String get messagePicture => 'Slika (neobavezno)';

  @override
  String get removePictureShort => 'Ukloni';

  @override
  String get salonLocation => 'Lokacija salona na mapi';

  @override
  String get salonLocationHint =>
      'Upišite adresu i pronađite je, ili kliknite na mapu da postavite oznaku.';

  @override
  String get searchAddress => 'Adresa za pretragu';

  @override
  String get findOnMap => 'Pronađi';

  @override
  String get addressNotFound =>
      'Adresa nije pronađena. Kliknite na mapu da postavite oznaku.';

  @override
  String get noPinYet => 'Oznaka još nije postavljena.';

  @override
  String get pinSet => 'Oznaka je postavljena.';

  @override
  String get removePin => 'Ukloni oznaku';

  @override
  String confirmDeleteNamed(String name) {
    return 'Obrisati „$name“? Ovo se ne može poništiti.';
  }

  @override
  String get removePictureQuestion => 'Ukloniti ovu sliku?';

  @override
  String fulfilOrderQuestion(int id) {
    return 'Označiti narudžbu #$id kao preuzetu? Ovo se ne može poništiti.';
  }

  @override
  String announceQuestion(String audience) {
    return 'Poslati ovu poruku: $audience? Primaoci je odmah vide u aplikaciji.';
  }

  @override
  String announceEmailQuestion(String audience) {
    return 'Poslati ovu poruku: $audience, i e-mailom? Poslani mailovi se ne mogu povući.';
  }

  @override
  String get completeQuestion => 'Označiti termin kao završen?';

  @override
  String get noShowQuestion => 'Označiti da klijent nije došao na termin?';

  @override
  String get sectionProductsCountError =>
      'Odaberite od 1 do 30 proizvoda za ovu sekciju.';

  @override
  String get quantityLess => 'Jedan komad manje';

  @override
  String get quantityMore => 'Jedan komad više';

  @override
  String get removeFromSelection => 'Ukloni iz odabira';

  @override
  String get employeeSearchHint => 'Pretraži po imenu ili zvanju';

  @override
  String get serviceSearchHint => 'Pretraži po nazivu ili opisu';

  @override
  String get clearSearch => 'Obriši pretragu';

  @override
  String get renameTitle => 'Preimenuj titulu';

  @override
  String get titleName => 'Naziv titule';
}

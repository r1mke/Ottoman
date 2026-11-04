import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:intl/intl.dart' as intl;

import 'app_localizations_bs.dart';
import 'app_localizations_en.dart';

// ignore_for_file: type=lint

/// Callers can lookup localized strings with an instance of AppLocalizations
/// returned by `AppLocalizations.of(context)`.
///
/// Applications need to include `AppLocalizations.delegate()` in their app's
/// `localizationDelegates` list, and the locales they support in the app's
/// `supportedLocales` list. For example:
///
/// ```dart
/// import 'l10n/app_localizations.dart';
///
/// return MaterialApp(
///   localizationsDelegates: AppLocalizations.localizationsDelegates,
///   supportedLocales: AppLocalizations.supportedLocales,
///   home: MyApplicationHome(),
/// );
/// ```
///
/// ## Update pubspec.yaml
///
/// Please make sure to update your pubspec.yaml to include the following
/// packages:
///
/// ```yaml
/// dependencies:
///   # Internationalization support.
///   flutter_localizations:
///     sdk: flutter
///   intl: any # Use the pinned version from flutter_localizations
///
///   # Rest of dependencies
/// ```
///
/// ## iOS Applications
///
/// iOS applications define key application metadata, including supported
/// locales, in an Info.plist file that is built into the application bundle.
/// To configure the locales supported by your app, you’ll need to edit this
/// file.
///
/// First, open your project’s ios/Runner.xcworkspace Xcode workspace file.
/// Then, in the Project Navigator, open the Info.plist file under the Runner
/// project’s Runner folder.
///
/// Next, select the Information Property List item, select Add Item from the
/// Editor menu, then select Localizations from the pop-up menu.
///
/// Select and expand the newly-created Localizations item then, for each
/// locale your application supports, add a new item and select the locale
/// you wish to add from the pop-up menu in the Value field. This list should
/// be consistent with the languages listed in the AppLocalizations.supportedLocales
/// property.
abstract class AppLocalizations {
  AppLocalizations(String locale)
    : localeName = intl.Intl.canonicalizedLocale(locale.toString());

  final String localeName;

  static AppLocalizations of(BuildContext context) {
    return Localizations.of<AppLocalizations>(context, AppLocalizations)!;
  }

  static const LocalizationsDelegate<AppLocalizations> delegate =
      _AppLocalizationsDelegate();

  /// A list of this localizations delegate along with the default localizations
  /// delegates.
  ///
  /// Returns a list of localizations delegates containing this delegate along with
  /// GlobalMaterialLocalizations.delegate, GlobalCupertinoLocalizations.delegate,
  /// and GlobalWidgetsLocalizations.delegate.
  ///
  /// Additional delegates can be added by appending to this list in
  /// MaterialApp. This list does not have to be used at all if a custom list
  /// of delegates is preferred or required.
  static const List<LocalizationsDelegate<dynamic>> localizationsDelegates =
      <LocalizationsDelegate<dynamic>>[
        delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
      ];

  /// A list of this localizations delegate's supported locales.
  static const List<Locale> supportedLocales = <Locale>[
    Locale('bs'),
    Locale('en'),
  ];

  /// No description provided for @appName.
  ///
  /// In bs, this message translates to:
  /// **'OTTOMAN'**
  String get appName;

  /// No description provided for @login.
  ///
  /// In bs, this message translates to:
  /// **'Prijava'**
  String get login;

  /// No description provided for @logout.
  ///
  /// In bs, this message translates to:
  /// **'Odjava'**
  String get logout;

  /// No description provided for @email.
  ///
  /// In bs, this message translates to:
  /// **'Email'**
  String get email;

  /// No description provided for @password.
  ///
  /// In bs, this message translates to:
  /// **'Lozinka'**
  String get password;

  /// No description provided for @retry.
  ///
  /// In bs, this message translates to:
  /// **'Pokušaj ponovo'**
  String get retry;

  /// No description provided for @cancel.
  ///
  /// In bs, this message translates to:
  /// **'Otkaži'**
  String get cancel;

  /// No description provided for @save.
  ///
  /// In bs, this message translates to:
  /// **'Spremi'**
  String get save;

  /// No description provided for @edit.
  ///
  /// In bs, this message translates to:
  /// **'Uredi'**
  String get edit;

  /// No description provided for @delete.
  ///
  /// In bs, this message translates to:
  /// **'Obriši'**
  String get delete;

  /// No description provided for @employees.
  ///
  /// In bs, this message translates to:
  /// **'Uposlenici'**
  String get employees;

  /// No description provided for @services.
  ///
  /// In bs, this message translates to:
  /// **'Usluge'**
  String get services;

  /// No description provided for @appointments.
  ///
  /// In bs, this message translates to:
  /// **'Termini'**
  String get appointments;

  /// No description provided for @products.
  ///
  /// In bs, this message translates to:
  /// **'Proizvodi'**
  String get products;

  /// No description provided for @settings.
  ///
  /// In bs, this message translates to:
  /// **'Postavke'**
  String get settings;

  /// No description provided for @home.
  ///
  /// In bs, this message translates to:
  /// **'Početna'**
  String get home;

  /// No description provided for @noData.
  ///
  /// In bs, this message translates to:
  /// **'Nema podataka'**
  String get noData;

  /// No description provided for @currencyShort.
  ///
  /// In bs, this message translates to:
  /// **'KM'**
  String get currencyShort;

  /// No description provided for @register.
  ///
  /// In bs, this message translates to:
  /// **'Registracija'**
  String get register;

  /// No description provided for @createAccount.
  ///
  /// In bs, this message translates to:
  /// **'Kreiraj račun'**
  String get createAccount;

  /// No description provided for @haveAccount.
  ///
  /// In bs, this message translates to:
  /// **'Već imate račun? Prijavite se'**
  String get haveAccount;

  /// No description provided for @noAccount.
  ///
  /// In bs, this message translates to:
  /// **'Nemate račun? Registrujte se'**
  String get noAccount;

  /// No description provided for @forgotPassword.
  ///
  /// In bs, this message translates to:
  /// **'Zaboravljena lozinka?'**
  String get forgotPassword;

  /// No description provided for @resetEmailSent.
  ///
  /// In bs, this message translates to:
  /// **'Ako račun postoji, poslali smo upute na email.'**
  String get resetEmailSent;

  /// No description provided for @firstName.
  ///
  /// In bs, this message translates to:
  /// **'Ime'**
  String get firstName;

  /// No description provided for @lastName.
  ///
  /// In bs, this message translates to:
  /// **'Prezime'**
  String get lastName;

  /// No description provided for @phone.
  ///
  /// In bs, this message translates to:
  /// **'Telefon'**
  String get phone;

  /// No description provided for @profile.
  ///
  /// In bs, this message translates to:
  /// **'Profil'**
  String get profile;

  /// No description provided for @saveChanges.
  ///
  /// In bs, this message translates to:
  /// **'Spremi izmjene'**
  String get saveChanges;

  /// No description provided for @profileSaved.
  ///
  /// In bs, this message translates to:
  /// **'Profil je spremljen.'**
  String get profileSaved;

  /// No description provided for @requiredField.
  ///
  /// In bs, this message translates to:
  /// **'Obavezno polje'**
  String get requiredField;

  /// No description provided for @invalidEmail.
  ///
  /// In bs, this message translates to:
  /// **'Unesite email u formatu ime@domena.ba'**
  String get invalidEmail;

  /// No description provided for @invalidPhone.
  ///
  /// In bs, this message translates to:
  /// **'Unesite ispravan broj telefona, npr. 061 123 456 ili +387 61 123 456.'**
  String get invalidPhone;

  /// No description provided for @invalidName.
  ///
  /// In bs, this message translates to:
  /// **'Unesite 2 do 50 slova (dozvoljeni su razmak, tačka, apostrof i crtica).'**
  String get invalidName;

  /// No description provided for @invalidPrice.
  ///
  /// In bs, this message translates to:
  /// **'Unesite cijenu veću od 0, najviše dvije decimale (npr. 12,50).'**
  String get invalidPrice;

  /// No description provided for @invalidWholeNumber.
  ///
  /// In bs, this message translates to:
  /// **'Unesite cijeli broj od {min} do {max}.'**
  String invalidWholeNumber(int min, int max);

  /// No description provided for @passwordRules.
  ///
  /// In bs, this message translates to:
  /// **'Najmanje 8 znakova, veliko i malo slovo, broj i poseban znak.'**
  String get passwordRules;

  /// No description provided for @registrationDone.
  ///
  /// In bs, this message translates to:
  /// **'Račun je kreiran. Sada se možete prijaviti.'**
  String get registrationDone;

  /// No description provided for @welcomeBack.
  ///
  /// In bs, this message translates to:
  /// **'Dobrodošli nazad'**
  String get welcomeBack;

  /// No description provided for @signInSubtitle.
  ///
  /// In bs, this message translates to:
  /// **'Prijavite se da nastavite'**
  String get signInSubtitle;

  /// No description provided for @language.
  ///
  /// In bs, this message translates to:
  /// **'Jezik'**
  String get language;

  /// No description provided for @overview.
  ///
  /// In bs, this message translates to:
  /// **'Pregled'**
  String get overview;

  /// No description provided for @clients.
  ///
  /// In bs, this message translates to:
  /// **'Klijenti'**
  String get clients;

  /// No description provided for @notifications.
  ///
  /// In bs, this message translates to:
  /// **'Notifikacije'**
  String get notifications;

  /// No description provided for @reports.
  ///
  /// In bs, this message translates to:
  /// **'Izvještaji'**
  String get reports;

  /// No description provided for @earnings.
  ///
  /// In bs, this message translates to:
  /// **'Zarada'**
  String get earnings;

  /// No description provided for @myProfile.
  ///
  /// In bs, this message translates to:
  /// **'Moj profil'**
  String get myProfile;

  /// No description provided for @comingSoon.
  ///
  /// In bs, this message translates to:
  /// **'Uskoro dostupno'**
  String get comingSoon;

  /// No description provided for @signedInAs.
  ///
  /// In bs, this message translates to:
  /// **'Prijavljeni ste kao {name}'**
  String signedInAs(String name);

  /// No description provided for @genericError.
  ///
  /// In bs, this message translates to:
  /// **'Došlo je do greške.'**
  String get genericError;

  /// No description provided for @chooseEmployee.
  ///
  /// In bs, this message translates to:
  /// **'Odaberite uposlenika'**
  String get chooseEmployee;

  /// No description provided for @chooseEmployeeHint.
  ///
  /// In bs, this message translates to:
  /// **'Prikazani su uposlenici koji rade sve odabrane usluge.'**
  String get chooseEmployeeHint;

  /// No description provided for @pricelist.
  ///
  /// In bs, this message translates to:
  /// **'Usluge & Cjenovnik'**
  String get pricelist;

  /// No description provided for @minutesShort.
  ///
  /// In bs, this message translates to:
  /// **'{count} min'**
  String minutesShort(int count);

  /// No description provided for @total.
  ///
  /// In bs, this message translates to:
  /// **'Ukupno'**
  String get total;

  /// No description provided for @continueLabel.
  ///
  /// In bs, this message translates to:
  /// **'Nastavi'**
  String get continueLabel;

  /// No description provided for @selectServices.
  ///
  /// In bs, this message translates to:
  /// **'Odaberite jednu ili više usluga'**
  String get selectServices;

  /// No description provided for @add.
  ///
  /// In bs, this message translates to:
  /// **'Dodaj'**
  String get add;

  /// No description provided for @name.
  ///
  /// In bs, this message translates to:
  /// **'Naziv'**
  String get name;

  /// No description provided for @description.
  ///
  /// In bs, this message translates to:
  /// **'Opis'**
  String get description;

  /// No description provided for @price.
  ///
  /// In bs, this message translates to:
  /// **'Cijena'**
  String get price;

  /// No description provided for @duration.
  ///
  /// In bs, this message translates to:
  /// **'Trajanje (min)'**
  String get duration;

  /// No description provided for @active.
  ///
  /// In bs, this message translates to:
  /// **'Aktivno'**
  String get active;

  /// No description provided for @inactive.
  ///
  /// In bs, this message translates to:
  /// **'Neaktivno'**
  String get inactive;

  /// No description provided for @title.
  ///
  /// In bs, this message translates to:
  /// **'Titula'**
  String get title;

  /// No description provided for @bio.
  ///
  /// In bs, this message translates to:
  /// **'Opis / bio'**
  String get bio;

  /// No description provided for @workingHours.
  ///
  /// In bs, this message translates to:
  /// **'Radno vrijeme'**
  String get workingHours;

  /// No description provided for @dayOff.
  ///
  /// In bs, this message translates to:
  /// **'Neradni dan'**
  String get dayOff;

  /// No description provided for @breakLabel.
  ///
  /// In bs, this message translates to:
  /// **'Pauza'**
  String get breakLabel;

  /// No description provided for @from.
  ///
  /// In bs, this message translates to:
  /// **'Od'**
  String get from;

  /// No description provided for @to.
  ///
  /// In bs, this message translates to:
  /// **'Do'**
  String get to;

  /// No description provided for @deactivate.
  ///
  /// In bs, this message translates to:
  /// **'Deaktiviraj'**
  String get deactivate;

  /// No description provided for @searchHint.
  ///
  /// In bs, this message translates to:
  /// **'Pretraga…'**
  String get searchHint;

  /// No description provided for @durationStepError.
  ///
  /// In bs, this message translates to:
  /// **'Trajanje mora biti višekratnik od 5 minuta.'**
  String get durationStepError;

  /// No description provided for @invalidNumber.
  ///
  /// In bs, this message translates to:
  /// **'Unesite ispravan broj'**
  String get invalidNumber;

  /// No description provided for @book.
  ///
  /// In bs, this message translates to:
  /// **'Rezerviši'**
  String get book;

  /// No description provided for @noSlots.
  ///
  /// In bs, this message translates to:
  /// **'Nema dostupnih termina za odabrani datum'**
  String get noSlots;

  /// No description provided for @showAllTimes.
  ///
  /// In bs, this message translates to:
  /// **'Prikaži sve termine'**
  String get showAllTimes;

  /// No description provided for @showFewerTimes.
  ///
  /// In bs, this message translates to:
  /// **'Prikaži manje termina'**
  String get showFewerTimes;

  /// No description provided for @morning.
  ///
  /// In bs, this message translates to:
  /// **'Jutro'**
  String get morning;

  /// No description provided for @afternoon.
  ///
  /// In bs, this message translates to:
  /// **'Popodne'**
  String get afternoon;

  /// No description provided for @evening.
  ///
  /// In bs, this message translates to:
  /// **'Veče'**
  String get evening;

  /// No description provided for @note.
  ///
  /// In bs, this message translates to:
  /// **'Napomena'**
  String get note;

  /// No description provided for @noteHint.
  ///
  /// In bs, this message translates to:
  /// **'Ostavite poruku'**
  String get noteHint;

  /// No description provided for @bookingConfirmed.
  ///
  /// In bs, this message translates to:
  /// **'Termin je rezervisan.'**
  String get bookingConfirmed;

  /// No description provided for @upcoming.
  ///
  /// In bs, this message translates to:
  /// **'Nadolazeći'**
  String get upcoming;

  /// No description provided for @past.
  ///
  /// In bs, this message translates to:
  /// **'Prošli'**
  String get past;

  /// No description provided for @cancelBooking.
  ///
  /// In bs, this message translates to:
  /// **'Otkaži termin'**
  String get cancelBooking;

  /// No description provided for @cancelBookingQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Da li sigurno želite otkazati ovaj termin?'**
  String get cancelBookingQuestion;

  /// No description provided for @no.
  ///
  /// In bs, this message translates to:
  /// **'Ne'**
  String get no;

  /// No description provided for @yes.
  ///
  /// In bs, this message translates to:
  /// **'Da'**
  String get yes;

  /// No description provided for @statusPending.
  ///
  /// In bs, this message translates to:
  /// **'Čeka potvrdu'**
  String get statusPending;

  /// No description provided for @statusConfirmed.
  ///
  /// In bs, this message translates to:
  /// **'Potvrđen'**
  String get statusConfirmed;

  /// No description provided for @statusCompleted.
  ///
  /// In bs, this message translates to:
  /// **'Završen'**
  String get statusCompleted;

  /// No description provided for @statusCancelled.
  ///
  /// In bs, this message translates to:
  /// **'Otkazan'**
  String get statusCancelled;

  /// No description provided for @statusNoShow.
  ///
  /// In bs, this message translates to:
  /// **'Nije došao'**
  String get statusNoShow;

  /// No description provided for @complete.
  ///
  /// In bs, this message translates to:
  /// **'Završi'**
  String get complete;

  /// No description provided for @markNoShow.
  ///
  /// In bs, this message translates to:
  /// **'Nije došao'**
  String get markNoShow;

  /// No description provided for @reschedule.
  ///
  /// In bs, this message translates to:
  /// **'Uredi termin'**
  String get reschedule;

  /// No description provided for @newAppointment.
  ///
  /// In bs, this message translates to:
  /// **'Novi termin'**
  String get newAppointment;

  /// No description provided for @client.
  ///
  /// In bs, this message translates to:
  /// **'Klijent'**
  String get client;

  /// No description provided for @employee.
  ///
  /// In bs, this message translates to:
  /// **'Uposlenik'**
  String get employee;

  /// No description provided for @date.
  ///
  /// In bs, this message translates to:
  /// **'Datum'**
  String get date;

  /// No description provided for @time.
  ///
  /// In bs, this message translates to:
  /// **'Vrijeme'**
  String get time;

  /// No description provided for @service.
  ///
  /// In bs, this message translates to:
  /// **'Usluga'**
  String get service;

  /// No description provided for @status.
  ///
  /// In bs, this message translates to:
  /// **'Status'**
  String get status;

  /// No description provided for @allEmployees.
  ///
  /// In bs, this message translates to:
  /// **'Svi uposlenici'**
  String get allEmployees;

  /// No description provided for @today.
  ///
  /// In bs, this message translates to:
  /// **'Danas'**
  String get today;

  /// No description provided for @weekly.
  ///
  /// In bs, this message translates to:
  /// **'Sedmično'**
  String get weekly;

  /// No description provided for @monthly.
  ///
  /// In bs, this message translates to:
  /// **'Mjesečno'**
  String get monthly;

  /// No description provided for @statUpcoming.
  ///
  /// In bs, this message translates to:
  /// **'Nadolazeći termini'**
  String get statUpcoming;

  /// No description provided for @statCompleted.
  ///
  /// In bs, this message translates to:
  /// **'Završeni termini'**
  String get statCompleted;

  /// No description provided for @statCancelled.
  ///
  /// In bs, this message translates to:
  /// **'Otkazani termini'**
  String get statCancelled;

  /// No description provided for @statClients.
  ///
  /// In bs, this message translates to:
  /// **'Ukupno mušterija'**
  String get statClients;

  /// No description provided for @appointmentsOverview.
  ///
  /// In bs, this message translates to:
  /// **'Pregled termina'**
  String get appointmentsOverview;

  /// No description provided for @noAppointments.
  ///
  /// In bs, this message translates to:
  /// **'Nema termina'**
  String get noAppointments;

  /// No description provided for @chooseClient.
  ///
  /// In bs, this message translates to:
  /// **'Odaberite klijenta'**
  String get chooseClient;

  /// No description provided for @searchClient.
  ///
  /// In bs, this message translates to:
  /// **'Pretraži klijente'**
  String get searchClient;

  /// No description provided for @previousDay.
  ///
  /// In bs, this message translates to:
  /// **'Prethodni dan'**
  String get previousDay;

  /// No description provided for @nextDay.
  ///
  /// In bs, this message translates to:
  /// **'Sljedeći dan'**
  String get nextDay;

  /// No description provided for @slotTaken.
  ///
  /// In bs, this message translates to:
  /// **'Termin je upravo zauzet. Odaberite drugi.'**
  String get slotTaken;

  /// No description provided for @cart.
  ///
  /// In bs, this message translates to:
  /// **'Korpa'**
  String get cart;

  /// No description provided for @addToCart.
  ///
  /// In bs, this message translates to:
  /// **'Dodaj u korpu'**
  String get addToCart;

  /// No description provided for @outOfStock.
  ///
  /// In bs, this message translates to:
  /// **'Nema na zalihi'**
  String get outOfStock;

  /// No description provided for @lowStock.
  ///
  /// In bs, this message translates to:
  /// **'Niska zaliha'**
  String get lowStock;

  /// No description provided for @stock.
  ///
  /// In bs, this message translates to:
  /// **'Zaliha'**
  String get stock;

  /// No description provided for @inStockCount.
  ///
  /// In bs, this message translates to:
  /// **'Na zalihi: {count}'**
  String inStockCount(int count);

  /// No description provided for @allCategories.
  ///
  /// In bs, this message translates to:
  /// **'Sve kategorije'**
  String get allCategories;

  /// No description provided for @sortBy.
  ///
  /// In bs, this message translates to:
  /// **'Sortiraj'**
  String get sortBy;

  /// No description provided for @sortName.
  ///
  /// In bs, this message translates to:
  /// **'Naziv'**
  String get sortName;

  /// No description provided for @sortPriceAsc.
  ///
  /// In bs, this message translates to:
  /// **'Cijena: manja prvo'**
  String get sortPriceAsc;

  /// No description provided for @sortPriceDesc.
  ///
  /// In bs, this message translates to:
  /// **'Cijena: veća prvo'**
  String get sortPriceDesc;

  /// No description provided for @sortNewest.
  ///
  /// In bs, this message translates to:
  /// **'Najnovije'**
  String get sortNewest;

  /// No description provided for @cartEmpty.
  ///
  /// In bs, this message translates to:
  /// **'Korpa je prazna'**
  String get cartEmpty;

  /// No description provided for @limitReached.
  ///
  /// In bs, this message translates to:
  /// **'Nema više komada na zalihi.'**
  String get limitReached;

  /// No description provided for @checkout.
  ///
  /// In bs, this message translates to:
  /// **'Naplata'**
  String get checkout;

  /// No description provided for @pickup.
  ///
  /// In bs, this message translates to:
  /// **'Preuzimanje u salonu'**
  String get pickup;

  /// No description provided for @delivery.
  ///
  /// In bs, this message translates to:
  /// **'Dostava'**
  String get delivery;

  /// No description provided for @deliveryAddress.
  ///
  /// In bs, this message translates to:
  /// **'Adresa za dostavu'**
  String get deliveryAddress;

  /// No description provided for @payByCard.
  ///
  /// In bs, this message translates to:
  /// **'Karticom'**
  String get payByCard;

  /// No description provided for @payOnReceipt.
  ///
  /// In bs, this message translates to:
  /// **'Pri preuzimanju'**
  String get payOnReceipt;

  /// No description provided for @placeOrder.
  ///
  /// In bs, this message translates to:
  /// **'Naruči'**
  String get placeOrder;

  /// No description provided for @orderPlaced.
  ///
  /// In bs, this message translates to:
  /// **'Narudžba je zaprimljena.'**
  String get orderPlaced;

  /// No description provided for @orderAwaitsPayment.
  ///
  /// In bs, this message translates to:
  /// **'Narudžba čeka plaćanje.'**
  String get orderAwaitsPayment;

  /// No description provided for @cardUnavailable.
  ///
  /// In bs, this message translates to:
  /// **'Plaćanje karticom trenutno nije dostupno u ovoj aplikaciji. Odaberite plaćanje pri preuzimanju.'**
  String get cardUnavailable;

  /// No description provided for @payNow.
  ///
  /// In bs, this message translates to:
  /// **'Plati'**
  String get payNow;

  /// No description provided for @myOrders.
  ///
  /// In bs, this message translates to:
  /// **'Moje narudžbe'**
  String get myOrders;

  /// No description provided for @noOrders.
  ///
  /// In bs, this message translates to:
  /// **'Nema narudžbi'**
  String get noOrders;

  /// No description provided for @orders.
  ///
  /// In bs, this message translates to:
  /// **'Narudžbe'**
  String get orders;

  /// No description provided for @orderNumber.
  ///
  /// In bs, this message translates to:
  /// **'Narudžba #{id}'**
  String orderNumber(int id);

  /// No description provided for @orderStatusPendingPayment.
  ///
  /// In bs, this message translates to:
  /// **'Čeka plaćanje'**
  String get orderStatusPendingPayment;

  /// No description provided for @orderStatusPlaced.
  ///
  /// In bs, this message translates to:
  /// **'Zaprimljena'**
  String get orderStatusPlaced;

  /// No description provided for @orderStatusFulfilled.
  ///
  /// In bs, this message translates to:
  /// **'Preuzeta'**
  String get orderStatusFulfilled;

  /// No description provided for @orderStatusCancelled.
  ///
  /// In bs, this message translates to:
  /// **'Otkazana'**
  String get orderStatusCancelled;

  /// No description provided for @orderStatusExpired.
  ///
  /// In bs, this message translates to:
  /// **'Istekla'**
  String get orderStatusExpired;

  /// No description provided for @cancelOrder.
  ///
  /// In bs, this message translates to:
  /// **'Otkaži narudžbu'**
  String get cancelOrder;

  /// No description provided for @cancelOrderQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Da li sigurno želite otkazati narudžbu?'**
  String get cancelOrderQuestion;

  /// No description provided for @markFulfilled.
  ///
  /// In bs, this message translates to:
  /// **'Preuzeta / isporučena'**
  String get markFulfilled;

  /// No description provided for @restock.
  ///
  /// In bs, this message translates to:
  /// **'Dopuni zalihu'**
  String get restock;

  /// No description provided for @stockChange.
  ///
  /// In bs, this message translates to:
  /// **'Promjena zalihe (npr. 5 ili -2)'**
  String get stockChange;

  /// No description provided for @brand.
  ///
  /// In bs, this message translates to:
  /// **'Brend'**
  String get brand;

  /// No description provided for @brands.
  ///
  /// In bs, this message translates to:
  /// **'Brendovi'**
  String get brands;

  /// No description provided for @category.
  ///
  /// In bs, this message translates to:
  /// **'Kategorija'**
  String get category;

  /// No description provided for @categories.
  ///
  /// In bs, this message translates to:
  /// **'Kategorije'**
  String get categories;

  /// No description provided for @lowStockOnly.
  ///
  /// In bs, this message translates to:
  /// **'Samo niska zaliha'**
  String get lowStockOnly;

  /// No description provided for @newProduct.
  ///
  /// In bs, this message translates to:
  /// **'Novi proizvod'**
  String get newProduct;

  /// No description provided for @lowStockThreshold.
  ///
  /// In bs, this message translates to:
  /// **'Prag niske zalihe'**
  String get lowStockThreshold;

  /// No description provided for @initialStock.
  ///
  /// In bs, this message translates to:
  /// **'Početna zaliha'**
  String get initialStock;

  /// No description provided for @manage.
  ///
  /// In bs, this message translates to:
  /// **'Upravljaj'**
  String get manage;

  /// No description provided for @allStatuses.
  ///
  /// In bs, this message translates to:
  /// **'Svi statusi'**
  String get allStatuses;

  /// No description provided for @productsCount.
  ///
  /// In bs, this message translates to:
  /// **'{count, plural, one{{count} proizvod} few{{count} proizvoda} other{{count} proizvoda}}'**
  String productsCount(int count);

  /// No description provided for @payment.
  ///
  /// In bs, this message translates to:
  /// **'Plaćanje'**
  String get payment;

  /// No description provided for @paymentStatusPending.
  ///
  /// In bs, this message translates to:
  /// **'Čeka uplatu'**
  String get paymentStatusPending;

  /// No description provided for @paymentStatusSucceeded.
  ///
  /// In bs, this message translates to:
  /// **'Plaćeno'**
  String get paymentStatusSucceeded;

  /// No description provided for @paymentStatusFailed.
  ///
  /// In bs, this message translates to:
  /// **'Neuspjelo'**
  String get paymentStatusFailed;

  /// No description provided for @paymentStatusRefunded.
  ///
  /// In bs, this message translates to:
  /// **'Vraćeno'**
  String get paymentStatusRefunded;

  /// No description provided for @paymentStatusRefundPending.
  ///
  /// In bs, this message translates to:
  /// **'Povrat u obradi'**
  String get paymentStatusRefundPending;

  /// No description provided for @paymentNotConfirmed.
  ///
  /// In bs, this message translates to:
  /// **'Plaćanje nije potvrđeno. Narudžba čeka uplatu u „Moje narudžbe“; ako ostane neplaćena, sama se zatvara.'**
  String get paymentNotConfirmed;

  /// No description provided for @paymentSucceeded.
  ///
  /// In bs, this message translates to:
  /// **'Plaćanje je uspjelo. Hvala!'**
  String get paymentSucceeded;

  /// No description provided for @paymentCancelledPayLater.
  ///
  /// In bs, this message translates to:
  /// **'Plaćanje je otkazano. Narudžbu možete platiti kasnije.'**
  String get paymentCancelledPayLater;

  /// No description provided for @none.
  ///
  /// In bs, this message translates to:
  /// **'Bez'**
  String get none;

  /// No description provided for @noNotifications.
  ///
  /// In bs, this message translates to:
  /// **'Nema obavijesti'**
  String get noNotifications;

  /// No description provided for @markAllRead.
  ///
  /// In bs, this message translates to:
  /// **'Označi sve kao pročitano'**
  String get markAllRead;

  /// No description provided for @audienceAll.
  ///
  /// In bs, this message translates to:
  /// **'Svi'**
  String get audienceAll;

  /// No description provided for @audienceClients.
  ///
  /// In bs, this message translates to:
  /// **'Klijenti'**
  String get audienceClients;

  /// No description provided for @audienceEmployees.
  ///
  /// In bs, this message translates to:
  /// **'Uposlenici'**
  String get audienceEmployees;

  /// No description provided for @audienceUser.
  ///
  /// In bs, this message translates to:
  /// **'Jedan korisnik'**
  String get audienceUser;

  /// No description provided for @audience.
  ///
  /// In bs, this message translates to:
  /// **'Publika'**
  String get audience;

  /// No description provided for @messageTitle.
  ///
  /// In bs, this message translates to:
  /// **'Naslov'**
  String get messageTitle;

  /// No description provided for @messageBody.
  ///
  /// In bs, this message translates to:
  /// **'Poruka'**
  String get messageBody;

  /// No description provided for @alsoSendEmail.
  ///
  /// In bs, this message translates to:
  /// **'Pošalji i na email'**
  String get alsoSendEmail;

  /// No description provided for @send.
  ///
  /// In bs, this message translates to:
  /// **'Pošalji'**
  String get send;

  /// No description provided for @announcementSent.
  ///
  /// In bs, this message translates to:
  /// **'Poruka je poslana.'**
  String get announcementSent;

  /// No description provided for @sentMessages.
  ///
  /// In bs, this message translates to:
  /// **'Poslane poruke'**
  String get sentMessages;

  /// No description provided for @recipientsCount.
  ///
  /// In bs, this message translates to:
  /// **'{count} primalaca'**
  String recipientsCount(int count);

  /// No description provided for @newMessage.
  ///
  /// In bs, this message translates to:
  /// **'Nova poruka'**
  String get newMessage;

  /// No description provided for @haveCode.
  ///
  /// In bs, this message translates to:
  /// **'Imam kod'**
  String get haveCode;

  /// No description provided for @resetCode.
  ///
  /// In bs, this message translates to:
  /// **'Kod iz maila'**
  String get resetCode;

  /// No description provided for @newPassword.
  ///
  /// In bs, this message translates to:
  /// **'Nova lozinka'**
  String get newPassword;

  /// No description provided for @passwordChanged.
  ///
  /// In bs, this message translates to:
  /// **'Lozinka je promijenjena. Prijavite se.'**
  String get passwordChanged;

  /// No description provided for @resetFailed.
  ///
  /// In bs, this message translates to:
  /// **'Kod nije ispravan ili je istekao.'**
  String get resetFailed;

  /// No description provided for @close.
  ///
  /// In bs, this message translates to:
  /// **'Zatvori'**
  String get close;

  /// No description provided for @daySunday.
  ///
  /// In bs, this message translates to:
  /// **'Nedjelja'**
  String get daySunday;

  /// No description provided for @dayMonday.
  ///
  /// In bs, this message translates to:
  /// **'Ponedjeljak'**
  String get dayMonday;

  /// No description provided for @dayTuesday.
  ///
  /// In bs, this message translates to:
  /// **'Utorak'**
  String get dayTuesday;

  /// No description provided for @dayWednesday.
  ///
  /// In bs, this message translates to:
  /// **'Srijeda'**
  String get dayWednesday;

  /// No description provided for @dayThursday.
  ///
  /// In bs, this message translates to:
  /// **'Četvrtak'**
  String get dayThursday;

  /// No description provided for @dayFriday.
  ///
  /// In bs, this message translates to:
  /// **'Petak'**
  String get dayFriday;

  /// No description provided for @daySaturday.
  ///
  /// In bs, this message translates to:
  /// **'Subota'**
  String get daySaturday;

  /// No description provided for @reviews.
  ///
  /// In bs, this message translates to:
  /// **'Recenzije'**
  String get reviews;

  /// No description provided for @noReviews.
  ///
  /// In bs, this message translates to:
  /// **'Još nema recenzija'**
  String get noReviews;

  /// No description provided for @writeReview.
  ///
  /// In bs, this message translates to:
  /// **'Ocijeni'**
  String get writeReview;

  /// No description provided for @yourRating.
  ///
  /// In bs, this message translates to:
  /// **'Vaša ocjena'**
  String get yourRating;

  /// No description provided for @yourComment.
  ///
  /// In bs, this message translates to:
  /// **'Komentar (nije obavezan)'**
  String get yourComment;

  /// No description provided for @reviewSaved.
  ///
  /// In bs, this message translates to:
  /// **'Hvala na recenziji!'**
  String get reviewSaved;

  /// No description provided for @pickRating.
  ///
  /// In bs, this message translates to:
  /// **'Odaberite ocjenu.'**
  String get pickRating;

  /// No description provided for @recommendedForYou.
  ///
  /// In bs, this message translates to:
  /// **'Možda vam se sviđa'**
  String get recommendedForYou;

  /// No description provided for @similarProducts.
  ///
  /// In bs, this message translates to:
  /// **'Slično'**
  String get similarProducts;

  /// No description provided for @hideReview.
  ///
  /// In bs, this message translates to:
  /// **'Sakrij'**
  String get hideReview;

  /// No description provided for @showReview.
  ///
  /// In bs, this message translates to:
  /// **'Prikaži'**
  String get showReview;

  /// No description provided for @reply.
  ///
  /// In bs, this message translates to:
  /// **'Odgovori'**
  String get reply;

  /// No description provided for @salonReply.
  ///
  /// In bs, this message translates to:
  /// **'Odgovor salona'**
  String get salonReply;

  /// No description provided for @hiddenReviews.
  ///
  /// In bs, this message translates to:
  /// **'Samo skrivene'**
  String get hiddenReviews;

  /// No description provided for @replySaved.
  ///
  /// In bs, this message translates to:
  /// **'Odgovor je spremljen.'**
  String get replySaved;

  /// No description provided for @yourReply.
  ///
  /// In bs, this message translates to:
  /// **'Vaš odgovor'**
  String get yourReply;

  /// No description provided for @periodToday.
  ///
  /// In bs, this message translates to:
  /// **'Danas'**
  String get periodToday;

  /// No description provided for @period7.
  ///
  /// In bs, this message translates to:
  /// **'7 dana'**
  String get period7;

  /// No description provided for @period30.
  ///
  /// In bs, this message translates to:
  /// **'30 dana'**
  String get period30;

  /// No description provided for @periodThisMonth.
  ///
  /// In bs, this message translates to:
  /// **'Ovaj mjesec'**
  String get periodThisMonth;

  /// No description provided for @periodLastMonth.
  ///
  /// In bs, this message translates to:
  /// **'Prošli mjesec'**
  String get periodLastMonth;

  /// No description provided for @periodThisYear.
  ///
  /// In bs, this message translates to:
  /// **'Ova godina'**
  String get periodThisYear;

  /// No description provided for @periodCustom.
  ///
  /// In bs, this message translates to:
  /// **'Odaberi period'**
  String get periodCustom;

  /// No description provided for @kpiRevenue.
  ///
  /// In bs, this message translates to:
  /// **'Ukupni prihod'**
  String get kpiRevenue;

  /// No description provided for @kpiServices.
  ///
  /// In bs, this message translates to:
  /// **'Prihod od usluga'**
  String get kpiServices;

  /// No description provided for @kpiProducts.
  ///
  /// In bs, this message translates to:
  /// **'Prihod od proizvoda'**
  String get kpiProducts;

  /// No description provided for @kpiAppointments.
  ///
  /// In bs, this message translates to:
  /// **'Završeni termini'**
  String get kpiAppointments;

  /// No description provided for @kpiOrders.
  ///
  /// In bs, this message translates to:
  /// **'Narudžbe'**
  String get kpiOrders;

  /// No description provided for @kpiNewClients.
  ///
  /// In bs, this message translates to:
  /// **'Novi klijenti'**
  String get kpiNewClients;

  /// No description provided for @kpiAverageTicket.
  ///
  /// In bs, this message translates to:
  /// **'Prosječan račun'**
  String get kpiAverageTicket;

  /// No description provided for @kpiNoShow.
  ///
  /// In bs, this message translates to:
  /// **'Nije došlo'**
  String get kpiNoShow;

  /// No description provided for @kpiCancelled.
  ///
  /// In bs, this message translates to:
  /// **'Otkazano'**
  String get kpiCancelled;

  /// No description provided for @vsPrevious.
  ///
  /// In bs, this message translates to:
  /// **'u odnosu na prethodni period'**
  String get vsPrevious;

  /// No description provided for @revenueOverTime.
  ///
  /// In bs, this message translates to:
  /// **'Prihod kroz vrijeme'**
  String get revenueOverTime;

  /// No description provided for @topServices.
  ///
  /// In bs, this message translates to:
  /// **'Najtraženije usluge'**
  String get topServices;

  /// No description provided for @topProducts.
  ///
  /// In bs, this message translates to:
  /// **'Najprodavaniji proizvodi'**
  String get topProducts;

  /// No description provided for @employeePerformance.
  ///
  /// In bs, this message translates to:
  /// **'Učinak uposlenika'**
  String get employeePerformance;

  /// No description provided for @completedShort.
  ///
  /// In bs, this message translates to:
  /// **'Završeno'**
  String get completedShort;

  /// No description provided for @noShowShort.
  ///
  /// In bs, this message translates to:
  /// **'Nije došao'**
  String get noShowShort;

  /// No description provided for @cancelledShort.
  ///
  /// In bs, this message translates to:
  /// **'Otkazano'**
  String get cancelledShort;

  /// No description provided for @revenueShort.
  ///
  /// In bs, this message translates to:
  /// **'Prihod'**
  String get revenueShort;

  /// No description provided for @ratingShort.
  ///
  /// In bs, this message translates to:
  /// **'Ocjena'**
  String get ratingShort;

  /// No description provided for @export.
  ///
  /// In bs, this message translates to:
  /// **'Izvezi'**
  String get export;

  /// No description provided for @exportCsv.
  ///
  /// In bs, this message translates to:
  /// **'CSV'**
  String get exportCsv;

  /// No description provided for @exportExcel.
  ///
  /// In bs, this message translates to:
  /// **'Excel'**
  String get exportExcel;

  /// No description provided for @exportPdf.
  ///
  /// In bs, this message translates to:
  /// **'PDF'**
  String get exportPdf;

  /// No description provided for @savedTo.
  ///
  /// In bs, this message translates to:
  /// **'Spremljeno: {path}'**
  String savedTo(String path);

  /// No description provided for @showInFolder.
  ///
  /// In bs, this message translates to:
  /// **'Prikaži'**
  String get showInFolder;

  /// No description provided for @reportRevenue.
  ///
  /// In bs, this message translates to:
  /// **'Prihod'**
  String get reportRevenue;

  /// No description provided for @reportEmployees.
  ///
  /// In bs, this message translates to:
  /// **'Uposlenici'**
  String get reportEmployees;

  /// No description provided for @reportServices.
  ///
  /// In bs, this message translates to:
  /// **'Usluge'**
  String get reportServices;

  /// No description provided for @reportProducts.
  ///
  /// In bs, this message translates to:
  /// **'Proizvodi'**
  String get reportProducts;

  /// No description provided for @reportReservations.
  ///
  /// In bs, this message translates to:
  /// **'Termini'**
  String get reportReservations;

  /// No description provided for @reportOrders.
  ///
  /// In bs, this message translates to:
  /// **'Narudžbe'**
  String get reportOrders;

  /// No description provided for @reportCustom.
  ///
  /// In bs, this message translates to:
  /// **'Prilagođeni izvještaj'**
  String get reportCustom;

  /// No description provided for @chooseSource.
  ///
  /// In bs, this message translates to:
  /// **'Izvor podataka'**
  String get chooseSource;

  /// No description provided for @chooseFields.
  ///
  /// In bs, this message translates to:
  /// **'Polja'**
  String get chooseFields;

  /// No description provided for @runReport.
  ///
  /// In bs, this message translates to:
  /// **'Prikaži izvještaj'**
  String get runReport;

  /// No description provided for @allStatusesOption.
  ///
  /// In bs, this message translates to:
  /// **'Svi statusi'**
  String get allStatusesOption;

  /// No description provided for @noDataForPeriod.
  ///
  /// In bs, this message translates to:
  /// **'Nema podataka za odabrani period.'**
  String get noDataForPeriod;

  /// No description provided for @pictures.
  ///
  /// In bs, this message translates to:
  /// **'Slike'**
  String get pictures;

  /// No description provided for @addPicture.
  ///
  /// In bs, this message translates to:
  /// **'Dodaj sliku'**
  String get addPicture;

  /// No description provided for @changePicture.
  ///
  /// In bs, this message translates to:
  /// **'Promijeni sliku'**
  String get changePicture;

  /// No description provided for @removePicture.
  ///
  /// In bs, this message translates to:
  /// **'Ukloni sliku'**
  String get removePicture;

  /// No description provided for @savePictureFirst.
  ///
  /// In bs, this message translates to:
  /// **'Slike se mogu dodati nakon što se stavka spremi.'**
  String get savePictureFirst;

  /// No description provided for @clientSearchHint.
  ///
  /// In bs, this message translates to:
  /// **'Pretraži po imenu, emailu ili telefonu'**
  String get clientSearchHint;

  /// No description provided for @clientRegistered.
  ///
  /// In bs, this message translates to:
  /// **'Registrovan'**
  String get clientRegistered;

  /// No description provided for @clientBlocked.
  ///
  /// In bs, this message translates to:
  /// **'Blokiran'**
  String get clientBlocked;

  /// No description provided for @clientBlock.
  ///
  /// In bs, this message translates to:
  /// **'Blokiraj'**
  String get clientBlock;

  /// No description provided for @clientUnblock.
  ///
  /// In bs, this message translates to:
  /// **'Odblokiraj'**
  String get clientUnblock;

  /// No description provided for @clientBlockQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Blokirati klijenta {name}? Odmah će biti odjavljen i neće se moći prijaviti dok ga ne odblokirate.'**
  String clientBlockQuestion(String name);

  /// No description provided for @clientUnblockQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Odblokirati klijenta {name}?'**
  String clientUnblockQuestion(String name);

  /// No description provided for @clientVisits.
  ///
  /// In bs, this message translates to:
  /// **'Posjete'**
  String get clientVisits;

  /// No description provided for @clientNoShows.
  ///
  /// In bs, this message translates to:
  /// **'Nije došao'**
  String get clientNoShows;

  /// No description provided for @clientCancelled.
  ///
  /// In bs, this message translates to:
  /// **'Otkazano'**
  String get clientCancelled;

  /// No description provided for @clientUpcoming.
  ///
  /// In bs, this message translates to:
  /// **'Predstojeći termini'**
  String get clientUpcoming;

  /// No description provided for @clientSpentServices.
  ///
  /// In bs, this message translates to:
  /// **'Potrošeno na usluge'**
  String get clientSpentServices;

  /// No description provided for @clientSpentProducts.
  ///
  /// In bs, this message translates to:
  /// **'Potrošeno na proizvode'**
  String get clientSpentProducts;

  /// No description provided for @clientLastVisit.
  ///
  /// In bs, this message translates to:
  /// **'Zadnja posjeta'**
  String get clientLastVisit;

  /// No description provided for @clientNever.
  ///
  /// In bs, this message translates to:
  /// **'Još nije dolazio'**
  String get clientNever;

  /// No description provided for @clientRecentReservations.
  ///
  /// In bs, this message translates to:
  /// **'Posljednji termini'**
  String get clientRecentReservations;

  /// No description provided for @clientRecentOrders.
  ///
  /// In bs, this message translates to:
  /// **'Posljednje narudžbe'**
  String get clientRecentOrders;

  /// No description provided for @previousPage.
  ///
  /// In bs, this message translates to:
  /// **'Prethodna stranica'**
  String get previousPage;

  /// No description provided for @nextPage.
  ///
  /// In bs, this message translates to:
  /// **'Sljedeća stranica'**
  String get nextPage;

  /// No description provided for @auditLog.
  ///
  /// In bs, this message translates to:
  /// **'Dnevnik aktivnosti'**
  String get auditLog;

  /// No description provided for @auditSearchHint.
  ///
  /// In bs, this message translates to:
  /// **'Pretraži po osobi, radnji ili zapisu'**
  String get auditSearchHint;

  /// No description provided for @auditWhen.
  ///
  /// In bs, this message translates to:
  /// **'Vrijeme'**
  String get auditWhen;

  /// No description provided for @auditWho.
  ///
  /// In bs, this message translates to:
  /// **'Ko'**
  String get auditWho;

  /// No description provided for @auditWhat.
  ///
  /// In bs, this message translates to:
  /// **'Radnja'**
  String get auditWhat;

  /// No description provided for @auditTarget.
  ///
  /// In bs, this message translates to:
  /// **'Zapis'**
  String get auditTarget;

  /// No description provided for @viewDay.
  ///
  /// In bs, this message translates to:
  /// **'Dan'**
  String get viewDay;

  /// No description provided for @viewWeek.
  ///
  /// In bs, this message translates to:
  /// **'Sedmica'**
  String get viewWeek;

  /// No description provided for @previousWeek.
  ///
  /// In bs, this message translates to:
  /// **'Prethodna sedmica'**
  String get previousWeek;

  /// No description provided for @changePassword.
  ///
  /// In bs, this message translates to:
  /// **'Promijeni lozinku'**
  String get changePassword;

  /// No description provided for @currentPassword.
  ///
  /// In bs, this message translates to:
  /// **'Trenutna lozinka'**
  String get currentPassword;

  /// No description provided for @repeatPassword.
  ///
  /// In bs, this message translates to:
  /// **'Ponovite novu lozinku'**
  String get repeatPassword;

  /// No description provided for @passwordsDoNotMatch.
  ///
  /// In bs, this message translates to:
  /// **'Lozinke se ne podudaraju.'**
  String get passwordsDoNotMatch;

  /// No description provided for @nextWeek.
  ///
  /// In bs, this message translates to:
  /// **'Sljedeća sedmica'**
  String get nextWeek;

  /// No description provided for @cancelReasonLabel.
  ///
  /// In bs, this message translates to:
  /// **'Razlog otkazivanja'**
  String get cancelReasonLabel;

  /// No description provided for @cancelReasonOptionalLabel.
  ///
  /// In bs, this message translates to:
  /// **'Razlog (nije obavezan)'**
  String get cancelReasonOptionalLabel;

  /// No description provided for @cancelReasonRequired.
  ///
  /// In bs, this message translates to:
  /// **'Unesite razlog (najmanje 3 znaka). Klijent ga dobija u obavijesti.'**
  String get cancelReasonRequired;

  /// No description provided for @pictureNotSaved.
  ///
  /// In bs, this message translates to:
  /// **'Slika nije spremljena'**
  String get pictureNotSaved;

  /// No description provided for @chooseAccount.
  ///
  /// In bs, this message translates to:
  /// **'Ko se prijavljuje?'**
  String get chooseAccount;

  /// No description provided for @addAccount.
  ///
  /// In bs, this message translates to:
  /// **'Dodaj račun'**
  String get addAccount;

  /// No description provided for @removeFromList.
  ///
  /// In bs, this message translates to:
  /// **'Ukloni s liste'**
  String get removeFromList;

  /// No description provided for @backToAccounts.
  ///
  /// In bs, this message translates to:
  /// **'Nazad na račune'**
  String get backToAccounts;

  /// No description provided for @mainPicture.
  ///
  /// In bs, this message translates to:
  /// **'Glavna'**
  String get mainPicture;

  /// No description provided for @pictureHint.
  ///
  /// In bs, this message translates to:
  /// **'JPEG, PNG ili WebP, do 5 MB. Ista slika se ne može dodati dvaput.'**
  String get pictureHint;

  /// No description provided for @manageTitles.
  ///
  /// In bs, this message translates to:
  /// **'Uredi listu titula'**
  String get manageTitles;

  /// No description provided for @newTitle.
  ///
  /// In bs, this message translates to:
  /// **'Nova titula'**
  String get newTitle;

  /// No description provided for @titleUsedBy.
  ///
  /// In bs, this message translates to:
  /// **'Koristi: {count}'**
  String titleUsedBy(int count);

  /// No description provided for @titleInUseHint.
  ///
  /// In bs, this message translates to:
  /// **'Titulu koriste uposlenici, promijenite im je prije brisanja.'**
  String get titleInUseHint;

  /// No description provided for @gender.
  ///
  /// In bs, this message translates to:
  /// **'Spol'**
  String get gender;

  /// No description provided for @genderMale.
  ///
  /// In bs, this message translates to:
  /// **'Muški'**
  String get genderMale;

  /// No description provided for @genderFemale.
  ///
  /// In bs, this message translates to:
  /// **'Ženski'**
  String get genderFemale;

  /// No description provided for @done.
  ///
  /// In bs, this message translates to:
  /// **'Gotovo'**
  String get done;

  /// No description provided for @manageAccounts.
  ///
  /// In bs, this message translates to:
  /// **'Uredi račune'**
  String get manageAccounts;

  /// No description provided for @removeAccountHint.
  ///
  /// In bs, this message translates to:
  /// **'Račun se samo uklanja s liste na ovom računaru.'**
  String get removeAccountHint;

  /// No description provided for @zoom.
  ///
  /// In bs, this message translates to:
  /// **'Zoom'**
  String get zoom;

  /// No description provided for @resetZoom.
  ///
  /// In bs, this message translates to:
  /// **'Resetuj'**
  String get resetZoom;

  /// No description provided for @zoomHint.
  ///
  /// In bs, this message translates to:
  /// **'Slika se sama centrira na lice. Zoom određuje koliko se približava.'**
  String get zoomHint;

  /// No description provided for @galleryTitle.
  ///
  /// In bs, this message translates to:
  /// **'Galerija radova'**
  String get galleryTitle;

  /// No description provided for @ourWork.
  ///
  /// In bs, this message translates to:
  /// **'Naši radovi'**
  String get ourWork;

  /// No description provided for @viewGallery.
  ///
  /// In bs, this message translates to:
  /// **'Pogledaj galeriju'**
  String get viewGallery;

  /// No description provided for @allStyles.
  ///
  /// In bs, this message translates to:
  /// **'Sve'**
  String get allStyles;

  /// No description provided for @galleryEmpty.
  ///
  /// In bs, this message translates to:
  /// **'Još nema dodanih radova.'**
  String get galleryEmpty;

  /// No description provided for @closedDay.
  ///
  /// In bs, this message translates to:
  /// **'Zatvoreno'**
  String get closedDay;

  /// No description provided for @openInMaps.
  ///
  /// In bs, this message translates to:
  /// **'Otvori u Google Maps'**
  String get openInMaps;

  /// No description provided for @callSalon.
  ///
  /// In bs, this message translates to:
  /// **'Pozovi'**
  String get callSalon;

  /// No description provided for @findUs.
  ///
  /// In bs, this message translates to:
  /// **'Gdje smo'**
  String get findUs;

  /// No description provided for @bookAppointment.
  ///
  /// In bs, this message translates to:
  /// **'Rezerviši termin'**
  String get bookAppointment;

  /// No description provided for @haircutStyle.
  ///
  /// In bs, this message translates to:
  /// **'Stil frizure'**
  String get haircutStyle;

  /// No description provided for @doneBy.
  ///
  /// In bs, this message translates to:
  /// **'Frizer'**
  String get doneBy;

  /// No description provided for @addWork.
  ///
  /// In bs, this message translates to:
  /// **'Dodaj rad'**
  String get addWork;

  /// No description provided for @editWork.
  ///
  /// In bs, this message translates to:
  /// **'Uredi rad'**
  String get editWork;

  /// No description provided for @captionOptional.
  ///
  /// In bs, this message translates to:
  /// **'Opis (nije obavezno)'**
  String get captionOptional;

  /// No description provided for @showOnHome.
  ///
  /// In bs, this message translates to:
  /// **'Prikaži na početnoj'**
  String get showOnHome;

  /// No description provided for @onHomeBadge.
  ///
  /// In bs, this message translates to:
  /// **'Na početnoj'**
  String get onHomeBadge;

  /// No description provided for @noBarber.
  ///
  /// In bs, this message translates to:
  /// **'Bez uposlenika'**
  String get noBarber;

  /// No description provided for @styles.
  ///
  /// In bs, this message translates to:
  /// **'Stilovi frizura'**
  String get styles;

  /// No description provided for @addStyle.
  ///
  /// In bs, this message translates to:
  /// **'Dodaj stil'**
  String get addStyle;

  /// No description provided for @styleName.
  ///
  /// In bs, this message translates to:
  /// **'Naziv stila'**
  String get styleName;

  /// No description provided for @chooseStyle.
  ///
  /// In bs, this message translates to:
  /// **'Odaberite stil frizure.'**
  String get chooseStyle;

  /// No description provided for @choosePicture.
  ///
  /// In bs, this message translates to:
  /// **'Odaberite sliku'**
  String get choosePicture;

  /// No description provided for @deleteWorkQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Obrisati ovaj rad iz galerije?'**
  String get deleteWorkQuestion;

  /// No description provided for @deleteStyleQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Obrisati ovaj stil?'**
  String get deleteStyleQuestion;

  /// No description provided for @workAdded.
  ///
  /// In bs, this message translates to:
  /// **'Rad je dodan.'**
  String get workAdded;

  /// No description provided for @salonProfile.
  ///
  /// In bs, this message translates to:
  /// **'Salon'**
  String get salonProfile;

  /// No description provided for @salonName.
  ///
  /// In bs, this message translates to:
  /// **'Naziv salona'**
  String get salonName;

  /// No description provided for @address.
  ///
  /// In bs, this message translates to:
  /// **'Adresa'**
  String get address;

  /// No description provided for @mapsLink.
  ///
  /// In bs, this message translates to:
  /// **'Google lokacija (link)'**
  String get mapsLink;

  /// No description provided for @logo.
  ///
  /// In bs, this message translates to:
  /// **'Logo'**
  String get logo;

  /// No description provided for @openFrom.
  ///
  /// In bs, this message translates to:
  /// **'Od'**
  String get openFrom;

  /// No description provided for @openTo.
  ///
  /// In bs, this message translates to:
  /// **'Do'**
  String get openTo;

  /// No description provided for @openDay.
  ///
  /// In bs, this message translates to:
  /// **'Radi'**
  String get openDay;

  /// No description provided for @invalidUrl.
  ///
  /// In bs, this message translates to:
  /// **'Unesite ispravnu web adresu (https://...).'**
  String get invalidUrl;

  /// No description provided for @hoursOrder.
  ///
  /// In bs, this message translates to:
  /// **'Vrijeme otvaranja mora biti prije zatvaranja.'**
  String get hoursOrder;

  /// No description provided for @salonSaved.
  ///
  /// In bs, this message translates to:
  /// **'Podaci o salonu su spremljeni.'**
  String get salonSaved;

  /// No description provided for @aboutSalon.
  ///
  /// In bs, this message translates to:
  /// **'O salonu'**
  String get aboutSalon;

  /// No description provided for @latitude.
  ///
  /// In bs, this message translates to:
  /// **'Geografska širina'**
  String get latitude;

  /// No description provided for @longitude.
  ///
  /// In bs, this message translates to:
  /// **'Geografska dužina'**
  String get longitude;

  /// No description provided for @coordinatesBoth.
  ///
  /// In bs, this message translates to:
  /// **'Unesite i širinu i dužinu, ili nijednu.'**
  String get coordinatesBoth;

  /// No description provided for @invalidCoordinate.
  ///
  /// In bs, this message translates to:
  /// **'Neispravna vrijednost.'**
  String get invalidCoordinate;

  /// No description provided for @chooseServicesHint.
  ///
  /// In bs, this message translates to:
  /// **'Odaberite jednu ili više usluga, a zatim uposlenika i termin.'**
  String get chooseServicesHint;

  /// No description provided for @noBarberForServices.
  ///
  /// In bs, this message translates to:
  /// **'Nijedan uposlenik ne radi sve odabrane usluge. Vratite se i promijenite odabir.'**
  String get noBarberForServices;

  /// No description provided for @quantity.
  ///
  /// In bs, this message translates to:
  /// **'Količina'**
  String get quantity;

  /// No description provided for @addedToCartCount.
  ///
  /// In bs, this message translates to:
  /// **'Dodano u korpu: {count}'**
  String addedToCartCount(int count);

  /// No description provided for @allProducts.
  ///
  /// In bs, this message translates to:
  /// **'Svi proizvodi'**
  String get allProducts;

  /// No description provided for @salePrice.
  ///
  /// In bs, this message translates to:
  /// **'Akcijska cijena'**
  String get salePrice;

  /// No description provided for @salePriceHint.
  ///
  /// In bs, this message translates to:
  /// **'Ostavite prazno ako proizvod nije na akciji'**
  String get salePriceHint;

  /// No description provided for @salePriceTooHigh.
  ///
  /// In bs, this message translates to:
  /// **'Mora biti manja od redovne cijene'**
  String get salePriceTooHigh;

  /// No description provided for @shopSectionsTitle.
  ///
  /// In bs, this message translates to:
  /// **'Početna shopa'**
  String get shopSectionsTitle;

  /// No description provided for @shopSectionsHint.
  ///
  /// In bs, this message translates to:
  /// **'Redovi koje kupci vide na vrhu shopa. Povucite za promjenu redoslijeda.'**
  String get shopSectionsHint;

  /// No description provided for @addSection.
  ///
  /// In bs, this message translates to:
  /// **'Dodaj sekciju'**
  String get addSection;

  /// No description provided for @editSection.
  ///
  /// In bs, this message translates to:
  /// **'Uredi sekciju'**
  String get editSection;

  /// No description provided for @sectionTitle.
  ///
  /// In bs, this message translates to:
  /// **'Naslov'**
  String get sectionTitle;

  /// No description provided for @sectionKind.
  ///
  /// In bs, this message translates to:
  /// **'Šta prikazuje'**
  String get sectionKind;

  /// No description provided for @kindNewArrivals.
  ///
  /// In bs, this message translates to:
  /// **'Novo u ponudi (zadnje dodani proizvodi)'**
  String get kindNewArrivals;

  /// No description provided for @kindOnSale.
  ///
  /// In bs, this message translates to:
  /// **'Akcije (proizvodi s akcijskom cijenom)'**
  String get kindOnSale;

  /// No description provided for @kindCategory.
  ///
  /// In bs, this message translates to:
  /// **'Proizvodi jedne kategorije i/ili brenda'**
  String get kindCategory;

  /// No description provided for @kindManual.
  ///
  /// In bs, this message translates to:
  /// **'Ručni izbor proizvoda'**
  String get kindManual;

  /// No description provided for @sectionMaxItems.
  ///
  /// In bs, this message translates to:
  /// **'Najviše proizvoda'**
  String get sectionMaxItems;

  /// No description provided for @sectionVisible.
  ///
  /// In bs, this message translates to:
  /// **'Prikazano kupcima'**
  String get sectionVisible;

  /// No description provided for @sectionPickProducts.
  ///
  /// In bs, this message translates to:
  /// **'Odaberite proizvode'**
  String get sectionPickProducts;

  /// No description provided for @sectionSelected.
  ///
  /// In bs, this message translates to:
  /// **'Odabrano'**
  String get sectionSelected;

  /// No description provided for @noSections.
  ///
  /// In bs, this message translates to:
  /// **'Još nema sekcija. Dodajte prvu.'**
  String get noSections;

  /// No description provided for @deleteSectionQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Obrisati sekciju? Proizvodi ostaju u ponudi.'**
  String get deleteSectionQuestion;

  /// No description provided for @searchProducts.
  ///
  /// In bs, this message translates to:
  /// **'Pretraži proizvode'**
  String get searchProducts;

  /// No description provided for @mapsLinkHint.
  ///
  /// In bs, this message translates to:
  /// **'Zalijepite link iz Google Maps (Dijeli). Pin za mapu u aplikaciji se očitava iz linka; ako ne uspije, unesite širinu i dužinu ispod.'**
  String get mapsLinkHint;

  /// No description provided for @paymentNotCompleted.
  ///
  /// In bs, this message translates to:
  /// **'Plaćanje nije završeno. Narudžba nije kreirana, a proizvodi su ostali u korpi.'**
  String get paymentNotCompleted;

  /// No description provided for @groupWork.
  ///
  /// In bs, this message translates to:
  /// **'Rad'**
  String get groupWork;

  /// No description provided for @groupShop.
  ///
  /// In bs, this message translates to:
  /// **'Shop'**
  String get groupShop;

  /// No description provided for @groupSalon.
  ///
  /// In bs, this message translates to:
  /// **'Salon'**
  String get groupSalon;

  /// No description provided for @groupClients.
  ///
  /// In bs, this message translates to:
  /// **'Klijenti'**
  String get groupClients;

  /// No description provided for @groupAnalytics.
  ///
  /// In bs, this message translates to:
  /// **'Analitika'**
  String get groupAnalytics;

  /// No description provided for @groupAccount.
  ///
  /// In bs, this message translates to:
  /// **'Račun'**
  String get groupAccount;

  /// No description provided for @any.
  ///
  /// In bs, this message translates to:
  /// **'Bilo koji'**
  String get any;

  /// No description provided for @pickCategoryOrBrand.
  ///
  /// In bs, this message translates to:
  /// **'Odaberite kategoriju ili brend'**
  String get pickCategoryOrBrand;

  /// No description provided for @instagramLink.
  ///
  /// In bs, this message translates to:
  /// **'Instagram (link)'**
  String get instagramLink;

  /// No description provided for @facebookLink.
  ///
  /// In bs, this message translates to:
  /// **'Facebook (link)'**
  String get facebookLink;

  /// No description provided for @connectWithUs.
  ///
  /// In bs, this message translates to:
  /// **'Povežite se s nama'**
  String get connectWithUs;

  /// No description provided for @findUsHere.
  ///
  /// In bs, this message translates to:
  /// **'Pronađite nas ovdje'**
  String get findUsHere;

  /// No description provided for @callUs.
  ///
  /// In bs, this message translates to:
  /// **'Pozovi'**
  String get callUs;

  /// No description provided for @instagram.
  ///
  /// In bs, this message translates to:
  /// **'Instagram'**
  String get instagram;

  /// No description provided for @facebook.
  ///
  /// In bs, this message translates to:
  /// **'Facebook'**
  String get facebook;

  /// No description provided for @eyebrowGallery.
  ///
  /// In bs, this message translates to:
  /// **'Galerija'**
  String get eyebrowGallery;

  /// No description provided for @eyebrowContact.
  ///
  /// In bs, this message translates to:
  /// **'Kontakt'**
  String get eyebrowContact;

  /// No description provided for @eyebrowLocation.
  ///
  /// In bs, this message translates to:
  /// **'Lokacija'**
  String get eyebrowLocation;

  /// No description provided for @eyebrowSchedule.
  ///
  /// In bs, this message translates to:
  /// **'Raspored'**
  String get eyebrowSchedule;

  /// No description provided for @eyebrowAbout.
  ///
  /// In bs, this message translates to:
  /// **'O nama'**
  String get eyebrowAbout;

  /// No description provided for @getToKnowUs.
  ///
  /// In bs, this message translates to:
  /// **'Upoznajte nas'**
  String get getToKnowUs;

  /// No description provided for @welcomeTo.
  ///
  /// In bs, this message translates to:
  /// **'Dobrodošli u {name}'**
  String welcomeTo(String name);

  /// No description provided for @groupPersonalData.
  ///
  /// In bs, this message translates to:
  /// **'Lični podaci'**
  String get groupPersonalData;

  /// No description provided for @groupAboutUs.
  ///
  /// In bs, this message translates to:
  /// **'O nama'**
  String get groupAboutUs;

  /// No description provided for @editProfile.
  ///
  /// In bs, this message translates to:
  /// **'Uredi profil'**
  String get editProfile;

  /// No description provided for @manageAccount.
  ///
  /// In bs, this message translates to:
  /// **'Upravljanje računom'**
  String get manageAccount;

  /// No description provided for @privacyPolicy.
  ///
  /// In bs, this message translates to:
  /// **'Pravila privatnosti'**
  String get privacyPolicy;

  /// No description provided for @termsOfUse.
  ///
  /// In bs, this message translates to:
  /// **'Uslovi korištenja'**
  String get termsOfUse;

  /// No description provided for @aboutApp.
  ///
  /// In bs, this message translates to:
  /// **'O aplikaciji'**
  String get aboutApp;

  /// No description provided for @chooseDate.
  ///
  /// In bs, this message translates to:
  /// **'Odaberite datum'**
  String get chooseDate;

  /// No description provided for @chooseTime.
  ///
  /// In bs, this message translates to:
  /// **'Odaberite sate'**
  String get chooseTime;

  /// No description provided for @unavailableMarkedRed.
  ///
  /// In bs, this message translates to:
  /// **'Nedostupni termini su označeni crvenom bojom'**
  String get unavailableMarkedRed;

  /// No description provided for @eyebrowAssortment.
  ///
  /// In bs, this message translates to:
  /// **'Asortiman'**
  String get eyebrowAssortment;

  /// No description provided for @eyebrowBrands.
  ///
  /// In bs, this message translates to:
  /// **'Marke'**
  String get eyebrowBrands;

  /// No description provided for @brandsTitle.
  ///
  /// In bs, this message translates to:
  /// **'Brendovi'**
  String get brandsTitle;

  /// No description provided for @onSaleTitle.
  ///
  /// In bs, this message translates to:
  /// **'Akcije'**
  String get onSaleTitle;

  /// No description provided for @newInStoreTitle.
  ///
  /// In bs, this message translates to:
  /// **'Novo u ponudi'**
  String get newInStoreTitle;

  /// No description provided for @newestProducts.
  ///
  /// In bs, this message translates to:
  /// **'Najnoviji proizvodi'**
  String get newestProducts;

  /// No description provided for @searchResults.
  ///
  /// In bs, this message translates to:
  /// **'Rezultati pretrage'**
  String get searchResults;

  /// No description provided for @discountUpTo.
  ///
  /// In bs, this message translates to:
  /// **'do -{percent}%'**
  String discountUpTo(int percent);

  /// No description provided for @showAsCard.
  ///
  /// In bs, this message translates to:
  /// **'Prikaži kao karticu na ulaznoj stranici'**
  String get showAsCard;

  /// No description provided for @cardPicture.
  ///
  /// In bs, this message translates to:
  /// **'Slika kartice'**
  String get cardPicture;

  /// No description provided for @cardPictureAuto.
  ///
  /// In bs, this message translates to:
  /// **'Automatska slika (prvi proizvod u sekciji)'**
  String get cardPictureAuto;

  /// No description provided for @uploadPicture.
  ///
  /// In bs, this message translates to:
  /// **'Pošalji sliku'**
  String get uploadPicture;

  /// No description provided for @pickProductPicture.
  ///
  /// In bs, this message translates to:
  /// **'Izaberi sliku proizvoda'**
  String get pickProductPicture;

  /// No description provided for @saveSectionFirst.
  ///
  /// In bs, this message translates to:
  /// **'Prvo spremite sekciju, pa joj dodajte sliku.'**
  String get saveSectionFirst;

  /// No description provided for @pickPictureTitle.
  ///
  /// In bs, this message translates to:
  /// **'Odaberite sliku proizvoda'**
  String get pickPictureTitle;

  /// No description provided for @noPicturesFound.
  ///
  /// In bs, this message translates to:
  /// **'Nema proizvoda sa slikama za ovu pretragu.'**
  String get noPicturesFound;

  /// No description provided for @brandLogoUpload.
  ///
  /// In bs, this message translates to:
  /// **'Postavi logotip'**
  String get brandLogoUpload;

  /// No description provided for @brandLogoRemove.
  ///
  /// In bs, this message translates to:
  /// **'Ukloni logotip'**
  String get brandLogoRemove;

  /// No description provided for @sectionTitleHint.
  ///
  /// In bs, this message translates to:
  /// **'Kratak naslov koji lijepo stane na karticu'**
  String get sectionTitleHint;

  /// No description provided for @sectionTitleTooShort.
  ///
  /// In bs, this message translates to:
  /// **'Naslov mora imati najmanje {min} znaka.'**
  String sectionTitleTooShort(int min);

  /// No description provided for @sectionTitleTooLong.
  ///
  /// In bs, this message translates to:
  /// **'Naslov može imati najviše {max} znaka.'**
  String sectionTitleTooLong(int max);

  /// No description provided for @sectionLayout.
  ///
  /// In bs, this message translates to:
  /// **'Oblik sekcije'**
  String get sectionLayout;

  /// No description provided for @layoutRow.
  ///
  /// In bs, this message translates to:
  /// **'Red'**
  String get layoutRow;

  /// No description provided for @layoutGrid.
  ///
  /// In bs, this message translates to:
  /// **'Mreža'**
  String get layoutGrid;

  /// No description provided for @layoutBanner.
  ///
  /// In bs, this message translates to:
  /// **'Veliki baner'**
  String get layoutBanner;

  /// No description provided for @layoutRowHint.
  ///
  /// In bs, this message translates to:
  /// **'Proizvodi u jednom redu koji se pomiče u stranu.'**
  String get layoutRowHint;

  /// No description provided for @layoutGridHint.
  ///
  /// In bs, this message translates to:
  /// **'Proizvodi u dvije kolone, jedan ispod drugog.'**
  String get layoutGridHint;

  /// No description provided for @layoutBannerHint.
  ///
  /// In bs, this message translates to:
  /// **'Široka slika s naslovom, a ispod nje red proizvoda.'**
  String get layoutBannerHint;

  /// No description provided for @previewTitle.
  ///
  /// In bs, this message translates to:
  /// **'Pregled u aplikaciji'**
  String get previewTitle;

  /// No description provided for @previewCard.
  ///
  /// In bs, this message translates to:
  /// **'Kartica'**
  String get previewCard;

  /// No description provided for @previewSection.
  ///
  /// In bs, this message translates to:
  /// **'Sekcija'**
  String get previewSection;

  /// No description provided for @previewSectionTitlePlaceholder.
  ///
  /// In bs, this message translates to:
  /// **'Naslov sekcije'**
  String get previewSectionTitlePlaceholder;

  /// No description provided for @shopTab.
  ///
  /// In bs, this message translates to:
  /// **'Shop'**
  String get shopTab;

  /// No description provided for @sectionEyebrow.
  ///
  /// In bs, this message translates to:
  /// **'Mali tekst iznad naslova (žuti)'**
  String get sectionEyebrow;

  /// No description provided for @sectionEyebrowHint.
  ///
  /// In bs, this message translates to:
  /// **'Neobavezno, npr. KOLEKCIJA. Prikazuje se žutim slovima.'**
  String get sectionEyebrowHint;

  /// No description provided for @sectionTitleWhite.
  ///
  /// In bs, this message translates to:
  /// **'Naslov (bijeli tekst)'**
  String get sectionTitleWhite;

  /// No description provided for @sectionEmptyWarning.
  ///
  /// In bs, this message translates to:
  /// **'Ova sekcija trenutno nema nijedan proizvod, pa je kupci ne vide. Promijenite odabir proizvoda.'**
  String get sectionEmptyWarning;

  /// No description provided for @sectionHasProducts.
  ///
  /// In bs, this message translates to:
  /// **'Trenutno prikazuje {count} proizvoda'**
  String sectionHasProducts(int count);

  /// No description provided for @sectionEmptyShort.
  ///
  /// In bs, this message translates to:
  /// **'Nema proizvoda: kupci ne vide ovu sekciju'**
  String get sectionEmptyShort;

  /// No description provided for @seeAll.
  ///
  /// In bs, this message translates to:
  /// **'Vidi sve'**
  String get seeAll;

  /// No description provided for @kindCategoryTiles.
  ///
  /// In bs, this message translates to:
  /// **'Blok kategorija (pločice)'**
  String get kindCategoryTiles;

  /// No description provided for @kindBrandTiles.
  ///
  /// In bs, this message translates to:
  /// **'Blok brendova (kartice)'**
  String get kindBrandTiles;

  /// No description provided for @kindRecommended.
  ///
  /// In bs, this message translates to:
  /// **'Preporuke („Možda vam se sviđa“)'**
  String get kindRecommended;

  /// No description provided for @styleTitle.
  ///
  /// In bs, this message translates to:
  /// **'Izgled'**
  String get styleTitle;

  /// No description provided for @backgroundLabel.
  ///
  /// In bs, this message translates to:
  /// **'Pozadina sekcije'**
  String get backgroundLabel;

  /// No description provided for @bgNone.
  ///
  /// In bs, this message translates to:
  /// **'Bez ploče'**
  String get bgNone;

  /// No description provided for @bgSoft.
  ///
  /// In bs, this message translates to:
  /// **'Tamna ploča'**
  String get bgSoft;

  /// No description provided for @bgAmber.
  ///
  /// In bs, this message translates to:
  /// **'Jantarna ploča'**
  String get bgAmber;

  /// No description provided for @bannerAlwaysPanel.
  ///
  /// In bs, this message translates to:
  /// **'Baner uvijek stoji na ploči, zajedno s proizvodima ispod.'**
  String get bannerAlwaysPanel;

  /// No description provided for @alignLabel.
  ///
  /// In bs, this message translates to:
  /// **'Poravnanje naslova'**
  String get alignLabel;

  /// No description provided for @alignLeft.
  ///
  /// In bs, this message translates to:
  /// **'Lijevo'**
  String get alignLeft;

  /// No description provided for @alignCenter.
  ///
  /// In bs, this message translates to:
  /// **'Sredina'**
  String get alignCenter;

  /// No description provided for @tileSizeLabel.
  ///
  /// In bs, this message translates to:
  /// **'Veličina proizvoda'**
  String get tileSizeLabel;

  /// No description provided for @tileCompact.
  ///
  /// In bs, this message translates to:
  /// **'Mali'**
  String get tileCompact;

  /// No description provided for @tileNormal.
  ///
  /// In bs, this message translates to:
  /// **'Srednji'**
  String get tileNormal;

  /// No description provided for @tileLarge.
  ///
  /// In bs, this message translates to:
  /// **'Veliki'**
  String get tileLarge;

  /// No description provided for @showSeeAllLabel.
  ///
  /// In bs, this message translates to:
  /// **'Prikaži vezu „Vidi sve“ ispod sekcije'**
  String get showSeeAllLabel;

  /// No description provided for @cardSpanLabel.
  ///
  /// In bs, this message translates to:
  /// **'Širina kartice'**
  String get cardSpanLabel;

  /// No description provided for @spanHalf.
  ///
  /// In bs, this message translates to:
  /// **'Pola širine'**
  String get spanHalf;

  /// No description provided for @spanFull.
  ///
  /// In bs, this message translates to:
  /// **'Cijela širina'**
  String get spanFull;

  /// No description provided for @tileCountCategories.
  ///
  /// In bs, this message translates to:
  /// **'Broj kategorija (paran)'**
  String get tileCountCategories;

  /// No description provided for @tileCountBrands.
  ///
  /// In bs, this message translates to:
  /// **'Broj brendova'**
  String get tileCountBrands;

  /// No description provided for @tilesManual.
  ///
  /// In bs, this message translates to:
  /// **'Ručni izbor i redoslijed'**
  String get tilesManual;

  /// No description provided for @tilesAutoHint.
  ///
  /// In bs, this message translates to:
  /// **'Automatski: prikazuju se samo oni koji imaju proizvoda, najpopunjeniji prvi.'**
  String get tilesAutoHint;

  /// No description provided for @tilesPickHint.
  ///
  /// In bs, this message translates to:
  /// **'Dodirnite da dodate, povucite da promijenite redoslijed. Prikazuju se samo oni koji imaju proizvoda.'**
  String get tilesPickHint;

  /// No description provided for @tilesSelected.
  ///
  /// In bs, this message translates to:
  /// **'Odabrano'**
  String get tilesSelected;

  /// No description provided for @tilesAvailable.
  ///
  /// In bs, this message translates to:
  /// **'Dostupno'**
  String get tilesAvailable;

  /// No description provided for @categoriesEvenError.
  ///
  /// In bs, this message translates to:
  /// **'Broj odabranih kategorija mora biti paran, da zadnji red bude pun.'**
  String get categoriesEvenError;

  /// No description provided for @blockNoDelete.
  ///
  /// In bs, this message translates to:
  /// **'Ovaj blok se ne briše, samo se sakriva prekidačem.'**
  String get blockNoDelete;

  /// No description provided for @sectionShows.
  ///
  /// In bs, this message translates to:
  /// **'Prikazuje: {count}'**
  String sectionShows(int count);

  /// No description provided for @previewSeeAll.
  ///
  /// In bs, this message translates to:
  /// **'Vidi sve'**
  String get previewSeeAll;

  /// No description provided for @confirmOrderTitle.
  ///
  /// In bs, this message translates to:
  /// **'Potvrda narudžbe'**
  String get confirmOrderTitle;

  /// No description provided for @confirmOrderBody.
  ///
  /// In bs, this message translates to:
  /// **'Narudžba na iznos {total}. {payment}. Preuzimanje u salonu.'**
  String confirmOrderBody(String total, String payment);

  /// No description provided for @confirmBookingTitle.
  ///
  /// In bs, this message translates to:
  /// **'Potvrda rezervacije'**
  String get confirmBookingTitle;

  /// No description provided for @confirmBookingBody.
  ///
  /// In bs, this message translates to:
  /// **'Rezervisati termin kod {barber}, {when}, ukupno {total}?'**
  String confirmBookingBody(String barber, String when, String total);

  /// No description provided for @messagePicture.
  ///
  /// In bs, this message translates to:
  /// **'Slika (neobavezno)'**
  String get messagePicture;

  /// No description provided for @removePictureShort.
  ///
  /// In bs, this message translates to:
  /// **'Ukloni'**
  String get removePictureShort;

  /// No description provided for @salonLocation.
  ///
  /// In bs, this message translates to:
  /// **'Lokacija salona na mapi'**
  String get salonLocation;

  /// No description provided for @salonLocationHint.
  ///
  /// In bs, this message translates to:
  /// **'Upišite adresu i pronađite je, ili kliknite na mapu da postavite oznaku.'**
  String get salonLocationHint;

  /// No description provided for @searchAddress.
  ///
  /// In bs, this message translates to:
  /// **'Adresa za pretragu'**
  String get searchAddress;

  /// No description provided for @findOnMap.
  ///
  /// In bs, this message translates to:
  /// **'Pronađi'**
  String get findOnMap;

  /// No description provided for @addressNotFound.
  ///
  /// In bs, this message translates to:
  /// **'Adresa nije pronađena. Kliknite na mapu da postavite oznaku.'**
  String get addressNotFound;

  /// No description provided for @noPinYet.
  ///
  /// In bs, this message translates to:
  /// **'Oznaka još nije postavljena.'**
  String get noPinYet;

  /// No description provided for @pinSet.
  ///
  /// In bs, this message translates to:
  /// **'Oznaka je postavljena.'**
  String get pinSet;

  /// No description provided for @removePin.
  ///
  /// In bs, this message translates to:
  /// **'Ukloni oznaku'**
  String get removePin;

  /// No description provided for @confirmDeleteNamed.
  ///
  /// In bs, this message translates to:
  /// **'Obrisati „{name}“? Ovo se ne može poništiti.'**
  String confirmDeleteNamed(String name);

  /// No description provided for @removePictureQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Ukloniti ovu sliku?'**
  String get removePictureQuestion;

  /// No description provided for @fulfilOrderQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Označiti narudžbu #{id} kao preuzetu? Ovo se ne može poništiti.'**
  String fulfilOrderQuestion(int id);

  /// No description provided for @announceQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Poslati ovu poruku: {audience}? Primaoci je odmah vide u aplikaciji.'**
  String announceQuestion(String audience);

  /// No description provided for @announceEmailQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Poslati ovu poruku: {audience}, i e-mailom? Poslani mailovi se ne mogu povući.'**
  String announceEmailQuestion(String audience);

  /// No description provided for @completeQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Označiti termin kao završen?'**
  String get completeQuestion;

  /// No description provided for @noShowQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Označiti da klijent nije došao na termin?'**
  String get noShowQuestion;

  /// No description provided for @sectionProductsCountError.
  ///
  /// In bs, this message translates to:
  /// **'Odaberite od 1 do 30 proizvoda za ovu sekciju.'**
  String get sectionProductsCountError;

  /// No description provided for @quantityLess.
  ///
  /// In bs, this message translates to:
  /// **'Jedan komad manje'**
  String get quantityLess;

  /// No description provided for @quantityMore.
  ///
  /// In bs, this message translates to:
  /// **'Jedan komad više'**
  String get quantityMore;

  /// No description provided for @removeFromSelection.
  ///
  /// In bs, this message translates to:
  /// **'Ukloni iz odabira'**
  String get removeFromSelection;

  /// No description provided for @employeeSearchHint.
  ///
  /// In bs, this message translates to:
  /// **'Pretraži po imenu ili zvanju'**
  String get employeeSearchHint;

  /// No description provided for @serviceSearchHint.
  ///
  /// In bs, this message translates to:
  /// **'Pretraži po nazivu ili opisu'**
  String get serviceSearchHint;

  /// No description provided for @clearSearch.
  ///
  /// In bs, this message translates to:
  /// **'Obriši pretragu'**
  String get clearSearch;

  /// No description provided for @renameTitle.
  ///
  /// In bs, this message translates to:
  /// **'Preimenuj titulu'**
  String get renameTitle;

  /// No description provided for @titleName.
  ///
  /// In bs, this message translates to:
  /// **'Naziv titule'**
  String get titleName;

  /// No description provided for @confirmBooking.
  ///
  /// In bs, this message translates to:
  /// **'Potvrdi'**
  String get confirmBooking;

  /// No description provided for @declineBooking.
  ///
  /// In bs, this message translates to:
  /// **'Odbij'**
  String get declineBooking;

  /// No description provided for @declineBookingTitle.
  ///
  /// In bs, this message translates to:
  /// **'Odbij rezervaciju'**
  String get declineBookingTitle;

  /// No description provided for @declineBookingQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Odbiti ovu rezervaciju? Vrijeme se oslobađa, a klijent u obavijesti dobija razlog.'**
  String get declineBookingQuestion;

  /// No description provided for @statusDeclined.
  ///
  /// In bs, this message translates to:
  /// **'Odbijen'**
  String get statusDeclined;

  /// No description provided for @bookingConfirmedByStaff.
  ///
  /// In bs, this message translates to:
  /// **'Termin je potvrđen.'**
  String get bookingConfirmedByStaff;

  /// No description provided for @bookingDeclined.
  ///
  /// In bs, this message translates to:
  /// **'Rezervacija je odbijena.'**
  String get bookingDeclined;

  /// No description provided for @autoConfirmReservations.
  ///
  /// In bs, this message translates to:
  /// **'Automatski potvrđuj rezervacije'**
  String get autoConfirmReservations;

  /// No description provided for @autoConfirmReservationsHint.
  ///
  /// In bs, this message translates to:
  /// **'Uključeno: termin je potvrđen čim ga klijent rezerviše. Isključeno: termin čeka (vrijeme je zauzeto) dok ga frizer ili administrator ne potvrdi ili odbije uz razlog.'**
  String get autoConfirmReservationsHint;

  /// No description provided for @pendingBookingHint.
  ///
  /// In bs, this message translates to:
  /// **'Salon još nije potvrdio ovaj termin. Vrijeme je rezervisano za vas.'**
  String get pendingBookingHint;

  /// No description provided for @changePhoto.
  ///
  /// In bs, this message translates to:
  /// **'Promijeni sliku'**
  String get changePhoto;

  /// No description provided for @adjustPicture.
  ///
  /// In bs, this message translates to:
  /// **'Namjesti sliku'**
  String get adjustPicture;

  /// No description provided for @unlockAccount.
  ///
  /// In bs, this message translates to:
  /// **'Otključaj'**
  String get unlockAccount;

  /// No description provided for @lockedUntil.
  ///
  /// In bs, this message translates to:
  /// **'Zaključan do {time}'**
  String lockedUntil(String time);

  /// No description provided for @unlockAccountQuestion.
  ///
  /// In bs, this message translates to:
  /// **'Otključati račun korisnika {name}? Moći će se odmah ponovo prijaviti.'**
  String unlockAccountQuestion(String name);

  /// No description provided for @accountUnlocked.
  ///
  /// In bs, this message translates to:
  /// **'Račun je otključan.'**
  String get accountUnlocked;
}

class _AppLocalizationsDelegate
    extends LocalizationsDelegate<AppLocalizations> {
  const _AppLocalizationsDelegate();

  @override
  Future<AppLocalizations> load(Locale locale) {
    return SynchronousFuture<AppLocalizations>(lookupAppLocalizations(locale));
  }

  @override
  bool isSupported(Locale locale) =>
      <String>['bs', 'en'].contains(locale.languageCode);

  @override
  bool shouldReload(_AppLocalizationsDelegate old) => false;
}

AppLocalizations lookupAppLocalizations(Locale locale) {
  // Lookup logic when only language code is specified.
  switch (locale.languageCode) {
    case 'bs':
      return AppLocalizationsBs();
    case 'en':
      return AppLocalizationsEn();
  }

  throw FlutterError(
    'AppLocalizations.delegate failed to load unsupported locale "$locale". This is likely '
    'an issue with the localizations generation tool. Please file an issue '
    'on GitHub with a reproducible sample app and the gen-l10n configuration '
    'that was used.',
  );
}

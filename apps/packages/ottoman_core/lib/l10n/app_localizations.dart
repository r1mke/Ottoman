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
  /// **'Unesite ispravan email'**
  String get invalidEmail;

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
  /// **'Za odabir usluga prvo odaberite nekog od naših uposlenika.'**
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
  /// **'Na čekanju'**
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
  /// **'{count} proizvoda'**
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

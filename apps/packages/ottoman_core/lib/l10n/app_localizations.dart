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

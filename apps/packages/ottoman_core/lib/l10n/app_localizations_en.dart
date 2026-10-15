// ignore: unused_import
import 'package:intl/intl.dart' as intl;

import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for English (`en`).
class AppLocalizationsEn extends AppLocalizations {
  AppLocalizationsEn([String locale = 'en']) : super(locale);

  @override
  String get appName => 'OTTOMAN';

  @override
  String get login => 'Sign in';

  @override
  String get logout => 'Sign out';

  @override
  String get email => 'Email';

  @override
  String get password => 'Password';

  @override
  String get retry => 'Try again';

  @override
  String get cancel => 'Cancel';

  @override
  String get save => 'Save';

  @override
  String get edit => 'Edit';

  @override
  String get delete => 'Delete';

  @override
  String get employees => 'Staff';

  @override
  String get services => 'Services';

  @override
  String get appointments => 'Appointments';

  @override
  String get products => 'Products';

  @override
  String get settings => 'Settings';

  @override
  String get home => 'Home';

  @override
  String get noData => 'No data';

  @override
  String get currencyShort => 'BAM';

  @override
  String get register => 'Sign up';

  @override
  String get createAccount => 'Create account';

  @override
  String get haveAccount => 'Already have an account? Sign in';

  @override
  String get noAccount => 'No account? Sign up';

  @override
  String get forgotPassword => 'Forgot password?';

  @override
  String get resetEmailSent =>
      'If the account exists, we\'ve emailed instructions.';

  @override
  String get firstName => 'First name';

  @override
  String get lastName => 'Last name';

  @override
  String get phone => 'Phone';

  @override
  String get profile => 'Profile';

  @override
  String get saveChanges => 'Save changes';

  @override
  String get profileSaved => 'Profile saved.';

  @override
  String get requiredField => 'Required';

  @override
  String get invalidEmail => 'Enter a valid email';

  @override
  String get passwordRules =>
      'At least 8 characters, upper and lower case, a digit and a symbol.';

  @override
  String get registrationDone => 'Account created. You can sign in now.';

  @override
  String get welcomeBack => 'Welcome back';

  @override
  String get signInSubtitle => 'Sign in to continue';

  @override
  String get language => 'Language';

  @override
  String get overview => 'Overview';

  @override
  String get clients => 'Clients';

  @override
  String get notifications => 'Notifications';

  @override
  String get reports => 'Reports';

  @override
  String get earnings => 'Earnings';

  @override
  String get myProfile => 'My profile';

  @override
  String get comingSoon => 'Coming soon';

  @override
  String signedInAs(String name) {
    return 'Signed in as $name';
  }

  @override
  String get genericError => 'Something went wrong.';

  @override
  String get chooseEmployee => 'Choose a barber';

  @override
  String get chooseEmployeeHint =>
      'To pick services, first choose one of our barbers.';

  @override
  String get pricelist => 'Services & Prices';

  @override
  String minutesShort(int count) {
    return '$count min';
  }

  @override
  String get total => 'Total';

  @override
  String get continueLabel => 'Continue';

  @override
  String get selectServices => 'Select one or more services';

  @override
  String get add => 'Add';

  @override
  String get name => 'Name';

  @override
  String get description => 'Description';

  @override
  String get price => 'Price';

  @override
  String get duration => 'Duration (min)';

  @override
  String get active => 'Active';

  @override
  String get inactive => 'Inactive';

  @override
  String get title => 'Title';

  @override
  String get bio => 'Bio';

  @override
  String get workingHours => 'Working hours';

  @override
  String get dayOff => 'Day off';

  @override
  String get breakLabel => 'Break';

  @override
  String get from => 'From';

  @override
  String get to => 'To';

  @override
  String get deactivate => 'Deactivate';

  @override
  String get searchHint => 'Search…';

  @override
  String get durationStepError => 'Duration must be a multiple of 5 minutes.';

  @override
  String get invalidNumber => 'Enter a valid number';
}

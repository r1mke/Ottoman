import '../l10n/app_localizations.dart';

/// Input rules for forms. Each returns the message to show under the field, or null when the value is fine.
/// The messages say what is expected ("u formatu ..."), so a person can correct the input without guessing.
/// The server checks the same rules again; these only save a round trip.
abstract final class Validators {
  static final _email = RegExp(
    r'^[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$',
  );
  static final _phoneChars = RegExp(r'^\+?[0-9 /().\-]+$');
  // National number after 0 / +387 / 00387: mobile 060-067 (+ 6 digits), landline area codes 03x, 049, 05x (+ 6).
  static final _bosnianPhone = RegExp(
    r'^(?:\+387|00387|0)(?:6[0-7]\d{6,7}|(?:3\d|49|5\d)\d{6})$',
  );
  static final _name = RegExp(r"^[\p{L}][\p{L} .'\-]*$", unicode: true);
  static final _money = RegExp(r'^\d{1,6}([.,]\d{1,2})?$');

  static String? required(AppLocalizations t, String? v) =>
      (v == null || v.trim().isEmpty) ? t.requiredField : null;

  static String? email(AppLocalizations t, String? v) {
    final s = (v ?? '').trim();
    if (s.isEmpty) return t.requiredField;
    return s.length <= 254 && _email.hasMatch(s) ? null : t.invalidEmail;
  }

  /// Optional unless [required]. A Bosnian number (mobile 06x or landline), as 061 123 456 or +387 61 123 456;
  /// spaces, dashes, slashes and brackets are allowed as separators.
  static String? phone(AppLocalizations t, String? v, {bool required = false}) {
    final s = (v ?? '').trim();
    if (s.isEmpty) return required ? t.requiredField : null;
    final compact = s.replaceAll(RegExp(r'[ /().\-]'), '');
    return _phoneChars.hasMatch(s) && _bosnianPhone.hasMatch(compact)
        ? null
        : t.invalidPhone;
  }

  /// A first or last name: letters (with diacritics), spaces, dots, apostrophes and hyphens; 2 to 50 characters.
  static String? personName(AppLocalizations t, String? v) {
    final s = (v ?? '').trim();
    if (s.isEmpty) return t.requiredField;
    return s.length >= 2 && s.length <= 50 && _name.hasMatch(s)
        ? null
        : t.invalidName;
  }

  /// A price: more than 0, at most [max], with at most two decimals; comma or dot as the separator.
  static String? price(AppLocalizations t, String? v, {double max = 10000}) {
    final s = (v ?? '').trim();
    if (s.isEmpty) return t.requiredField;
    if (!_money.hasMatch(s)) return t.invalidPrice;
    final n = double.parse(s.replaceAll(',', '.'));
    return n > 0 && n <= max ? null : t.invalidPrice;
  }

  static String? wholeNumber(
    AppLocalizations t,
    String? v, {
    int min = 0,
    int max = 1000000,
  }) {
    final s = (v ?? '').trim();
    if (s.isEmpty) return t.requiredField;
    final n = int.tryParse(s);
    return n != null && n >= min && n <= max
        ? null
        : t.invalidWholeNumber(min, max);
  }

  /// The password policy of the server: 8+ characters with an upper and a lower case letter, a digit and a symbol.
  static String? newPassword(AppLocalizations t, String? v) {
    final s = v ?? '';
    if (s.isEmpty) return t.requiredField;
    final ok =
        s.length >= 8 &&
        s.contains(RegExp(r'[A-Z]')) &&
        s.contains(RegExp(r'[a-z]')) &&
        s.contains(RegExp(r'\d')) &&
        s.contains(RegExp(r'[^A-Za-z0-9]'));
    return ok ? null : t.passwordRules;
  }
}

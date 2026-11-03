import 'dart:ui';

import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

void main() {
  final t = lookupAppLocalizations(const Locale('bs'));

  group('email', () {
    test('accepts ordinary addresses', () {
      for (final ok in [
        'ana@x.ba',
        'ana.anic+salon@mail.fit.ba',
        'A_B-9@sub.example.com',
        '  ana@x.ba  ',
      ]) {
        expect(Validators.email(t, ok), isNull, reason: ok);
      }
    });

    test(
      'refuses what is not an address, with the expected format in the message',
      () {
        for (final bad in [
          'ana',
          'ana@',
          '@x.ba',
          'ana@x',
          'ana x@y.ba',
          'ana@@x.ba',
          'ana@x..ba',
          'ana@x.b',
        ]) {
          expect(Validators.email(t, bad), t.invalidEmail, reason: bad);
        }
        expect(Validators.email(t, ''), t.requiredField);
        expect(Validators.email(t, null), t.requiredField);
        expect(t.invalidEmail, contains('ime@domena.ba'));
      },
    );

    test('an address longer than 254 characters is refused', () {
      expect(Validators.email(t, '${'a' * 250}@x.ba'), t.invalidEmail);
    });
  });

  group('phone', () {
    test('is optional unless required', () {
      expect(Validators.phone(t, ''), isNull);
      expect(Validators.phone(t, null), isNull);
      expect(Validators.phone(t, '', required: true), t.requiredField);
    });

    test('accepts the usual ways of writing a number', () {
      for (final ok in [
        '061 123 456',
        '+387 61 123 456',
        '061/123-456',
        '(033) 123 456',
        '0611234567',
      ]) {
        expect(Validators.phone(t, ok), isNull, reason: ok);
      }
    });

    test('refuses letters, too few or too many digits', () {
      for (final bad in [
        'abc',
        '12345',
        '061 abc 456',
        '+1 202 555 0143',
        '051 12',
        '070 123 456',
        '+387 61 123 456 789 000',
        '+38761123456789',
        '--',
        '061#123456',
      ]) {
        expect(Validators.phone(t, bad), t.invalidPhone, reason: bad);
      }
      expect(t.invalidPhone, contains('061 123 456'));
      expect(t.invalidPhone, contains('ispravan'));
    });
  });

  group('personName', () {
    test('accepts names with diacritics, hyphens and apostrophes', () {
      for (final ok in [
        'Ana',
        'Šćepan',
        'Đorđe Ćirić',
        'Mary-Jane',
        "O'Brien",
        'Dr. Hasan',
      ]) {
        expect(Validators.personName(t, ok), isNull, reason: ok);
      }
    });

    test('refuses digits, symbols, one letter and very long text', () {
      for (final bad in ['A', 'Ana1', 'Ana@', '123', '-Ana', 'a' * 51]) {
        expect(Validators.personName(t, bad), t.invalidName, reason: bad);
      }
      expect(Validators.personName(t, '  '), t.requiredField);
    });
  });

  group('price', () {
    test('accepts comma or dot with at most two decimals', () {
      for (final ok in ['12', '12,5', '12.50', '0,01', '10000', '9999.99']) {
        expect(Validators.price(t, ok), isNull, reason: ok);
      }
    });

    test(
      'refuses zero, negatives, three decimals, text and absurd amounts',
      () {
        for (final bad in [
          '0',
          '0,00',
          '-5',
          '12,345',
          'abc',
          '10000,01',
          '1 000',
          '12,',
        ]) {
          expect(Validators.price(t, bad), t.invalidPrice, reason: bad);
        }
        expect(Validators.price(t, ''), t.requiredField);
      },
    );

    test('the limit can differ per form', () {
      expect(Validators.price(t, '600', max: 500), t.invalidPrice);
      expect(Validators.price(t, '500', max: 500), isNull);
    });
  });

  group('wholeNumber', () {
    test('respects the range and says what the range is', () {
      expect(Validators.wholeNumber(t, '5', min: 5, max: 480), isNull);
      expect(
        Validators.wholeNumber(t, '4', min: 5, max: 480),
        t.invalidWholeNumber(5, 480),
      );
      expect(
        Validators.wholeNumber(t, '481', min: 5, max: 480),
        t.invalidWholeNumber(5, 480),
      );
      expect(
        Validators.wholeNumber(t, '2,5'),
        t.invalidWholeNumber(0, 1000000),
      );
      expect(Validators.wholeNumber(t, 'x'), t.invalidWholeNumber(0, 1000000));
      expect(Validators.wholeNumber(t, ''), t.requiredField);
      expect(t.invalidWholeNumber(5, 480), 'Unesite cijeli broj od 5 do 480.');
    });
  });

  group('newPassword', () {
    test('follows the server policy', () {
      expect(Validators.newPassword(t, 'Str0ng_Pass!'), isNull);
      for (final bad in [
        'Sh0rt!A',
        'alllowercase1!',
        'ALLUPPERCASE1!',
        'NoDigits_here!',
        'NoSymbol123Ab',
        '',
      ]) {
        expect(Validators.newPassword(t, bad), isNotNull, reason: bad);
      }
      expect(Validators.newPassword(t, 'abc'), t.passwordRules);
    });
  });

  test('required treats blank as missing', () {
    expect(Validators.required(t, '   '), t.requiredField);
    expect(Validators.required(t, null), t.requiredField);
    expect(Validators.required(t, 'x'), isNull);
  });

  group('url', () {
    test('empty is fine, a full http(s) link is fine', () {
      expect(Validators.url(t, ''), isNull);
      expect(Validators.url(t, null), isNull);
      expect(Validators.url(t, 'https://maps.google.com/x'), isNull);
      expect(Validators.url(t, ' http://ottoman.ba '), isNull);
    });
    test('anything else is refused', () {
      expect(Validators.url(t, 'nije link'), isNotNull);
      expect(Validators.url(t, 'ftp://x.ba'), isNotNull);
      expect(Validators.url(t, 'www.ottoman.ba'), isNotNull);
    });
  });
}

import 'package:flutter/material.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

DioException _err(int status, Object? data) => DioException(
  requestOptions: RequestOptions(path: '/x'),
  response: Response(
    requestOptions: RequestOptions(path: '/x'),
    statusCode: status,
    data: data,
  ),
);

void main() {
  test('uses ProblemDetails detail and field errors', () {
    final e = AppException.fromDio(
      _err(409, {
        'title': 'Conflict',
        'detail': 'Termin je upravo zauzet',
        'errors': {
          'start': ['Zauzeto'],
        },
      }),
    );
    expect(e.message, 'Termin je upravo zauzet');
    expect(e.isConflict, isTrue);
    expect(e.errors['start'], ['Zauzeto']);
  });

  test('falls back to a friendly message for non-JSON errors', () {
    expect(
      AppException.fromDio(_err(500, '<html/>')).message,
      contains('server'),
    );
    expect(AppException.fromDio(_err(403, null)).isForbidden, isTrue);
  });

  test('flags network failures without a response', () {
    final e = AppException.fromDio(
      DioException(requestOptions: RequestOptions(path: '/x')),
    );
    expect(e.isNetwork, isTrue);
  });

  testWidgets('ErrorView shows message and retry', (tester) async {
    var tapped = false;
    await tester.pumpWidget(
      MaterialApp(
        home: ErrorView(message: 'Greška', onRetry: () => tapped = true),
      ),
    );
    expect(find.text('Greška'), findsOneWidget);
    await tester.tap(find.text('Pokušaj ponovo'));
    expect(tapped, isTrue);
  });

  test('detail prefers the field messages and falls back to the message', () {
    expect(AppException('Greška').detail, 'Greška');
    final e = AppException(
      'Greška',
      errors: {
        'Naziv': ['Obavezno.'],
        'Cijena': ['Mora biti pozitivna.'],
      },
    );
    expect(e.detail, 'Obavezno.\nMora biti pozitivna.');
    expect(e.fieldErrors, e.detail);
    expect(AppException('x').fieldErrors, '');
  });
}

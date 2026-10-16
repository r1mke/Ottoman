import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_mobile/app.dart';

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  int resetStatus = 204;
  Map<String, dynamic> resetBody = {};

  RequestOptions? last(String part) =>
      requests.reversed.where((r) => r.path.contains(part)).firstOrNull;

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
    final reset = o.path.endsWith('/reset-password');
    final status = reset ? resetStatus : 204;
    return ResponseBody.fromString(
      reset && resetStatus != 204 ? jsonEncode(resetBody) : '',
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

Future<_Api> _open(WidgetTester tester) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(420, 900);
  addTearDown(tester.view.reset);

  final api = _Api();
  late final AuthController auth;
  auth = AuthController(
    baseUrl: 'http://test',
    storage: MemoryTokenStorage(),
    api: ApiClient(
      baseUrl: 'http://test',
      dio: Dio()..httpClientAdapter = api,
      readToken: () async => auth.accessToken,
    ),
  );
  await auth.restore();
  await tester.pumpWidget(OttomanMobileApp(auth: auth));
  await tester.pumpAndSettle();
  return api;
}

Future<void> _startReset(WidgetTester tester, {required bool haveCode}) async {
  await tester.tap(find.text('Zaboravljena lozinka?'));
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('forgot_email')), 'ana@x.ba');
  if (haveCode) {
    await tester.tap(find.byKey(const Key('have_code')));
  } else {
    await tester.tap(find.widgetWithText(FilledButton, 'Spremi'));
  }
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('"Zaboravljena lozinka" asks for the mail, then for the code', (
    tester,
  ) async {
    final api = await _open(tester);

    await _startReset(tester, haveCode: false);

    expect(api.last('/forgot-password'), isNotNull);
    expect((api.last('/forgot-password')!.data as Map)['email'], 'ana@x.ba');
    expect(find.byKey(const Key('reset_code')), findsOneWidget);
  });

  testWidgets('"Imam kod" skips sending another mail', (tester) async {
    final api = await _open(tester);

    await _startReset(tester, haveCode: true);

    expect(api.last('/forgot-password'), isNull);
    expect(find.byKey(const Key('reset_code')), findsOneWidget);
  });

  testWidgets('a correct code and password change it and say so', (
    tester,
  ) async {
    final api = await _open(tester);
    await _startReset(tester, haveCode: true);

    await tester.enterText(find.byKey(const Key('reset_code')), '  CODE123 ');
    await tester.enterText(
      find.byKey(const Key('reset_password')),
      'Nova123!x',
    );
    await tester.tap(find.byKey(const Key('reset_submit')));
    await tester.pumpAndSettle();

    final sent = api.last('/reset-password')!.data as Map;
    expect(sent['email'], 'ana@x.ba');
    expect(
      sent['token'],
      'CODE123',
      reason: 'whitespace from pasting is trimmed',
    );
    expect(sent['newPassword'], 'Nova123!x');
    expect(find.text('Lozinka je promijenjena. Prijavite se.'), findsOneWidget);
    expect(find.byKey(const Key('reset_code')), findsNothing);
  });

  testWidgets('a wrong code keeps the dialog open with a generic message', (
    tester,
  ) async {
    final api = await _open(tester);
    api
      ..resetStatus = 400
      ..resetBody = {'status': 400, 'title': 'Podaci nisu ispravni.'};
    await _startReset(tester, haveCode: true);

    await tester.enterText(find.byKey(const Key('reset_code')), 'LOSE');
    await tester.enterText(
      find.byKey(const Key('reset_password')),
      'Nova123!x',
    );
    await tester.tap(find.byKey(const Key('reset_submit')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('reset_error')), findsOneWidget);
    expect(find.text('Kod nije ispravan ili je istekao.'), findsOneWidget);
    expect(find.byKey(const Key('reset_code')), findsOneWidget);
  });

  testWidgets(
    'the server\'s password rules are shown when the password is weak',
    (tester) async {
      final api = await _open(tester);
      api
        ..resetStatus = 400
        ..resetBody = {
          'status': 400,
          'title': 'One or more validation errors occurred.',
          'errors': {
            'NewPassword': ['Lozinka mora imati veliko slovo.'],
          },
        };
      await _startReset(tester, haveCode: true);

      await tester.enterText(find.byKey(const Key('reset_code')), 'CODE');
      await tester.enterText(find.byKey(const Key('reset_password')), 'slaba');
      await tester.tap(find.byKey(const Key('reset_submit')));
      await tester.pumpAndSettle();

      expect(find.text('Lozinka mora imati veliko slovo.'), findsOneWidget);
    },
  );
}

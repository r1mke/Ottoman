import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_mobile/app.dart';

class _Adapter implements HttpClientAdapter {
  _Adapter({this.roles = const ['Client'], this.registerStatus = 202});
  final List<String> roles;
  final int registerStatus;
  final calls = <String>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    calls.add('${o.method} ${o.path}');
    Object? body;
    var status = 204;
    if (o.path.endsWith('/login')) {
      status = 200;
      body = {
        'accessToken': 'a',
        'refreshToken': 'r',
        'accessTokenExpiresUtc': DateTime.utc(2030).toIso8601String(),
        'user': {
          'id': '1',
          'email': 'x@y.z',
          'firstName': 'Ana',
          'lastName': 'Anić',
          'phone': null,
          'roles': roles,
        },
      };
    } else if (o.path.endsWith('/register')) {
      status = registerStatus;
      if (status == 400) {
        body = {
          'errors': {
            'Password': ['Lozinka mora sadržavati broj.'],
          },
        };
      }
    }
    return ResponseBody.fromString(
      jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

AuthController _auth(_Adapter adapter) {
  late final AuthController c;
  c = AuthController(
    baseUrl: 'http://test',
    storage: MemoryTokenStorage(),
    allowedRoles: {Roles.client, Roles.employee},
    api: ApiClient(
      baseUrl: 'http://test',
      dio: Dio()..httpClientAdapter = adapter,
      readToken: () async => c.accessToken,
    ),
  );
  return c;
}

Future<void> _pump(WidgetTester tester, AuthController auth) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(420, 900);
  addTearDown(tester.view.reset);
  await auth.restore();
  await tester.pumpWidget(OttomanMobileApp(auth: auth));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('client sees booking tabs after login', (tester) async {
    final auth = _auth(_Adapter());
    await _pump(tester, auth);

    await tester.enterText(find.byKey(const Key('login_email')), 'x@y.z');
    await tester.enterText(find.byKey(const Key('login_password')), 'pw');
    await tester.tap(find.byKey(const Key('login_submit')));
    await tester.pumpAndSettle();

    expect(find.text('Proizvodi'), findsWidgets);
    expect(find.text('Termini'), findsWidgets);
  });

  testWidgets('employee gets the staff navigation, not the shop', (
    tester,
  ) async {
    final auth = _auth(_Adapter(roles: ['Employee']));
    await _pump(tester, auth);

    await tester.enterText(find.byKey(const Key('login_email')), 'x@y.z');
    await tester.enterText(find.byKey(const Key('login_password')), 'pw');
    await tester.tap(find.byKey(const Key('login_submit')));
    await tester.pumpAndSettle();

    expect(find.text('Notifikacije'), findsWidgets);
    expect(find.text('Proizvodi'), findsNothing);
  });

  testWidgets('registration shows server password errors per field', (
    tester,
  ) async {
    final adapter = _Adapter(registerStatus: 400);
    await _pump(tester, _auth(adapter));

    await tester.tap(find.byKey(const Key('go_register')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('reg_first')), 'Ana');
    await tester.enterText(find.byKey(const Key('reg_last')), 'Anić');
    await tester.enterText(find.byKey(const Key('reg_email')), 'ana@x.com');
    await tester.enterText(find.byKey(const Key('reg_password')), 'NoDigits!!');
    await tester.ensureVisible(find.byKey(const Key('reg_submit')));
    await tester.tap(find.byKey(const Key('reg_submit')));
    await tester.pumpAndSettle();

    expect(find.text('Lozinka mora sadržavati broj.'), findsOneWidget);
  });

  testWidgets('successful registration returns to login with a confirmation', (
    tester,
  ) async {
    await _pump(tester, _auth(_Adapter()));

    await tester.tap(find.byKey(const Key('go_register')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('reg_first')), 'Ana');
    await tester.enterText(find.byKey(const Key('reg_last')), 'Anić');
    await tester.enterText(find.byKey(const Key('reg_email')), 'ana@x.com');
    await tester.enterText(
      find.byKey(const Key('reg_password')),
      'Str0ng_Pass!',
    );
    await tester.ensureVisible(find.byKey(const Key('reg_submit')));
    await tester.tap(find.byKey(const Key('reg_submit')));
    await tester.pumpAndSettle();

    expect(
      find.text('Račun je kreiran. Sada se možete prijaviti.'),
      findsOneWidget,
    );
    expect(find.byKey(const Key('login_submit')), findsOneWidget);
  });
}

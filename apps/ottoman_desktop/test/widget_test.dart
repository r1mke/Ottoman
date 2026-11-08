import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/app.dart';

class _Adapter implements HttpClientAdapter {
  _Adapter(this.roles);
  final List<String> roles;

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    final body = o.path.endsWith('/login')
        ? {
            'accessToken': 'a',
            'refreshToken': 'r',
            'accessTokenExpiresUtc': DateTime.utc(2030).toIso8601String(),
            'user': {
              'id': '1',
              'email': 'x@y.z',
              'firstName': 'Kerim',
              'lastName': 'Begić',
              'phone': null,
              'roles': roles,
            },
          }
        : null;
    return ResponseBody.fromString(
      jsonEncode(body),
      body == null ? 204 : 200,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

AuthController _auth(List<String> roles) {
  late final AuthController c;
  c = AuthController(
    baseUrl: 'http://test',
    storage: MemoryTokenStorage(),
    allowedRoles: {Roles.admin, Roles.employee},
    api: ApiClient(
      baseUrl: 'http://test',
      dio: Dio()..httpClientAdapter = _Adapter(roles),
      readToken: () async => c.accessToken,
    ),
  );
  return c;
}

Future<void> _signIn(WidgetTester tester) async {
  await tester.enterText(find.byKey(const Key('login_email')), 'x@y.z');
  await tester.enterText(find.byKey(const Key('login_password')), 'pw');
  await tester.tap(find.byKey(const Key('login_submit')));
  await tester.pumpAndSettle();
}

void main() {
  setUp(() => TestWidgetsFlutterBinding.ensureInitialized());

  testWidgets('shows validation errors before calling the API', (tester) async {
    tester.view.devicePixelRatio = 1.0;
    tester.view.physicalSize = const Size(1400, 900);
    addTearDown(tester.view.reset);
    final auth = _auth(['Admin']);
    await auth.restore();
    await tester.pumpWidget(
      OttomanDesktopApp(
        auth: auth,
        profiles: SavedProfiles(MemorySavedProfilesStore()),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('login_submit')));
    await tester.pump();

    expect(find.text('Obavezno polje'), findsWidgets);
    expect(auth.status, AuthStatus.unauthenticated);
  });

  testWidgets('admin sees every section after signing in', (tester) async {
    tester.view.devicePixelRatio = 1.0;
    tester.view.physicalSize = const Size(1400, 900);
    addTearDown(tester.view.reset);
    final auth = _auth(['Admin']);
    await auth.restore();
    await tester.pumpWidget(
      OttomanDesktopApp(
        auth: auth,
        profiles: SavedProfiles(MemorySavedProfilesStore()),
      ),
    );
    await tester.pumpAndSettle();

    await _signIn(tester);

    expect(find.text('Klijenti'), findsWidgets);
    expect(find.text('Izvještaji'), findsWidgets);
  });

  testWidgets('employee only sees own schedule and profile', (tester) async {
    tester.view.devicePixelRatio = 1.0;
    tester.view.physicalSize = const Size(1400, 900);
    addTearDown(tester.view.reset);
    final auth = _auth(['Employee']);
    await auth.restore();
    await tester.pumpWidget(
      OttomanDesktopApp(
        auth: auth,
        profiles: SavedProfiles(MemorySavedProfilesStore()),
      ),
    );
    await tester.pumpAndSettle();

    await _signIn(tester);

    expect(find.text('Termini'), findsWidgets);
    expect(find.text('Moj profil'), findsWidgets);
    expect(find.text('Klijenti'), findsNothing);
    expect(find.text('Izvještaji'), findsNothing);
  });
}

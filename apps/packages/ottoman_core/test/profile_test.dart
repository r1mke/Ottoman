import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

Map<String, dynamic> _user({String first = 'Ana', String? phone}) => {
  'id': 'u1',
  'email': 'ana@x.ba',
  'firstName': first,
  'lastName': 'Anić',
  'phone': phone,
  'roles': ['Client'],
};

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  int changeStatus = 204;
  String? savedFirst;

  Iterable<RequestOptions> to(String method, String pathEnd) =>
      requests.where((r) => r.method == method && r.path.endsWith(pathEnd));

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
    Object? body;
    var status = 200;
    final p = o.path;
    if (p.endsWith('/login')) {
      body = {
        'accessToken': 'a',
        'refreshToken': 'r',
        'accessTokenExpiresUtc': DateTime.utc(2030).toIso8601String(),
        'user': _user(),
      };
    } else if (p.endsWith('/change-password')) {
      status = changeStatus;
      if (changeStatus != 204) {
        body = {
          'status': 400,
          'title': 'Podaci nisu ispravni.',
          'errors': {
            'request': ['Trenutna lozinka nije ispravna.'],
          },
        };
      }
    } else if (p.endsWith('/logout')) {
      status = 204;
    } else if (p.endsWith('/api/v1/me') && o.method == 'PUT') {
      savedFirst = (o.data as Map)['firstName'] as String;
      body = _user(first: savedFirst!);
    } else if (p.endsWith('/api/v1/me')) {
      body = _user(first: savedFirst ?? 'Ana');
    }
    return ResponseBody.fromString(
      body == null ? '' : jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

Future<(_Api, AuthController)> _open(WidgetTester tester) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(900, 1200);
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
  await tester.runAsync(() => auth.login('ana@x.ba', 'pw'));
  await tester.pumpWidget(
    MaterialApp(
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
      locale: const Locale('bs'),
      home: Scaffold(
        body: SingleChildScrollView(child: ProfileForm(auth: auth)),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return (api, auth);
}

Future<void> _openDialog(WidgetTester tester) async {
  await tester.tap(find.byKey(const Key('change_password')));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets(
    'shows the email and the current details; saving sends trimmed values',
    (tester) async {
      final (api, _) = await _open(tester);
      expect(find.text('ana@x.ba'), findsOneWidget);

      await tester.enterText(
        find.byKey(const Key('profile_first')),
        '  Anita ',
      );
      await tester.tap(find.byKey(const Key('profile_save')));
      await tester.pumpAndSettle();

      final put = api.to('PUT', '/api/v1/me').single;
      expect((put.data as Map)['firstName'], 'Anita');
      expect(
        (put.data as Map)['phone'],
        isNull,
        reason: 'an empty phone is not sent as an empty string',
      );
      expect(find.text('Profil je spremljen.'), findsOneWidget);
    },
  );

  testWidgets(
    'an empty name is refused under the field before anything is sent',
    (tester) async {
      final (api, _) = await _open(tester);

      await tester.enterText(find.byKey(const Key('profile_first')), '   ');
      await tester.tap(find.byKey(const Key('profile_save')));
      await tester.pumpAndSettle();

      expect(find.text('Obavezno polje'), findsOneWidget);
      expect(api.to('PUT', '/api/v1/me'), isEmpty);
    },
  );

  testWidgets(
    'the new password must be repeated correctly, nothing is sent otherwise',
    (tester) async {
      final (api, _) = await _open(tester);
      await _openDialog(tester);

      await tester.enterText(find.byKey(const Key('pw_current')), 'Stara_1!aa');
      await tester.enterText(find.byKey(const Key('pw_new')), 'Nova_Lozinka1!');
      await tester.enterText(
        find.byKey(const Key('pw_repeat')),
        'Druga_Lozinka1!',
      );
      await tester.tap(find.byKey(const Key('pw_submit')));
      await tester.pumpAndSettle();

      expect(find.text('Lozinke se ne podudaraju.'), findsOneWidget);
      expect(api.to('POST', '/change-password'), isEmpty);
    },
  );

  testWidgets(
    'a correct change sends both passwords and signs the person out',
    (tester) async {
      final (api, auth) = await _open(tester);
      await _openDialog(tester);

      await tester.enterText(find.byKey(const Key('pw_current')), 'Stara_1!aa');
      await tester.enterText(find.byKey(const Key('pw_new')), 'Nova_Lozinka1!');
      await tester.enterText(
        find.byKey(const Key('pw_repeat')),
        'Nova_Lozinka1!',
      );
      await tester.tap(find.byKey(const Key('pw_submit')));
      await tester.pumpAndSettle();

      final sent = api.to('POST', '/change-password').single.data as Map;
      expect(sent, {
        'currentPassword': 'Stara_1!aa',
        'newPassword': 'Nova_Lozinka1!',
      });
      expect(
        auth.status,
        AuthStatus.unauthenticated,
        reason: 'the server ended every session',
      );
      expect(
        find.text('Lozinka je promijenjena. Prijavite se.'),
        findsOneWidget,
      );
    },
  );

  testWidgets(
    'a wrong current password shows the server message and keeps the session',
    (tester) async {
      final (api, auth) = await _open(tester);
      api.changeStatus = 400;
      await _openDialog(tester);

      await tester.enterText(
        find.byKey(const Key('pw_current')),
        'Pogresna_1!',
      );
      await tester.enterText(find.byKey(const Key('pw_new')), 'Nova_Lozinka1!');
      await tester.enterText(
        find.byKey(const Key('pw_repeat')),
        'Nova_Lozinka1!',
      );
      await tester.tap(find.byKey(const Key('pw_submit')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('pw_error')), findsOneWidget);
      expect(find.text('Trenutna lozinka nije ispravna.'), findsOneWidget);
      expect(auth.status, AuthStatus.authenticated);
    },
  );

  testWidgets('empty fields are refused under each field', (tester) async {
    final (api, _) = await _open(tester);
    await _openDialog(tester);

    await tester.tap(find.byKey(const Key('pw_submit')));
    await tester.pumpAndSettle();

    expect(find.text('Obavezno polje'), findsNWidgets(3));
    expect(api.to('POST', '/change-password'), isEmpty);
  });
}

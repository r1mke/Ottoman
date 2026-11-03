import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/app.dart';

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  int announceStatus = 201;
  final sent = <Map<String, dynamic>>[
    {
      'id': 1,
      'title': 'Ranija akcija',
      'body': 'Šišanje -10%',
      'audience': 'Clients',
      'sendEmail': true,
      'recipientCount': 12,
      'createdAtUtc': DateTime.utc(2030, 1, 5, 9).toIso8601String(),
    },
  ];

  Iterable<RequestOptions> where(String method, String part) =>
      requests.where((r) => r.method == method && r.path.contains(part));

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
    Object? body;
    var status = 200;
    final path = o.path;
    if (path.endsWith('/login')) {
      body = {
        'accessToken': 'a',
        'refreshToken': 'r',
        'accessTokenExpiresUtc': DateTime.utc(2030).toIso8601String(),
        'user': {
          'id': 'a1',
          'email': 'x@y.z',
          'firstName': 'Admin',
          'lastName': 'Ottoman',
          'phone': null,
          'roles': ['Admin'],
        },
      };
    } else if (path.endsWith('/announcements') && o.method == 'POST') {
      status = announceStatus;
      body = announceStatus == 201
          ? {
              'id': 2,
              'title': 'x',
              'body': 'y',
              'audience': 'Clients',
              'sendEmail': false,
              'recipientCount': 3,
              'createdAtUtc': DateTime.utc(2030, 1, 6).toIso8601String(),
            }
          : {
              'status': 400,
              'title': 'Podaci nisu ispravni.',
              'detail': 'Odabrani korisnik ne postoji.',
            };
    } else if (path.endsWith('/announcements')) {
      body = {'items': sent, 'totalCount': sent.length};
    } else if (path.endsWith('/clients')) {
      body = {
        'items': [
          {
            'id': 'c9',
            'fullName': 'Elmir Mujkić',
            'email': 'elmir@x.ba',
            'phone': null,
          },
        ],
        'totalCount': 1,
      };
    } else {
      body = [];
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

Future<_Api> _open(WidgetTester tester) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(1600, 1000);
  addTearDown(tester.view.reset);

  final api = _Api();
  late final AuthController auth;
  auth = AuthController(
    baseUrl: 'http://test',
    storage: MemoryTokenStorage(),
    allowedRoles: {Roles.admin, Roles.employee},
    api: ApiClient(
      baseUrl: 'http://test',
      dio: Dio()..httpClientAdapter = api,
      readToken: () async => auth.accessToken,
    ),
  );
  await auth.restore();
  await tester.pumpWidget(
    OttomanDesktopApp(
      auth: auth,
      profiles: SavedProfiles(MemorySavedProfilesStore()),
    ),
  );
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('login_email')), 'x@y.z');
  await tester.enterText(find.byKey(const Key('login_password')), 'pw');
  await tester.tap(find.byKey(const Key('login_submit')));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Notifikacije').first);
  await tester.pumpAndSettle();
  return api;
}

void main() {
  testWidgets('shows the history of sent messages', (tester) async {
    await _open(tester);

    expect(find.byKey(const Key('sent_1')), findsOneWidget);
    expect(find.text('Ranija akcija'), findsOneWidget);
    expect(find.textContaining('12 primalaca'), findsOneWidget);
  });

  testWidgets('sending to all clients posts the audience and clears the form', (
    tester,
  ) async {
    final api = await _open(tester);

    await tester.enterText(find.byKey(const Key('msg_title')), 'Nova akcija');
    await tester.enterText(
      find.byKey(const Key('msg_body')),
      'Svi termini -10%',
    );
    await tester.tap(find.byKey(const Key('msg_email')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('msg_send')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_action')));
    await tester.pumpAndSettle();

    final sent = api.where('POST', '/announcements').single.data as Map;
    expect(sent['title'], 'Nova akcija');
    expect(sent['audience'], 'Clients');
    expect(sent['sendEmail'], true);
    expect(sent['targetUserId'], isNull);
    expect(find.text('Poruka je poslana.'), findsOneWidget);
    expect(
      tester
          .widget<TextFormField>(find.byKey(const Key('msg_title')))
          .controller!
          .text,
      isEmpty,
    );
    // history was reloaded
    expect(api.where('GET', '/announcements').length, greaterThanOrEqualTo(2));
  });

  testWidgets(
    'a message to a whole group asks first; saying no sends nothing',
    (tester) async {
      final api = await _open(tester);
      await tester.enterText(find.byKey(const Key('msg_title')), 'Nova akcija');
      await tester.enterText(find.byKey(const Key('msg_body')), 'Svi -10%');
      await tester.tap(find.byKey(const Key('msg_send')));
      await tester.pumpAndSettle();
      expect(find.textContaining('Klijenti'), findsWidgets);
      await tester.tap(find.byKey(const Key('confirm_action_cancel')));
      await tester.pumpAndSettle();
      expect(api.where('POST', '/announcements'), isEmpty);
      expect(
        find.text('Nova akcija'),
        findsOneWidget,
        reason: 'the text is kept',
      );
    },
  );

  testWidgets('empty title or message is caught before any request', (
    tester,
  ) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('msg_send')));
    await tester.pumpAndSettle();

    expect(api.where('POST', '/announcements'), isEmpty);
    expect(find.text('Obavezno polje'), findsNWidgets(2));
  });

  testWidgets(
    'a personal message needs a chosen client and targets that client',
    (tester) async {
      final api = await _open(tester);

      await tester.tap(find.byKey(const Key('audience')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Jedan korisnik').last);
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('msg_title')), 'Pozdrav');
      await tester.enterText(find.byKey(const Key('msg_body')), 'Zdravo');

      // no client chosen yet
      await tester.tap(find.byKey(const Key('msg_send')));
      await tester.pumpAndSettle();
      expect(api.where('POST', '/announcements'), isEmpty);
      expect(find.byKey(const Key('msg_error')), findsOneWidget);

      await tester.enterText(find.byKey(const Key('target_search')), 'elmir');
      await tester.testTextInput.receiveAction(TextInputAction.done);
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('target_c9')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('msg_send')));
      await tester.pumpAndSettle();

      final sent = api.where('POST', '/announcements').single.data as Map;
      expect(sent['audience'], 'User');
      expect(sent['targetUserId'], 'c9');
    },
  );

  testWidgets('a server refusal is shown and the text is kept', (tester) async {
    final api = await _open(tester);
    api.announceStatus = 400;

    await tester.enterText(find.byKey(const Key('msg_title')), 'T');
    await tester.enterText(find.byKey(const Key('msg_body')), 'B');
    await tester.tap(find.byKey(const Key('msg_send')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_action')));
    await tester.pumpAndSettle();

    expect(find.text('Odabrani korisnik ne postoji.'), findsOneWidget);
    expect(
      tester
          .widget<TextFormField>(find.byKey(const Key('msg_title')))
          .controller!
          .text,
      'T',
    );
  });
}

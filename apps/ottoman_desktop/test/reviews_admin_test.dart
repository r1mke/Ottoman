import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/app.dart';

Map<String, dynamic> _review(int id, {bool hidden = false, String? reply}) => {
  'id': id,
  'targetType': 'Employee',
  'targetKey': 'e1',
  'targetName': 'Kerim Begić',
  'authorName': 'Ana A.',
  'rating': id == 1 ? 5 : 1,
  'comment': id == 1 ? 'Odlično' : 'Loše iskustvo',
  'createdAtUtc': DateTime.utc(2030, 1, 7).toIso8601String(),
  'isHidden': hidden,
  'replyText': reply,
  'isMine': false,
};

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  final hidden = <int>{};

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
    } else if (path.endsWith('/reviews/all')) {
      final only = o.queryParameters['onlyHidden'] == true;
      final items = [
        _review(1),
        _review(2, hidden: hidden.contains(2)),
      ].where((r) => !only || r['isHidden'] == true).toList();
      body = {'items': items, 'totalCount': items.length};
    } else if (path.endsWith('/hide')) {
      final id = int.parse(path.split('/')[path.split('/').length - 2]);
      final want = (o.data as Map)['hidden'] == true;
      want ? hidden.add(id) : hidden.remove(id);
      body = _review(id, hidden: want);
    } else if (path.endsWith('/reply')) {
      body = _review(1, reply: (o.data as Map)['text'] as String);
    } else if (o.method == 'DELETE') {
      status = 204;
    } else {
      body = [];
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
  await tester.tap(find.text('Recenzije').first);
  await tester.pumpAndSettle();
  return api;
}

void main() {
  testWidgets('lists every review with its target', (tester) async {
    await _open(tester);

    expect(find.byKey(const Key('review_1')), findsOneWidget);
    expect(find.byKey(const Key('review_2')), findsOneWidget);
    expect(find.text('Loše iskustvo'), findsOneWidget);
    expect(find.textContaining('Kerim Begić · Employee'), findsNWidgets(2));
  });

  testWidgets('hiding sends the flag and the "only hidden" filter shows it', (
    tester,
  ) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('hide_2')));
    await tester.pumpAndSettle();
    expect((api.where('PUT', '/hide').single.data as Map)['hidden'], true);

    await tester.tap(find.byKey(const Key('only_hidden')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('review_2')), findsOneWidget);
    expect(find.byKey(const Key('review_1')), findsNothing);
  });

  testWidgets('replying posts the text', (tester) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('reply_1')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('reply_text')), 'Hvala vam!');
    await tester.tap(find.byKey(const Key('reply_send')));
    await tester.pumpAndSettle();

    expect(
      (api.where('POST', '/reply').single.data as Map)['text'],
      'Hvala vam!',
    );
    expect(find.text('Odgovor je spremljen.'), findsOneWidget);
  });

  testWidgets('deleting asks first', (tester) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('delete_2')));
    await tester.pumpAndSettle();
    expect(api.where('DELETE', '/reviews/2'), isEmpty);

    await tester.tap(find.byKey(const Key('confirm_delete_review')));
    await tester.pumpAndSettle();
    expect(api.where('DELETE', '/reviews/2'), hasLength(1));
  });
}

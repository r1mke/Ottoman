import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/features/images/picture_editor.dart';
import 'package:ottoman_desktop/features/notifications/notifications_page.dart';

/// A real 1x1 PNG, so the preview can decode it.
final _png = base64Decode(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
);

class _Chooser implements ImageChooser {
  @override
  Future<ChosenImage?> choose() async => ChosenImage('vijest.png', _png);
}

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];

  RequestOptions? last(String method, String part) => requests.reversed
      .where((r) => r.method == method && r.path.contains(part))
      .firstOrNull;

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
    Object? body;
    var status = 200;
    if (o.method == 'POST' && o.path.contains('/announcements')) {
      status = 201;
      body = {
        'id': 9,
        'title': 'x',
        'body': 'y',
        'audience': 'Clients',
        'sendEmail': false,
        'recipientCount': 3,
        'createdAtUtc': DateTime.utc(2030, 1, 6).toIso8601String(),
      };
    } else if (o.path.endsWith('/announcements')) {
      body = {
        'items': [
          {
            'id': 1,
            'title': 'Sa slikom',
            'body': 'Tekst',
            'audience': 'Clients',
            'sendEmail': false,
            'recipientCount': 4,
            'createdAtUtc': DateTime.utc(2030, 1, 5).toIso8601String(),
            'imageUrl': 'https://img/vijest.jpg',
          },
          {
            'id': 2,
            'title': 'Bez slike',
            'body': 'Tekst',
            'audience': 'Clients',
            'sendEmail': false,
            'recipientCount': 4,
            'createdAtUtc': DateTime.utc(2030, 1, 4).toIso8601String(),
          },
        ],
        'totalCount': 2,
      };
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
  tester.view.physicalSize = const Size(1600, 1200);
  addTearDown(tester.view.reset);
  final api = _Api();
  final client = ApiClient(
    baseUrl: 'http://test',
    dio: Dio()..httpClientAdapter = api,
    readToken: () async => 't',
  );
  await tester.pumpWidget(
    MaterialApp(
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      locale: const Locale('bs'),
      home: Scaffold(
        body: NotificationsPage(
          api: NotificationsApi(client),
          reservations: ReservationsApi(client),
          chooser: _Chooser(),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return api;
}

void main() {
  testWidgets('a picture can be added to a news item, previewed and removed', (
    tester,
  ) async {
    await _open(tester);

    expect(find.byKey(const Key('msg_picture_preview')), findsNothing);
    await tester.tap(find.byKey(const Key('msg_picture_add')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('msg_picture_preview')), findsOneWidget);

    await tester.tap(find.byKey(const Key('msg_picture_remove')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('msg_picture_preview')), findsNothing);
  });

  testWidgets('with a picture the news goes as a form, without it as JSON, and '
      'the form is emptied afterwards', (tester) async {
    final api = await _open(tester);

    await tester.enterText(find.byKey(const Key('msg_title')), 'Ljetna akcija');
    await tester.enterText(find.byKey(const Key('msg_body')), 'Šišanje -10%');
    await tester.tap(find.byKey(const Key('msg_picture_add')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('msg_send')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_action')));
    await tester.pumpAndSettle();

    final form =
        api.last('POST', '/announcements/with-image')!.data as FormData;
    expect(
      form.fields.map((f) => f.key),
      containsAll(['title', 'body', 'audience', 'sendEmail']),
    );
    expect(form.files.single.key, 'file');
    expect(find.byKey(const Key('msg_picture_preview')), findsNothing);

    await tester.enterText(find.byKey(const Key('msg_title')), 'Bez slike');
    await tester.enterText(find.byKey(const Key('msg_body')), 'Samo tekst');
    await tester.tap(find.byKey(const Key('msg_send')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_action')));
    await tester.pumpAndSettle();
    final plain = api.requests.lastWhere(
      (r) => r.method == 'POST' && r.path.endsWith('/announcements'),
    );
    expect((plain.data as Map)['title'], 'Bez slike');
  });

  testWidgets(
    'the history shows the picture of a news item next to its title',
    (tester) async {
      await _open(tester);

      expect(find.byKey(const Key('sent_picture_1')), findsOneWidget);
      expect(find.byKey(const Key('sent_picture_2')), findsNothing);
      expect(find.byIcon(Icons.campaign_outlined), findsOneWidget);
    },
  );
}

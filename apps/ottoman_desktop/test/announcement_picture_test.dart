import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/features/images/picture_editor.dart';
import 'package:ottoman_desktop/features/notifications/notifications_page.dart';

class _Chooser implements ImageChooser {
  _Chooser(this.next);
  ChosenImage? next;

  @override
  Future<ChosenImage?> choose() async => next;
}

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  int imageStatus = 200;

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
    if (p.endsWith('/image')) {
      status = imageStatus;
      body = imageStatus == 200
          ? _announcement(7, image: 'https://res.cloudinary.com/x/y.png')
          : {
              'status': 400,
              'title': 'Podaci nisu ispravni.',
              'detail': 'Dozvoljene su samo JPEG, PNG i WebP slike.',
            };
    } else if (p.endsWith('/announcements') && o.method == 'POST') {
      status = 201;
      body = _announcement(7);
    } else if (p.endsWith('/announcements')) {
      body = {
        'items': [
          _announcement(3, image: 'https://res.cloudinary.com/x/old.png'),
          _announcement(2),
        ],
        'totalCount': 2,
      };
    } else {
      body = {'items': [], 'totalCount': 0};
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

Map<String, dynamic> _announcement(int id, {String? image}) => {
  'id': id,
  'title': 'Vijest $id',
  'body': 'tekst',
  'audience': 'Clients',
  'sendEmail': false,
  'recipientCount': 3,
  'createdAtUtc': DateTime.utc(2030, 1, 6).toIso8601String(),
  'imageUrl': image,
};

// A real 1x1 PNG so the preview can decode it.
final _png = base64Decode(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==',
);

Future<_Api> _open(WidgetTester tester, ImageChooser chooser) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(1500, 1000);
  addTearDown(tester.view.reset);
  final api = _Api();
  final client = ApiClient(
    baseUrl: 'http://test',
    dio: Dio()..httpClientAdapter = api,
    readToken: () async => 't',
  );
  await tester.pumpWidget(
    MaterialApp(
      theme: WorkspaceTheme.build(),
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
      locale: const Locale('bs'),
      home: Scaffold(
        body: NotificationsPage(
          api: NotificationsApi(client),
          reservations: ReservationsApi(client),
          chooser: chooser,
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return api;
}

Future<void> _fillAndSend(WidgetTester tester) async {
  await tester.enterText(find.byKey(const Key('msg_title')), 'Nova akcija');
  await tester.enterText(
    find.byKey(const Key('msg_body')),
    'Šišanje -10% ove sedmice',
  );
  await tester.ensureVisible(find.byKey(const Key('msg_send')));
  await tester.tap(find.byKey(const Key('msg_send')));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets(
    'the history shows each message with its picture or a placeholder',
    (tester) async {
      await _open(tester, _Chooser(null));

      expect(find.byKey(const Key('sent_picture_3')), findsOneWidget);
      expect(find.byKey(const Key('sent_picture_2')), findsOneWidget);
      expect(find.byKey(const Key('picture-placeholder')), findsWidgets);
    },
  );

  testWidgets(
    'a chosen picture is previewed, uploaded after the message is sent, and the form is cleared',
    (tester) async {
      final api = await _open(
        tester,
        _Chooser(ChosenImage('akcija.png', _png)),
      );

      await tester.ensureVisible(find.byKey(const Key('msg_picture')));
      await tester.tap(find.byKey(const Key('msg_picture')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('msg_picture_preview')), findsOneWidget);

      await _fillAndSend(tester);

      expect(api.to('POST', '/announcements'), hasLength(1));
      final upload = api.to('PUT', '/announcements/7/image').single;
      expect(upload.data, isA<FormData>());
      expect(
        (upload.data as FormData).files.single.value.filename,
        'akcija.png',
      );
      expect(
        find.byKey(const Key('msg_picture_preview')),
        findsNothing,
        reason: 'the form starts empty again',
      );
      expect(
        tester
            .widget<TextFormField>(find.byKey(const Key('msg_title')))
            .controller!
            .text,
        isEmpty,
      );
    },
  );

  testWidgets('without a picture only the message is sent', (tester) async {
    final api = await _open(tester, _Chooser(null));

    await _fillAndSend(tester);

    expect(api.to('POST', '/announcements'), hasLength(1));
    expect(api.to('PUT', '/image'), isEmpty);
  });

  testWidgets('a picture can be taken back before sending', (tester) async {
    final api = await _open(tester, _Chooser(ChosenImage('a.png', _png)));
    await tester.ensureVisible(find.byKey(const Key('msg_picture')));
    await tester.tap(find.byKey(const Key('msg_picture')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('msg_picture_remove')));
    await tester.pumpAndSettle();
    await _fillAndSend(tester);

    expect(find.byKey(const Key('msg_picture_preview')), findsNothing);
    expect(api.to('PUT', '/image'), isEmpty);
  });

  testWidgets(
    'if the picture is refused the message still went out and the reason is shown',
    (tester) async {
      final api = await _open(tester, _Chooser(ChosenImage('a.png', _png)));
      api.imageStatus = 400;
      await tester.ensureVisible(find.byKey(const Key('msg_picture')));
      await tester.tap(find.byKey(const Key('msg_picture')));
      await tester.pumpAndSettle();

      await _fillAndSend(tester);

      expect(api.to('POST', '/announcements'), hasLength(1));
      expect(find.textContaining('Slika nije spremljena'), findsOneWidget);
      expect(find.textContaining('JPEG, PNG i WebP'), findsOneWidget);
    },
  );

  testWidgets('cancelling the file dialog keeps the form as it was', (
    tester,
  ) async {
    await _open(tester, _Chooser(null));

    await tester.ensureVisible(find.byKey(const Key('msg_picture')));
    await tester.tap(find.byKey(const Key('msg_picture')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('msg_picture_preview')), findsNothing);
    expect(find.text('Dodaj sliku'), findsOneWidget);
  });
}

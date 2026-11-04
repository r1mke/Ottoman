import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_mobile/app.dart';
import 'package:ottoman_mobile/features/profile/profile_photo.dart';

/// Remembers the account's picture like the server does.
class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  String? photoUrl;
  double photoZoom = 1.0;
  int photoStatus = 200;

  /// Like the server: the crop value in the URL is the inverse of the zoom.
  String? get croppedUrl => photoUrl == null
      ? null
      : '$photoUrl'.replaceFirst(
          'z_1',
          'z_${(1 / photoZoom).toStringAsFixed(2).replaceFirst(RegExp(r'\.?0+$'), '')}',
        );

  Iterable<RequestOptions> to(String method, String pathEnd) =>
      requests.where((r) => r.method == method && r.path.endsWith(pathEnd));

  Map<String, dynamic> get user => {
    'id': 'c1',
    'email': 'ana@example.com',
    'firstName': 'Ana',
    'lastName': 'Anić',
    'phone': null,
    'roles': ['Client'],
    'photoUrl': croppedUrl,
    'photoZoom': photoZoom,
  };

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
        'user': user,
      };
    } else if (p.endsWith('/me/photo/zoom')) {
      photoZoom = ((o.data as Map)['photoZoom'] as num).toDouble();
      body = user;
    } else if (p.endsWith('/me/photo')) {
      if (photoStatus != 200) {
        status = photoStatus;
        body = {
          'status': photoStatus,
          'title': 'Podaci nisu ispravni.',
          'detail': 'Datoteka nije slika.',
        };
      } else {
        photoUrl = o.method == 'PUT'
            ? 'https://res.cloudinary.com/demo/image/upload/c_thumb,g_face,z_1,q_auto/ana'
            : null;
        photoZoom = 1.0;
        body = user;
      }
    } else if (p.endsWith('/me')) {
      body = user;
    } else if (p.endsWith('/salon')) {
      body = {
        'name': 'Ottoman',
        'hours': [
          for (final d in [1, 2, 3, 4, 5, 6, 0])
            {'day': d, 'openMinutes': null, 'closeMinutes': null},
        ],
      };
    } else if (p.contains('/gallery')) {
      body = {'items': [], 'totalCount': 0};
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

class _Chooser extends PhotoChooser {
  bool cancel = false;

  @override
  Future<PickedPhoto?> choose() async =>
      cancel ? null : const PickedPhoto([1, 2, 3], 'ana.jpg');
}

Future<_Api> _openAccount(WidgetTester tester, _Chooser chooser) async {
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
  await tester.pumpWidget(OttomanMobileApp(auth: auth, photoChooser: chooser));
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('login_email')), 'x@y.z');
  await tester.enterText(find.byKey(const Key('login_password')), 'pw');
  await tester.tap(find.byKey(const Key('login_submit')));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Račun').last);
  await tester.pumpAndSettle();
  return api;
}

void main() {
  testWidgets('a client sets a profile picture and removes it again', (
    tester,
  ) async {
    final chooser = _Chooser();
    final api = await _openAccount(tester, chooser);

    expect(find.byKey(const Key('profile_photo')), findsOneWidget);
    expect(find.byKey(const Key('photo_remove')), findsNothing);

    await tester.tap(find.byKey(const Key('photo_change')));
    await tester.pumpAndSettle();
    final put = api.to('PUT', '/me/photo').single;
    expect(put.data, isA<FormData>());
    expect((put.data as FormData).files.single.value.filename, 'ana.jpg');
    // The new picture opens in the zoom editor; backing out keeps the default crop.
    expect(find.byKey(const Key('avatar_zoom_slider')), findsOneWidget);
    await tester.tap(find.byKey(const Key('avatar_zoom_cancel')));
    await tester.pumpAndSettle();
    expect(api.to('PUT', '/me/photo/zoom'), isEmpty);
    expect(find.byKey(const Key('photo_remove')), findsOneWidget);

    await tester.tap(find.byKey(const Key('photo_remove')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_action')));
    await tester.pumpAndSettle();
    expect(api.to('DELETE', '/me/photo'), hasLength(1));
    expect(find.byKey(const Key('photo_remove')), findsNothing);
  });

  testWidgets('backing out of the gallery sends nothing; a refused file is '
      'explained', (tester) async {
    final chooser = _Chooser()..cancel = true;
    final api = await _openAccount(tester, chooser);

    await tester.tap(find.byKey(const Key('photo_change')));
    await tester.pumpAndSettle();
    expect(api.to('PUT', '/me/photo'), isEmpty);

    chooser.cancel = false;
    api.photoStatus = 400;
    await tester.tap(find.byKey(const Key('photo_change')));
    await tester.pumpAndSettle();
    expect(find.text('Datoteka nije slika.'), findsOneWidget);
    expect(find.byKey(const Key('photo_remove')), findsNothing);
  });

  testWidgets('a new picture is zoomed in the editor before it is shown', (
    tester,
  ) async {
    final api = await _openAccount(tester, _Chooser());

    await tester.tap(find.byKey(const Key('photo_change')));
    await tester.pumpAndSettle();
    expect(api.to('PUT', '/me/photo'), hasLength(1));
    // The editor previews the server's crop of the uploaded picture.
    expect(
      tester
          .widget<PersonAvatar>(find.byKey(const Key('avatar_zoom_preview')))
          .photoUrl,
      contains('z_1,'),
    );

    await tester.drag(
      find.byKey(const Key('avatar_zoom_slider')),
      const Offset(2000, 0),
    );
    await tester.pumpAndSettle();
    expect(find.text('2.0×'), findsOneWidget);
    await tester.tap(find.byKey(const Key('avatar_zoom_save')));
    await tester.pumpAndSettle();

    final zoom = api.to('PUT', '/me/photo/zoom').single;
    expect(zoom.data, {'photoZoom': 2.0});
    expect(
      tester
          .widget<PersonAvatar>(find.byKey(const Key('profile_photo')))
          .photoUrl,
      contains('z_0.5,'),
    );
  });

  testWidgets('the zoom button adjusts the current picture; an unchanged zoom '
      'sends nothing', (tester) async {
    final api = await _openAccount(tester, _Chooser());
    expect(find.byKey(const Key('photo_zoom')), findsNothing);
    await tester.tap(find.byKey(const Key('photo_change')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('avatar_zoom_cancel')));
    await tester.pumpAndSettle();

    // Saving the zoom it already has is not a change.
    await tester.tap(find.byKey(const Key('photo_zoom')));
    await tester.pumpAndSettle();
    expect(find.text('1.0×'), findsOneWidget);
    await tester.tap(find.byKey(const Key('avatar_zoom_save')));
    await tester.pumpAndSettle();
    expect(api.to('PUT', '/me/photo/zoom'), isEmpty);

    await tester.tap(find.byKey(const Key('photo_zoom')));
    await tester.pumpAndSettle();
    await tester.drag(
      find.byKey(const Key('avatar_zoom_slider')),
      const Offset(2000, 0),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('avatar_zoom_save')));
    await tester.pumpAndSettle();
    expect(api.to('PUT', '/me/photo/zoom').single.data, {'photoZoom': 2.0});

    // Opening it again starts from the saved zoom; reset brings it back to 1.
    await tester.tap(find.byKey(const Key('photo_zoom')));
    await tester.pumpAndSettle();
    expect(find.text('2.0×'), findsOneWidget);
    await tester.tap(find.byKey(const Key('avatar_zoom_reset')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('avatar_zoom_save')));
    await tester.pumpAndSettle();
    expect(api.to('PUT', '/me/photo/zoom').last.data, {'photoZoom': 1.0});
    expect(
      tester
          .widget<PersonAvatar>(find.byKey(const Key('profile_photo')))
          .photoUrl,
      contains('z_1,'),
    );
  });
}

import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/features/employees/employee_dialogs.dart';
import 'package:ottoman_desktop/features/images/picture_editor.dart';
import 'package:ottoman_desktop/features/profile/own_photo_section.dart';
import 'package:ottoman_desktop/features/profile/profile_page.dart';

String _avatar(double zoom) {
  final z = (1 / zoom).toStringAsFixed(2).replaceFirst(RegExp(r'\.?0+$'), '');
  return 'https://res.cloudinary.com/demo/image/upload/c_thumb,g_face,w_512,h_512,z_$z,q_auto,f_auto/ottoman/employees/me';
}

class _Server implements HttpClientAdapter {
  _Server({this.role = 'Employee', this.hasPhoto = true});

  final String role;
  bool hasPhoto;
  double zoom = 1.0;
  String? gender = 'Female';
  String? bio = 'Stari opis';
  final requests = <RequestOptions>[];

  Iterable<RequestOptions> sent(String method, String part) =>
      requests.where((r) => r.method == method && r.path.contains(part));

  Map<String, dynamic> get _user => {
    'id': 'b1',
    'email': 'selma@ottoman.local',
    'firstName': 'Selma',
    'lastName': 'Brijač',
    'phone': null,
    'roles': [role],
    'photoUrl': hasPhoto ? _avatar(zoom) : null,
    'gender': gender,
    'bio': bio,
    'photoZoom': zoom,
  };

  Map<String, dynamic> get _employee => {
    'id': 'b1',
    'firstName': 'Selma',
    'lastName': 'Brijač',
    'fullName': 'Selma Brijač',
    'title': 'Barber',
    'photoUrl': hasPhoto ? _avatar(zoom) : null,
    'photoZoom': zoom,
    'gender': gender,
    'isActive': true,
    'serviceIds': <int>[],
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
    if (p.endsWith('/auth/login')) {
      body = {
        'accessToken': 'a',
        'refreshToken': 'r',
        'accessTokenExpiresUtc': DateTime.utc(2031).toIso8601String(),
        'user': _user,
      };
    } else if (p.endsWith('/me') && o.method == 'GET') {
      body = _user;
    } else if (p.endsWith('/me') && o.method == 'PUT') {
      body = _user;
    } else if (p.endsWith('/me/employee-profile')) {
      final d = o.data as Map;
      if (d.containsKey('photoZoom') && d['photoZoom'] != null) {
        zoom = (d['photoZoom'] as num).toDouble();
      }
      if (d.containsKey('gender') && d['gender'] != null) {
        gender = d['gender'] as String;
      }
      if (d.containsKey('bio')) bio = d['bio'] as String?;
      body = _user;
    } else if (p.endsWith('/me/photo') && o.method == 'PUT') {
      hasPhoto = true;
      zoom = 1.0;
      body = _user;
    } else if (p.endsWith('/me/photo') && o.method == 'DELETE') {
      hasPhoto = false;
      zoom = 1.0;
      body = _user;
    } else if (p.endsWith('/employee-titles')) {
      body = <Object>[
        {'id': 1, 'name': 'Barber', 'inUse': 1},
      ];
    } else if (p.endsWith('/employees/b1/photo') && o.method == 'PUT') {
      hasPhoto = true;
      zoom = 1.0;
      body = _employee;
    } else if (p.endsWith('/employees/b1/photo') && o.method == 'DELETE') {
      hasPhoto = false;
      zoom = 1.0;
      body = _employee;
    } else if (p.endsWith('/employees/b1') && o.method == 'PUT') {
      final d = o.data as Map;
      if (d['photoZoom'] != null) zoom = (d['photoZoom'] as num).toDouble();
      body = _employee;
    } else {
      status = 204;
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

class _Chooser implements ImageChooser {
  @override
  Future<ChosenImage?> choose() async =>
      ChosenImage('me.png', Uint8List.fromList([0x89, 0x50, 0x4E, 0x47]));
}

({AuthController auth, ApiClient client}) _make(_Server server) {
  late final AuthController auth;
  final client = ApiClient(
    baseUrl: 'http://test',
    dio: Dio()..httpClientAdapter = server,
    readToken: () async => auth.accessToken,
  );
  auth = AuthController(
    baseUrl: 'http://test',
    storage: MemoryTokenStorage(),
    api: client,
  );
  return (auth: auth, client: client);
}

Widget _app(Widget home) => MaterialApp(
  theme: WorkspaceTheme.build(),
  locale: const Locale('bs'),
  localizationsDelegates: AppLocalizations.localizationsDelegates,
  supportedLocales: AppLocalizations.supportedLocales,
  home: Scaffold(body: home),
);

void _tall(WidgetTester tester) {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(1200, 2200);
  addTearDown(tester.view.reset);
}

Future<(_Server, AuthController)> _openProfile(
  WidgetTester tester, {
  _Server? server,
}) async {
  _tall(tester);
  final s = server ?? _Server();
  final m = _make(s);
  await tester.runAsync(() => m.auth.login('selma@ottoman.local', 'pw'));
  await tester.pumpWidget(_app(ProfilePage(auth: m.auth)));
  await tester.pumpAndSettle();
  return (s, m.auth);
}

void main() {
  testWidgets(
    'a barber sees the picture (no zoom), a locked e-mail, gender and bio of their own',
    (tester) async {
      await _openProfile(tester);

      expect(find.byKey(const Key('own_zoom')), findsNothing);
      expect(find.byKey(const Key('avatar_zoom_slider')), findsNothing);
      final email = tester.widget<TextFormField>(
        find.byKey(const Key('profile_email')),
      );
      expect(email.enabled, isFalse);
      expect(find.text('selma@ottoman.local'), findsOneWidget);
      expect(
        find.text(
          AppLocalizations.of(tester.element(find.byType(ProfileForm)))
              .pictureHint,
        ),
        findsNothing,
      );
      expect(find.byKey(const Key('profile_gender')), findsOneWidget);
      expect(find.byKey(const Key('profile_bio')), findsOneWidget);
      expect(find.text('Stari opis'), findsOneWidget);
    },
  );

  testWidgets(
    'an admin sees the same profile as a barber: picture, gender and bio',
    (tester) async {
      await _openProfile(
        tester,
        server: _Server(role: 'Admin', hasPhoto: false),
      );

      expect(find.byKey(const Key('add_picture')), findsOneWidget);
      expect(find.byKey(const Key('profile_gender')), findsOneWidget);
      expect(find.byKey(const Key('profile_bio')), findsOneWidget);
      expect(find.byKey(const Key('avatar_zoom_slider')), findsNothing);
    },
  );

  testWidgets(
    'a picture can be added to a profile without one and removed again',
    (tester) async {
      _tall(tester);
      final server = _Server(hasPhoto: false);
      final m = _make(server);
      await tester.runAsync(() => m.auth.login('selma@ottoman.local', 'pw'));
      await tester.pumpWidget(
        _app(
          SingleChildScrollView(
            child: ProfileForm(
              auth: m.auth,
              photoSection: OwnPhotoSection(auth: m.auth, chooser: _Chooser()),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('remove_picture_photo')), findsNothing);

      await tester.tap(find.byKey(const Key('add_picture')));
      await tester.pumpAndSettle();

      expect(server.sent('PUT', '/me/photo'), hasLength(1));
      expect(find.byKey(const Key('remove_picture_photo')), findsOneWidget);

      await tester.tap(find.byKey(const Key('remove_picture_photo')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm_action')));
      await tester.pumpAndSettle();
      expect(server.sent('DELETE', '/me/photo'), hasLength(1));
      expect(find.byKey(const Key('remove_picture_photo')), findsNothing);
    },
  );

  testWidgets('a non-Bosnian phone number is refused before anything is sent', (
    tester,
  ) async {
    final (server, _) = await _openProfile(tester);

    await tester.enterText(find.byKey(const Key('profile_phone')), '12345');
    await tester.ensureVisible(find.byKey(const Key('profile_save')));
    await tester.tap(find.byKey(const Key('profile_save')));
    await tester.pumpAndSettle();

    expect(
      find.textContaining('Unesite ispravan broj telefona'),
      findsOneWidget,
    );
    expect(server.sent('PUT', '/me/employee-profile'), isEmpty);
  });

  testWidgets(
    'saving the profile sends gender and bio to the barber endpoint',
    (tester) async {
      final (server, _) = await _openProfile(tester);

      await tester.ensureVisible(find.byKey(const Key('profile_gender')));
      await tester.tap(find.byKey(const Key('profile_gender')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Muški').last);
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const Key('profile_bio')),
        '  Novi opis  ',
      );
      await tester.ensureVisible(find.byKey(const Key('profile_save')));
      await tester.tap(find.byKey(const Key('profile_save')));
      await tester.pumpAndSettle();

      expect(
        server.sent('PUT', '/api/v1/me').where((r) => r.path.endsWith('/me')),
        hasLength(1),
      );
      final put = server.sent('PUT', '/me/employee-profile').single;
      expect((put.data as Map)['gender'], 'Male');
      expect((put.data as Map)['bio'], 'Novi opis');
    },
  );

  testWidgets('the admin dialog has no zoom and saves without one', (
    tester,
  ) async {
    _tall(tester);
    final server = _Server();
    final m = _make(server);
    await tester.pumpWidget(
      _app(
        Builder(
          builder: (context) => TextButton(
            onPressed: () => showDialog<void>(
              context: context,
              builder: (_) => EmployeeDialog(
                catalog: CatalogApi(m.client),
                employee: Employee.fromJson(server._employee),
                chooser: _Chooser(),
              ),
            ),
            child: const Text('open'),
          ),
        ),
      ),
    );
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('avatar_zoom_slider')), findsNothing);
    await tester.ensureVisible(find.text('Spremi'));
    await tester.tap(find.text('Spremi'));
    await tester.pumpAndSettle();

    final put = server
        .sent('PUT', '/employees/b1')
        .where((r) => !r.path.contains('photo'))
        .single;
    expect((put.data as Map)['photoZoom'], isNull);
  });
}

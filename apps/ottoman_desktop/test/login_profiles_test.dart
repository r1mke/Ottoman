import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/app.dart';
import 'package:ottoman_desktop/features/appointments/appointment_dialogs.dart';

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];

  static Map<String, dynamic> barber({
    String? photoUrl,
    String? gender = 'Male',
  }) => {
    'id': 'e1',
    'firstName': 'Kerim',
    'lastName': 'Begić',
    'fullName': 'Kerim Begić',
    'title': 'Master Barber',
    'photoUrl': photoUrl,
    'gender': gender,
    'isActive': true,
    'serviceIds': [1, 2],
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
      final sent = (o.data as Map)['password'];
      if (sent != 'pw') {
        status = 401;
        body = {'title': 'Pogrešan email ili lozinka.', 'status': 401};
      } else {
        body = {
          'accessToken': 'a',
          'refreshToken': 'r',
          'accessTokenExpiresUtc': DateTime.utc(2031).toIso8601String(),
          'user': {
            'id': 'a1',
            'email': (o.data as Map)['email'],
            'firstName': 'Kerim',
            'lastName': 'Begić',
            'phone': null,
            'roles': ['Admin'],
            'photoUrl': 'https://img.test/kerim.jpg',
            'gender': 'Male',
          },
        };
      }
    } else if (p.endsWith('/clients')) {
      body = {
        'items': [
          {
            'id': 'c1',
            'fullName': 'Elmir Mujkić',
            'email': 'elmir@ottoman.local',
          },
          {
            'id': 'c2',
            'fullName': 'Amra Hodžić',
            'email': 'amra@ottoman.local',
          },
        ],
        'totalCount': 2,
      };
    } else if (p.endsWith('/employees')) {
      body = [barber()];
    } else if (p.endsWith('/services')) {
      body = [
        {
          'id': 1,
          'name': 'Šišanje',
          'price': 25,
          'durationMinutes': 30,
          'isActive': true,
        },
        {
          'id': 2,
          'name': 'Brada',
          'price': 15,
          'durationMinutes': 30,
          'isActive': true,
        },
      ];
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

({_Api http, AuthController auth, ApiClient client}) _make() {
  final http = _Api();
  late final AuthController auth;
  final client = ApiClient(
    baseUrl: 'http://test',
    dio: Dio()..httpClientAdapter = http,
    readToken: () async => auth.accessToken,
  );
  auth = AuthController(
    baseUrl: 'http://test',
    storage: MemoryTokenStorage(),
    allowedRoles: {Roles.admin, Roles.employee},
    api: client,
  );
  return (http: http, auth: auth, client: client);
}

void _wide(WidgetTester tester) {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(1400, 900);
  addTearDown(tester.view.reset);
}

Future<SavedProfiles> _profiles(List<SavedProfile> saved) async {
  final p = SavedProfiles(MemorySavedProfilesStore(saved));
  await p.load();
  return p;
}

const _kerim = SavedProfile(
  email: 'kerim@ottoman.local',
  name: 'Kerim Begić',
  photoUrl: 'https://img.test/kerim.jpg',
  gender: 'Male',
);
const _amra = SavedProfile(
  email: 'amra@ottoman.local',
  name: 'Amra Alić',
  gender: 'Female',
);

void main() {
  testWidgets('earlier accounts are cards; a card asks only for the password', (
    tester,
  ) async {
    _wide(tester);
    final m = _make();
    await m.auth.restore();
    await tester.pumpWidget(
      OttomanDesktopApp(
        auth: m.auth,
        profiles: await _profiles([_kerim, _amra]),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Ko se prijavljuje?'), findsOneWidget);
    expect(find.text('Kerim Begić'), findsOneWidget);
    expect(find.text('Amra Alić'), findsOneWidget);
    expect(find.text('Dodaj račun'), findsOneWidget);
    expect(find.byKey(const Key('login_email')), findsNothing);

    await tester.tap(find.byKey(const Key('profile_kerim@ottoman.local')));
    await tester.pumpAndSettle();

    // No e-mail field: the card already says who this is.
    expect(find.byKey(const Key('login_email')), findsNothing);
    expect(find.byKey(const Key('login_password')), findsOneWidget);

    await tester.enterText(find.byKey(const Key('login_password')), 'pw');
    await tester.tap(find.byKey(const Key('login_submit')));
    await tester.pumpAndSettle();

    final login = m.http.requests.singleWhere((r) => r.path.endsWith('/login'));
    expect((login.data as Map)['email'], 'kerim@ottoman.local');
    expect(m.auth.status, AuthStatus.authenticated);
  });

  testWidgets('a card shows the barber photo, or a default by gender', (
    tester,
  ) async {
    _wide(tester);
    final m = _make();
    await m.auth.restore();
    await tester.pumpWidget(
      OttomanDesktopApp(
        auth: m.auth,
        profiles: await _profiles([_kerim, _amra]),
      ),
    );
    await tester.pumpAndSettle();

    final tiles = tester
        .widgetList<PersonAvatarTile>(find.byType(PersonAvatarTile))
        .toList();
    expect(tiles, hasLength(2));
    expect(tiles[0].photoUrl, 'https://img.test/kerim.jpg');
    expect(tiles[1].photoUrl, isNull);
    // Underneath the photo (or alone, when there is none) sits the drawn default for the gender.
    final assets = tester
        .widgetList<Image>(find.byKey(const Key('avatar-default')))
        .map((i) => (i.image as AssetImage).assetName)
        .toList();
    expect(assets[0], startsWith('assets/avatars/male_'));
    expect(assets[1], startsWith('assets/avatars/female_'));
  });

  testWidgets('a wrong password keeps the card and shows the error', (
    tester,
  ) async {
    _wide(tester);
    final m = _make();
    await m.auth.restore();
    await tester.pumpWidget(
      OttomanDesktopApp(auth: m.auth, profiles: await _profiles([_kerim])),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('profile_kerim@ottoman.local')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('login_password')), 'wrong');
    await tester.tap(find.byKey(const Key('login_submit')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('login_error')), findsOneWidget);
    expect(m.auth.status, AuthStatus.unauthenticated);

    await tester.tap(find.byKey(const Key('login_back')));
    await tester.pumpAndSettle();
    expect(find.text('Ko se prijavljuje?'), findsOneWidget);
  });

  testWidgets('the first sign-in is remembered with name, photo and gender', (
    tester,
  ) async {
    _wide(tester);
    final m = _make();
    await m.auth.restore();
    final store = MemorySavedProfilesStore();
    final profiles = SavedProfiles(store);
    await profiles.load();
    await tester.pumpWidget(
      OttomanDesktopApp(auth: m.auth, profiles: profiles),
    );
    await tester.pumpAndSettle();

    // Nothing remembered yet: the form for a first account is shown straight away, with no avatar to pick.
    expect(find.byKey(const Key('login_email')), findsOneWidget);
    expect(find.byKey(const Key('avatar_option_0')), findsNothing);
    await tester.enterText(
      find.byKey(const Key('login_email')),
      'new@ottoman.local',
    );
    await tester.enterText(find.byKey(const Key('login_password')), 'pw');
    await tester.tap(find.byKey(const Key('login_submit')));
    await tester.pumpAndSettle();

    final saved = await store.load();
    expect(saved, hasLength(1));
    expect(saved.single.email, 'new@ottoman.local');
    expect(saved.single.name, 'Kerim Begić');
    expect(saved.single.photoUrl, 'https://img.test/kerim.jpg');
    expect(saved.single.gender, 'Male');
    // Only what the card needs: nothing that could sign in on its own.
    expect(saved.single.toJson().keys, {
      'email',
      'name',
      'id',
      'photoUrl',
      'gender',
    });
  });

  testWidgets('the pencil switches to editing, where a card can be removed', (
    tester,
  ) async {
    _wide(tester);
    final m = _make();
    await m.auth.restore();
    final profiles = await _profiles([_kerim, _amra]);
    await tester.pumpWidget(
      OttomanDesktopApp(auth: m.auth, profiles: profiles),
    );
    await tester.pumpAndSettle();

    expect(
      find.byKey(const Key('profile_remove_kerim@ottoman.local')),
      findsNothing,
    );
    await tester.tap(find.byKey(const Key('profiles_manage')));
    await tester.pumpAndSettle();

    expect(find.text('Uredi račune'), findsOneWidget);
    expect(find.text('Dodaj račun'), findsNothing);
    // Tapping a card while editing does not start a sign-in.
    await tester.tapAt(
      tester.getTopLeft(find.byKey(const Key('profile_kerim@ottoman.local'))) +
          const Offset(12, 12),
    );
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('login_password')), findsNothing);

    await tester.tap(
      find.byKey(const Key('profile_remove_kerim@ottoman.local')),
    );
    await tester.pumpAndSettle();
    expect(profiles.items.map((p) => p.email), ['amra@ottoman.local']);
    expect(find.text('Kerim Begić'), findsNothing);

    await tester.tap(find.text('Gotovo'));
    await tester.pumpAndSettle();
    expect(find.text('Dodaj račun'), findsOneWidget);

    // Removing the last one leaves the form for a new account.
    await tester.tap(find.byKey(const Key('profiles_manage')));
    await tester.pumpAndSettle();
    await tester.tap(
      find.byKey(const Key('profile_remove_amra@ottoman.local')),
    );
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('login_email')), findsOneWidget);
  });

  test(
    'remember refreshes the photo and matches e-mails ignoring case',
    () async {
      final profiles = await _profiles([
        const SavedProfile(email: 'kerim@ottoman.local', name: 'Kerim'),
      ]);
      const user = AppUser(
        id: '1',
        email: 'Kerim@Ottoman.local',
        firstName: 'Kerim',
        lastName: 'Begić',
        roles: ['Admin'],
        photoUrl: 'https://img.test/new.jpg',
        gender: 'Male',
      );

      await profiles.remember(user);

      expect(profiles.items, hasLength(1));
      expect(profiles.items.single.name, 'Kerim Begić');
      expect(profiles.items.single.photoUrl, 'https://img.test/new.jpg');
    },
  );

  Future<void> openDialog(
    WidgetTester tester,
    _Api http,
    ApiClient client,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: WorkspaceTheme.build(),
        locale: const Locale('bs'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: Scaffold(
          body: NewAppointmentDialog(
            catalog: CatalogApi(client),
            reservations: ReservationsApi(client),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('the chosen client is marked in the results, with no extra box', (
    tester,
  ) async {
    _wide(tester);
    final m = _make();
    await openDialog(tester, m.http, m.client);

    expect(tester.takeException(), isNull);
    expect(find.byKey(const Key('selected_client')), findsNothing);
    ListTile tile(String id) =>
        tester.widget<ListTile>(find.byKey(Key('client_$id')));
    expect(tile('c2').selected, isFalse);

    await tester.tap(find.byKey(const Key('client_c2')));
    await tester.pumpAndSettle();

    expect(tile('c2').selected, isTrue);
    expect(tile('c1').selected, isFalse);
    expect(
      find.descendant(
        of: find.byKey(const Key('client_c2')),
        matching: find.byIcon(Icons.check_circle),
      ),
      findsOneWidget,
    );
    expect(find.byKey(const Key('selected_client')), findsNothing);
    expect(find.text('Odabrani klijent'), findsNothing);
  });

  testWidgets(
    'services are a drop-down; several can be ticked and the total shows',
    (tester) async {
      _wide(tester);
      final m = _make();
      await openDialog(tester, m.http, m.client);

      // Services appear once a barber is chosen.
      expect(find.byKey(const Key('new_services')), findsNothing);
      await tester.tap(find.byKey(const Key('new_employee')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Kerim Begić').last);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('new_services')), findsOneWidget);
      expect(find.byType(FilterChip), findsNothing);

      await tester.tap(find.byKey(const Key('new_services')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('new_service_1')).first);
      await tester.pumpAndSettle();
      // The menu stays open so a second service can be ticked.
      await tester.tap(find.byKey(const Key('new_service_2')).first);
      await tester.pumpAndSettle();

      final field = find.byKey(const Key('new_services'));
      expect(
        find.descendant(
          of: field,
          matching: find.textContaining('Šišanje, Brada'),
        ),
        findsOneWidget,
      );
      expect(
        find.descendant(of: field, matching: find.textContaining('40 KM')),
        findsOneWidget,
      );
    },
  );
}

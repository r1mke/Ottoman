import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:latlong2/latlong.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/features/gallery/gallery_page.dart';
import 'package:ottoman_desktop/features/images/picture_editor.dart';
import 'package:ottoman_desktop/features/salon/salon_page.dart';

const _pic = 'https://res.cloudinary.com/demo/image/upload/sample.jpg';

class _Server implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  int nextId = 10;
  int status = 200;
  Object? errorBody;
  final styles = <Map<String, dynamic>>[
    {'id': 1, 'name': 'Buzz cut', 'count': 1},
    {'id': 2, 'name': 'French crop', 'count': 0},
  ];
  final works = <Map<String, dynamic>>[
    {
      'id': 1,
      'imageUrl': _pic,
      'categoryId': 1,
      'categoryName': 'Buzz cut',
      'employeeId': 'e1',
      'employeeName': 'Kerim Begić',
      'caption': null,
      'isFeatured': true,
      'createdAtUtc': '2026-01-01T00:00:00Z',
    },
  ];
  Map<String, dynamic> salon = {
    'name': 'Ottoman',
    'description': 'Stari opis',
    'address': 'Ferhadija 12',
    'phone': '033 123 456',
    'mapsUrl': null,
    'logoUrl': null,
    'latitude': null,
    'longitude': null,
    'hours': [
      for (final d in [1, 2, 3, 4, 5, 6, 0])
        {
          'day': d,
          'openMinutes': d == 0 ? null : 540,
          'closeMinutes': d == 0 ? null : 1140,
        },
    ],
  };

  Iterable<RequestOptions> sent(String method, String part) =>
      requests.where((r) => r.method == method && r.path.contains(part));

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
    Object? body;
    var code = 200;
    final p = o.path;
    if (errorBody != null && o.method != 'GET') {
      code = status;
      body = errorBody;
    } else if (p.endsWith('/gallery/categories') && o.method == 'GET') {
      body = styles;
    } else if (p.endsWith('/gallery/categories') && o.method == 'POST') {
      body = {'id': nextId++, 'name': (o.data as Map)['name'], 'count': 0};
    } else if (p.contains('/gallery/categories/') && o.method == 'DELETE') {
      code = 204;
    } else if (p.endsWith('/gallery') && o.method == 'GET') {
      body = {'items': works, 'totalCount': works.length};
    } else if (p.endsWith('/gallery') && o.method == 'POST') {
      body = {...works.first, 'id': nextId++};
    } else if (p.contains('/gallery/') && o.method == 'PUT') {
      body = works.first;
    } else if (p.contains('/gallery/') && o.method == 'DELETE') {
      code = 204;
    } else if (p.endsWith('/employees')) {
      body = [
        {
          'id': 'e1',
          'firstName': 'Kerim',
          'lastName': 'Begić',
          'fullName': 'Kerim Begić',
          'title': 'Barber',
          'isActive': true,
          'serviceIds': <int>[],
        },
      ];
    } else if (p.endsWith('/salon') && o.method == 'GET') {
      body = salon;
    } else if (p.endsWith('/salon') && o.method == 'PUT') {
      body = salon;
    } else {
      code = 204;
    }
    return ResponseBody.fromString(
      code == 204 ? '' : jsonEncode(body),
      code,
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
  Future<ChosenImage?> choose() async => ChosenImage(
    'cut.png',
    Uint8List.fromList([
      0x89,
      0x50,
      0x4E,
      0x47,
      0x0D,
      0x0A,
      0x1A,
      0x0A,
      0,
      0,
      0,
      13,
      73,
      72,
      68,
      82,
      0,
      0,
      0,
      1,
      0,
      0,
      0,
      1,
      8,
      6,
      0,
      0,
      0,
      31,
      21,
      196,
      137,
      0,
      0,
      0,
      13,
      73,
      68,
      65,
      84,
      120,
      156,
      99,
      248,
      255,
      255,
      63,
      0,
      5,
      254,
      2,
      254,
      167,
      53,
      129,
      132,
      0,
      0,
      0,
      0,
      73,
      69,
      78,
      68,
      174,
      66,
      96,
      130,
    ]),
  );
}

ApiClient _client(_Server s) => ApiClient(
  baseUrl: 'http://test',
  dio: Dio()..httpClientAdapter = s,
  readToken: () async => 'a',
);

Future<void> _pump(
  WidgetTester tester,
  Widget Function(ApiClient) home,
  _Server s,
) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(1300, 1800);
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      theme: WorkspaceTheme.build(),
      locale: const Locale('bs'),
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      home: Scaffold(body: home(_client(s))),
    ),
  );
  await tester.pumpAndSettle();
}

Widget _gallery(ApiClient api) => GalleryPage(
  gallery: GalleryApi(api),
  catalog: CatalogApi(api),
  chooser: _Chooser(),
);

void main() {
  testWidgets('the gallery lists works with style, barber and the home badge', (
    tester,
  ) async {
    await _pump(tester, _gallery, _Server());

    expect(find.byKey(const Key('work_1')), findsOneWidget);
    expect(find.text('Kerim Begić'), findsNothing); // shown as "Frizer: ..."
    expect(find.text('Frizer: Kerim Begić'), findsOneWidget);
    expect(find.byKey(const Key('work_home_1')), findsOneWidget);
    expect(find.byKey(const Key('style_1')), findsOneWidget);
    expect(find.text('Buzz cut (1)'), findsOneWidget);
  });

  testWidgets('a style chip asks the server for that style only', (
    tester,
  ) async {
    final server = _Server();
    await _pump(tester, _gallery, server);

    await tester.tap(find.byKey(const Key('style_2')));
    await tester.pumpAndSettle();

    expect(
      server
          .sent('GET', '/gallery')
          .any((r) => r.queryParameters['categoryId'] == 2),
      isTrue,
    );
  });

  testWidgets(
    'adding a work needs a picture and a style, then sends style, barber and the home choice',
    (tester) async {
      final server = _Server();
      await _pump(tester, _gallery, server);

      await tester.tap(find.byKey(const Key('add_work')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('work_save')));
      await tester.pumpAndSettle();
      expect(server.sent('POST', '/gallery'), isEmpty); // no picture, no style

      await tester.tap(find.byKey(const Key('work_choose_picture')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('work_style')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('French crop').last);
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('work_barber')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Kerim Begić').last);
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('work_featured')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('work_save')));
      await tester.pumpAndSettle();

      final post = server.sent('POST', '/gallery').single;
      final fields = {
        for (final f in (post.data as FormData).fields) f.key: f.value,
      };
      expect(fields['categoryId'], '2');
      expect(fields['employeeId'], 'e1');
      expect(fields['isFeatured'], 'true');
      expect((post.data as FormData).files.single.key, 'file');
    },
  );

  testWidgets(
    'a refused picture is explained in a toast with what is accepted',
    (tester) async {
      final server = _Server()
        ..status = 409
        ..errorBody = {'detail': 'Ta slika je već dodana. Odaberite drugu.'};
      await _pump(tester, _gallery, server);

      await tester.tap(find.byKey(const Key('add_work')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('work_choose_picture')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('work_style')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Buzz cut').last);
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('work_save')));
      await tester.pumpAndSettle();

      expect(find.byType(SnackBar), findsOneWidget);
      expect(find.textContaining('već dodana'), findsOneWidget);
      expect(find.textContaining('5 MB'), findsOneWidget);
    },
  );

  testWidgets('deleting a work asks first', (tester) async {
    final server = _Server();
    await _pump(tester, _gallery, server);

    await tester.tap(find.byKey(const Key('delete_work_1')));
    await tester.pumpAndSettle();
    expect(server.sent('DELETE', '/gallery/1'), isEmpty);

    await tester.tap(find.byKey(const Key('confirm_delete_work')));
    await tester.pumpAndSettle();
    expect(server.sent('DELETE', '/gallery/1'), hasLength(1));
  });

  testWidgets(
    'styles can be added and a style in use is refused with the reason',
    (tester) async {
      final server = _Server();
      await _pump(tester, _gallery, server);

      await tester.tap(find.byKey(const Key('manage_styles')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('new_style_field')), 'Fade');
      await tester.tap(find.byKey(const Key('add_style')));
      await tester.pumpAndSettle();
      expect(
        (server.sent('POST', '/gallery/categories').single.data as Map)['name'],
        'Fade',
      );

      server
        ..status = 409
        ..errorBody = {'detail': 'Kategorija se koristi u galeriji.'};
      await tester.tap(find.byKey(const Key('delete_style_1')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm_action')));
      await tester.pumpAndSettle();
      expect(find.textContaining('koristi u galeriji'), findsOneWidget);
    },
  );

  // ------------------------------------------------------------------ salon

  Future<LatLng?> findAddress(String address) async =>
      address.contains('Ferhadija') ? const LatLng(43.8586, 18.431) : null;

  Widget salonPage(ApiClient api) =>
      SalonPage(salon: SalonApi(api), chooser: _Chooser(), search: findAddress);

  testWidgets('the salon page shows the profile and its week', (tester) async {
    await _pump(tester, salonPage, _Server());

    expect(find.text('Stari opis'), findsOneWidget);
    expect(find.text('Ferhadija 12'), findsWidgets);
    expect(find.byKey(const Key('hours_from_1')), findsOneWidget);
    expect(
      find.byKey(const Key('hours_from_0')),
      findsNothing,
    ); // Sunday closed
    expect(find.text('Zatvoreno'), findsOneWidget);
  });

  testWidgets('saving sends the text, the link and all seven days', (
    tester,
  ) async {
    final server = _Server();
    await _pump(tester, salonPage, server);

    await tester.enterText(
      find.byKey(const Key('salon_description')),
      'Novi opis',
    );
    await tester.enterText(
      find.byKey(const Key('salon_maps')),
      'https://maps.example/ottoman',
    );
    await tester.ensureVisible(find.byKey(const Key('location_find')));
    await tester.tap(find.byKey(const Key('location_find')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('location_pin')), findsOneWidget);
    await tester.ensureVisible(find.byKey(const Key('hours_open_0')));
    await tester.tap(find.byKey(const Key('hours_open_0'))); // open on Sunday
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.byKey(const Key('salon_save')));
    await tester.tap(find.byKey(const Key('salon_save')));
    await tester.pumpAndSettle();

    final put = (server.sent('PUT', '/salon').single.data as Map);
    expect(put['latitude'], 43.8586);
    expect(put['longitude'], 18.431);
    expect(put['description'], 'Novi opis');
    expect(put['mapsUrl'], 'https://maps.example/ottoman');
    final hours = put['hours'] as List;
    expect(hours, hasLength(7));
    final sunday = hours.firstWhere((h) => (h as Map)['day'] == 0) as Map;
    expect(sunday['openMinutes'], 540);
    expect(sunday['closeMinutes'], 1080);
  });

  testWidgets('typing a new maps link keeps the pin on screen; on save a pin '
      'the admin did not move is left to the server to read from the link', (
    tester,
  ) async {
    final server = _Server()
      ..salon = {
        ..._Server().salon,
        'mapsUrl': 'https://maps.example/old',
        'latitude': 43.85,
        'longitude': 18.41,
      };
    await _pump(tester, salonPage, server);
    expect(find.byKey(const Key('location_pin')), findsOneWidget);

    await tester.enterText(
      find.byKey(const Key('salon_maps')),
      'https://maps.example/new',
    );
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('location_pin')), findsOneWidget);

    await tester.ensureVisible(find.byKey(const Key('salon_save')));
    await tester.tap(find.byKey(const Key('salon_save')));
    await tester.pumpAndSettle();
    final put = server.sent('PUT', '/salon').single.data as Map;
    expect(put['mapsUrl'], 'https://maps.example/new');
    expect(put['latitude'], isNull);
    expect(put['longitude'], isNull);
  });

  testWidgets('with the link unchanged the pin is saved as it is', (
    tester,
  ) async {
    final server = _Server()
      ..salon = {
        ..._Server().salon,
        'mapsUrl': 'https://maps.example/old',
        'latitude': 43.85,
        'longitude': 18.41,
      };
    await _pump(tester, salonPage, server);
    await tester.ensureVisible(find.byKey(const Key('salon_save')));
    await tester.tap(find.byKey(const Key('salon_save')));
    await tester.pumpAndSettle();
    final put = server.sent('PUT', '/salon').single.data as Map;
    expect(put['latitude'], 43.85);
    expect(put['longitude'], 18.41);
  });

  testWidgets('an address search that fails in any way says not found', (
    tester,
  ) async {
    Widget failing(ApiClient api) => SalonPage(
      salon: SalonApi(api),
      chooser: _Chooser(),
      search: (_) async => throw StateError('offline'),
    );
    await _pump(tester, failing, _Server());
    await tester.enterText(
      find.byKey(const Key('location_query')),
      'Ferhadija',
    );
    await tester.ensureVisible(find.byKey(const Key('location_find')));
    await tester.tap(find.byKey(const Key('location_find')));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(find.textContaining('Adresa nije pronađena'), findsOneWidget);
  });

  testWidgets('a wrong phone number or link stops the save under the field', (
    tester,
  ) async {
    final server = _Server();
    await _pump(tester, salonPage, server);

    await tester.enterText(find.byKey(const Key('salon_phone')), '12345');
    await tester.enterText(find.byKey(const Key('salon_maps')), 'nije link');
    await tester.ensureVisible(find.byKey(const Key('salon_save')));
    await tester.tap(find.byKey(const Key('salon_save')));
    await tester.pumpAndSettle();

    expect(
      find.textContaining('Unesite ispravan broj telefona'),
      findsOneWidget,
    );
    expect(
      find.text('Unesite ispravnu web adresu (https://...).'),
      findsOneWidget,
    );
    expect(server.sent('PUT', '/salon'), isEmpty);
  });

  testWidgets('the place is picked on the map: an unknown address says so, a '
      'tap drops the pin, and the pin can be removed', (tester) async {
    final server = _Server();
    await _pump(tester, salonPage, server);

    await tester.enterText(find.byKey(const Key('location_query')), 'Nigdje 1');
    await tester.ensureVisible(find.byKey(const Key('location_find')));
    await tester.tap(find.byKey(const Key('location_find')));
    await tester.pumpAndSettle();
    expect(find.textContaining('Adresa nije pronađena'), findsOneWidget);
    expect(find.byKey(const Key('location_pin')), findsNothing);

    await tester.ensureVisible(find.byKey(const Key('location_map')));
    await tester.tap(find.byKey(const Key('location_map')));
    // The map waits a moment to tell a tap from the first half of a double tap.
    await tester.pump(const Duration(milliseconds: 500));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('location_pin')), findsOneWidget);

    await tester.tap(find.byKey(const Key('location_clear')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('location_pin')), findsNothing);
    expect(find.text('Oznaka još nije postavljena.'), findsOneWidget);

    expect(find.byKey(const Key('salon_lat')), findsNothing);
  });
}

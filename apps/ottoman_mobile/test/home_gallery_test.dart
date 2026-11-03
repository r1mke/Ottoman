import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_mobile/features/home/home_tab.dart';

const _pic = 'https://res.cloudinary.com/demo/image/upload/sample.jpg';

Map<String, dynamic> _work(int id, int cat, String catName, {String? barber}) =>
    {
      'id': id,
      'imageUrl': _pic,
      'categoryId': cat,
      'categoryName': catName,
      'employeeId': barber == null ? null : 'e1',
      'employeeName': barber,
      'caption': null,
      'isFeatured': id <= 2,
      'createdAtUtc': DateTime.utc(2026).toIso8601String(),
    };

class _Server implements HttpClientAdapter {
  _Server({
    this.featured = true,
    this.fullProfile = true,
    this.mapsLink = true,
  });

  final bool featured;
  final bool fullProfile;
  final bool mapsLink;
  final requests = <RequestOptions>[];

  Map<String, dynamic> get _profile => {
    'name': 'Ottoman',
    'description': 'Moderan frizerski salon.',
    'address': fullProfile ? 'Ferhadija 12, Sarajevo' : null,
    'phone': fullProfile ? '033 123 456' : null,
    'mapsUrl': fullProfile && mapsLink ? 'https://maps.example/ottoman' : null,
    'logoUrl': null,
    'instagramUrl': fullProfile ? 'https://instagram.com/ottoman' : null,
    'facebookUrl': fullProfile ? 'https://facebook.com/ottoman' : null,
    'latitude': fullProfile ? 43.8586 : null,
    'longitude': fullProfile ? 18.431 : null,
    'hours': [
      for (final d in [1, 2, 3, 4, 5, 6, 0])
        {
          'day': d,
          'openMinutes': d == 0 ? null : 540,
          'closeMinutes': d == 0 ? null : (d == 6 ? 900 : 1140),
        },
    ],
  };

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
    Object? body;
    if (o.path.endsWith('/salon')) {
      body = _profile;
    } else if (o.path.endsWith('/gallery/categories')) {
      body = [
        {'id': 1, 'name': 'Buzz cut', 'count': 2},
        {'id': 2, 'name': 'French crop', 'count': 1},
      ];
    } else if (o.path.endsWith('/gallery')) {
      final q = o.queryParameters;
      var all = [
        _work(1, 1, 'Buzz cut', barber: 'Kerim Begić'),
        _work(2, 1, 'Buzz cut'),
        _work(3, 2, 'French crop', barber: 'Tarik Mujkić'),
      ];
      if (q['featured'] == true) {
        all = featured
            ? all.where((w) => w['isFeatured'] == true).toList()
            : [];
      }
      if (q['categoryId'] != null) {
        all = all.where((w) => w['categoryId'] == q['categoryId']).toList();
      }
      body = {'items': all, 'totalCount': all.length};
    }
    return ResponseBody.fromString(
      jsonEncode(body),
      200,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

Future<({_Server server, List<Uri> opened, List<String> events})> _pump(
  WidgetTester tester, {
  _Server? server,
  bool openSucceeds = true,
  int today = 3,
}) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(420, 1800);
  addTearDown(tester.view.reset);

  final s = server ?? _Server();
  final api = ApiClient(
    baseUrl: 'http://test',
    dio: Dio()..httpClientAdapter = s,
    readToken: () async => 'a',
  );
  final opened = <Uri>[];
  final events = <String>[];
  await tester.pumpWidget(
    MaterialApp(
      locale: const Locale('bs'),
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      home: Scaffold(
        body: HomeTab(
          salon: SalonApi(api),
          gallery: GalleryApi(api),
          onBook: () => events.add('book'),
          today: today,
          mapBuilder: (context, lat, lon) =>
              Container(key: Key('map_$lat,$lon'), color: Colors.green),
          openUrl: (uri) async {
            opened.add(uri);
            return openSucceeds;
          },
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return (server: s, opened: opened, events: events);
}

void main() {
  testWidgets(
    'the home shows the salon, its work, hours and where to find it',
    (tester) async {
      await _pump(tester);

      expect(find.byKey(const Key('salon_logo')), findsOneWidget);
      expect(find.text('Dobrodošli u Ottoman'), findsOneWidget);
      expect(find.text('Moderan frizerski salon.'), findsOneWidget);
      expect(find.text('Naši radovi'), findsOneWidget);
      expect(find.byKey(const Key('featured_1')), findsOneWidget);
      expect(find.byKey(const Key('featured_2')), findsOneWidget);
      expect(
        find.byKey(const Key('featured_3')),
        findsNothing,
      ); // not on the home screen

      await tester.scrollUntilVisible(
        find.byKey(const Key('hours_row_0')),
        200,
        scrollable: find.byType(Scrollable).first,
      );
      expect(
        find.descendant(
          of: find.byKey(const Key('hours_row_1')),
          matching: find.text('09:00 – 19:00'),
        ),
        findsOneWidget,
      );
      expect(
        find.descendant(
          of: find.byKey(const Key('hours_row_6')),
          matching: find.text('09:00 – 15:00'),
        ),
        findsOneWidget,
      );
      expect(
        find.descendant(
          of: find.byKey(const Key('hours_row_0')),
          matching: find.text('Zatvoreno'),
        ),
        findsOneWidget,
      );
      await tester.scrollUntilVisible(
        find.byKey(const Key('salon_address')),
        200,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('Ferhadija 12, Sarajevo'), findsOneWidget);
    },
  );

  testWidgets('today is the highlighted weekday', (tester) async {
    await _pump(tester, today: 3);
    await tester.scrollUntilVisible(
      find.byKey(const Key('hours_row_3')),
      200,
      scrollable: find.byType(Scrollable).first,
    );

    FontWeight? weightOf(int day) => tester
        .widget<Text>(
          find.descendant(
            of: find.byKey(Key('hours_row_$day')),
            matching: find.text(const {1: 'Ponedjeljak', 3: 'Srijeda'}[day]!),
          ),
        )
        .style
        ?.fontWeight;

    expect(weightOf(3), FontWeight.w800);
    expect(weightOf(1), FontWeight.w500);
  });

  testWidgets('the map and phone buttons open the salon\'s link and number', (
    tester,
  ) async {
    final h = await _pump(tester);
    await tester.scrollUntilVisible(
      find.byKey(const Key('salon_map')),
      200,
      scrollable: find.byType(Scrollable).first,
    );

    expect(find.byKey(const Key('open_maps')), findsNothing);
    expect(find.textContaining('OpenStreetMap'), findsNothing);
    expect(find.text('Otvori u Google Maps'), findsNothing);

    await tester.tap(find.byKey(const Key('salon_map')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('call_salon')));
    await tester.pump();

    expect(h.opened[0].toString(), 'https://maps.example/ottoman');
    expect(h.opened[1].scheme, 'tel');
    expect(h.opened[1].path, '033123456');
  });

  testWidgets('the map shows the salon and a tap opens Google Maps', (
    tester,
  ) async {
    final h = await _pump(tester);
    await tester.scrollUntilVisible(
      find.byKey(const Key('salon_map')),
      200,
      scrollable: find.byType(Scrollable).first,
    );

    expect(find.byKey(const Key('map_43.8586,18.431')), findsOneWidget);
    await tester.tap(find.byKey(const Key('salon_map')));
    await tester.pump();

    expect(h.opened.single.toString(), 'https://maps.example/ottoman');
  });

  testWidgets(
    'without a link the tap searches the coordinates on Google Maps',
    (tester) async {
      final h = await _pump(tester, server: _Server(mapsLink: false));
      await tester.scrollUntilVisible(
        find.byKey(const Key('salon_map')),
        200,
        scrollable: find.byType(Scrollable).first,
      );

      await tester.tap(find.byKey(const Key('salon_map')));
      await tester.pump();

      expect(h.opened.single.host, 'www.google.com');
      expect(h.opened.single.queryParameters['query'], '43.8586,18.431');
    },
  );

  testWidgets('a link that cannot be opened is said so, not ignored', (
    tester,
  ) async {
    await _pump(tester, openSucceeds: false);
    await tester.scrollUntilVisible(
      find.byKey(const Key('salon_map')),
      200,
      scrollable: find.byType(Scrollable).first,
    );

    await tester.tap(find.byKey(const Key('salon_map')));
    await tester.pump();

    expect(find.byType(SnackBar), findsOneWidget);
  });

  testWidgets('without work on the home screen the section is left out', (
    tester,
  ) async {
    await _pump(tester, server: _Server(featured: false));

    expect(find.text('Naši radovi'), findsNothing);
    expect(find.byKey(const Key('open_gallery')), findsOneWidget);
  });

  testWidgets('parts the admin left empty are not shown', (tester) async {
    await _pump(tester, server: _Server(fullProfile: false));

    expect(find.text('Gdje smo'), findsNothing);
    expect(find.byKey(const Key('salon_map')), findsNothing);
    expect(find.byKey(const Key('open_maps')), findsNothing);
    expect(find.byKey(const Key('call_salon')), findsNothing);
  });

  testWidgets('"Rezerviši termin" starts the booking', (tester) async {
    final h = await _pump(tester);

    await tester.tap(find.byKey(const Key('book_now')));

    expect(h.events, ['book']);
  });

  testWidgets(
    'the gallery filters by style with a horizontal menu and opens a picture',
    (tester) async {
      final h = await _pump(tester);
      await tester.tap(find.byKey(const Key('open_gallery')));
      await tester.pumpAndSettle();

      expect(find.text('Galerija radova'), findsOneWidget);
      expect(find.byKey(const Key('gallery_all')), findsOneWidget);
      expect(find.byKey(const Key('gallery_style_1')), findsOneWidget);
      expect(find.byKey(const Key('gallery_style_2')), findsOneWidget);
      expect(find.byKey(const Key('work_1')), findsOneWidget);
      expect(find.byKey(const Key('work_3')), findsOneWidget);

      await tester.tap(find.byKey(const Key('gallery_style_2')));
      await tester.pumpAndSettle();

      final filtered = h.server.requests.where(
        (r) =>
            r.path.endsWith('/gallery') && r.queryParameters['categoryId'] == 2,
      );
      expect(filtered, isNotEmpty);
      expect(find.byKey(const Key('work_3')), findsOneWidget);
      expect(find.byKey(const Key('work_1')), findsNothing);

      await tester.tap(find.byKey(const Key('work_3')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('work_viewer')), findsOneWidget);
      expect(find.textContaining('Tarik Mujkić'), findsOneWidget);
    },
  );

  testWidgets('"Naši radovi" opens the gallery on that work\'s style', (
    tester,
  ) async {
    final h = await _pump(tester);

    await tester.tap(find.byKey(const Key('featured_1')));
    await tester.pumpAndSettle();

    expect(find.text('Galerija radova'), findsOneWidget);
    expect(
      h.server.requests.any(
        (r) =>
            r.path.endsWith('/gallery') && r.queryParameters['categoryId'] == 1,
      ),
      isTrue,
    );
  });

  testWidgets('"Povežite se s nama" has call, Instagram and Facebook, each '
      'opening its own link', (tester) async {
    final h = await _pump(tester);
    await tester.scrollUntilVisible(
      find.text('Povežite se s nama'),
      200,
      scrollable: find.byType(Scrollable).first,
    );

    await tester.tap(find.byKey(const Key('open_instagram')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('open_facebook')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('call_salon')));
    await tester.pump();

    expect(h.opened[0].toString(), 'https://instagram.com/ottoman');
    expect(h.opened[1].toString(), 'https://facebook.com/ottoman');
    expect(h.opened[2].scheme, 'tel');
  });

  testWidgets('the sections come in order: our work, connect, find us, hours; '
      'and find us has no phone number', (tester) async {
    await _pump(tester);
    await tester.scrollUntilVisible(
      find.text('Radno vrijeme'),
      300,
      scrollable: find.byType(Scrollable).first,
    );

    double top(String text) => tester.getTopLeft(find.text(text)).dy;
    final connect = find.text('Povežite se s nama');
    final findUs = find.text('Pronađite nas ovdje');
    expect(connect, findsOneWidget);
    expect(findUs, findsOneWidget);
    expect(top('Povežite se s nama') < top('Pronađite nas ovdje'), isTrue);
    expect(top('Pronađite nas ovdje') < top('Radno vrijeme'), isTrue);
    expect(find.byKey(const Key('salon_phone')), findsNothing);
    expect(find.byKey(const Key('salon_address')), findsOneWidget);
  });

  testWidgets('a network the admin left empty gets no button', (tester) async {
    await _pump(tester, server: _Server(fullProfile: false));

    expect(find.byKey(const Key('open_instagram')), findsNothing);
    expect(find.byKey(const Key('open_facebook')), findsNothing);
    expect(find.text('Povežite se s nama'), findsNothing);
    expect(find.text('Pronađite nas ovdje'), findsNothing);
  });
}

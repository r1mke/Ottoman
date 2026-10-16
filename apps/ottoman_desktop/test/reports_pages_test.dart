import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/features/reports/file_saver.dart';
import 'package:ottoman_desktop/features/reports/overview_page.dart';
import 'package:ottoman_desktop/features/reports/reports_page.dart';

final _now = DateTime(2030, 3, 15, 12);

Map<String, dynamic> _kpi(num v, [num? change]) => {
  'value': v,
  'changePercent': change,
};

Map<String, dynamic> _dashboard({bool empty = false}) => {
  'from': '2030-02-14',
  'to': '2030-03-15',
  'granularity': 'Day',
  'revenue': _kpi(empty ? 0 : 85, empty ? null : 70.0),
  'servicesRevenue': _kpi(empty ? 0 : 65, empty ? null : -12.5),
  'productsRevenue': _kpi(empty ? 0 : 20),
  'appointments': _kpi(empty ? 0 : 2, 0),
  'orders': _kpi(empty ? 0 : 2),
  'newClients': _kpi(1),
  'averageTicket': empty ? 0 : 28.33,
  'noShowRate': 25,
  'cancellationRate': 12.5,
  'series': [
    for (var i = 1; i <= 5; i++)
      {
        'start': '2030-03-0$i',
        'label': '0$i.03.',
        'services': empty ? 0 : i * 10,
        'products': empty ? 0 : i * 2,
        'total': empty ? 0 : i * 12,
      },
  ],
  'topServices': empty
      ? []
      : [
          {'key': '1', 'name': 'Šišanje', 'quantity': 4, 'revenue': 100},
          {'key': '2', 'name': 'Brada', 'quantity': 1, 'revenue': 15},
        ],
  'topProducts': empty
      ? []
      : [
          {'key': '9', 'name': 'Vosak', 'quantity': 3, 'revenue': 30},
        ],
  'employees': [
    {
      'employeeId': 'e1',
      'name': 'Kerim Begić',
      'completed': 3,
      'cancelled': 0,
      'noShow': 1,
      'revenue': 90,
      'averageRating': 4.5,
      'reviewCount': 2,
    },
    {
      'employeeId': 'e2',
      'name': 'Tarik Mujkić',
      'completed': 0,
      'cancelled': 0,
      'noShow': 0,
      'revenue': 0,
      'averageRating': null,
      'reviewCount': 0,
    },
  ],
};

Map<String, dynamic> _table(String title, {bool empty = false}) => {
  'title': title,
  'columns': [
    {'key': 'n', 'title': 'Naziv', 'type': 'Text'},
    {'key': 'q', 'title': 'Količina', 'type': 'Number'},
    {'key': 'r', 'title': 'Prihod', 'type': 'Money'},
  ],
  'rows': empty
      ? []
      : [
          ['Šišanje', 4, 100],
          ['Brada', 1, 15.5],
        ],
  'totals': empty ? null : ['Ukupno', 5, 115.5],
};

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  bool empty = false;
  int customStatus = 200;

  Iterable<RequestOptions> where(String part) =>
      requests.where((r) => r.path.contains(part));

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
    final path = o.path;
    if (path.endsWith('/export') || path.endsWith('/custom/export')) {
      return ResponseBody.fromBytes(
        [1, 2, 3],
        200,
        headers: {
          'content-disposition': ['attachment; filename=izvjestaj.csv'],
        },
      );
    }
    Object? body;
    var status = 200;
    if (path.endsWith('/dashboard')) {
      body = _dashboard(empty: empty);
    } else if (path.endsWith('/custom/fields')) {
      body = [
        {
          'source': 'Reservations',
          'title': 'Termini',
          'columns': [
            {'key': 'date', 'title': 'Datum', 'type': 'Date'},
            {'key': 'client', 'title': 'Klijent', 'type': 'Text'},
            {'key': 'price', 'title': 'Cijena', 'type': 'Money'},
          ],
          'statuses': ['Completed', 'Cancelled'],
        },
        {
          'source': 'Orders',
          'title': 'Narudžbe',
          'columns': [
            {'key': 'date', 'title': 'Datum', 'type': 'Date'},
            {'key': 'total', 'title': 'Ukupno', 'type': 'Money'},
          ],
          'statuses': ['Fulfilled'],
        },
      ];
    } else if (path.endsWith('/custom')) {
      status = customStatus;
      body = customStatus == 200
          ? _table('Termini')
          : {
              'status': 400,
              'title': 'Podaci nisu ispravni.',
              'detail': 'Nepoznato polje.',
            };
    } else if (path.contains('/reports/')) {
      body = _table(path.split('/').last, empty: empty);
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

class _Saver implements FileSaver {
  final saved = <DownloadedFile>[];
  final revealed = <String>[];

  @override
  Future<String> save(DownloadedFile file) async {
    saved.add(file);
    return r'C:\Downloads\' + file.fileName;
  }

  @override
  Future<void> reveal(String path) async => revealed.add(path);
}

Future<(_Api, _Saver)> _pump(
  WidgetTester tester,
  Widget Function(ReportsApi api, _Saver saver) page, {
  bool empty = false,
}) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(1600, 1400);
  addTearDown(tester.view.reset);

  final adapter = _Api()..empty = empty;
  final api = ReportsApi(
    ApiClient(
      baseUrl: 'http://test',
      dio: Dio()..httpClientAdapter = adapter,
      readToken: () async => 't',
    ),
  );
  final saver = _Saver();
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
      home: Scaffold(body: page(api, saver)),
    ),
  );
  await tester.pumpAndSettle();
  return (adapter, saver);
}

void main() {
  group('Pregled (dashboard)', () {
    testWidgets(
      'shows the KPIs with their change against the previous period',
      (tester) async {
        await _pump(tester, (api, _) => OverviewPage(api: api, now: _now));

        expect(
          tester.widget<Text>(find.byKey(const Key('kpi_value_revenue'))).data,
          '85.00 KM',
        );
        expect(
          tester.widget<Text>(find.byKey(const Key('kpi_change_revenue'))).data,
          '70.0%',
        );
        // a decrease is shown as a down arrow with the absolute value
        expect(
          tester
              .widget<Text>(find.byKey(const Key('kpi_change_services')))
              .data,
          '12.5%',
        );
        expect(
          find.byKey(const Key('kpi_change_products')),
          findsNothing,
          reason: 'nothing to compare with -> no percentage',
        );
        expect(find.byKey(const Key('kpi_value_ticket')), findsOneWidget);
      },
    );

    testWidgets(
      'requests the last 30 days by default and a new period on tap',
      (tester) async {
        final (api, _) = await _pump(
          tester,
          (api, _) => OverviewPage(api: api, now: _now),
        );
        final first = api.where('/dashboard').single.queryParameters;
        expect(first['from'], '2030-02-14');
        expect(first['to'], '2030-03-15');

        await tester.tap(find.byKey(const Key('period_lastMonth')));
        await tester.pumpAndSettle();

        final second = api.where('/dashboard').last.queryParameters;
        expect(second['from'], '2030-02-01');
        expect(second['to'], '2030-02-28');
      },
    );

    testWidgets('lists the top services, top products and each employee', (
      tester,
    ) async {
      await _pump(tester, (api, _) => OverviewPage(api: api, now: _now));
      await tester.scrollUntilVisible(
        find.byKey(const Key('employee_table')),
        300,
        scrollable: find.byType(Scrollable).first,
      );

      expect(find.textContaining('Šišanje · 4×'), findsOneWidget);
      expect(find.textContaining('Vosak · 3×'), findsOneWidget);
      expect(find.text('Kerim Begić'), findsOneWidget);
      expect(find.text('4.5 (2)'), findsOneWidget);
      expect(find.text('-'), findsOneWidget, reason: 'no rating yet');
    });

    testWidgets('draws the chart when there is revenue', (tester) async {
      await _pump(tester, (api, _) => OverviewPage(api: api, now: _now));
      await tester.scrollUntilVisible(
        find.byKey(const Key('revenue_chart')),
        300,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.byKey(const Key('revenue_chart')), findsOneWidget);
      expect(find.byKey(const Key('chart_empty')), findsNothing);
    });

    testWidgets('an empty period says so instead of drawing an empty chart', (
      tester,
    ) async {
      await _pump(
        tester,
        (api, _) => OverviewPage(api: api, now: _now),
        empty: true,
      );
      await tester.scrollUntilVisible(
        find.byKey(const Key('chart_empty')),
        300,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.byKey(const Key('chart_empty')), findsOneWidget);
      expect(find.byKey(const Key('revenue_chart')), findsNothing);
    });
  });

  group('Zarada & Izvještaji', () {
    testWidgets('earnings shows the revenue table with a totals row', (
      tester,
    ) async {
      final (api, _) = await _pump(
        tester,
        (api, saver) => EarningsPage(api: api, saver: saver, now: _now),
      );

      expect(api.where('/reports/revenue').isNotEmpty, isTrue);
      expect(find.text('Šišanje'), findsOneWidget);
      expect(find.text('15.50'), findsOneWidget);
      expect(find.text('115.50'), findsOneWidget, reason: 'the totals row');
      expect(
        find.byKey(const ValueKey('report_totals')),
        findsNothing,
        reason: 'DataRow keys are not widget keys',
      );
    });

    testWidgets('exporting saves the downloaded file and offers to show it', (
      tester,
    ) async {
      final (api, saver) = await _pump(
        tester,
        (api, saver) => EarningsPage(api: api, saver: saver, now: _now),
      );

      await tester.tap(find.byKey(const Key('export_menu')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('export_xlsx')));
      await tester.pumpAndSettle();

      final req = api.where('/export').single;
      expect(req.queryParameters['format'], 'Xlsx');
      expect(req.queryParameters['from'], '2030-02-14');
      expect(saver.saved.single.fileName, 'izvjestaj.csv');
      expect(
        find.textContaining(r'C:\Downloads\izvjestaj.csv'),
        findsOneWidget,
      );

      await tester.tap(find.text('Prikaži'));
      await tester.pump();
      expect(saver.revealed.single, r'C:\Downloads\izvjestaj.csv');
    });

    testWidgets('the reports page switches between ready-made reports', (
      tester,
    ) async {
      final (api, _) = await _pump(
        tester,
        (api, saver) => ReportsPage(api: api, saver: saver, now: _now),
      );
      expect(api.where('/reports/employees').isNotEmpty, isTrue);

      await tester.tap(find.byKey(const Key('report_services')));
      await tester.pumpAndSettle();
      expect(api.where('/reports/services').isNotEmpty, isTrue);
    });

    testWidgets('an empty report says so', (tester) async {
      await _pump(
        tester,
        (api, saver) => ReportsPage(api: api, saver: saver, now: _now),
        empty: true,
      );
      expect(find.byKey(const Key('report_empty')), findsOneWidget);
    });

    testWidgets('the builder sends only the picked fields, in menu order', (
      tester,
    ) async {
      final (api, _) = await _pump(
        tester,
        (api, saver) => ReportsPage(api: api, saver: saver, now: _now),
      );
      await tester.tap(find.byKey(const Key('report_custom')));
      await tester.pumpAndSettle();

      // start with everything, drop the client column
      await tester.tap(find.byKey(const Key('field_client')));
      await tester.pump();
      await tester.tap(find.byKey(const Key('run_report')));
      await tester.pumpAndSettle();

      final sent = api.where('/reports/custom').last.data as Map;
      expect(sent['source'], 'Reservations');
      expect(sent['columns'], ['date', 'price']);
      expect(sent['status'], isNull);
      expect(find.byKey(const Key('report_table')), findsOneWidget);
    });

    testWidgets('the builder cannot run without any field', (tester) async {
      final (api, _) = await _pump(
        tester,
        (api, saver) => ReportsPage(api: api, saver: saver, now: _now),
      );
      await tester.tap(find.byKey(const Key('report_custom')));
      await tester.pumpAndSettle();
      for (final k in ['date', 'client', 'price']) {
        await tester.tap(find.byKey(Key('field_$k')));
        await tester.pump();
      }

      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('run_report')))
            .onPressed,
        isNull,
      );
      expect(
        api.where('/reports/custom').where((r) => r.method == 'POST'),
        isEmpty,
      );
    });

    testWidgets('switching source resets the fields to that source\'s menu', (
      tester,
    ) async {
      await _pump(
        tester,
        (api, saver) => ReportsPage(api: api, saver: saver, now: _now),
      );
      await tester.tap(find.byKey(const Key('report_custom')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('field_client')), findsOneWidget);

      await tester.tap(find.byKey(const Key('custom_source')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Narudžbe').last);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('field_client')), findsNothing);
      expect(find.byKey(const Key('field_total')), findsOneWidget);
    });

    testWidgets('a server refusal in the builder is shown', (tester) async {
      final (api, _) = await _pump(
        tester,
        (api, saver) => ReportsPage(api: api, saver: saver, now: _now),
      );
      await tester.tap(find.byKey(const Key('report_custom')));
      await tester.pumpAndSettle();
      api.customStatus = 400;

      await tester.tap(find.byKey(const Key('run_report')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('custom_error')), findsOneWidget);
      expect(find.text('Nepoznato polje.'), findsOneWidget);
    });
  });
}

import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

class _Adapter implements HttpClientAdapter {
  _Adapter(this.body, {this.headers = const {}, this.bytes});
  final Object body;
  final Map<String, List<String>> headers;
  final List<int>? bytes;
  RequestOptions? last;

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    last = o;
    if (bytes != null) {
      return ResponseBody.fromBytes(bytes!, 200, headers: headers);
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

ApiClient _client(_Adapter a) => ApiClient(
  baseUrl: 'http://test',
  dio: Dio()..httpClientAdapter = a,
  readToken: () async => 't',
);

Map<String, dynamic> _kpi(num v, [num? change]) => {
  'value': v,
  'changePercent': change,
};

void main() {
  group('DateRange', () {
    final now = DateTime(2030, 3, 15, 14, 30);

    test('today is one day', () {
      final r = DateRange.today(now);
      expect(r.days, 1);
      expect(r.from, DateTime(2030, 3, 15));
    });

    test('the last 7 days include today', () {
      final r = DateRange.lastDays(now, 7);
      expect(r.from, DateTime(2030, 3, 9));
      expect(r.to, DateTime(2030, 3, 15));
      expect(r.days, 7);
    });

    test('this month runs from the first to today', () {
      final r = DateRange.thisMonth(now);
      expect((r.from, r.to), (DateTime(2030, 3, 1), DateTime(2030, 3, 15)));
    });

    test('last month is the whole previous month, also across a year end', () {
      expect(
        DateRange.lastMonth(now),
        DateRange(DateTime(2030, 2, 1), DateTime(2030, 2, 28)),
      );
      expect(
        DateRange.lastMonth(DateTime(2030, 1, 10)),
        DateRange(DateTime(2029, 12, 1), DateTime(2029, 12, 31)),
      );
    });

    test('the previous period has the same length and ends the day before', () {
      final r = DateRange(DateTime(2030, 3, 11), DateTime(2030, 3, 20));
      expect(
        r.previous,
        DateRange(DateTime(2030, 3, 1), DateTime(2030, 3, 10)),
      );
      expect(r.previous.days, r.days);
    });
  });

  group('ReportsApi', () {
    test('the dashboard is requested with local dates and parsed', () async {
      final adapter = _Adapter({
        'from': '2030-03-01',
        'to': '2030-03-10',
        'granularity': 'Day',
        'revenue': _kpi(85, 70.0),
        'servicesRevenue': _kpi(65, 30.0),
        'productsRevenue': _kpi(20),
        'appointments': _kpi(2, 0),
        'orders': _kpi(2),
        'newClients': _kpi(1),
        'averageTicket': 28.33,
        'noShowRate': 25,
        'cancellationRate': 25.0,
        'series': [
          {
            'start': '2030-03-01',
            'label': '01.03.',
            'services': 25,
            'products': 0,
            'total': 25,
          },
        ],
        'topServices': [
          {'key': '1', 'name': 'Šišanje', 'quantity': 2, 'revenue': 50},
        ],
        'topProducts': [],
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
        ],
      });

      final d = await ReportsApi(_client(adapter))
          .dashboard(DateTime(2030, 3, 1), DateTime(2030, 3, 10));

      expect(adapter.last!.uri.queryParameters, {
        'from': '2030-03-01',
        'to': '2030-03-10',
      });
      expect(d.revenue.value, 85);
      expect(d.revenue.changePercent, 70.0);
      expect(d.productsRevenue.changePercent, isNull);
      expect(d.series.single.label, '01.03.');
      expect(d.topServices.single.name, 'Šišanje');
      expect(d.employees.single.averageRating, 4.5);
    });

    test(
      'a table formats money with two decimals and keeps integers',
      () async {
        final table = ReportTable.fromJson({
          'title': 'T',
          'columns': [
            {'key': 'n', 'title': 'Naziv', 'type': 'Text'},
            {'key': 'q', 'title': 'Kol', 'type': 'Number'},
            {'key': 'r', 'title': 'Prihod', 'type': 'Money'},
          ],
          'rows': [
            ['Šišanje', 2, 50],
            ['Brada', 1, 12.5],
          ],
          'totals': ['Ukupno', 3, 62.5],
        });

        expect(table.format(table.rows[0][1], table.columns[1]), '2');
        expect(table.format(table.rows[0][2], table.columns[2]), '50.00');
        expect(table.format(table.rows[1][2], table.columns[2]), '12.50');
        expect(table.format(null, table.columns[2]), '');
        expect(table.totals![2], 62.5);
      },
    );

    test('the custom query is sent as plain choices', () async {
      final adapter = _Adapter({'title': 'T', 'columns': [], 'rows': []});
      await ReportsApi(_client(adapter)).custom(
        CustomReportQuery(
          source: 'Reservations',
          columns: ['date', 'price'],
          from: DateTime(2030, 3, 1),
          to: DateTime(2030, 3, 31),
          status: 'Completed',
        ),
      );

      final sent = adapter.last!.data as Map;
      expect(sent['source'], 'Reservations');
      expect(sent['columns'], ['date', 'price']);
      expect(sent['from'], '2030-03-01');
      expect(sent['status'], 'Completed');
      expect(sent['employeeId'], isNull);
    });

    test('export downloads bytes and reads the file name', () async {
      final adapter = _Adapter(
        {},
        bytes: [1, 2, 3],
        headers: {
          'content-disposition': [
            'attachment; filename=prihod.xlsx; filename*=UTF-8\'\'prihod.xlsx',
          ],
        },
      );

      final f = await ReportsApi(_client(adapter)).export(
        'revenue',
        DateTime(2030, 3, 1),
        DateTime(2030, 3, 31),
        ExportKind.xlsx,
      );

      expect(adapter.last!.uri.queryParameters['format'], 'Xlsx');
      expect(f.fileName, 'prihod.xlsx');
      expect(f.bytes, [1, 2, 3]);
    });

    test('a hostile file name cannot escape the download folder', () async {
      final adapter = _Adapter(
        {},
        bytes: [1],
        headers: {
          'content-disposition': ['attachment; filename="..%2F..%2Fevil.exe"'],
        },
      );
      final f = await ReportsApi(_client(adapter)).export(
        'revenue',
        DateTime(2030, 3, 1),
        DateTime(2030, 3, 2),
        ExportKind.csv,
      );
      expect(f.fileName, isNot(contains('/')));
      expect(f.fileName, isNot(contains(r'\')));
    });
  });
}

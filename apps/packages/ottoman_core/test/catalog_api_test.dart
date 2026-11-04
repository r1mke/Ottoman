import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

class _Adapter implements HttpClientAdapter {
  _Adapter(this.body);
  final Object body;
  RequestOptions? last;

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    last = o;
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

CatalogApi _api(_Adapter a) => CatalogApi(
  ApiClient(
    baseUrl: 'http://test',
    dio: Dio()..httpClientAdapter = a,
    readToken: () async => 't',
  ),
);

void main() {
  test(
    'availability sends repeated serviceIds and a local date, and parses times',
    () async {
      final adapter = _Adapter({
        'date': '2030-01-07',
        'durationMinutes': 60,
        'totalPrice': 40,
        'allTimes': false,
        'starts': ['2030-01-07T08:00:00Z', '2030-01-07T08:15:00Z'],
        'localTimes': ['09:00', '09:15'],
      });

      final result = await _api(adapter).availability(
        employeeId: 'e1',
        date: DateTime(2030, 1, 7),
        serviceIds: [1, 2],
      );

      final uri = adapter.last!.uri.toString();
      expect(uri, contains('date=2030-01-07'));
      expect(uri, contains('serviceIds=1'));
      expect(uri, contains('serviceIds=2'));
      expect(uri, contains('allTimes=false'));
      expect(result.durationMinutes, 60);
      expect(result.starts.first, DateTime.utc(2030, 1, 7, 8));
      expect(result.localTimes, ['09:00', '09:15']);
    },
  );

  test('services filter by employee and parse prices as doubles', () async {
    final adapter = _Adapter([
      {
        'id': 1,
        'name': 'Šišanje',
        'description': null,
        'price': 25,
        'durationMinutes': 30,
        'imageUrl': null,
        'isActive': true,
      },
    ]);

    final list = await _api(adapter).services(employeeId: 'e1');

    expect(adapter.last!.uri.queryParameters['employeeId'], 'e1');
    expect(list.single.price, 25.0);
    expect(list.single.name, 'Šišanje');
  });

  test(
    'employees parse service ids and hide private fields when absent',
    () async {
      final adapter = _Adapter([
        {
          'id': 'e1',
          'firstName': 'Kerim',
          'lastName': 'Begić',
          'fullName': 'Kerim Begić',
          'title': 'Master Barber',
          'titleId': 3,
          'bio': null,
          'photoUrl': null,
          'isActive': true,
          'serviceIds': [1, 2],
          'email': null,
          'phone': null,
        },
      ]);

      final e = (await _api(adapter).employees()).single;

      expect(e.fullName, 'Kerim Begić');
      expect(e.serviceIds, [1, 2]);
      expect(e.email, isNull);
      expect(e.title, 'Master Barber');
      expect(e.titleId, 3);
    },
  );
}

import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

class _Adapter implements HttpClientAdapter {
  _Adapter(this.body);
  Object body;
  final int status = 200;
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
      status,
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

Map<String, dynamic> _review({int id = 1, int rating = 5}) => {
  'id': id,
  'targetType': 'Employee',
  'targetKey': 'e1',
  'targetName': 'Kerim Begić',
  'authorName': 'Ana A.',
  'rating': rating,
  'comment': 'Odlično',
  'createdAtUtc': '2030-01-07T09:00:00Z',
  'isHidden': false,
  'replyText': null,
  'isMine': true,
};

void main() {
  test('list parses reviews and the average', () async {
    final adapter = _Adapter({
      'items': [_review()],
      'totalCount': 1,
      'average': 4.5,
      'count': 2,
    });
    final list = await ReviewsApi(_client(adapter))
        .list(ReviewTarget.employee, 'e1');

    expect(adapter.last!.uri.queryParameters['targetType'], 'Employee');
    expect(adapter.last!.uri.queryParameters['targetKey'], 'e1');
    expect(list.average, 4.5);
    expect(list.items.single.authorName, 'Ana A.');
  });

  test('summary is keyed by target', () async {
    final api = ReviewsApi(
      _client(
        _Adapter([
          {'targetKey': 'e1', 'average': 4.5, 'count': 2},
          {'targetKey': 'e2', 'average': 3.0, 'count': 1},
        ]),
      ),
    );
    final s = await api.summary(ReviewTarget.employee);
    expect(s['e1']!.count, 2);
    expect(s['e2']!.average, 3.0);
  });

  test('eligibility reads the reason', () async {
    final api = ReviewsApi(
      _client(
        _Adapter({
          'canReview': false,
          'reason': 'Nakon termina.',
          'existingReviewId': null,
        }),
      ),
    );
    final e = await api.eligibility(ReviewTarget.employee, 'e1');
    expect(e.canReview, isFalse);
    expect(e.reason, 'Nakon termina.');
  });

  test('recommendations parse products', () async {
    final api = RecommendationsApi(
      _client(
        _Adapter([
          {
            'id': 1,
            'name': 'Vosak',
            'description': null,
            'brandId': null,
            'brandName': null,
            'categoryId': null,
            'categoryName': null,
            'price': 10,
            'stock': 3,
            'lowStockThreshold': null,
            'isLowStock': false,
            'isActive': true,
          },
        ]),
      ),
    );
    expect((await api.products()).single.name, 'Vosak');
  });

  testWidgets('stars are tappable only when there is a callback', (
    tester,
  ) async {
    int? picked;
    await tester.pumpWidget(
      MaterialApp(
        home: Column(
          children: [
            StarRating(value: 2, onChanged: (v) => picked = v),
            const StarRating(key: Key('ro'), value: 4.5),
          ],
        ),
      ),
    );

    await tester.tap(find.byKey(const Key('star_4')).first);
    expect(picked, 4);
    expect(
      find.descendant(
        of: find.byKey(const Key('ro')),
        matching: find.byIcon(Icons.star_half),
      ),
      findsOneWidget,
    );
  });

  testWidgets('RatingSummary shows nothing without reviews', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(home: RatingSummary(average: 0, count: 0)),
    );
    expect(find.byType(StarRating), findsNothing);
  });
}

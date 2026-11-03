import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_mobile/app.dart';

const _employee = {
  'id': 'e1',
  'firstName': 'Kerim',
  'lastName': 'Begić',
  'fullName': 'Kerim Begić',
  'title': 'Master Barber',
  'bio': null,
  'photoUrl': null,
  'isActive': true,
  'serviceIds': [1],
  'email': null,
  'phone': null,
};

Map<String, dynamic> _product(int id, String name) => {
  'id': id,
  'name': name,
  'description': 'Opis',
  'brandId': 1,
  'brandName': 'Dandy',
  'categoryId': 1,
  'categoryName': 'Voskovi',
  'price': 10,
  'stock': 5,
  'lowStockThreshold': null,
  'isLowStock': false,
  'isActive': true,
};

Map<String, dynamic> _review({int id = 1}) => {
  'id': id,
  'targetType': 'Product',
  'targetKey': '1',
  'targetName': 'Vosak',
  'authorName': 'Ana A.',
  'rating': 4,
  'comment': 'Dobar proizvod',
  'createdAtUtc': DateTime.utc(2030, 1, 7).toIso8601String(),
  'isHidden': false,
  'replyText': 'Hvala!',
  'isMine': false,
};

Map<String, dynamic> _visit() => {
  'id': 5,
  'clientId': 'c1',
  'clientName': 'Ana Anić',
  'employeeId': 'e1',
  'employeeName': 'Kerim Begić',
  'startUtc': DateTime.utc(2029, 1, 7, 9).toIso8601String(),
  'endUtc': DateTime.utc(2029, 1, 7, 9, 30).toIso8601String(),
  'localStart': '2029-01-07 10:00',
  'status': 'Completed',
  'note': null,
  'totalPrice': 25,
  'totalDurationMinutes': 30,
  'items': [
    {'serviceId': 1, 'name': 'Šišanje', 'price': 25, 'durationMinutes': 30},
  ],
  'canCancel': false,
  'createdAtUtc': DateTime.utc(2029).toIso8601String(),
  'cancelledAtUtc': null,
  'cancelReason': null,
};

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  bool canReview = true;
  int createStatus = 201;

  RequestOptions? last(String method, String part) => requests.reversed
      .where((r) => r.method == method && r.path.contains(part))
      .firstOrNull;

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
    Object? body;
    var status = 200;
    final path = o.path;
    if (path.endsWith('/salon')) {
      body = {
        'name': 'Ottoman',
        'description': 'Opis salona',
        'address': null,
        'phone': null,
        'mapsUrl': null,
        'logoUrl': null,
        'hours': [],
      };
    } else if (path.contains('/gallery')) {
      body = {'items': [], 'totalCount': 0};
    } else if (path.endsWith('/login')) {
      body = {
        'accessToken': 'a',
        'refreshToken': 'r',
        'accessTokenExpiresUtc': DateTime.utc(2030).toIso8601String(),
        'user': {
          'id': 'c1',
          'email': 'x@y.z',
          'firstName': 'Ana',
          'lastName': 'Anić',
          'phone': null,
          'roles': ['Client'],
        },
      };
    } else if (path.endsWith('/recommendations/employees')) {
      body = [
        {
          'item': _employee,
          'reason': {
            'kind': 'SimilarToInterest',
            'sourceName': 'Tarik Mujkić',
            'text':
                'Jer ste bili kod frizera Tarik Mujkić - slični pojmovi: fade',
          },
        },
      ];
    } else if (path.endsWith('/recommendations/products')) {
      body = [
        {
          'item': _product(2, 'Preporučeni vosak'),
          'reason': {
            'kind': 'SimilarToInterest',
            'sourceName': 'Dandy Pomade',
            'text': 'Jer ste kupili „Dandy Pomade“ - ista kategorija (Voskovi)',
          },
        },
      ];
    } else if (path.endsWith('/related')) {
      body = [
        {
          'item': _product(3, 'Slični gel'),
          'reason': {
            'kind': 'SimilarToItem',
            'sourceName': 'Vosak',
            'text': 'Slično kao „Vosak“ - isti brend (Dandy)',
          },
        },
      ];
    } else if (path.endsWith('/reviews/summary')) {
      body = [
        {'targetKey': 'e1', 'average': 4.5, 'count': 2},
      ];
    } else if (path.endsWith('/reviews/eligibility')) {
      body = {
        'canReview': canReview,
        'reason': canReview ? null : 'Nakon termina.',
        'existingReviewId': null,
      };
    } else if (path.endsWith('/reviews') && o.method == 'POST') {
      status = createStatus;
      body = createStatus == 201
          ? _review()
          : {
              'status': 400,
              'title': 'Podaci nisu ispravni.',
              'detail': 'Recenziju možete ostaviti nakon završenog termina.',
            };
    } else if (path.endsWith('/reviews')) {
      body = {
        'items': [_review()],
        'totalCount': 1,
        'average': 4.0,
        'count': 1,
      };
    } else if (path.endsWith('/employees') || path.endsWith('/services')) {
      body = path.endsWith('/employees')
          ? [_employee]
          : [
              {
                'id': 1,
                'name': 'Šišanje',
                'description': null,
                'price': 25,
                'durationMinutes': 30,
                'imageUrl': null,
                'isActive': true,
              },
            ];
    } else if (path.endsWith('/overview')) {
      body = {
        'totalProducts': 2,
        'newestImageUrl': null,
        'onSale': {'count': 0, 'maxDiscountPercent': 0, 'imageUrl': null},
        'categories': [],
        'brands': [],
        'blocks': [
          {
            'sectionId': 1,
            'kind': 'Recommended',
            'title': 'Možda vam se sviđa',
          },
        ],
      };
    } else if (path.endsWith('/categories')) {
      body = [];
    } else if (path.endsWith('/products')) {
      body = {
        'items': [_product(1, 'Vosak')],
        'totalCount': 1,
      };
    } else if (path.endsWith('/reservations')) {
      body = {
        'items': o.queryParameters['upcoming'] == false ? [_visit()] : [],
        'totalCount': o.queryParameters['upcoming'] == false ? 1 : 0,
      };
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

Future<_Api> _open(WidgetTester tester, {bool book = false}) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(420, 1200);
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
  await tester.pumpWidget(OttomanMobileApp(auth: auth));
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('login_email')), 'x@y.z');
  await tester.enterText(find.byKey(const Key('login_password')), 'pw');
  await tester.tap(find.byKey(const Key('login_submit')));
  await tester.pumpAndSettle();
  if (book) {
    await tester.tap(find.byKey(const Key('book_now')));
    await tester.pumpAndSettle();
  }
  return api;
}

void main() {
  testWidgets('home shows barber ratings and a "you might like" row', (
    tester,
  ) async {
    await _open(tester, book: true);
    // The barbers come after the services.
    await tester.tap(find.byKey(const Key('service_1')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('continue')));
    await tester.pumpAndSettle();

    expect(find.text('Možda vam se sviđa'), findsOneWidget);
    expect(find.byKey(const Key('reco_employee_e1')), findsOneWidget);
    expect(
      find.descendant(
        of: find.byKey(const Key('reco_reason_e1')),
        matching: find.textContaining('Jer ste bili kod frizera'),
      ),
      findsNothing,
    );
    expect(
      find.textContaining('Jer ste bili kod frizera Tarik Mujkić'),
      findsOneWidget,
    );
    expect(find.text('4.5 (2)'), findsOneWidget);
  });

  testWidgets('the shop suggests products, and a detail page shows reviews, '
      'the salon reply and similar products', (tester) async {
    await _open(tester);
    await tester.tap(find.text('Shop').last);
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('reco_2')), findsOneWidget);
    expect(find.text('Preporučeni vosak'), findsOneWidget);
    expect(find.byKey(const Key('reco_reason_2')), findsOneWidget);
    expect(
      find.textContaining('Jer ste kupili „Dandy Pomade“'),
      findsOneWidget,
    );

    await tester.tap(find.byKey(const Key('all_products')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('product_1')));
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(
      find.byKey(const Key('review_1')),
      200,
      scrollable: find.byType(Scrollable).first,
    );

    expect(find.text('Dobar proizvod'), findsOneWidget);
    expect(find.text('Hvala!'), findsOneWidget);
    expect(find.text('Ana A.'), findsOneWidget);
    expect(find.text('Slični gel'), findsOneWidget);
    expect(find.textContaining('Slično kao „Vosak“'), findsOneWidget);
  });

  testWidgets('a client who may review writes one: stars are required', (
    tester,
  ) async {
    final api = await _open(tester);
    await tester.tap(find.text('Shop').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('all_products')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('product_1')));
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(
      find.byKey(const Key('write_review')),
      200,
      scrollable: find.byType(Scrollable).first,
    );

    await tester.tap(find.byKey(const Key('write_review')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('review_save')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('review_error')), findsOneWidget);
    expect(api.last('POST', '/reviews'), isNull);

    await tester.tap(
      find.descendant(
        of: find.byKey(const Key('review_stars')),
        matching: find.byKey(const Key('star_5')),
      ),
    );
    await tester.enterText(find.byKey(const Key('review_comment')), 'Super');
    await tester.tap(find.byKey(const Key('review_save')));
    await tester.pumpAndSettle();

    final sent = api.last('POST', '/reviews')!.data as Map;
    expect(sent['targetType'], 'Product');
    expect(sent['targetKey'], '1');
    expect(sent['rating'], 5);
    expect(sent['comment'], 'Super');
    expect(find.text('Hvala na recenziji!'), findsOneWidget);
  });

  testWidgets(
    'no write button when the server says the client may not review',
    (tester) async {
      final api = await _open(tester);
      api.canReview = false;
      await tester.tap(find.text('Shop').last);
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('all_products')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('product_1')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('write_review')), findsNothing);
    },
  );

  testWidgets(
    'a finished appointment offers "Ocijeni" and shows the server refusal',
    (tester) async {
      final api = await _open(tester);
      api.createStatus = 400;
      await tester.tap(find.text('Termini').last);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Prošli'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('review_5')));
      await tester.pumpAndSettle();
      await tester.tap(
        find.descendant(
          of: find.byKey(const Key('review_stars')),
          matching: find.byKey(const Key('star_3')),
        ),
      );
      await tester.tap(find.byKey(const Key('review_save')));
      await tester.pumpAndSettle();

      final sent = api.last('POST', '/reviews')!.data as Map;
      expect(sent['targetType'], 'Employee');
      expect(sent['targetKey'], 'e1');
      expect(
        find.text('Recenziju možete ostaviti nakon završenog termina.'),
        findsOneWidget,
      );
    },
  );
}

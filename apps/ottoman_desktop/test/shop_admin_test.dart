import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/app.dart';

Map<String, dynamic> _product(
  int id,
  String name,
  int stock, {
  bool active = true,
}) => {
  'id': id,
  'name': name,
  'description': null,
  'brandId': 1,
  'brandName': 'Dandy',
  'categoryId': 1,
  'categoryName': 'Voskovi',
  'price': 10,
  'stock': stock,
  'lowStockThreshold': 5,
  'isLowStock': stock <= 5,
  'isActive': active,
};

Map<String, dynamic> _order(
  int id,
  String status, {
  String method = 'Card',
  String? paymentStatus = 'Succeeded',
}) => {
  'id': id,
  'clientId': 'c1',
  'clientName': 'Elmir Mujkić',
  'status': status,
  'fulfilment': 'Pickup',
  'paymentMethod': method,
  'paymentStatus': paymentStatus,
  'deliveryAddress': null,
  'note': null,
  'totalPrice': 20,
  'createdAtUtc': DateTime.utc(2030, 1, 7, 10).toIso8601String(),
  'expiresUtc': null,
  'items': [
    {
      'productId': 1,
      'productName': 'Vosak',
      'unitPrice': 10,
      'quantity': 2,
      'lineTotal': 20,
    },
  ],
  'paymentClientSecret': null,
};

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  int stockStatus = 200;
  List<Map<String, dynamic>> products = [
    _product(1, 'Dandy Pomade', 25),
    _product(2, 'Beard Balm', 3),
  ];
  List<Map<String, dynamic>> orders = [
    _order(11, 'Placed'),
    _order(12, 'PendingPayment', paymentStatus: 'Pending'),
    _order(13, 'Fulfilled'),
  ];

  Iterable<RequestOptions> where(String method, String part) =>
      requests.where((r) => r.method == method && r.path.contains(part));

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
    if (path.endsWith('/login')) {
      body = {
        'accessToken': 'a',
        'refreshToken': 'r',
        'accessTokenExpiresUtc': DateTime.utc(2030).toIso8601String(),
        'user': {
          'id': 'a1',
          'email': 'x@y.z',
          'firstName': 'Admin',
          'lastName': 'Ottoman',
          'phone': null,
          'roles': ['Admin'],
        },
      };
    } else if (path.endsWith('/brands')) {
      body = [
        {'id': 1, 'name': 'Dandy'},
      ];
    } else if (path.endsWith('/categories')) {
      body = [
        {'id': 1, 'name': 'Voskovi', 'description': null},
      ];
    } else if (path.endsWith('/stock')) {
      status = stockStatus;
      body = stockStatus == 200
          ? _product(2, 'Beard Balm', 13)
          : {
              'status': 409,
              'title': 'Sukob podataka.',
              'detail': 'Zaliha ne može biti negativna.',
            };
    } else if (path.endsWith('/products') && o.method == 'GET') {
      var items = products;
      if (o.queryParameters['lowStock'] == true) {
        items = items.where((p) => p['isLowStock'] == true).toList();
      }
      body = {'items': items, 'totalCount': items.length};
    } else if (path.endsWith('/products') && o.method == 'POST') {
      status = 201;
      body = _product(9, 'Novi', 4);
    } else if (path.endsWith('/orders')) {
      final f = o.queryParameters['status'];
      final items = f == null
          ? orders
          : orders.where((x) => x['status'] == f).toList();
      body = {'items': items, 'totalCount': items.length};
    } else if (path.endsWith('/fulfil')) {
      body = _order(11, 'Fulfilled');
    } else if (path.endsWith('/cancel')) {
      body = _order(11, 'Cancelled', paymentStatus: 'Refunded');
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

Future<_Api> _open(WidgetTester tester, String section) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(1600, 1000);
  addTearDown(tester.view.reset);

  final api = _Api();
  late final AuthController auth;
  auth = AuthController(
    baseUrl: 'http://test',
    storage: MemoryTokenStorage(),
    allowedRoles: {Roles.admin, Roles.employee},
    api: ApiClient(
      baseUrl: 'http://test',
      dio: Dio()..httpClientAdapter = api,
      readToken: () async => auth.accessToken,
    ),
  );
  await auth.restore();
  await tester.pumpWidget(
    OttomanDesktopApp(
      auth: auth,
      profiles: SavedProfiles(MemorySavedProfilesStore()),
    ),
  );
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('login_email')), 'x@y.z');
  await tester.enterText(find.byKey(const Key('login_password')), 'pw');
  await tester.tap(find.byKey(const Key('login_submit')));
  await tester.pumpAndSettle();
  await tester.tap(find.text(section).first);
  await tester.pumpAndSettle();
  return api;
}

void main() {
  testWidgets('products: table shows stock and flags low stock', (
    tester,
  ) async {
    await _open(tester, 'Proizvodi');

    expect(find.text('Dandy Pomade'), findsOneWidget);
    expect(find.text('Beard Balm'), findsOneWidget);
    expect(find.byKey(const Key('low_2')), findsOneWidget);
    expect(find.byKey(const Key('low_1')), findsNothing);
    expect(find.text('2 proizvoda'), findsOneWidget);
    // Every product row carries its picture (a placeholder when it has none).
    expect(find.byKey(const Key('product_picture_1')), findsOneWidget);
    expect(find.byKey(const Key('product_picture_2')), findsOneWidget);
  });

  testWidgets('products: the low-stock filter is sent to the server', (
    tester,
  ) async {
    final api = await _open(tester, 'Proizvodi');

    await tester.tap(find.byKey(const Key('low_only')));
    await tester.pumpAndSettle();

    expect(
      api.where('GET', '/products').last.queryParameters['lowStock'],
      true,
    );
    expect(find.text('Dandy Pomade'), findsNothing);
    expect(find.text('Beard Balm'), findsOneWidget);
  });

  testWidgets('products: search is sent to the server on submit', (
    tester,
  ) async {
    final api = await _open(tester, 'Proizvodi');

    await tester.enterText(find.byKey(const Key('products_search')), 'balm');
    await tester.testTextInput.receiveAction(TextInputAction.search);
    await tester.pumpAndSettle();

    expect(
      api.where('GET', '/products').last.queryParameters['search'],
      'balm',
    );
  });

  testWidgets('products: creating one posts the typed values', (tester) async {
    final api = await _open(tester, 'Proizvodi');

    await tester.tap(find.byKey(const Key('add_product')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('prod_name')), 'Novi');
    await tester.enterText(find.byKey(const Key('prod_price')), '12,50');
    await tester.enterText(find.byKey(const Key('prod_stock')), '4');
    await tester.enterText(find.byKey(const Key('prod_threshold')), '2');
    await tester.tap(find.byKey(const Key('prod_save')));
    await tester.pumpAndSettle();

    final sent = api.where('POST', '/products').single.data as Map;
    expect(sent['name'], 'Novi');
    expect(sent['price'], 12.5);
    expect(sent['stock'], 4);
    expect(sent['lowStockThreshold'], 2);
    expect(find.byKey(const Key('prod_save')), findsNothing); // dialog closed
  });

  testWidgets('products: an invalid price is caught before any request', (
    tester,
  ) async {
    final api = await _open(tester, 'Proizvodi');

    await tester.tap(find.byKey(const Key('add_product')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('prod_name')), 'X');
    await tester.enterText(find.byKey(const Key('prod_price')), '0');
    await tester.tap(find.byKey(const Key('prod_save')));
    await tester.pumpAndSettle();

    expect(api.where('POST', '/products'), isEmpty);
    expect(find.text('Unesite ispravan broj'), findsOneWidget);
  });

  testWidgets(
    'products: stock adjustment sends the delta; a server refusal is shown',
    (tester) async {
      final api = await _open(tester, 'Proizvodi');

      await tester.tap(find.byKey(const Key('stock_2')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('stock_delta')), '10');
      await tester.tap(find.byKey(const Key('stock_apply')));
      await tester.pumpAndSettle();
      expect((api.where('POST', '/stock').single.data as Map)['delta'], 10);
      expect(find.byKey(const Key('stock_apply')), findsNothing);

      api.stockStatus = 409;
      await tester.tap(find.byKey(const Key('stock_2')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('stock_delta')), '-99');
      await tester.tap(find.byKey(const Key('stock_apply')));
      await tester.pumpAndSettle();
      expect(find.text('Zaliha ne može biti negativna.'), findsOneWidget);
      expect(
        find.byKey(const Key('stock_apply')),
        findsOneWidget,
      ); // stays open
    },
  );

  testWidgets('handing over an order asks first; saying no sends nothing', (
    tester,
  ) async {
    final api = await _open(tester, 'Narudžbe');
    await tester.tap(find.byKey(const Key('fulfil_11')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('confirm_action_text')), findsOneWidget);
    await tester.tap(find.byKey(const Key('confirm_action_cancel')));
    await tester.pumpAndSettle();
    expect(api.where('POST', '/orders/11/fulfil'), isEmpty);
  });

  testWidgets(
    'orders: admin sees all, can hand over a paid order and cancel one',
    (tester) async {
      final api = await _open(tester, 'Narudžbe');

      expect(find.byKey(const Key('pay_status_11')), findsOneWidget);
      expect(find.text('Elmir Mujkić'), findsWidgets);
      expect(find.text('2× Vosak'), findsWidgets);
      expect(find.byKey(const Key('fulfil_11')), findsOneWidget);
      expect(
        find.byKey(const Key('fulfil_12')),
        findsNothing,
      ); // unpaid: nothing to hand over
      expect(find.byKey(const Key('fulfil_13')), findsNothing); // already done
      expect(find.byKey(const Key('cancel_13')), findsNothing);

      await tester.tap(find.byKey(const Key('fulfil_11')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm_action')));
      await tester.pumpAndSettle();
      expect(api.where('POST', '/orders/11/fulfil'), hasLength(1));

      await tester.tap(find.byKey(const Key('cancel_11')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm_cancel_order')));
      await tester.pumpAndSettle();
      expect(api.where('POST', '/orders/11/cancel'), hasLength(1));
    },
  );

  testWidgets('orders: the status filter is sent to the server', (
    tester,
  ) async {
    final api = await _open(tester, 'Narudžbe');

    await tester.tap(find.byKey(const Key('order_status_filter')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Preuzeta').last);
    await tester.pumpAndSettle();

    expect(
      api.where('GET', '/orders').last.queryParameters['status'],
      'Fulfilled',
    );
    expect(find.byKey(const Key('pay_status_13')), findsOneWidget);
    expect(find.byKey(const Key('pay_status_11')), findsNothing);
  });
}

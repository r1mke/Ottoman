import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/features/images/picture_editor.dart';
import 'package:ottoman_desktop/features/products/product_dialogs.dart';

class _Chooser implements ImageChooser {
  _Chooser(this.next);
  ChosenImage? next;
  int calls = 0;

  @override
  Future<ChosenImage?> choose() async {
    calls++;
    return next;
  }
}

class _Server implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  int status = 200;
  Object body = {};

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
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

Map<String, dynamic> _product(List<Map<String, dynamic>> images) => {
  'id': 7,
  'name': 'Vosak',
  'price': 10,
  'stock': 3,
  'isLowStock': false,
  'isActive': true,
  'imageUrl': images.isEmpty ? null : images.first['url'],
  'images': images,
};

Future<void> _pump(WidgetTester tester, Widget child) {
  tester.view.physicalSize = const Size(1000, 1600);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  return tester.pumpWidget(
    MaterialApp(
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
      locale: const Locale('bs'),
      home: Scaffold(body: child),
    ),
  );
}

ShopApi _shop(_Server server) => ShopApi(
  ApiClient(
    baseUrl: 'http://test',
    dio: Dio()..httpClientAdapter = server,
    readToken: () async => 't',
  ),
);

void main() {
  final png = ChosenImage('foto.png', Uint8List.fromList([1, 2, 3]));

  testWidgets('adding a picture uploads it and shows what the server now has', (
    tester,
  ) async {
    final server = _Server()
      ..body = _product([
        {'id': 11, 'url': 'https://x/a'},
      ]);
    final chooser = _Chooser(png);
    final product = Product.fromJson(_product([]));

    await _pump(
      tester,
      ProductDialogHost(
        shop: _shop(server),
        product: product,
        chooser: chooser,
      ),
    );
    await tester.ensureVisible(find.byKey(const Key('add_picture')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('add_picture')));
    await tester.pumpAndSettle();

    expect(chooser.calls, 1);
    expect(server.requests.single.path, '/api/v1/products/7/images');
    expect(server.requests.single.method, 'POST');
    expect(find.byKey(const Key('picture_11')), findsOneWidget);
  });

  testWidgets('cancelling the file dialog sends nothing', (tester) async {
    final server = _Server();
    await _pump(
      tester,
      ProductDialogHost(
        shop: _shop(server),
        product: Product.fromJson(_product([])),
        chooser: _Chooser(null),
      ),
    );
    await tester.ensureVisible(find.byKey(const Key('add_picture')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('add_picture')));
    await tester.pumpAndSettle();

    expect(server.requests, isEmpty);
  });

  testWidgets(
    'a picture the server refuses shows its message and changes nothing',
    (tester) async {
      final server = _Server()
        ..status = 400
        ..body = {
          'title': 'Podaci nisu ispravni.',
          'detail': 'Dozvoljene su samo JPEG, PNG i WebP slike.',
        };
      await _pump(
        tester,
        ProductDialogHost(
          shop: _shop(server),
          product: Product.fromJson(_product([])),
          chooser: _Chooser(png),
        ),
      );
      await tester.tap(find.byKey(const Key('add_picture')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('picture_error')), findsOneWidget);
      expect(find.textContaining('JPEG'), findsOneWidget);
      expect(find.byKey(const Key('add_picture')), findsOneWidget);
    },
  );

  testWidgets(
    'removing a picture calls the delete endpoint and drops the thumbnail',
    (tester) async {
      final server = _Server()..body = _product([]);
      await _pump(
        tester,
        ProductDialogHost(
          shop: _shop(server),
          product: Product.fromJson(
            _product([
              {'id': 11, 'url': 'https://x/a'},
            ]),
          ),
          chooser: _Chooser(null),
        ),
      );
      expect(find.byKey(const Key('picture_11')), findsOneWidget);

      await tester.tap(find.byKey(const Key('remove_picture_11')));
      await tester.pumpAndSettle();

      expect(server.requests.single.method, 'DELETE');
      expect(server.requests.single.path, '/api/v1/products/7/images/11');
      expect(find.byKey(const Key('picture_11')), findsNothing);
    },
  );

  testWidgets('add is hidden once the product is at its limit', (tester) async {
    final server = _Server();
    final eight = [
      for (var i = 1; i <= 8; i++) {'id': i, 'url': 'https://x/$i'},
    ];
    await _pump(
      tester,
      ProductDialogHost(
        shop: _shop(server),
        product: Product.fromJson(_product(eight)),
        chooser: _Chooser(png),
      ),
    );
    expect(find.byKey(const Key('add_picture')), findsNothing);
  });

  testWidgets('a new product explains that pictures come after saving', (
    tester,
  ) async {
    await _pump(
      tester,
      ProductDialogHost(
        shop: _shop(_Server()),
        product: null,
        chooser: _Chooser(png),
      ),
    );
    expect(find.byKey(const Key('add_picture')), findsNothing);
    expect(
      find.text('Slike se mogu dodati nakon što se stavka spremi.'),
      findsOneWidget,
    );
  });
}

/// The real product dialog, shown inline so the picture section can be driven without opening routes.
class ProductDialogHost extends StatelessWidget {
  const ProductDialogHost({
    super.key,
    required this.shop,
    required this.product,
    required this.chooser,
  });

  final ShopApi shop;
  final Product? product;
  final ImageChooser chooser;

  @override
  Widget build(BuildContext context) => SizedBox(
    height: 1500,
    child: ProductDialog(
      shop: shop,
      brands: const [],
      categories: const [],
      product: product,
      chooser: chooser,
    ),
  );
}

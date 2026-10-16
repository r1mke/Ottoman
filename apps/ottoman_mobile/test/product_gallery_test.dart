import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_mobile/features/shop/product_gallery.dart';

Widget _host(List<ProductImage> images) => MaterialApp(
  home: Scaffold(body: ProductGallery(images: images)),
);

void main() {
  testWidgets(
    'a product without pictures shows the placeholder, not a gallery',
    (tester) async {
      await tester.pumpWidget(_host(const []));
      expect(find.byKey(const Key('picture-placeholder')), findsOneWidget);
      expect(find.byKey(const Key('gallery')), findsNothing);
    },
  );

  testWidgets(
    'several pictures can be swiped and a failing one degrades to the placeholder',
    (tester) async {
      await tester.pumpWidget(
        _host(const [
          ProductImage(id: 1, url: 'http://127.0.0.1:1/a.png'),
          ProductImage(id: 2, url: 'http://127.0.0.1:1/b.png'),
        ]),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 50));
      expect(find.byKey(const Key('gallery')), findsOneWidget);

      await tester.drag(
        find.byKey(const Key('gallery')),
        const Offset(-400, 0),
      );
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.byKey(const Key('picture-placeholder')), findsWidgets);
    },
  );
}

import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../reviews/reviews_section.dart';
import 'product_gallery.dart';
import 'product_visuals.dart';
import 'recommended_row.dart';

class ProductDetailPage extends StatefulWidget {
  const ProductDetailPage({
    super.key,
    required this.product,
    required this.cart,
    required this.reviews,
    required this.recommendations,
  });

  final Product product;
  final CartController cart;
  final ReviewsApi reviews;
  final RecommendationsApi recommendations;

  @override
  State<ProductDetailPage> createState() => _ProductDetailPageState();
}

class _ProductDetailPageState extends State<ProductDetailPage> {
  int _qty = 1;

  Product get product => widget.product;
  CartController get cart => widget.cart;
  ReviewsApi get reviews => widget.reviews;
  RecommendationsApi get recommendations => widget.recommendations;

  /// How many more can still go in: the stock (capped per line) minus what the basket already holds.
  int get _room => cart.limitFor(product) - cart.quantityOf(product.id);

  void _add() {
    final t = AppLocalizations.of(context);
    final q = _qty < _room ? _qty : _room;
    if (q <= 0) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(t.limitReached)));
      return;
    }
    cart.setQuantity(product, cart.quantityOf(product.id) + q);
    setState(() => _qty = 1);
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(t.addedToCartCount(q))));
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    final p = product;
    final maxQty = _room < 1 ? 1 : _room;
    return Scaffold(
      appBar: AppBar(title: Text(p.name)),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          ProductGallery(images: p.images),
          const SizedBox(height: 16),
          Text(
            p.name,
            style: Theme.of(context).textTheme.headlineSmall
                ?.copyWith(fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 4),
          Wrap(
            spacing: 8,
            children: [
              if (p.brandName != null) Chip(label: Text(p.brandName!)),
              if (p.categoryName != null) Chip(label: Text(p.categoryName!)),
            ],
          ),
          const SizedBox(height: 8),
          PriceTag(product: p, big: true),
          const SizedBox(height: 4),
          Text(
            p.inStock ? t.inStockCount(p.stock) : t.outOfStock,
            style: TextStyle(color: p.inStock ? scheme.outline : scheme.error),
          ),
          if (p.description != null) ...[
            const SizedBox(height: 16),
            Text(p.description!),
          ],
          const SizedBox(height: 24),
          if (p.inStock)
            Row(
              children: [
                Text(t.quantity, style: Theme.of(context).textTheme.titleSmall),
                const Spacer(),
                IconButton.outlined(
                  key: const Key('qty_minus'),
                  onPressed: _qty > 1 ? () => setState(() => _qty--) : null,
                  icon: const Icon(Icons.remove),
                ),
                SizedBox(
                  width: 48,
                  child: Text(
                    '$_qty',
                    key: const Key('qty_value'),
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.titleMedium
                        ?.copyWith(fontWeight: FontWeight.w800),
                  ),
                ),
                IconButton.outlined(
                  key: const Key('qty_plus'),
                  onPressed: _qty < maxQty
                      ? () => setState(() => _qty++)
                      : null,
                  icon: const Icon(Icons.add),
                ),
              ],
            ),
          const SizedBox(height: 12),
          FilledButton.icon(
            key: const Key('detail_add'),
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(52),
            ),
            onPressed: p.inStock ? _add : null,
            icon: const Icon(Icons.add_shopping_cart),
            label: Text(t.addToCart),
          ),
          const SizedBox(height: 24),
          ReviewsSection(
            api: reviews,
            targetType: ReviewTarget.product,
            targetKey: '${p.id}',
            title: p.name,
          ),
          const SizedBox(height: 16),
          RecommendedRow(
            title: t.similarProducts,
            load: () => recommendations.relatedProducts(p.id),
            cart: cart,
            onOpen: (other) => Navigator.of(context).push(
              MaterialPageRoute<void>(
                builder: (_) => ProductDetailPage(
                  product: other,
                  cart: cart,
                  reviews: reviews,
                  recommendations: recommendations,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

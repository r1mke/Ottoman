import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../reviews/reviews_section.dart';
import 'recommended_row.dart';

class ProductDetailPage extends StatelessWidget {
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
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    final p = product;
    return Scaffold(
      appBar: AppBar(title: Text(p.name)),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Container(
            height: 200,
            decoration: BoxDecoration(
              color: scheme.surfaceContainerHighest,
              borderRadius: BorderRadius.circular(16),
            ),
            child: Icon(
              Icons.inventory_2_outlined,
              size: 72,
              color: scheme.outline,
            ),
          ),
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
          Text(
            money(t, p.price),
            style: Theme.of(context).textTheme.titleLarge
                ?.copyWith(fontWeight: FontWeight.w800),
          ),
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
          FilledButton.icon(
            key: const Key('detail_add'),
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(52),
            ),
            onPressed: p.inStock
                ? () {
                    final ok = cart.add(p);
                    ScaffoldMessenger.of(context)
                      ..hideCurrentSnackBar()
                      ..showSnackBar(
                        SnackBar(
                          content: Text(ok ? t.addToCart : t.limitReached),
                        ),
                      );
                  }
                : null,
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

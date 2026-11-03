import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'shop_sections.dart';

/// "Možda vam se sviđa" / "Slično": a row that looks like every other section of the shop, with the reason for
/// each suggestion under the product. Hidden while loading, on error and when there is nothing to suggest:
/// a suggestion row must never get in the way.
class RecommendedRow extends StatelessWidget {
  const RecommendedRow({
    super.key,
    required this.title,
    this.eyebrow,
    this.centered = false,
    this.top = 28,
    required this.load,
    required this.cart,
    required this.onOpen,
  });

  final String title;
  final String? eyebrow;
  final bool centered;
  final double top;
  final Future<List<Recommended<Product>>> Function() load;
  final CartController cart;
  final void Function(Product) onOpen;

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<List<Recommended<Product>>>(
      future: load(),
      builder: (context, snap) {
        final items = snap.data;
        if (items == null || items.isEmpty) return const SizedBox.shrink();
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SectionHeading(
              title,
              eyebrow: eyebrow,
              centered: centered,
              top: 28,
            ),
            SizedBox(
              height: 280,
              child: ListView.separated(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.symmetric(horizontal: 16),
                itemCount: items.length,
                separatorBuilder: (_, _) => const SizedBox(width: 12),
                itemBuilder: (context, i) => ProductTile(
                  product: items[i].item,
                  cart: cart,
                  onOpen: onOpen,
                  keyPrefix: 'reco',
                  reason: items[i].reason,
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}

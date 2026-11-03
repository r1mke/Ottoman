import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'product_visuals.dart';

/// One product as a row of a list: picture, name, brand, price and a basket button.
class ProductCard extends StatelessWidget {
  const ProductCard({
    super.key,
    required this.product,
    required this.onAdd,
    required this.onOpen,
  });

  final Product product;
  final VoidCallback onAdd;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    final p = product;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        key: Key('product_${p.id}'),
        onTap: onOpen,
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              ClipRRect(
                borderRadius: BorderRadius.circular(12),
                child: ProductPicture(product: p, width: 72, height: 72),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      p.name,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                    if (p.brandName != null)
                      Text(
                        p.brandName!,
                        style: TextStyle(color: scheme.outline, fontSize: 12),
                      ),
                    const SizedBox(height: 4),
                    PriceTag(product: p),
                    if (!p.inStock)
                      Text(
                        t.outOfStock,
                        style: TextStyle(color: scheme.error, fontSize: 12),
                      )
                    else if (p.isLowStock)
                      Text(
                        t.inStockCount(p.stock),
                        style: TextStyle(
                          color: Colors.orange.shade800,
                          fontSize: 12,
                        ),
                      ),
                  ],
                ),
              ),
              IconButton.filled(
                key: Key('add_${p.id}'),
                tooltip: t.addToCart,
                onPressed: p.inStock ? onAdd : null,
                icon: const Icon(Icons.add_shopping_cart),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

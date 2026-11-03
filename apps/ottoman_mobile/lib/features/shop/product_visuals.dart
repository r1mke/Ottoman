import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// The price a customer pays, with the regular price struck through while the product is on sale.
class PriceTag extends StatelessWidget {
  const PriceTag({super.key, required this.product, this.big = false});

  final Product product;
  final bool big;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    final p = product;
    final size = big ? 22.0 : 14.0;
    final main = Text(
      money(t, p.payPrice),
      key: Key('price_${p.id}'),
      style: TextStyle(
        fontWeight: FontWeight.w800,
        fontSize: size,
        color: p.onSale ? scheme.error : null,
      ),
    );
    if (!p.onSale) return main;
    return Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      spacing: 6,
      children: [
        main,
        Text(
          money(t, p.price),
          key: Key('old_price_${p.id}'),
          style: TextStyle(
            decoration: TextDecoration.lineThrough,
            color: scheme.outline,
            fontSize: size * 0.8,
          ),
        ),
      ],
    );
  }
}

/// A small red "-20%" corner label.
class SaleBadge extends StatelessWidget {
  const SaleBadge({super.key, required this.percent});

  final int percent;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: scheme.error,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        '-$percent%',
        style: TextStyle(
          color: scheme.onError,
          fontWeight: FontWeight.w800,
          fontSize: 12,
        ),
      ),
    );
  }
}

/// A product picture with the sale badge laid over its corner.
class ProductPicture extends StatelessWidget {
  const ProductPicture({
    super.key,
    required this.product,
    required this.width,
    required this.height,
  });

  final Product product;
  final double width;
  final double height;

  @override
  Widget build(BuildContext context) {
    return Stack(
      children: [
        NetworkPicture(
          url: product.imageUrl,
          width: width,
          height: height,
          fallbackIcon: Icons.inventory_2_outlined,
        ),
        if (product.onSale)
          Positioned(
            top: 6,
            left: 6,
            child: SaleBadge(percent: product.discountPercent),
          ),
      ],
    );
  }
}

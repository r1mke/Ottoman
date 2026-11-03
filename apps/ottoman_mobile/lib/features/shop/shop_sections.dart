import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'product_visuals.dart';

/// How big the product cards of a section are: width, picture height and the height of the sideways row.
class _TileMetrics {
  const _TileMetrics(this.width, this.picture, this.rowHeight, this.gridHeight);

  final double width;
  final double picture;
  final double rowHeight;

  /// Height of one cell when the products are laid out in two columns.
  final double gridHeight;

  static _TileMetrics of(String size) => switch (size) {
    'Compact' => const _TileMetrics(128, 100, 232, 222),
    'Large' => const _TileMetrics(188, 156, 306, 292),
    _ => const _TileMetrics(156, 124, 262, 250),
  };
}

/// One section of the shop entry page, drawn the way the admin set it up: its heading (a small amber text, a white
/// title and a bar), its products as a row, a grid or under a banner, on a panel if one is chosen, and a "see all"
/// link when asked for.
class ShopSectionBlock extends StatelessWidget {
  const ShopSectionBlock({
    super.key,
    required this.section,
    required this.cart,
    required this.onOpen,
    required this.onSeeAll,
  });

  final ShopSection section;
  final CartController cart;
  final void Function(Product) onOpen;
  final VoidCallback onSeeAll;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final style = section.style;
    final banner = section.layout == ShopSectionLayout.banner;
    // A banner always stands on a panel together with its products, so the two read as one piece.
    final background = banner && style.background == 'None'
        ? 'Soft'
        : style.background;
    final onPanel = background != 'None';
    final metrics = _TileMetrics.of(style.tileSize);

    final content = Column(
      key: Key('section_${section.id}'),
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (banner)
          _Banner(section: section, onPanel: onPanel)
        else
          SectionHeading(
            section.title,
            eyebrow: section.eyebrow,
            centered: style.centered,
            top: onPanel ? 20 : 28,
          ),
        if (section.layout == ShopSectionLayout.grid)
          _ProductGrid(
            section: section,
            cart: cart,
            onOpen: onOpen,
            metrics: metrics,
          )
        else
          SizedBox(
            height: metrics.rowHeight,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16),
              itemCount: section.products.length,
              separatorBuilder: (_, _) => const SizedBox(width: 12),
              itemBuilder: (context, i) => ProductTile(
                product: section.products[i],
                cart: cart,
                onOpen: onOpen,
                width: metrics.width,
                pictureHeight: metrics.picture,
              ),
            ),
          ),
        if (style.showSeeAll)
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 6, 16, 0),
            child: Align(
              alignment: style.centered
                  ? Alignment.center
                  : Alignment.centerRight,
              child: TextButton.icon(
                key: Key('see_all_${section.id}'),
                onPressed: onSeeAll,
                icon: const Icon(Icons.arrow_forward_rounded, size: 18),
                iconAlignment: IconAlignment.end,
                label: Text(t.seeAll),
              ),
            ),
          ),
        if (onPanel) const SizedBox(height: 14),
      ],
    );
    if (!onPanel) return content;
    return SectionPanel(background: background, child: content);
  }
}

/// The ground a block stands on: a soft dark panel, or one with a warm amber tint. The admin chooses it for every
/// block of the entry page: sections, category tiles, brand tiles and suggestions alike.
class SectionPanel extends StatelessWidget {
  const SectionPanel({
    super.key,
    required this.background,
    required this.child,
  });

  final String background;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final amber = background == 'Amber';
    return Container(
      key: Key('panel_${background.toLowerCase()}'),
      margin: const EdgeInsets.fromLTRB(12, 18, 12, 4),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(28),
        border: Border.all(
          color: amber
              ? scheme.primary.withValues(alpha: 0.35)
              : scheme.outlineVariant,
        ),
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: amber
              ? const [Color(0xFF3A2B0A), Color(0xFF181510)]
              : const [Color(0xFF1B1C26), Color(0xFF14151C)],
        ),
      ),
      child: child,
    );
  }
}

/// A wide picture with the section title over it: the "banner" layout. On a panel it sits inside it, with room
/// around, so the banner and the products below belong together.
class _Banner extends StatelessWidget {
  const _Banner({required this.section, required this.onPanel});

  final ShopSection section;
  final bool onPanel;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: EdgeInsets.fromLTRB(
        onPanel ? 12 : 16,
        onPanel ? 12 : 12,
        onPanel ? 12 : 16,
        14,
      ),
      child: ClipRRect(
        key: Key('banner_${section.id}'),
        borderRadius: BorderRadius.circular(onPanel ? 20 : 24),
        child: SizedBox(
          height: 170,
          width: double.infinity,
          child: Stack(
            fit: StackFit.expand,
            children: [
              if (section.imageUrl != null)
                NetworkPicture(url: section.imageUrl)
              else
                ColoredBox(color: scheme.surfaceContainer),
              const DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                    colors: [Colors.transparent, Color(0xCC000000)],
                    stops: [0.3, 1],
                  ),
                ),
              ),
              Positioned(
                left: 18,
                right: 18,
                bottom: 16,
                child: Column(
                  crossAxisAlignment: section.style.centered
                      ? CrossAxisAlignment.center
                      : CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    if (section.eyebrow != null &&
                        section.eyebrow!.trim().isNotEmpty) ...[
                      Text(
                        section.eyebrow!.toUpperCase(),
                        style: TextStyle(
                          color: scheme.primary,
                          fontWeight: FontWeight.w800,
                          fontSize: 13,
                          letterSpacing: 0.6,
                        ),
                      ),
                      const SizedBox(height: 2),
                    ],
                    Text(
                      section.title,
                      maxLines: 2,
                      textAlign: section.style.centered
                          ? TextAlign.center
                          : TextAlign.start,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.w800,
                        fontSize: 26,
                        height: 1.1,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Container(
                      width: 48,
                      height: 4,
                      decoration: BoxDecoration(
                        color: scheme.primary,
                        borderRadius: BorderRadius.circular(2),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Two columns of product tiles, one under the other.
class _ProductGrid extends StatelessWidget {
  const _ProductGrid({
    required this.section,
    required this.cart,
    required this.onOpen,
    required this.metrics,
  });

  final ShopSection section;
  final CartController cart;
  final void Function(Product) onOpen;
  final _TileMetrics metrics;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: LayoutBuilder(
        builder: (context, box) {
          const gap = 12.0;
          final width = (box.maxWidth - gap) / 2;
          return GridView.count(
            key: Key('grid_${section.id}'),
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            crossAxisCount: 2,
            mainAxisSpacing: gap,
            crossAxisSpacing: gap,
            childAspectRatio: width / metrics.gridHeight,
            children: [
              for (final p in section.products)
                ProductTile(
                  product: p,
                  cart: cart,
                  onOpen: onOpen,
                  width: width,
                  pictureHeight: metrics.picture,
                ),
            ],
          );
        },
      ),
    );
  }
}

/// One product card of a horizontal row: picture, name, brand, price and a basket button. Shared by the arranged
/// sections and the suggestions so both look the same. [keyPrefix] keeps their widget keys apart.
class ProductTile extends StatelessWidget {
  const ProductTile({
    super.key,
    required this.product,
    required this.cart,
    required this.onOpen,
    this.keyPrefix = 'tile',
    this.reason,
    this.width = 156,
    this.pictureHeight = 124,
  });

  final Product product;
  final CartController cart;
  final void Function(Product) onOpen;
  final String keyPrefix;

  /// How wide the tile is (a tile of a sideways row is fixed; in a grid it is half the page).
  final double width;
  final double pictureHeight;

  /// Why this was suggested (suggestions only); shown in small italics under the brand.
  final String? reason;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final p = product;
    return SizedBox(
      width: width,
      child: Card(
        key: Key('${keyPrefix}_${p.id}'),
        margin: EdgeInsets.zero,
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: () => onOpen(p),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              ProductPicture(product: p, width: width, height: pictureHeight),
              Padding(
                padding: const EdgeInsets.fromLTRB(10, 8, 6, 4),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      p.name,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 13,
                      ),
                    ),
                    if (p.brandName != null)
                      Text(
                        p.brandName!,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(color: scheme.outline, fontSize: 11),
                      ),
                    if (reason != null)
                      Text(
                        reason!,
                        key: Key('${keyPrefix}_reason_${p.id}'),
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          color: scheme.primary,
                          fontSize: 11,
                          fontStyle: FontStyle.italic,
                        ),
                      ),
                  ],
                ),
              ),
              const Spacer(),
              Padding(
                padding: const EdgeInsets.fromLTRB(10, 0, 2, 4),
                child: Row(
                  children: [
                    Expanded(child: PriceTag(product: p)),
                    IconButton(
                      key: Key('${keyPrefix}_add_${p.id}'),
                      visualDensity: VisualDensity.compact,
                      onPressed: p.inStock ? () => cart.add(p) : null,
                      icon: const Icon(Icons.add_shopping_cart, size: 20),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

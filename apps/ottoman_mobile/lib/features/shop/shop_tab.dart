import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'product_detail_page.dart';
import 'product_list_page.dart';
import 'recommended_row.dart';
import 'shop_sections.dart';

/// The shop's entry page: what it offers at a glance (sales, new products, categories, brands). Tapping any of
/// them opens the list of products behind it; nothing here is a long list of products itself.
class ShopTab extends StatefulWidget {
  const ShopTab({
    super.key,
    required this.shop,
    required this.cart,
    required this.reviews,
    required this.recommendations,
    this.cardPayment = const UnavailableCardPayment(),
  });

  final ShopApi shop;
  final CartController cart;
  final ReviewsApi reviews;
  final RecommendationsApi recommendations;
  final CardPaymentLauncher cardPayment;

  @override
  State<ShopTab> createState() => _ShopTabState();
}

class _ShopTabState extends State<ShopTab> {
  final _search = TextEditingController();
  int _version = 0;

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  void _openList({
    required String title,
    int? categoryId,
    int? brandId,
    bool onSale = false,
    String sort = 'Name',
    String search = '',
    bool showCategoryFilter = false,
    int? sectionId,
  }) => Navigator.of(context).push(
    MaterialPageRoute<void>(
      builder: (_) => ProductListPage(
        title: title,
        shop: widget.shop,
        cart: widget.cart,
        reviews: widget.reviews,
        recommendations: widget.recommendations,
        cardPayment: widget.cardPayment,
        categoryId: categoryId,
        brandId: brandId,
        onSale: onSale,
        sort: sort,
        search: search,
        showCategoryFilter: showCategoryFilter,
        sectionId: sectionId,
      ),
    ),
  );

  void _open(Product p) => Navigator.of(context).push(
    MaterialPageRoute<void>(
      builder: (_) => ProductDetailPage(
        product: p,
        cart: widget.cart,
        reviews: widget.reviews,
        recommendations: widget.recommendations,
      ),
    ),
  );

  void _submitSearch() {
    final t = AppLocalizations.of(context);
    final text = _search.text.trim();
    if (text.isEmpty) return;
    _search.clear();
    _openList(title: t.searchResults, search: text, showCategoryFilter: true);
  }

  /// One block of the entry page. The admin decides which there are, in which order, and how each looks.
  Widget _block(BuildContext context, ShopBlock block) {
    final drawn = _drawBlock(context, block);
    final background = block.style.background;
    // Sections put themselves on their panel; the built-in blocks are put on it here.
    if (background == 'None' || !ShopSectionKind.isBuiltIn(block.kind)) {
      return drawn;
    }
    return SectionPanel(
      background: background,
      child: Padding(padding: const EdgeInsets.only(bottom: 14), child: drawn),
    );
  }

  Widget _drawBlock(BuildContext context, ShopBlock block) {
    final t = AppLocalizations.of(context);
    final centered = block.style.centered;
    switch (block.kind) {
      case ShopSectionKind.categoryTiles:
        return Column(
          key: Key('block_${block.sectionId}'),
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SectionHeading(
              block.title,
              eyebrow: block.eyebrow,
              centered: centered,
              top: block.style.background == 'None' ? 28 : 20,
              trailing: TextButton(
                key: const Key('all_products'),
                onPressed: () =>
                    _openList(title: t.allProducts, showCategoryFilter: true),
                child: Text(t.allProducts),
              ),
            ),
            _CategoryGrid(
              tiles: block.tiles,
              onTile: (c) => _openList(title: c.name, categoryId: c.id),
            ),
          ],
        );
      case ShopSectionKind.brandTiles:
        return Column(
          key: Key('block_${block.sectionId}'),
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SectionHeading(
              block.title,
              eyebrow: block.eyebrow,
              centered: centered,
              top: block.style.background == 'None' ? 36 : 20,
            ),
            _BrandRow(
              tiles: block.tiles,
              onTile: (b) => _openList(title: b.name, brandId: b.id),
            ),
          ],
        );
      case ShopSectionKind.recommended:
        return RecommendedRow(
          key: ValueKey('reco${block.sectionId}$_version'),
          title: block.title,
          eyebrow: block.eyebrow,
          centered: centered,
          top: block.style.background == 'None' ? 28 : 20,
          load: widget.recommendations.products,
          cart: widget.cart,
          onOpen: _open,
        );
      default:
        return ShopSectionBlock(
          key: Key('block_${block.sectionId}'),
          section: block.toSection(),
          cart: widget.cart,
          onOpen: _open,
          onSeeAll: () =>
              _openList(title: block.title, sectionId: block.sectionId),
        );
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 8, 4),
          child: Row(
            children: [
              Expanded(
                child: TextField(
                  key: const Key('shop_search'),
                  controller: _search,
                  textInputAction: TextInputAction.search,
                  decoration: InputDecoration(
                    hintText: t.searchHint,
                    prefixIcon: const Icon(Icons.search),
                    isDense: true,
                  ),
                  onSubmitted: (_) => _submitSearch(),
                ),
              ),
              ShopActions(
                shop: widget.shop,
                cart: widget.cart,
                cardPayment: widget.cardPayment,
              ),
            ],
          ),
        ),
        Expanded(
          child: AsyncView<ShopOverview>(
            key: ValueKey(_version),
            load: widget.shop.overview,
            skeleton: const ListSkeleton(rowHeight: 120),
            builder: (context, o, reload) => RefreshIndicator(
              onRefresh: () async => setState(() => _version++),
              child: ListView(
                key: const Key('shop_hub'),
                padding: const EdgeInsets.only(bottom: 24),
                children: [
                  _Cards(
                    cards: o.cards,
                    onCard: (c) =>
                        _openList(title: c.title, sectionId: c.sectionId),
                  ),
                  // Without a category block the way to the whole list would be gone: keep a plain button for it.
                  if (!o.blocks.any(
                    (b) => b.kind == ShopSectionKind.categoryTiles,
                  ))
                    Padding(
                      padding: const EdgeInsets.fromLTRB(20, 20, 20, 0),
                      child: OutlinedButton(
                        key: const Key('all_products'),
                        onPressed: () => _openList(
                          title: t.allProducts,
                          showCategoryFilter: true,
                        ),
                        child: Text(t.allProducts),
                      ),
                    ),
                  for (final block in o.blocks) _block(context, block),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }
}

/// The cards at the top: the sections the admin chose to show as cards, and nothing else. No cards, no strip.
class _Cards extends StatelessWidget {
  const _Cards({required this.cards, required this.onCard});

  final List<ShopCard> cards;
  final void Function(ShopCard) onCard;

  Widget _card(BuildContext context, ShopCard c) {
    final t = AppLocalizations.of(context);
    return _ImageCard(
      key: Key('card_${c.sectionId}'),
      eyebrow: c.eyebrow,
      title: c.title,
      subtitle: c.maxDiscountPercent > 0
          ? t.discountUpTo(c.maxDiscountPercent)
          : t.productsCount(c.productCount),
      imageUrl: c.imageUrl,
      icon: Icons.local_offer_outlined,
      accent: c.maxDiscountPercent > 0,
      onTap: () => onCard(c),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (cards.isEmpty) return const SizedBox.shrink();
    // A full-width card takes a row of its own; half-width cards go two by two, in the order the admin arranged.
    final rows = <Widget>[];
    var i = 0;
    while (i < cards.length) {
      final c = cards[i];
      final next = i + 1 < cards.length ? cards[i + 1] : null;
      if (c.style.fullCard) {
        rows.add(_row([_card(context, c)]));
        i++;
      } else if (next != null && !next.style.fullCard) {
        rows.add(_row([_card(context, c), _card(context, next)]));
        i += 2;
      } else {
        rows.add(_row([_card(context, c), const SizedBox.shrink()]));
        i++;
      }
    }
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
      child: Column(children: rows),
    );
  }

  static Widget _row(List<Widget> cells) => Padding(
    padding: const EdgeInsets.only(bottom: 12),
    child: SizedBox(
      height: 140,
      child: Row(
        children: [
          for (var i = 0; i < cells.length; i++) ...[
            if (i > 0) const SizedBox(width: 12),
            Expanded(child: cells[i]),
          ],
        ],
      ),
    ),
  );
}

/// A rounded picture with a dark fade at the bottom and a title and a line of text laid over it.
class _ImageCard extends StatelessWidget {
  const _ImageCard({
    super.key,
    required this.title,
    required this.onTap,
    this.subtitle,
    this.imageUrl,
    this.icon,
    this.accent = false,
    this.eyebrow,
  });

  final String title;

  /// The small amber text above the title (optional).
  final String? eyebrow;
  final String? subtitle;
  final String? imageUrl;
  final IconData? icon;

  /// Draws an amber border (the sale shortcut).
  final bool accent;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Material(
      color: scheme.surfaceContainer,
      clipBehavior: Clip.antiAlias,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(24),
        side: BorderSide(
          color: accent ? scheme.primary : scheme.outlineVariant,
          width: accent ? 1.5 : 1,
        ),
      ),
      child: InkWell(
        onTap: onTap,
        child: Stack(
          fit: StackFit.expand,
          children: [
            if (imageUrl != null)
              NetworkPicture(url: imageUrl)
            else
              Center(
                child: Icon(
                  icon ?? Icons.inventory_2_outlined,
                  size: 44,
                  color: scheme.primary.withValues(alpha: 0.5),
                ),
              ),
            const DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                  colors: [Colors.transparent, Color(0xCC000000)],
                  stops: [0.35, 1],
                ),
              ),
            ),
            Positioned(
              left: 14,
              right: 12,
              bottom: 12,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (eyebrow != null && eyebrow!.trim().isNotEmpty)
                    Text(
                      eyebrow!.toUpperCase(),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        color: scheme.primary,
                        fontWeight: FontWeight.w800,
                        fontSize: 11,
                        letterSpacing: 0.5,
                      ),
                    ),
                  Text(
                    title,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w800,
                      fontSize: 17,
                      height: 1.15,
                    ),
                  ),
                  if (subtitle != null)
                    Text(
                      subtitle!,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        color: accent ? scheme.primary : Colors.white70,
                        fontWeight: FontWeight.w700,
                        fontSize: 13,
                      ),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Two columns of category tiles. The admin picks an even number, so the last row is always full.
class _CategoryGrid extends StatelessWidget {
  const _CategoryGrid({required this.tiles, required this.onTile});

  final List<ShopTile> tiles;
  final void Function(ShopTile) onTile;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: GridView.count(
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        crossAxisCount: 2,
        mainAxisSpacing: 12,
        crossAxisSpacing: 12,
        childAspectRatio: 1.15,
        children: [
          for (final c in tiles)
            _ImageCard(
              key: Key('category_tile_${c.id}'),
              title: c.name,
              subtitle: t.productsCount(c.productCount),
              imageUrl: c.imageUrl,
              icon: Icons.category_outlined,
              onTap: () => onTile(c),
            ),
        ],
      ),
    );
  }
}

/// Brands as picture cards in a row that scrolls sideways (the busiest brands first): the logo the admin set, shown
/// whole on a light ground, or else a photo of one of the brand's products.
class _BrandRow extends StatelessWidget {
  const _BrandRow({required this.tiles, required this.onTile});

  final List<ShopTile> tiles;
  final void Function(ShopTile) onTile;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 126,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        itemCount: tiles.length,
        separatorBuilder: (_, _) => const SizedBox(width: 12),
        itemBuilder: (context, i) => _BrandCard(
          key: Key('brand_tile_${tiles[i].id}'),
          tile: tiles[i],
          onTap: () => onTile(tiles[i]),
        ),
      ),
    );
  }
}

class _BrandCard extends StatelessWidget {
  const _BrandCard({super.key, required this.tile, required this.onTap});

  final ShopTile tile;
  final VoidCallback onTap;

  static String _initials(String name) {
    final words = name.trim().split(RegExp(r'\s+')).where((w) => w.isNotEmpty);
    return words.take(2).map((w) => w[0]).join().toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final Widget picture;
    if (tile.imageUrl == null) {
      // Nothing to show yet: the initials, so the card is never empty.
      picture = Container(
        color: scheme.surfaceContainerHigh,
        alignment: Alignment.center,
        child: Text(
          _initials(tile.name),
          style: TextStyle(
            color: scheme.primary,
            fontWeight: FontWeight.w800,
            fontSize: 26,
          ),
        ),
      );
    } else if (tile.isLogo) {
      // Logos are made for a light ground, and must not be cropped.
      picture = Container(
        key: Key('brand_logo_${tile.id}'),
        color: const Color(0xFFF4F4F6),
        padding: const EdgeInsets.all(14),
        child: NetworkPicture(url: tile.imageUrl, fit: BoxFit.contain),
      );
    } else {
      picture = NetworkPicture(url: tile.imageUrl);
    }
    return SizedBox(
      width: 150,
      child: Material(
        color: scheme.surfaceContainer,
        clipBehavior: Clip.antiAlias,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(20),
          side: BorderSide(color: scheme.outlineVariant),
        ),
        child: InkWell(
          onTap: onTap,
          child: Column(
            children: [
              Expanded(child: SizedBox.expand(child: picture)),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.symmetric(
                  horizontal: 10,
                  vertical: 8,
                ),
                child: Text(
                  tile.name,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 13,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

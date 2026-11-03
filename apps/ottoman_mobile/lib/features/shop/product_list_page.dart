import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'cart_page.dart';
import 'my_orders_page.dart';
import 'product_card.dart';
import 'product_detail_page.dart';

/// "My orders" and the basket (with a count badge): the two shortcuts every shop screen carries.
class ShopActions extends StatelessWidget {
  const ShopActions({
    super.key,
    required this.shop,
    required this.cart,
    required this.cardPayment,
  });

  final ShopApi shop;
  final CartController cart;
  final CardPaymentLauncher cardPayment;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        IconButton(
          key: const Key('open_orders'),
          tooltip: t.myOrders,
          icon: const Icon(Icons.receipt_long_outlined),
          onPressed: () => Navigator.of(context).push(
            MaterialPageRoute<void>(
              builder: (_) =>
                  MyOrdersPage(shop: shop, cardPayment: cardPayment),
            ),
          ),
        ),
        ListenableBuilder(
          listenable: cart,
          builder: (context, _) => IconButton(
            key: const Key('open_cart'),
            tooltip: t.cart,
            icon: Badge(
              isLabelVisible: !cart.isEmpty,
              label: Text('${cart.itemCount}'),
              child: const Icon(Icons.shopping_cart_outlined),
            ),
            onPressed: () {
              ScaffoldMessenger.of(context).hideCurrentSnackBar();
              Navigator.of(context).push(
                MaterialPageRoute<void>(
                  builder: (_) => CartPage(
                    shop: shop,
                    cart: cart,
                    cardPayment: cardPayment,
                  ),
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}

/// A list of products with search and sorting, optionally narrowed to a category, a brand or what is on sale.
/// Search, filters and sorting happen on the server; the basket stays on the device.
class ProductListPage extends StatefulWidget {
  const ProductListPage({
    super.key,
    required this.title,
    required this.shop,
    required this.cart,
    required this.reviews,
    required this.recommendations,
    this.cardPayment = const UnavailableCardPayment(),
    this.categoryId,
    this.brandId,
    this.onSale = false,
    this.sort = 'Name',
    this.search = '',
    this.showCategoryFilter = false,
    this.sectionId,
  });

  final String title;
  final ShopApi shop;
  final CartController cart;
  final ReviewsApi reviews;
  final RecommendationsApi recommendations;
  final CardPaymentLauncher cardPayment;
  final int? categoryId;
  final int? brandId;
  final bool onSale;
  final String sort;
  final String search;

  /// The row of category chips: for the general list ("all products", a search), not for a list that is already
  /// about one category or brand.
  final bool showCategoryFilter;

  /// Lists what one shop section holds (what its card opens). Such a list has its own fixed contents, so it has no
  /// search or sorting.
  final int? sectionId;

  @override
  State<ProductListPage> createState() => _ProductListPageState();
}

class _ProductListPageState extends State<ProductListPage> {
  late final _search = TextEditingController(text: widget.search);
  late int? _categoryId = widget.categoryId;
  late String _sort = widget.sort;
  int _version = 0;
  late final Future<List<Category>> _categories = widget.showCategoryFilter
      ? widget.shop.categories()
      : Future.value(const <Category>[]);

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  void _reload() => setState(() => _version++);

  void _addToCart(Product p) {
    final t = AppLocalizations.of(context);
    final ok = widget.cart.add(p);
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(ok ? '${p.name} · ${t.cart}' : t.limitReached),
          duration: const Duration(seconds: 2),
        ),
      );
  }

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

  // ---- a section's own list: loaded once (it is capped and already small), then searched and sorted on the device

  Future<List<Product>>? _sectionLoad;

  Future<List<Product>> get _sectionProducts =>
      _sectionLoad ??= widget.shop.sectionProducts(widget.sectionId!);

  /// Lower case without the Bosnian diacritics, so "sampon" finds "Šampon".
  static String _fold(String s) {
    const from = 'čćžšđ';
    const to = 'cczsd';
    final b = StringBuffer();
    for (final c in s.toLowerCase().split('')) {
      final i = from.indexOf(c);
      b.write(i >= 0 ? to[i] : c);
    }
    return b.toString();
  }

  List<Product> _searchedAndSorted(List<Product> all) {
    final term = _fold(_search.text.trim());
    final items = all
        .where(
          (p) =>
              term.isEmpty ||
              _fold(p.name).contains(term) ||
              _fold(p.brandName ?? '').contains(term),
        )
        .toList();
    switch (_sort) {
      case 'PriceAsc':
        items.sort((a, b) => a.payPrice.compareTo(b.payPrice));
      case 'PriceDesc':
        items.sort((a, b) => b.payPrice.compareTo(a.payPrice));
      case 'Newest':
        items.sort((a, b) => b.id.compareTo(a.id));
      default:
        items.sort((a, b) => _fold(a.name).compareTo(_fold(b.name)));
    }
    return items;
  }

  Widget _sectionList(AppLocalizations t) {
    return FutureBuilder<List<Product>>(
      future: _sectionProducts,
      builder: (context, snap) {
        if (snap.connectionState != ConnectionState.done) {
          return const ListSkeleton(rowHeight: 96);
        }
        if (snap.hasError) {
          final e = snap.error;
          return ErrorView(
            message: e is AppException ? e.message : t.genericError,
            retryLabel: t.retry,
            onRetry: () => setState(() => _sectionLoad = null),
          );
        }
        final items = _searchedAndSorted(snap.data!);
        return RefreshIndicator(
          onRefresh: () async {
            setState(() => _sectionLoad = null);
            await _sectionProducts;
          },
          child: items.isEmpty
              ? ListView(
                  children: [
                    Padding(
                      padding: const EdgeInsets.all(40),
                      child: Center(
                        key: const Key('section_list_empty'),
                        child: Text(t.noData),
                      ),
                    ),
                  ],
                )
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
                  itemCount: items.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 12),
                  itemBuilder: (context, i) => ProductCard(
                    product: items[i],
                    onAdd: () => _addToCart(items[i]),
                    onOpen: () => _open(items[i]),
                  ),
                ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.title),
        actions: [
          ShopActions(
            shop: widget.shop,
            cart: widget.cart,
            cardPayment: widget.cardPayment,
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 4, 8, 0),
            child: Row(
              children: [
                Expanded(
                  child: TextField(
                    key: const Key('product_search'),
                    controller: _search,
                    textInputAction: TextInputAction.search,
                    decoration: InputDecoration(
                      hintText: t.searchHint,
                      prefixIcon: const Icon(Icons.search),
                      isDense: true,
                    ),
                    onChanged: (_) {
                      if (widget.sectionId != null) setState(() {});
                    },
                    onSubmitted: (_) => _reload(),
                  ),
                ),
                PopupMenuButton<String>(
                  key: const Key('product_sort'),
                  tooltip: t.sortBy,
                  icon: const Icon(Icons.sort),
                  initialValue: _sort,
                  onSelected: (v) {
                    _sort = v;
                    // A section list is sorted where it is; the others ask the server again.
                    widget.sectionId != null ? setState(() {}) : _reload();
                  },
                  itemBuilder: (_) => [
                    PopupMenuItem(value: 'Name', child: Text(t.sortName)),
                    PopupMenuItem(
                      value: 'PriceAsc',
                      child: Text(t.sortPriceAsc),
                    ),
                    PopupMenuItem(
                      value: 'PriceDesc',
                      child: Text(t.sortPriceDesc),
                    ),
                    PopupMenuItem(value: 'Newest', child: Text(t.sortNewest)),
                  ],
                ),
              ],
            ),
          ),
          if (widget.showCategoryFilter)
            SizedBox(
              height: 56,
              child: FutureBuilder<List<Category>>(
                future: _categories,
                builder: (context, snap) {
                  final categories = snap.data ?? const <Category>[];
                  return ListView(
                    scrollDirection: Axis.horizontal,
                    padding: const EdgeInsets.symmetric(
                      horizontal: 16,
                      vertical: 8,
                    ),
                    children: [
                      Padding(
                        padding: const EdgeInsets.only(right: 8),
                        child: ChoiceChip(
                          label: Text(t.allCategories),
                          selected: _categoryId == null,
                          onSelected: (_) {
                            _categoryId = null;
                            _reload();
                          },
                        ),
                      ),
                      for (final c in categories)
                        Padding(
                          padding: const EdgeInsets.only(right: 8),
                          child: ChoiceChip(
                            key: Key('category_${c.id}'),
                            label: Text(c.name),
                            selected: _categoryId == c.id,
                            onSelected: (_) {
                              _categoryId = c.id;
                              _reload();
                            },
                          ),
                        ),
                    ],
                  );
                },
              ),
            ),
          Expanded(
            child: widget.sectionId != null
                ? _sectionList(t)
                : AsyncView<Paged<Product>>(
                    key: ValueKey(_version),
                    load: () => widget.sectionId != null
                        ? widget.shop
                              .sectionProducts(widget.sectionId!)
                              .then((items) => Paged(items, items.length))
                        : widget.shop.products(
                            search: _search.text,
                            categoryId: _categoryId,
                            brandId: widget.brandId,
                            onSale: widget.onSale,
                            sort: _sort,
                            pageSize: 50,
                          ),
                    skeleton: const ListSkeleton(rowHeight: 96),
                    isEmpty: (p) => p.items.isEmpty,
                    emptyMessage: t.noData,
                    builder: (context, page, reload) => RefreshIndicator(
                      onRefresh: reload,
                      child: ListView.separated(
                        padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
                        itemCount: page.items.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 12),
                        itemBuilder: (context, i) => ProductCard(
                          product: page.items[i],
                          onAdd: () => _addToCart(page.items[i]),
                          onOpen: () => _open(page.items[i]),
                        ),
                      ),
                    ),
                  ),
          ),
        ],
      ),
    );
  }
}

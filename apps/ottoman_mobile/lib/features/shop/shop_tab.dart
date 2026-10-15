import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'cart_page.dart';
import 'my_orders_page.dart';
import 'product_detail_page.dart';

/// The shop: search, category filter and sorting happen on the server; the basket stays on the device.
class ShopTab extends StatefulWidget {
  const ShopTab({
    super.key,
    required this.shop,
    required this.cart,
    this.cardPayment = const UnavailableCardPayment(),
  });

  final ShopApi shop;
  final CartController cart;
  final CardPaymentLauncher cardPayment;

  @override
  State<ShopTab> createState() => _ShopTabState();
}

class _ShopTabState extends State<ShopTab> {
  final _search = TextEditingController();
  int? _categoryId;
  String _sort = 'Name';
  int _version = 0;
  late final Future<List<Category>> _categories = widget.shop.categories();

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

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 8, 0),
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
                  _reload();
                },
                itemBuilder: (_) => [
                  PopupMenuItem(value: 'Name', child: Text(t.sortName)),
                  PopupMenuItem(value: 'PriceAsc', child: Text(t.sortPriceAsc)),
                  PopupMenuItem(
                    value: 'PriceDesc',
                    child: Text(t.sortPriceDesc),
                  ),
                  PopupMenuItem(value: 'Newest', child: Text(t.sortNewest)),
                ],
              ),
              IconButton(
                key: const Key('open_orders'),
                tooltip: t.myOrders,
                icon: const Icon(Icons.receipt_long_outlined),
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute<void>(
                    builder: (_) => MyOrdersPage(
                      shop: widget.shop,
                      cardPayment: widget.cardPayment,
                    ),
                  ),
                ),
              ),
              ListenableBuilder(
                listenable: widget.cart,
                builder: (context, _) => IconButton(
                  key: const Key('open_cart'),
                  tooltip: t.cart,
                  icon: Badge(
                    isLabelVisible: !widget.cart.isEmpty,
                    label: Text('${widget.cart.itemCount}'),
                    child: const Icon(Icons.shopping_cart_outlined),
                  ),
                  onPressed: () {
                    ScaffoldMessenger.of(context).hideCurrentSnackBar();
                    Navigator.of(context).push(
                      MaterialPageRoute<void>(
                        builder: (_) => CartPage(
                          shop: widget.shop,
                          cart: widget.cart,
                          cardPayment: widget.cardPayment,
                        ),
                      ),
                    );
                  },
                ),
              ),
            ],
          ),
        ),
        SizedBox(
          height: 52,
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
          child: AsyncView<Paged<Product>>(
            key: ValueKey(_version),
            load: () => widget.shop.products(
              search: _search.text,
              categoryId: _categoryId,
              sort: _sort,
              pageSize: 50,
            ),
            skeleton: const ListSkeleton(rowHeight: 96),
            isEmpty: (p) => p.items.isEmpty,
            emptyMessage: t.noData,
            builder: (context, page, reload) => RefreshIndicator(
              onRefresh: reload,
              child: ListView.separated(
                padding: const EdgeInsets.fromLTRB(16, 4, 16, 16),
                itemCount: page.items.length,
                separatorBuilder: (_, _) => const SizedBox(height: 12),
                itemBuilder: (context, i) => ProductCard(
                  product: page.items[i],
                  onAdd: () => _addToCart(page.items[i]),
                  onOpen: () => Navigator.of(context).push(
                    MaterialPageRoute<void>(
                      builder: (_) => ProductDetailPage(
                        product: page.items[i],
                        cart: widget.cart,
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }
}

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
              Container(
                width: 64,
                height: 64,
                decoration: BoxDecoration(
                  color: scheme.surfaceContainerHighest,
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Icon(Icons.inventory_2_outlined, color: scheme.outline),
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
                    Text(
                      money(t, p.price),
                      style: const TextStyle(fontWeight: FontWeight.w800),
                    ),
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

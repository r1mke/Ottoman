import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'product_dialogs.dart';

/// Admin: catalogue with low-stock warnings. Search, filters and paging run on the server.
class ProductsPage extends StatefulWidget {
  const ProductsPage({super.key, required this.shop});

  final ShopApi shop;

  @override
  State<ProductsPage> createState() => _ProductsPageState();
}

class _ProductsPageState extends State<ProductsPage> {
  final _search = TextEditingController();
  int? _categoryId;
  bool _lowOnly = false;
  int _version = 0;
  int _page = 1;
  static const _pageSize = 15;

  List<Brand> _brands = const [];
  List<Category> _categories = const [];

  @override
  void initState() {
    super.initState();
    _loadLookups();
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<void> _loadLookups() async {
    try {
      final results = await Future.wait([
        widget.shop.brands(),
        widget.shop.categories(),
      ]);
      if (!mounted) return;
      setState(() {
        _brands = results[0] as List<Brand>;
        _categories = results[1] as List<Category>;
        if (_categoryId != null &&
            !_categories.any((c) => c.id == _categoryId)) {
          _categoryId = null;
        }
      });
    } on AppException {
      // The table below reports its own errors; lookups just stay empty.
    }
  }

  void _refresh({bool resetPage = false}) => setState(() {
    if (resetPage) _page = 1;
    _version++;
  });

  Future<void> _edit([Product? product]) async {
    await showDialog<bool>(
      context: context,
      builder: (_) => ProductDialog(
        shop: widget.shop,
        brands: _brands,
        categories: _categories,
        product: product,
      ),
    );
    if (mounted) _refresh();
  }

  Future<void> _adjust(Product p) async {
    final saved = await showDialog<bool>(
      context: context,
      builder: (_) => StockDialog(shop: widget.shop, product: p),
    );
    if (saved == true) _refresh();
  }

  Future<void> _toggle(Product p) async {
    try {
      if (p.isActive) {
        await widget.shop.deactivateProduct(p.id);
      } else {
        await widget.shop.saveProduct(
          id: p.id,
          name: p.name,
          description: p.description,
          brandId: p.brandId,
          categoryId: p.categoryId,
          price: p.price,
          stock: p.stock,
          lowStockThreshold: p.lowStockThreshold,
          isActive: true,
        );
      }
      _refresh();
    } on AppException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  Future<void> _manage(bool brands) async {
    final changed = await showDialog<bool>(
      context: context,
      builder: (_) => brands
          ? NamesDialog.brands(shop: widget.shop)
          : NamesDialog.categories(shop: widget.shop),
    );
    if (changed == true) {
      await _loadLookups();
      _refresh();
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Padding(
      padding: const EdgeInsets.fromLTRB(32, 0, 32, 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 12,
            runSpacing: 12,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              SizedBox(
                width: 280,
                child: TextField(
                  key: const Key('products_search'),
                  controller: _search,
                  decoration: InputDecoration(
                    hintText: t.searchHint,
                    prefixIcon: const Icon(Icons.search),
                    isDense: true,
                  ),
                  onSubmitted: (_) => _refresh(resetPage: true),
                ),
              ),
              SizedBox(
                width: 220,
                child: DropdownButtonFormField<int?>(
                  key: ValueKey('cat_filter_$_categoryId${_categories.length}'),
                  isExpanded: true,
                  initialValue: _categoryId,
                  decoration: InputDecoration(
                    labelText: t.category,
                    isDense: true,
                  ),
                  items: [
                    DropdownMenuItem(value: null, child: Text(t.allCategories)),
                    for (final c in _categories)
                      DropdownMenuItem(value: c.id, child: Text(c.name)),
                  ],
                  onChanged: (v) {
                    _categoryId = v;
                    _refresh(resetPage: true);
                  },
                ),
              ),
              FilterChip(
                key: const Key('low_only'),
                label: Text(t.lowStockOnly),
                avatar: const Icon(Icons.warning_amber_outlined, size: 18),
                selected: _lowOnly,
                onSelected: (v) {
                  _lowOnly = v;
                  _refresh(resetPage: true);
                },
              ),
              OutlinedButton(
                key: const Key('manage_categories'),
                onPressed: () => _manage(false),
                child: Text(t.categories),
              ),
              OutlinedButton(
                key: const Key('manage_brands'),
                onPressed: () => _manage(true),
                child: Text(t.brands),
              ),
              FilledButton.icon(
                key: const Key('add_product'),
                style: FilledButton.styleFrom(minimumSize: const Size(160, 44)),
                onPressed: _edit,
                icon: const Icon(Icons.add),
                label: Text(t.newProduct),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Expanded(
            child: AsyncView<Paged<Product>>(
              key: ValueKey('$_version-$_page'),
              load: () => widget.shop.products(
                search: _search.text,
                categoryId: _categoryId,
                includeInactive: true,
                lowStock: _lowOnly,
                page: _page,
                pageSize: _pageSize,
              ),
              skeleton: const ListSkeleton(),
              isEmpty: (p) => p.items.isEmpty,
              emptyMessage: t.noData,
              builder: (context, page, _) => _Table(
                page: page,
                pageNumber: _page,
                pageSize: _pageSize,
                onEdit: _edit,
                onStock: _adjust,
                onToggle: _toggle,
                onPage: (p) => setState(() => _page = p),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _Table extends StatelessWidget {
  const _Table({
    required this.page,
    required this.pageNumber,
    required this.pageSize,
    required this.onEdit,
    required this.onStock,
    required this.onToggle,
    required this.onPage,
  });

  final Paged<Product> page;
  final int pageNumber;
  final int pageSize;
  final void Function(Product) onEdit;
  final void Function(Product) onStock;
  final void Function(Product) onToggle;
  final void Function(int) onPage;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    final pages = (page.totalCount / pageSize).ceil();
    return Column(
      children: [
        Expanded(
          child: Card(
            clipBehavior: Clip.antiAlias,
            child: SingleChildScrollView(
              child: SizedBox(
                width: double.infinity,
                child: DataTable(
                  headingRowColor: WidgetStatePropertyAll(
                    scheme.surfaceContainerHighest,
                  ),
                  columns: [
                    DataColumn(label: Text(t.name)),
                    DataColumn(label: Text(t.category)),
                    DataColumn(label: Text(t.price), numeric: true),
                    DataColumn(label: Text(t.stock), numeric: true),
                    DataColumn(label: Text(t.status)),
                    const DataColumn(label: SizedBox.shrink()),
                  ],
                  rows: [
                    for (final p in page.items)
                      DataRow(
                        key: ValueKey('product_row_${p.id}'),
                        cells: [
                          DataCell(
                            Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  p.name,
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                                if (p.brandName != null)
                                  Text(
                                    p.brandName!,
                                    style: TextStyle(
                                      fontSize: 12,
                                      color: scheme.outline,
                                    ),
                                  ),
                              ],
                            ),
                          ),
                          DataCell(Text(p.categoryName ?? '-')),
                          DataCell(Text(money(t, p.price))),
                          DataCell(
                            Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                if (p.isLowStock)
                                  Tooltip(
                                    message: t.lowStock,
                                    child: Icon(
                                      Icons.warning_amber_rounded,
                                      key: Key('low_${p.id}'),
                                      size: 18,
                                      color: p.inStock
                                          ? Colors.orange.shade800
                                          : scheme.error,
                                    ),
                                  ),
                                const SizedBox(width: 4),
                                Text('${p.stock}'),
                              ],
                            ),
                          ),
                          DataCell(
                            Chip(
                              label: Text(p.isActive ? t.active : t.inactive),
                              visualDensity: VisualDensity.compact,
                            ),
                          ),
                          DataCell(
                            Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                IconButton(
                                  key: Key('stock_${p.id}'),
                                  tooltip: t.restock,
                                  icon: const Icon(Icons.inventory_outlined),
                                  onPressed: () => onStock(p),
                                ),
                                IconButton(
                                  key: Key('edit_${p.id}'),
                                  tooltip: t.edit,
                                  icon: const Icon(Icons.edit_outlined),
                                  onPressed: () => onEdit(p),
                                ),
                                IconButton(
                                  key: Key('toggle_${p.id}'),
                                  tooltip: p.isActive ? t.deactivate : t.active,
                                  icon: Icon(
                                    p.isActive
                                        ? Icons.visibility_off_outlined
                                        : Icons.visibility_outlined,
                                  ),
                                  onPressed: () => onToggle(p),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                  ],
                ),
              ),
            ),
          ),
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            Text(t.productsCount(page.totalCount)),
            const Spacer(),
            IconButton(
              key: const Key('prev_page'),
              icon: const Icon(Icons.chevron_left),
              onPressed: pageNumber > 1 ? () => onPage(pageNumber - 1) : null,
            ),
            Text('$pageNumber / ${pages < 1 ? 1 : pages}'),
            IconButton(
              key: const Key('next_page'),
              icon: const Icon(Icons.chevron_right),
              onPressed: pageNumber < pages
                  ? () => onPage(pageNumber + 1)
                  : null,
            ),
          ],
        ),
      ],
    );
  }
}

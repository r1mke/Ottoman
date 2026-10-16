import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

String _detail(AppException e) {
  final detail = e.errors.values.expand((x) => x).join('\n');
  return detail.isEmpty ? e.message : detail;
}

/// Create or edit a product. Stock is entered only for a new product; afterwards it changes through
/// [StockDialog] (atomic on the server) so it can never overwrite what a concurrent order just took.
class ProductDialog extends StatefulWidget {
  const ProductDialog({
    super.key,
    required this.shop,
    required this.brands,
    required this.categories,
    this.product,
  });

  final ShopApi shop;
  final List<Brand> brands;
  final List<Category> categories;
  final Product? product;

  @override
  State<ProductDialog> createState() => _ProductDialogState();
}

class _ProductDialogState extends State<ProductDialog> {
  final _form = GlobalKey<FormState>();
  late final _name = TextEditingController(text: widget.product?.name);
  late final _description = TextEditingController(
    text: widget.product?.description,
  );
  late final _price = TextEditingController(
    text: widget.product?.price.toStringAsFixed(2),
  );
  late final _stock = TextEditingController(
    text: widget.product?.stock.toString() ?? '0',
  );
  late final _threshold = TextEditingController(
    text: widget.product?.lowStockThreshold?.toString(),
  );
  late int? _brandId = widget.product?.brandId;
  late int? _categoryId = widget.product?.categoryId;
  late bool _active = widget.product?.isActive ?? true;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    for (final c in [_name, _description, _price, _stock, _threshold]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.shop.saveProduct(
        id: widget.product?.id,
        name: _name.text.trim(),
        description: _description.text.trim().isEmpty
            ? null
            : _description.text.trim(),
        brandId: _brandId,
        categoryId: _categoryId,
        price: double.parse(_price.text.replaceAll(',', '.')),
        stock: int.parse(_stock.text),
        lowStockThreshold: _threshold.text.trim().isEmpty
            ? null
            : int.parse(_threshold.text),
        isActive: _active,
      );
      if (mounted) Navigator.pop(context, true);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = _detail(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    String? required(String? v) =>
        (v == null || v.trim().isEmpty) ? t.requiredField : null;
    String? wholeNumber(String? v) {
      final n = int.tryParse(v ?? '');
      return (n == null || n < 0) ? t.invalidNumber : null;
    }

    return AlertDialog(
      title: Text(widget.product == null ? t.newProduct : t.edit),
      content: SizedBox(
        width: 460,
        child: Form(
          key: _form,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextFormField(
                  key: const Key('prod_name'),
                  controller: _name,
                  decoration: InputDecoration(labelText: t.name),
                  validator: required,
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _description,
                  decoration: InputDecoration(labelText: t.description),
                  maxLines: 2,
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: DropdownButtonFormField<int?>(
                        isExpanded: true,
                        initialValue: _brandId,
                        decoration: InputDecoration(labelText: t.brand),
                        items: [
                          DropdownMenuItem(value: null, child: Text(t.none)),
                          for (final b in widget.brands)
                            DropdownMenuItem(value: b.id, child: Text(b.name)),
                        ],
                        onChanged: (v) => _brandId = v,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: DropdownButtonFormField<int?>(
                        isExpanded: true,
                        initialValue: _categoryId,
                        decoration: InputDecoration(labelText: t.category),
                        items: [
                          DropdownMenuItem(value: null, child: Text(t.none)),
                          for (final c in widget.categories)
                            DropdownMenuItem(value: c.id, child: Text(c.name)),
                        ],
                        onChanged: (v) => _categoryId = v,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: TextFormField(
                        key: const Key('prod_price'),
                        controller: _price,
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: true,
                        ),
                        decoration: InputDecoration(
                          labelText: '${t.price} (${t.currencyShort})',
                        ),
                        validator: (v) {
                          final n = double.tryParse(
                            (v ?? '').replaceAll(',', '.'),
                          );
                          return (n == null || n <= 0) ? t.invalidNumber : null;
                        },
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: TextFormField(
                        key: const Key('prod_stock'),
                        controller: _stock,
                        enabled: widget.product == null,
                        keyboardType: TextInputType.number,
                        decoration: InputDecoration(
                          labelText: widget.product == null
                              ? t.initialStock
                              : t.stock,
                        ),
                        validator: widget.product == null ? wholeNumber : null,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                TextFormField(
                  key: const Key('prod_threshold'),
                  controller: _threshold,
                  keyboardType: TextInputType.number,
                  decoration: InputDecoration(labelText: t.lowStockThreshold),
                  validator: (v) =>
                      (v == null || v.trim().isEmpty) ? null : wholeNumber(v),
                ),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(t.active),
                  value: _active,
                  onChanged: (v) => setState(() => _active = v),
                ),
                if (_error != null)
                  Align(
                    alignment: Alignment.centerLeft,
                    child: Text(
                      _error!,
                      key: const Key('prod_error'),
                      style: TextStyle(
                        color: Theme.of(context).colorScheme.error,
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : () => Navigator.pop(context),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('prod_save'),
          style: FilledButton.styleFrom(minimumSize: const Size(120, 44)),
          onPressed: _busy ? null : _save,
          child: Text(t.save),
        ),
      ],
    );
  }
}

/// Restock (positive) or write off (negative) in one atomic server-side step.
class StockDialog extends StatefulWidget {
  const StockDialog({super.key, required this.shop, required this.product});

  final ShopApi shop;
  final Product product;

  @override
  State<StockDialog> createState() => _StockDialogState();
}

class _StockDialogState extends State<StockDialog> {
  final _delta = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _delta.dispose();
    super.dispose();
  }

  Future<void> _apply() async {
    final t = AppLocalizations.of(context);
    final delta = int.tryParse(_delta.text.trim());
    if (delta == null || delta == 0) {
      setState(() => _error = t.invalidNumber);
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.shop.adjustStock(widget.product.id, delta);
      if (mounted) Navigator.pop(context, true);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = _detail(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AlertDialog(
      title: Text('${t.restock}: ${widget.product.name}'),
      content: SizedBox(
        width: 360,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(t.inStockCount(widget.product.stock)),
            const SizedBox(height: 12),
            TextField(
              key: const Key('stock_delta'),
              controller: _delta,
              autofocus: true,
              keyboardType: const TextInputType.numberWithOptions(signed: true),
              decoration: InputDecoration(labelText: t.stockChange),
              onSubmitted: (_) => _apply(),
            ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Text(
                  _error!,
                  key: const Key('stock_error'),
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : () => Navigator.pop(context),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('stock_apply'),
          style: FilledButton.styleFrom(minimumSize: const Size(120, 44)),
          onPressed: _busy ? null : _apply,
          child: Text(t.save),
        ),
      ],
    );
  }
}

/// Manage brands or categories: add, rename, delete (products keep existing without them).
class NamesDialog extends StatefulWidget {
  const NamesDialog.brands({super.key, required this.shop}) : isBrands = true;
  const NamesDialog.categories({super.key, required this.shop})
    : isBrands = false;

  final ShopApi shop;
  final bool isBrands;

  @override
  State<NamesDialog> createState() => _NamesDialogState();
}

class _NamesDialogState extends State<NamesDialog> {
  final _new = TextEditingController();
  List<({int id, String name})>? _items;
  bool _changed = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _new.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final items = widget.isBrands
          ? (await widget.shop.brands())
                .map((b) => (id: b.id, name: b.name))
                .toList()
          : (await widget.shop.categories())
                .map((c) => (id: c.id, name: c.name))
                .toList();
      if (mounted) setState(() => _items = items);
    } on AppException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  Future<void> _run(Future<void> Function() action) async {
    setState(() => _error = null);
    try {
      await action();
      _changed = true;
      await _load();
    } on AppException catch (e) {
      if (mounted) setState(() => _error = _detail(e));
    }
  }

  Future<void> _add() async {
    final name = _new.text.trim();
    if (name.isEmpty) return;
    await _run(() async {
      if (widget.isBrands) {
        await widget.shop.saveBrand(name: name);
      } else {
        await widget.shop.saveCategory(name: name);
      }
      _new.clear();
    });
  }

  Future<void> _rename(int id, String current) async {
    final name = await showDialog<String>(
      context: context,
      builder: (_) => _RenameDialog(current: current),
    );
    if (name == null || name.isEmpty || name == current) return;
    await _run(() async {
      if (widget.isBrands) {
        await widget.shop.saveBrand(id: id, name: name);
      } else {
        await widget.shop.saveCategory(id: id, name: name);
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AlertDialog(
      title: Text(widget.isBrands ? t.brands : t.categories),
      content: SizedBox(
        width: 420,
        height: 420,
        child: Column(
          children: [
            Row(
              children: [
                Expanded(
                  child: TextField(
                    key: const Key('names_new'),
                    controller: _new,
                    decoration: InputDecoration(labelText: t.name),
                    onSubmitted: (_) => _add(),
                  ),
                ),
                const SizedBox(width: 8),
                FilledButton(
                  key: const Key('names_add'),
                  style: FilledButton.styleFrom(
                    minimumSize: const Size(80, 44),
                  ),
                  onPressed: _add,
                  child: Text(t.add),
                ),
              ],
            ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Text(
                  _error!,
                  key: const Key('names_error'),
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ),
            const SizedBox(height: 8),
            Expanded(
              child: _items == null
                  ? const Center(child: CircularProgressIndicator())
                  : ListView(
                      children: [
                        for (final item in _items!)
                          ListTile(
                            key: Key('name_row_${item.id}'),
                            dense: true,
                            title: Text(item.name),
                            trailing: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                IconButton(
                                  tooltip: t.edit,
                                  icon: const Icon(Icons.edit_outlined),
                                  onPressed: () => _rename(item.id, item.name),
                                ),
                                IconButton(
                                  key: Key('name_delete_${item.id}'),
                                  tooltip: t.delete,
                                  icon: const Icon(Icons.delete_outline),
                                  onPressed: () => _run(
                                    () => widget.isBrands
                                        ? widget.shop.deleteBrand(item.id)
                                        : widget.shop.deleteCategory(item.id),
                                  ),
                                ),
                              ],
                            ),
                          ),
                      ],
                    ),
            ),
          ],
        ),
      ),
      actions: [
        FilledButton(
          key: const Key('names_close'),
          style: FilledButton.styleFrom(minimumSize: const Size(100, 44)),
          onPressed: () => Navigator.pop(context, _changed),
          child: Text(t.save),
        ),
      ],
    );
  }
}

/// Owns its controller so it is disposed with the dialog, not while the dialog is still animating away.
class _RenameDialog extends StatefulWidget {
  const _RenameDialog({required this.current});

  final String current;

  @override
  State<_RenameDialog> createState() => _RenameDialogState();
}

class _RenameDialogState extends State<_RenameDialog> {
  late final _controller = TextEditingController(text: widget.current);

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AlertDialog(
      title: Text(t.edit),
      content: TextField(
        key: const Key('rename_field'),
        controller: _controller,
        autofocus: true,
        decoration: InputDecoration(labelText: t.name),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: const Key('rename_ok'),
          onPressed: () => Navigator.pop(context, _controller.text.trim()),
          child: Text(t.save),
        ),
      ],
    );
  }
}

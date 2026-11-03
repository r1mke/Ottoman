import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// The work gallery: a horizontal menu of haircut styles (like the shop's categories) over a grid of pictures.
/// Tapping a picture opens it large with its style and the barber who did it.
class GalleryPage extends StatefulWidget {
  const GalleryPage({super.key, required this.gallery, this.initialCategoryId});

  final GalleryApi gallery;
  final int? initialCategoryId;

  @override
  State<GalleryPage> createState() => _GalleryPageState();
}

class _GalleryPageState extends State<GalleryPage> {
  static const _pageSize = 24;

  final _scroll = ScrollController();
  final _items = <GalleryItem>[];
  late final Future<List<HaircutCategory>> _categories = widget.gallery
      .categories()
      .catchError((_) => <HaircutCategory>[]);
  late int? _categoryId = widget.initialCategoryId;
  int _total = 0;
  bool _loading = true;
  Object? _error;

  @override
  void initState() {
    super.initState();
    _scroll.addListener(_onScroll);
    _load(reset: true);
  }

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  void _onScroll() {
    if (_scroll.position.extentAfter < 400 &&
        !_loading &&
        _items.length < _total) {
      _load();
    }
  }

  Future<void> _load({bool reset = false}) async {
    final category = _categoryId;
    setState(() {
      _loading = true;
      _error = null;
      if (reset) {
        _items.clear();
        _total = 0;
      }
    });
    try {
      final page = await widget.gallery.items(
        categoryId: category,
        page: _items.length ~/ _pageSize + 1,
        pageSize: _pageSize,
      );
      if (!mounted || category != _categoryId) return;
      setState(() {
        _items.addAll(page.items);
        _total = page.totalCount;
      });
    } on Object catch (e) {
      if (mounted) setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _select(int? id) {
    if (id == _categoryId) return;
    _categoryId = id;
    _load(reset: true);
  }

  void _open(int index) => Navigator.of(context).push(
    MaterialPageRoute<void>(
      builder: (_) => _WorkViewer(items: List.of(_items), initial: index),
    ),
  );

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(t.galleryTitle)),
      body: Column(
        children: [
          SizedBox(
            height: 52,
            child: FutureBuilder<List<HaircutCategory>>(
              future: _categories,
              builder: (context, snap) => ListView(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.symmetric(
                  horizontal: 16,
                  vertical: 8,
                ),
                children: [
                  Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: ChoiceChip(
                      key: const Key('gallery_all'),
                      label: Text(t.allStyles),
                      selected: _categoryId == null,
                      onSelected: (_) => _select(null),
                    ),
                  ),
                  for (final c in snap.data ?? const <HaircutCategory>[])
                    Padding(
                      padding: const EdgeInsets.only(right: 8),
                      child: ChoiceChip(
                        key: Key('gallery_style_${c.id}'),
                        label: Text(c.name),
                        selected: _categoryId == c.id,
                        onSelected: (_) => _select(c.id),
                      ),
                    ),
                ],
              ),
            ),
          ),
          Expanded(child: _body(t)),
        ],
      ),
    );
  }

  Widget _body(AppLocalizations t) {
    if (_items.isEmpty && _loading) return const ListSkeleton();
    if (_items.isEmpty && _error != null) {
      return ErrorView(
        message: _error is AppException
            ? (_error! as AppException).message
            : t.genericError,
        retryLabel: t.retry,
        onRetry: () => _load(reset: true),
      );
    }
    if (_items.isEmpty) {
      return EmptyState(
        icon: Icons.photo_library_outlined,
        message: t.galleryEmpty,
      );
    }
    return GridView.builder(
      key: const Key('gallery_grid'),
      controller: _scroll,
      padding: const EdgeInsets.fromLTRB(16, 4, 16, 16),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2,
        mainAxisSpacing: 12,
        crossAxisSpacing: 12,
        childAspectRatio: 0.8,
      ),
      itemCount: _items.length + (_loading ? 1 : 0),
      itemBuilder: (context, i) {
        if (i >= _items.length) {
          return const Center(child: CircularProgressIndicator());
        }
        final item = _items[i];
        return GestureDetector(
          key: Key('work_${item.id}'),
          onTap: () => _open(i),
          child: ClipRRect(
            borderRadius: BorderRadius.circular(14),
            child: Stack(
              fit: StackFit.expand,
              children: [
                NetworkPicture(url: item.imageUrl),
                Positioned(
                  left: 0,
                  right: 0,
                  bottom: 0,
                  child: Container(
                    padding: const EdgeInsets.fromLTRB(10, 18, 10, 8),
                    decoration: const BoxDecoration(
                      gradient: LinearGradient(
                        begin: Alignment.topCenter,
                        end: Alignment.bottomCenter,
                        colors: [Colors.transparent, Color(0xB3000000)],
                      ),
                    ),
                    child: Text(
                      item.categoryName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

/// One picture large, swipeable through the ones loaded so far.
class _WorkViewer extends StatefulWidget {
  const _WorkViewer({required this.items, required this.initial});

  final List<GalleryItem> items;
  final int initial;

  @override
  State<_WorkViewer> createState() => _WorkViewerState();
}

class _WorkViewerState extends State<_WorkViewer> {
  late final _controller = PageController(initialPage: widget.initial);
  late int _index = widget.initial;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final item = widget.items[_index];
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
        title: Text(item.categoryName),
      ),
      body: Column(
        children: [
          Expanded(
            child: PageView.builder(
              key: const Key('work_viewer'),
              controller: _controller,
              itemCount: widget.items.length,
              onPageChanged: (i) => setState(() => _index = i),
              itemBuilder: (_, i) => InteractiveViewer(
                child: NetworkPicture(
                  url: widget.items[i].imageUrl,
                  fit: BoxFit.contain,
                ),
              ),
            ),
          ),
          SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(20, 12, 20, 16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (item.caption != null)
                    Text(
                      item.caption!,
                      style: const TextStyle(color: Colors.white, fontSize: 15),
                    ),
                  if (item.employeeName != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 4),
                      child: Text(
                        '${t.doneBy}: ${item.employeeName}',
                        key: const Key('work_barber'),
                        style: const TextStyle(color: Colors.white70),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

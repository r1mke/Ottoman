import 'dart:async';

import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import '../network/app_exception.dart';
import 'states.dart';

/// Loads data once (and again on retry/refresh) and renders loading, error,
/// empty and data states. The skeleton is supplied by the caller so it can mirror the real layout.
///
/// With [refreshEvery] the data is also reloaded quietly in the background (no skeleton, no flicker; a failed
/// background load keeps what is on screen), so lists that change on their own never need a manual refresh.
class AsyncView<T> extends StatefulWidget {
  const AsyncView({
    super.key,
    required this.load,
    required this.builder,
    this.skeleton,
    this.isEmpty,
    this.emptyMessage,
    this.refreshEvery,
  });

  final Future<T> Function() load;
  final Widget Function(
    BuildContext context,
    T data,
    Future<void> Function() reload,
  )
  builder;
  final Widget? skeleton;

  /// When it returns true the empty state is shown instead of [builder].
  final bool Function(T data)? isEmpty;
  final String? emptyMessage;
  final Duration? refreshEvery;

  @override
  State<AsyncView<T>> createState() => _AsyncViewState<T>();
}

class _AsyncViewState<T> extends State<AsyncView<T>> {
  T? _data;
  bool _hasData = false;
  Object? _error;
  bool _loading = true;
  int _generation = 0;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _run(showLoading: true);
    final every = widget.refreshEvery;
    if (every != null) {
      _timer = Timer.periodic(every, (_) => _run(showLoading: false));
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _reload() => _run(showLoading: true);

  Future<void> _run({required bool showLoading}) async {
    final generation = ++_generation;
    if (showLoading) {
      setState(() {
        _loading = true;
        _error = null;
      });
    }
    try {
      final data = await widget.load();
      if (!mounted || generation != _generation) return;
      setState(() {
        _data = data;
        _hasData = true;
        _error = null;
        _loading = false;
      });
    } catch (e) {
      if (!mounted || generation != _generation) return;
      // A quiet background refresh must not replace good data with an error.
      if (!showLoading && _hasData) return;
      setState(() {
        _error = e;
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    if (_loading) {
      return widget.skeleton ??
          const Center(child: CircularProgressIndicator());
    }
    final error = _error;
    if (error != null) {
      return ErrorView(
        message: error is AppException ? error.message : t.genericError,
        retryLabel: t.retry,
        onRetry: _reload,
      );
    }
    final data = _data as T;
    if (widget.isEmpty?.call(data) ?? false) {
      return EmptyState(message: widget.emptyMessage ?? t.noData);
    }
    return widget.builder(context, data, _reload);
  }
}

/// A column of grey bars that stands in for list rows while loading.
class ListSkeleton extends StatelessWidget {
  const ListSkeleton({super.key, this.rows = 5, this.rowHeight = 72});

  final int rows;
  final double rowHeight;

  @override
  Widget build(BuildContext context) => ListView.separated(
    physics: const NeverScrollableScrollPhysics(),
    padding: const EdgeInsets.all(16),
    itemCount: rows,
    separatorBuilder: (_, _) => const SizedBox(height: 12),
    itemBuilder: (_, _) => SkeletonBox(height: rowHeight, radius: 12),
  );
}

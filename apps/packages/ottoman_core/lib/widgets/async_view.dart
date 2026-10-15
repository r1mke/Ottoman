import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import '../network/app_exception.dart';
import 'states.dart';

/// Loads data once (and again on retry/refresh) and renders loading, error,
/// empty and data states. The skeleton is supplied by the caller so it can mirror the real layout.
class AsyncView<T> extends StatefulWidget {
  const AsyncView({
    super.key,
    required this.load,
    required this.builder,
    this.skeleton,
    this.isEmpty,
    this.emptyMessage,
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

  @override
  State<AsyncView<T>> createState() => _AsyncViewState<T>();
}

class _AsyncViewState<T> extends State<AsyncView<T>> {
  late Future<T> _future = widget.load();

  Future<void> _reload() async {
    final next = widget.load();
    setState(() {
      _future = next;
    });
    try {
      await next;
    } catch (_) {
      // Surfaced by the FutureBuilder below.
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return FutureBuilder<T>(
      future: _future,
      builder: (context, snap) {
        if (snap.connectionState != ConnectionState.done) {
          return widget.skeleton ??
              const Center(child: CircularProgressIndicator());
        }
        if (snap.hasError) {
          final e = snap.error;
          return ErrorView(
            message: e is AppException ? e.message : t.genericError,
            retryLabel: t.retry,
            onRetry: _reload,
          );
        }
        final data = snap.data as T;
        if (widget.isEmpty?.call(data) ?? false) {
          return EmptyState(message: widget.emptyMessage ?? t.noData);
        }
        return widget.builder(context, data, _reload);
      },
    );
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

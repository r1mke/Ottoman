import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// The inbox. Opening it does not mark anything read: tapping a message does, or "mark all".
class NotificationsPage extends StatefulWidget {
  const NotificationsPage({
    super.key,
    required this.api,
    this.onChanged,
    this.embedded = false,
  });

  final NotificationsApi api;

  /// Called after read state changed so the bell badge can refresh.
  final VoidCallback? onChanged;

  /// When shown as a tab the shell already provides the app bar.
  final bool embedded;

  @override
  State<NotificationsPage> createState() => _NotificationsPageState();
}

class _NotificationsPageState extends State<NotificationsPage> {
  int _version = 0;

  Future<void> _markAll() async {
    final messenger = ScaffoldMessenger.of(context);
    try {
      await widget.api.markAllRead();
      widget.onChanged?.call();
    } on AppException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
    if (mounted) setState(() => _version++);
  }

  Future<void> _open(AppNotification n) async {
    final t = AppLocalizations.of(context);
    if (!n.isRead) {
      try {
        await widget.api.markRead(n.id);
        widget.onChanged?.call();
      } on AppException {
        // Reading still works; the badge refreshes on the next poll.
      }
      if (mounted) setState(() => _version++);
    }
    if (!mounted) return;
    await showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: DialogHeader(n.title),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (n.imageUrl != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: ClipRRect(
                  borderRadius: BorderRadius.circular(16),
                  child: NetworkPicture(
                    key: const Key('notification_detail_picture'),
                    url: n.imageUrl,
                    height: 180,
                  ),
                ),
              ),
            Text(n.body),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: Text(t.close)),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;

    final body = AsyncView<Paged<AppNotification>>(
      key: ValueKey(_version),
      // New messages appear on their own; nobody has to pull to refresh.
      refreshEvery: const Duration(seconds: 30),
      load: () => widget.api.list(),
      skeleton: const ListSkeleton(rowHeight: 84),
      isEmpty: (p) => p.items.isEmpty,
      emptyMessage: t.noNotifications,
      builder: (context, page, reload) => RefreshIndicator(
        onRefresh: () async {
          widget.onChanged?.call();
          await reload();
        },
        child: ListView.separated(
          padding: const EdgeInsets.all(16),
          itemCount: page.items.length,
          separatorBuilder: (_, _) => const SizedBox(height: 8),
          itemBuilder: (context, i) {
            final n = page.items[i];
            return Card(
              key: Key('notification_${n.id}'),
              color: n.isRead ? null : scheme.primaryContainer,
              clipBehavior: Clip.antiAlias,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (n.imageUrl != null)
                    InkWell(
                      onTap: () => _open(n),
                      child: NetworkPicture(
                        key: Key('notification_picture_${n.id}'),
                        url: n.imageUrl,
                        height: 150,
                      ),
                    ),
                  ListTile(
                    onTap: () => _open(n),
                    leading: Icon(notificationIcon(n.kind)),
                    title: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        if (n.category != null)
                          Container(
                            key: Key('notification_category_${n.id}'),
                            margin: const EdgeInsets.only(bottom: 4),
                            padding: const EdgeInsets.symmetric(
                              horizontal: 8,
                              vertical: 2,
                            ),
                            decoration: BoxDecoration(
                              color: scheme.secondaryContainer,
                              borderRadius: BorderRadius.circular(999),
                            ),
                            child: Text(
                              announcementCategoryLabel(t, n.category!),
                              style: TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.w700,
                                color: scheme.onSecondaryContainer,
                              ),
                            ),
                          ),
                        Text(
                          n.title,
                          style: TextStyle(
                            fontWeight: n.isRead
                                ? FontWeight.w500
                                : FontWeight.w800,
                          ),
                        ),
                      ],
                    ),
                    subtitle: Text(
                      n.body,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                    trailing: Text(
                      formatWhen(n.createdAtUtc),
                      style: TextStyle(color: scheme.outline, fontSize: 11),
                    ),
                  ),
                ],
              ),
            );
          },
        ),
      ),
    );

    if (widget.embedded) {
      return Column(
        children: [
          Align(
            alignment: Alignment.centerRight,
            child: TextButton.icon(
              key: const Key('mark_all_read'),
              onPressed: _markAll,
              icon: const Icon(Icons.done_all),
              label: Text(t.markAllRead),
            ),
          ),
          Expanded(child: body),
        ],
      );
    }

    return Scaffold(
      appBar: AppBar(
        title: Text(t.notifications),
        actions: [
          IconButton(
            key: const Key('mark_all_read'),
            tooltip: t.markAllRead,
            icon: const Icon(Icons.done_all),
            onPressed: _markAll,
          ),
        ],
      ),
      body: body,
    );
  }
}

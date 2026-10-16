import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// Rating summary, the latest reviews and (if the server allows it) a button to write one.
class ReviewsSection extends StatefulWidget {
  const ReviewsSection({
    super.key,
    required this.api,
    required this.targetType,
    required this.targetKey,
    required this.title,
  });

  final ReviewsApi api;
  final String targetType;
  final String targetKey;

  /// What is being reviewed, shown in the dialog.
  final String title;

  @override
  State<ReviewsSection> createState() => _ReviewsSectionState();
}

class _ReviewsSectionState extends State<ReviewsSection> {
  int _version = 0;

  Future<({ReviewList list, Eligibility eligibility})> _load() async {
    final list = await widget.api.list(widget.targetType, widget.targetKey);
    // Staff cannot review; asking is harmless but pointless, so treat any failure as "not eligible".
    var eligibility = const Eligibility(canReview: false);
    try {
      eligibility = await widget.api.eligibility(
        widget.targetType,
        widget.targetKey,
      );
    } on AppException {
      // keep the default
    }
    return (list: list, eligibility: eligibility);
  }

  Future<void> _write() async {
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final saved = await showReviewDialog(
      context,
      api: widget.api,
      targetType: widget.targetType,
      targetKey: widget.targetKey,
      title: widget.title,
    );
    if (saved) {
      messenger.showSnackBar(SnackBar(content: Text(t.reviewSaved)));
      if (mounted) setState(() => _version++);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return FutureBuilder(
      key: ValueKey(_version),
      future: _load(),
      builder: (context, snap) {
        final data = snap.data;
        if (data == null) return const SizedBox.shrink();
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              crossAxisAlignment: WrapCrossAlignment.center,
              spacing: 8,
              runSpacing: 4,
              children: [
                Text(
                  t.reviews,
                  style: Theme.of(context).textTheme.titleMedium
                      ?.copyWith(fontWeight: FontWeight.w700),
                ),
                RatingSummary(
                  average: data.list.average,
                  count: data.list.count,
                ),
                if (data.eligibility.canReview)
                  FilledButton.tonal(
                    key: const Key('write_review'),
                    style: FilledButton.styleFrom(
                      minimumSize: const Size(0, 44),
                    ),
                    onPressed: _write,
                    child: Text(t.writeReview),
                  ),
              ],
            ),
            const SizedBox(height: 8),
            if (data.list.items.isEmpty)
              Text(t.noReviews)
            else
              for (final r in data.list.items.take(5)) ReviewTile(review: r),
          ],
        );
      },
    );
  }
}
